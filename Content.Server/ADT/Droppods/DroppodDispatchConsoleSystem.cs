using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Power.EntitySystems;
using Content.Shared.ADT.Droppods;
using Content.Shared.ADT.Droppods.Components;
using Content.Shared.ADT.Droppods.EntitySystems;
using Content.Shared.ADT.Shuttles.Components;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.IdentityManagement;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.UserInterface;
using Content.Shared.Warps;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.ADT.Droppods;

public sealed class DroppodDispatchConsoleSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DroppodSystem _droppod = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DroppodDispatchConsoleComponent, AfterActivatableUIOpenEvent>(OnOpened);
        Subs.BuiEvents<DroppodDispatchConsoleComponent>(DroppodDispatchUiKey.Key, subs =>
        {
            subs.Event<DroppodDispatchLaunchMessage>(OnLaunch);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DroppodDispatchConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_ui.IsUiOpen(uid, DroppodDispatchUiKey.Key))
                continue;
            if (_timing.CurTime < comp.NextUiRefresh)
                continue;

            comp.NextUiRefresh = _timing.CurTime + TimeSpan.FromSeconds(0.5);
            UpdateUi((uid, comp));
        }
    }

    private void OnOpened(Entity<DroppodDispatchConsoleComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<DroppodDispatchConsoleComponent> ent)
    {
        var (uid, comp) = ent;
        var powered = IsConsolePowered(ent);
        var cooldown = GetCooldownRemaining(comp);
        var canLaunch = powered && cooldown <= 0;

        var cargo = new List<DroppodDispatchCargoInfo>();
        foreach (var entity in ScanCargo(ent))
        {
            cargo.Add(new DroppodDispatchCargoInfo
            {
                Uid = GetNetEntity(entity),
                Name = Identity.Name(entity, EntityManager),
                IsMob = HasComp<MobStateComponent>(entity),
            });
        }

        var ghosts = new List<DroppodDispatchGhostOption>();
        foreach (var protoId in comp.ExtraSpawnOptions)
        {
            var name = protoId.Id;
            if (_proto.TryIndex(protoId, out EntityPrototype? proto) && !string.IsNullOrEmpty(proto.Name))
                name = Loc.GetString(proto.Name);

            ghosts.Add(new DroppodDispatchGhostOption
            {
                Prototype = protoId,
                Name = name,
            });
        }

        _ui.SetUiState(uid, DroppodDispatchUiKey.Key, new DroppodDispatchConsoleBuiState
        {
            Cargo = cargo,
            GhostOptions = ghosts,
            Beacons = CollectBeacons(comp),
            CanLaunch = canLaunch,
            Powered = powered,
            CooldownRemaining = cooldown,
            MaxCargo = comp.MaxCargo,
        });
    }

    private void OnLaunch(Entity<DroppodDispatchConsoleComponent> ent, ref DroppodDispatchLaunchMessage args)
    {
        var (uid, comp) = ent;
        var user = args.Actor;

        if (!IsConsolePowered(ent))
        {
            _popup.PopupEntity(Loc.GetString("droppod-dispatch-popup-unpowered"), uid, user);
            UpdateUi(ent);
            return;
        }

        var cooldown = GetCooldownRemaining(comp);
        if (cooldown > 0)
        {
            _popup.PopupEntity(Loc.GetString("droppod-dispatch-popup-cooldown", ("seconds", cooldown)), uid, user);
            UpdateUi(ent);
            return;
        }

        var beacon = GetEntity(args.TargetBeacon);
        if (!ResolveBeacon(beacon, comp, out var targetCoords))
        {
            _popup.PopupEntity(Loc.GetString("droppod-dispatch-popup-need-target"), uid, user);
            UpdateUi(ent);
            return;
        }

        var loadable = ScanCargo(ent).ToHashSet();
        var passengers = new List<EntityUid>();
        foreach (var netEnt in args.Cargo)
        {
            var cargoUid = GetEntity(netEnt);
            if (!loadable.Contains(cargoUid))
                continue;
            if (!IsLoadable(ent, cargoUid))
                continue;
            passengers.Add(cargoUid);
        }

        passengers = passengers
            .OrderByDescending(HasComp<MobStateComponent>)
            .Take(comp.MaxCargo)
            .ToList();

        var extras = new List<EntProtoId>();
        foreach (var protoId in args.ExtraSpawns)
        {
            if (!comp.ExtraSpawnOptions.Contains(protoId))
                continue;
            extras.Add(protoId);
        }

        if (passengers.Count == 0 && extras.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("droppod-dispatch-popup-need-cargo"), uid, user);
            UpdateUi(ent);
            return;
        }

        var pod = _droppod.CreateDroppod(targetCoords, extras, passengers, comp.DroppodPrototype);
        if (pod == null)
        {
            _popup.PopupEntity(Loc.GetString("droppod-dispatch-popup-insert-fail"), uid, user);
            return;
        }

        comp.LastLaunchTime = _timing.CurTime;
        if (comp.LaunchSound != null)
            _audio.PlayPvs(comp.LaunchSound, uid);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(user):player} launched droppod from {ToPrettyString(uid)} with {passengers.Count} passengers and {extras.Count} extra spawns.");

        _popup.PopupEntity(Loc.GetString("droppod-dispatch-popup-launched"), uid, user);
        UpdateUi(ent);
    }

    private bool IsConsolePowered(Entity<DroppodDispatchConsoleComponent> ent)
    {
        if (!ent.Comp.NeedsPower)
            return true;
        return _power.IsPowered(ent.Owner);
    }

    private int GetCooldownRemaining(DroppodDispatchConsoleComponent comp)
    {
        if (comp.LastLaunchTime == TimeSpan.Zero)
            return 0;

        var elapsed = _timing.CurTime - comp.LastLaunchTime;
        if (elapsed >= comp.Cooldown)
            return 0;

        return (int)Math.Ceiling((comp.Cooldown - elapsed).TotalSeconds);
    }

    private IEnumerable<EntityUid> ScanCargo(Entity<DroppodDispatchConsoleComponent> ent)
    {
        var pads = new List<EntityUid>();
        foreach (var entity in _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.ConsoleScanRange, LookupFlags.Static | LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (HasComp<DroppodLoadPadComponent>(entity))
                pads.Add(entity);
        }

        var seen = new HashSet<EntityUid>();
        var cargoFlags = LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries;
        if (pads.Count > 0)
        {
            foreach (var pad in pads)
            {
                foreach (var cargo in _lookup.GetEntitiesInRange(pad, ent.Comp.PadLoadRange, cargoFlags))
                {
                    if (!seen.Add(cargo))
                        continue;
                    if (IsLoadable(ent, cargo))
                        yield return cargo;
                }
            }

            yield break;
        }

        foreach (var cargo in _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.ConsoleScanRange, cargoFlags))
        {
            if (!seen.Add(cargo))
                continue;
            if (IsLoadable(ent, cargo))
                yield return cargo;
        }
    }

    private bool IsLoadable(Entity<DroppodDispatchConsoleComponent> ent, EntityUid cargo)
    {
        var (uid, comp) = ent;
        if (cargo == uid)
            return false;
        if (TerminatingOrDeleted(cargo))
            return false;
        if (HasComp<DroppodDispatchConsoleComponent>(cargo) || HasComp<DroppodLoadPadComponent>(cargo))
            return false;
        if (HasComp<GhostComponent>(cargo))
            return false;
        if (_container.IsEntityInContainer(cargo))
            return false;
        if (HasComp<MapGridComponent>(cargo))
            return false;

        var xform = Transform(cargo);
        var isItem = HasComp<ItemComponent>(cargo);
        var isMob = HasComp<MobStateComponent>(cargo);
        var isCrate = HasComp<EntityStorageComponent>(cargo);

        // Floor crates are structures, not handheld items. Anchored lockers stay put.
        if (xform.Anchored && !isItem && !isCrate)
            return false;

        if (!isItem && !isMob && !isCrate)
            return false;

        if (!_whitelist.CheckBoth(cargo, comp.CargoBlacklist, comp.CargoWhitelist))
            return false;

        return true;
    }

    private List<DroppodDispatchBeaconInfo> CollectBeacons(DroppodDispatchConsoleComponent comp)
    {
        var validGrids = GetValidStationGrids();
        var beacons = new List<DroppodDispatchBeaconInfo>();
        var query = EntityQueryEnumerator<WarpPointComponent, NavMapBeaconComponent, MetaDataComponent>();
        while (query.MoveNext(out var beaconUid, out _, out var navMap, out var meta))
        {
            var beaconXform = Transform(beaconUid);
            if (beaconXform.GridUid is not { } grid)
                continue;
            if (validGrids.Count > 0 && !validGrids.Contains(grid))
                continue;

            var protoId = meta.EntityPrototype?.ID;
            if (protoId != null && comp.BeaconBlacklist.Contains(protoId))
                continue;

            var name = navMap.Text ?? navMap.DefaultText ?? meta.EntityName;
            if (string.IsNullOrEmpty(name))
                continue;

            beacons.Add(new DroppodDispatchBeaconInfo
            {
                Uid = GetNetEntity(beaconUid),
                Name = name,
                WorldPos = _transform.GetWorldPosition(beaconUid),
            });
        }

        return beacons;
    }

    private HashSet<EntityUid> GetValidStationGrids()
    {
        var valid = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<DropPodTargetStationComponent, StationDataComponent>();
        while (query.MoveNext(out _, out var stationData))
        {
            foreach (var gridUid in stationData.Grids)
            {
                if (!TerminatingOrDeleted(gridUid) && HasComp<MapGridComponent>(gridUid))
                    valid.Add(gridUid);
            }
        }

        return valid;
    }

    private bool ResolveBeacon(EntityUid beacon, DroppodDispatchConsoleComponent comp, out EntityCoordinates coords)
    {
        coords = default;
        if (TerminatingOrDeleted(beacon))
            return false;
        if (!HasComp<WarpPointComponent>(beacon) || !TryComp<NavMapBeaconComponent>(beacon, out var navMap))
            return false;

        var protoId = MetaData(beacon).EntityPrototype?.ID;
        if (protoId != null && comp.BeaconBlacklist.Contains(protoId))
            return false;

        var name = navMap.Text ?? navMap.DefaultText ?? MetaData(beacon).EntityName;
        if (string.IsNullOrEmpty(name))
            return false;

        var validGrids = GetValidStationGrids();
        var beaconXform = Transform(beacon);
        if (beaconXform.GridUid is null)
            return false;
        if (validGrids.Count > 0 && !validGrids.Contains(beaconXform.GridUid.Value))
            return false;
        if (beaconXform.MapUid == null)
            return false;

        coords = GetLandingCoords(beacon);
        return true;
    }

    private EntityCoordinates GetLandingCoords(EntityUid beaconEnt)
    {
        var beaconXform = Transform(beaconEnt);
        var mapUid = beaconXform.MapUid!.Value;
        var beaconWorldPos = _transform.GetWorldPosition(beaconEnt);
        var beaconMapCoords = new MapCoordinates(beaconWorldPos, beaconXform.MapID);

        if (_mapManager.TryFindGridAt(beaconMapCoords, out var gridUid, out var gridComp))
        {
            for (var i = 0; i < 16; i++)
            {
                var offsetAngle = new Angle(_random.NextDouble() * Math.Tau);
                var offsetDist = _random.NextFloat(2f, 6f);
                var testWorldPos = beaconWorldPos + offsetAngle.RotateVec(new Vector2(offsetDist, 0f));
                if (!IsPositionSafeFromSpace(gridUid, gridComp, testWorldPos, 1))
                    continue;

                var tileIdx = _mapSystem.WorldToTile(gridUid, gridComp, testWorldPos);
                var snappedPos = Vector2.Transform(
                    new Vector2(tileIdx.X * gridComp.TileSize, tileIdx.Y * gridComp.TileSize),
                    _transform.GetWorldMatrix(gridUid));
                return new EntityCoordinates(mapUid, snappedPos);
            }

            var fallbackIdx = _mapSystem.WorldToTile(gridUid, gridComp, beaconWorldPos);
            var fallbackPos = Vector2.Transform(
                new Vector2(fallbackIdx.X * gridComp.TileSize, fallbackIdx.Y * gridComp.TileSize),
                _transform.GetWorldMatrix(gridUid));
            return new EntityCoordinates(mapUid, fallbackPos);
        }

        return new EntityCoordinates(mapUid, beaconWorldPos);
    }

    private bool IsPositionSafeFromSpace(EntityUid gridUid, MapGridComponent grid, Vector2 worldPos, int checkRadius)
    {
        var tileIdx = _mapSystem.WorldToTile(gridUid, grid, worldPos);
        for (var dx = -checkRadius; dx <= checkRadius; dx++)
        {
            for (var dy = -checkRadius; dy <= checkRadius; dy++)
            {
                var neighbor = tileIdx + new Vector2i(dx, dy);
                var tileRef = _mapSystem.GetTileRef(gridUid, grid, neighbor);
                if (_turf.IsSpace(tileRef))
                    return false;
            }
        }

        return true;
    }
}
