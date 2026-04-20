using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.EntitySystems;
using Content.Server.Medical.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using System.Linq;

namespace Content.Server.Medical;

public sealed partial class CryoPodSystem : SharedCryoPodSystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly IGameTiming _cryoTiming = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _cryoUi = default!;
    [Dependency] private readonly GasCanisterSystem _gasCanisterSystem = default!;
    [Dependency] private readonly GasAnalyzerSystem _gasAnalyzerSystem = default!;
    [Dependency] private readonly HealthAnalyzerSystem _healthAnalyzerSystem = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CryoPodComponent, AtmosDeviceUpdateEvent>(OnCryoPodUpdateAtmosphere);
        SubscribeLocalEvent<CryoPodComponent, GasAnalyzerScanEvent>(OnGasAnalyzed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveCryoPodComponent, CryoPodComponent>();

        while (query.MoveNext(out var uid, out _, out var cryoPod))
        {
            if (_cryoTiming.CurTime < cryoPod.NextUiUpdateTime)
                continue;

            cryoPod.NextUiUpdateTime += cryoPod.UiUpdateInterval;
            Dirty(uid, cryoPod);
            UpdateUi((uid, cryoPod));
        }
    }

    protected override void UpdateUi(Entity<CryoPodComponent> entity)
    {
        if (!_cryoUi.IsUiOpen(entity.Owner, CryoPodUiKey.Key)
            || !TryComp(entity, out CryoPodAirComponent? air))
            return;

        var patient = entity.Comp.BodyContainer.ContainedEntity;
        var gasMix = _gasAnalyzerSystem.GenerateGasMixEntry("Cryo pod", air.Air);
        var (beakerCapacity, beaker) = GetBeakerInfo(entity);
        var injecting = GetInjectingReagents(entity);
        var health = _healthAnalyzerSystem.GetHealthAnalyzerUiState(patient);
        health.ScanMode = true;

        // SpaceDream edit
        var hasDamage = patient.HasValue
                        && TryComp<DamageableComponent>(patient.Value, out var damageable)
                        && damageable.TotalDamage > FixedPoint2.Zero;

        _cryoUi.ServerSendUiMessage(
            entity.Owner,
            CryoPodUiKey.Key,
            new CryoPodUserMessage(gasMix, health, beakerCapacity, beaker, injecting, hasDamage: hasDamage)
        );
    }

    private void OnCryoPodUpdateAtmosphere(Entity<CryoPodComponent> entity, ref AtmosDeviceUpdateEvent args)
    {
        if (!_nodeContainer.TryGetNode(entity.Owner, entity.Comp.PortName, out PortablePipeNode? portNode))
            return;

        if (!TryComp(entity, out CryoPodAirComponent? cryoPodAir))
            return;

        _atmosphereSystem.React(cryoPodAir.Air, portNode);

        if (portNode.NodeGroup is PipeNet { NodeCount: > 1 } net)
        {
            _gasCanisterSystem.MixContainerWithPipeNet(cryoPodAir.Air, net.Air);
        }
    }

    private void OnGasAnalyzed(Entity<CryoPodComponent> entity, ref GasAnalyzerScanEvent args)
    {
        if (!TryComp(entity, out CryoPodAirComponent? cryoPodAir))
            return;

        args.GasMixtures ??= new List<(string, GasMixture?)>();
        args.GasMixtures.Add((Name(entity.Owner), cryoPodAir.Air));
        // If it's connected to a port, include the port side
        // multiply by volume fraction to make sure to send only the gas inside the analyzed pipe element, not the whole pipe system
        if (_nodeContainer.TryGetNode(entity.Owner, entity.Comp.PortName, out PipeNode? port) && port.Air.Volume != 0f)
        {
            var portAirLocal = port.Air.Clone();
            portAirLocal.Multiply(port.Volume / port.Air.Volume);
            portAirLocal.Volume = port.Volume;
            args.GasMixtures.Add((entity.Comp.PortName, portAirLocal));
        }
    }
    // SpaceDream start
    private (FixedPoint2? capacity, List<ReagentQuantity>? reagents) GetBeakerInfo(Entity<CryoPodComponent> entity)
    {
        var beakerUid = _itemSlots.GetItemOrNull(entity.Owner, entity.Comp.SolutionContainerName);
        if (beakerUid == null)
            return (null, null);

        if (!_solutionContainer.TryGetFitsInDispenser(beakerUid.Value, out _, out var solution))
            return (null, null);

        return (solution.MaxVolume, solution.Contents.ToList());
    }

    private List<ReagentQuantity>? GetInjectingReagents(Entity<CryoPodComponent> entity)
    {
        if (!_solutionContainer.TryGetSolution(
                entity.Owner,
                CryoPodComponent.InjectionBufferSolutionName,
                out _,
                out var buffer))
            return new List<ReagentQuantity>();

        return buffer.Contents.ToList();
    }
    // SpaceDream end
}
