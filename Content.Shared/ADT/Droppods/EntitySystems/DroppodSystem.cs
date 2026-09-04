using Content.Shared.ADT.Droppods.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Shared.ADT.Droppods.EntitySystems;

public sealed class DroppodSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DroppodComponent, TimedDespawnEvent>(OnDespawn);
    }

    private void OnDespawn(EntityUid uid, DroppodComponent comp, ref TimedDespawnEvent args)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        if (!_net.IsClient && _container.TryGetContainer(uid, DroppodComponent.CargoContainerId, out var cargo))
            _container.EmptyContainer(cargo, destination: xform.Coordinates);

        if (comp.Prototypes == null)
            return;

        foreach (var spawned in comp.Prototypes)
        {
            Spawn(spawned.Id, xform.Coordinates);
        }
    }

    public void CreateDroppod(EntityCoordinates coords, List<EntProtoId> spawns)
    {
        CreateDroppod(coords, spawns, passengers: null);
    }

    /// <summary>
    /// Spawns a falling droppod. Passengers are stuffed into an internal container and ejected on landing.
    /// Extra prototypes (ghost roles, mobs, items) spawn when the pod opens.
    /// </summary>
    public EntityUid? CreateDroppod(
        EntityCoordinates coords,
        IReadOnlyList<EntProtoId>? spawns,
        IEnumerable<EntityUid>? passengers,
        EntProtoId? droppodProto = null)
    {
        if (_net.IsClient)
            return null;

        EntProtoId proto = droppodProto ?? "ADTDroppodDropping";
        var droppod = Spawn(proto, coords);
        if (!TryComp<DroppodComponent>(droppod, out var pod))
            return droppod;

        if (spawns != null)
        {
            foreach (var spawn in spawns)
            {
                pod.Prototypes.Add(spawn);
            }
        }

        if (passengers == null)
            return droppod;

        var container = _container.EnsureContainer<Container>(droppod, DroppodComponent.CargoContainerId);
        foreach (var passenger in passengers)
        {
            if (TerminatingOrDeleted(passenger) || passenger == droppod)
                continue;

            PreparePassenger(passenger);

            if (!_container.Insert(passenger, container))
                _transform.SetCoordinates(passenger, coords);
        }

        return droppod;
    }

    private void PreparePassenger(EntityUid passenger)
    {
        var xform = Transform(passenger);
        if (xform.Anchored)
            _transform.Unanchor(passenger, xform);

        if (TryComp<BuckleComponent>(passenger, out var buckle) && buckle.Buckled)
            _buckle.TryUnbuckle(passenger, passenger, buckle, popup: false);

        if (TryComp<PullableComponent>(passenger, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(passenger, pullable, ignoreGrab: true);

        if (TryComp<PullerComponent>(passenger, out var puller) &&
            puller.Pulling is { } pulled &&
            TryComp<PullableComponent>(pulled, out var pulledComp))
        {
            _pulling.TryStopPull(pulled, pulledComp, passenger, ignoreGrab: true);
        }
    }
}
