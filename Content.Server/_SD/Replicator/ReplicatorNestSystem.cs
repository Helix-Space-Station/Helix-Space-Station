// // these are HEAVILY based on the Bingle free-agent ghostrole from GoobStation, but reflavored and reprogrammed to make them more Robust (and less of a meme.)
// // all credit for the core gameplay concepts and a lot of the core functionality of the code goes to the folks over at Goob, but I re-wrote enough of it to justify putting it in our filestructure.
// // the original Bingle PR can be found here: https://github.com/Goob-Station/Goob-Station/pull/1519

// using Content.Server.Actions;
// using Content.Server.Audio;
// using Content.Server.Buckle.Systems;
// using Content.Server.GameTicking;
// using Content.Server.Pinpointer;
// using Content.Server.Popups;
// using Content.Server.Storage.Components;
// using Content.Server.Storage.EntitySystems;
// using Content.Server.Stunnable;
// using Content.Shared.SD.Replicator;
// using Content.Shared.Actions;
// using Content.Shared.Audio;
// using Content.Shared.Buckle.Components;
// using Content.Shared.Destructible;
// using Content.Shared.Inventory;
// using Content.Shared.Mind.Components;
// using Content.Shared.Mobs.Components;
// using Content.Shared.Mobs.Systems;
// using Content.Shared.Movement.Events;
// using Content.Shared.Movement.Pulling.Components;
// using Content.Shared.Movement.Pulling.Systems;
// using Content.Shared.Pinpointer;
// using Content.Shared.StepTrigger.Systems;
// using Content.Shared.Stunnable;
// using Content.Shared.Throwing;
// using Content.Shared.Whitelist;
// using Robust.Server.Containers;
// using Robust.Server.GameObjects;
// using Robust.Shared.Containers;
// using Robust.Shared.Player;
// using Robust.Shared.Random;
// using Robust.Shared.Timing;
// using System.Linq;
// using Content.Shared.Movement.Systems;
// using Robust.Shared.Serialization.TypeSerializers.Implementations;

// namespace Content.Server.SD.Replicator;

// public sealed class ReplicatorNestSystem : SharedReplicatorNestSystem
// {
//     [Dependency] private readonly IGameTiming _timing = default!;
//     [Dependency] private readonly IRobustRandom _random = default!;

//     [Dependency] private readonly SharedReplicatorNestSystem _sharedNest = default!;
//     [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
//     [Dependency] private readonly ActionsSystem _actions = default!;
//     [Dependency] private readonly ContainerSystem _containerSystem = default!;
//     [Dependency] private readonly MobStateSystem _mobState = default!;
//     [Dependency] private readonly NavMapSystem _navMap = default!;
//     [Dependency] private readonly PopupSystem _popup = default!;
//     [Dependency] private readonly StunSystem _stun = default!;
//     [Dependency] private readonly TransformSystem _xform = default!;
//     [Dependency] private readonly InventorySystem _inventory = default!;
//     [Dependency] private readonly PinpointerSystem _pinpointer = default!;
//     [Dependency] private readonly AmbientSoundSystem _ambientSound = default!;
//     [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
//     [Dependency] private readonly PullingSystem _pulling = default!;
//     [Dependency] private readonly ThrowingSystem _throwing = default!;
//     [Dependency] private readonly EntityStorageSystem _entStorage = default!;
//     [Dependency] private readonly BuckleSystem _buckle = default!;

//     public override void Initialize()
//     {
//         base.Initialize();

//         SubscribeLocalEvent<ReplicatorNestComponent, MapInitEvent>(OnMapInit);
//         SubscribeLocalEvent<ReplicatorNestComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
//         SubscribeLocalEvent<ReplicatorNestComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
//         SubscribeLocalEvent<ReplicatorNestComponent, StepTriggeredOffEvent>(OnStepTriggered);
//         SubscribeLocalEvent<ReplicatorNestFallingComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
//         SubscribeLocalEvent<ReplicatorNestComponent, DestructionEventArgs>(OnDestroyed);
//         SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
//     }

//     public override void Update(float frameTime)
//     {
//         base.Update(frameTime);

//         HashSet<EntityUid> toDel = [];

//         var query = EntityQueryEnumerator<ReplicatorNestFallingComponent>();
//         while (query.MoveNext(out var uid, out var falling))
//         {
//             if (_timing.CurTime < falling.NextDeletionTime)
//                 continue;

//             var nestComp = falling.FallingTarget.Comp;

//             if (_whitelist.IsBlacklistPass(nestComp.PreservationBlacklist, uid) ||
//                 (!_whitelist.IsWhitelistPass(nestComp.PreservationWhitelist, uid) &&
//                  (!TryComp<MindContainerComponent>(uid, out var mind) || (mind != null && !mind.HasMind))))
//                 toDel.Add(uid);

//             _containerSystem.Insert(uid, falling.FallingTarget.Comp.Hole);
//             EnsureComp<StunnedComponent>(uid);
//             RemCompDeferred(uid, falling);
//         }

//         foreach (var uid in toDel)
//         {
//             QueueDel(uid);
//         }
//     }

//     private void OnEntRemoved(Entity<ReplicatorNestComponent> ent, ref EntRemovedFromContainerMessage args)
//     {
//         RemCompDeferred<StunnedComponent>(args.Entity);
//     }

//     private void OnMapInit(Entity<ReplicatorNestComponent> ent, ref MapInitEvent args)
//     {
//         if (!Transform(ent).Coordinates.IsValid(EntityManager))
//             QueueDel(ent);

//         ent.Comp.Hole = _containerSystem.EnsureContainer<Container>(ent, "hole");

//         ent.Comp.NextSpawnAt = ent.Comp.SpawnNewAt;
//         ent.Comp.NextUpgradeAt = ent.Comp.UpgradeAt;
//         ent.Comp.NextTileConvertAt = ent.Comp.TileConvertAt;

//         var pointsStorageEnt = Spawn("ReplicatorNestPointsStorage", Transform(ent).Coordinates);
//         EnsureComp<ReplicatorNestPointsStorageComponent>(pointsStorageEnt);

//         ent.Comp.PointsStorage = pointsStorageEnt;
//     }

//     private void OnStepTriggerAttempt(Entity<ReplicatorNestComponent> ent, ref StepTriggerAttemptEvent args)
//     {
//         args.Continue = true;
//     }

//     private void OnStepTriggered(Entity<ReplicatorNestComponent> ent, ref StepTriggeredOffEvent args)
//     {
//         if (HasComp<ReplicatorNestFallingComponent>(args.Tripper))
//             return;

//         if (_whitelist.IsBlacklistPass(ent.Comp.Blacklist, args.Tripper))
//         {
//             if (TryComp<PullableComponent>(args.Tripper, out var pullable) && pullable.BeingPulled)
//                 _pulling.TryStopPull(args.Tripper, pullable);

//             var xform = Transform(ent);
//             var xformQuery = GetEntityQuery<TransformComponent>();
//             var worldPos = _xform.GetWorldPosition(xform, xformQuery);

//             var direction = _xform.GetWorldPosition(args.Tripper, xformQuery) - worldPos;
//             _throwing.TryThrow(args.Tripper, direction * 10, 7, ent, 0);
//             return;
//         }

//         var isReplicator = HasComp<ReplicatorComponent>(args.Tripper);

//         if (TryComp<MobStateComponent>(args.Tripper, out var mobState) && isReplicator && _mobState.IsDead(args.Tripper))
//         {
//             _sharedNest.StartFalling(ent, args.Tripper);
//             return;
//         }

//         if (mobState != null && _mobState.IsAlive(args.Tripper))
//             return;

//         if (TryComp<StrapComponent>(args.Tripper, out var strapComp) && strapComp.BuckledEntities.Count > 0)
//         {
//             foreach (var buckled in strapComp.BuckledEntities)
//             {
//                 if (!TryComp<BuckleComponent>(buckled, out var buckleComp))
//                 {
//                     _buckle.Unbuckle((args.Tripper, buckleComp), null);
//                 }
//             }
//         }

//         _sharedNest.StartFalling(ent, args.Tripper);
//     }

//     private void OnUpdateCanMove(Entity<ReplicatorNestFallingComponent> ent, ref UpdateCanMoveEvent args)
//     {
//         args.Cancel();
//     }

//     private void OnDestroyed(Entity<ReplicatorNestComponent> ent, ref DestructionEventArgs args)
//     {
//         HandleDestruction(ent);
//     }

//     private void HandleDestruction(Entity<ReplicatorNestComponent> ent)
//     {
//         if (TryComp<AmbientSoundComponent>(ent.Comp.PointsStorage, out var ambientComp))
//             _ambientSound.SetAmbience(ent.Comp.PointsStorage, false, ambientComp);

//         if (ent.Comp.Hole != null)
//         {
//             foreach (var uid in _containerSystem.EmptyContainer(ent.Comp.Hole))
//             {
//                 RemCompDeferred<StunnedComponent>(uid);
//                 _stun.TryKnockdown(uid, TimeSpan.FromSeconds(2), false);
//             }
//         }

//         foreach (var spawner in ent.Comp.UnclaimedSpawners.ToList())
//         {
//             ent.Comp.UnclaimedSpawners.Remove(spawner);
//             QueueDel(spawner);
//         }

//         var query = EntityQueryEnumerator<ReplicatorNestFallingComponent>();
//         while (query.MoveNext(out var uid, out var comp))
//         {
//             if (comp.FallingTarget == ent)
//                 RemCompDeferred<ReplicatorNestFallingComponent>(uid);
//         }

//         EntityUid? queen = null;
//         HashSet<Entity<ReplicatorComponent>> livingReplicators = [];
//         var repQuery = EntityQueryEnumerator<ReplicatorComponent>();
//         while (repQuery.MoveNext(out var uid, out var comp))
//         {
//             if (!_mobState.IsAlive(uid))
//                 continue;

//             if (comp.MyNest != ent.Owner)
//                 continue;

//             comp.MyNest = null;
//             if (comp.Queen)
//                 queen = uid;

//             livingReplicators.Add((uid, comp));
//         }

//         if (livingReplicators.Count > 0)
//         {
//             EntityUid queenNotNull = queen ?? _random.Pick(livingReplicators.Select(e => e.Owner).ToList());

//             var comp = EnsureComp<ReplicatorComponent>(queenNotNull);
//             comp.Queen = true;
//             comp.RelatedReplicators = livingReplicators;

//             var upgradedQueen = ForceUpgrade((queenNotNull, comp), "MobReplicatorQueen");
//             if (!TryComp<ReplicatorComponent>(upgradedQueen, out var upgradedComp))
//                 return;

//             if (upgradedQueen is not { } upgradedQueenNotNull || !TryComp<MindContainerComponent>(upgradedQueen, out var mindContainer) || mindContainer.Mind is not { } mind)
//                 return;

//             if (!TryComp<ReplicatorComponent>(upgradedQueenNotNull, out var upgradedQueenReplicatorComp))
//                 return;

//             queen = upgradedQueenNotNull;
//             livingReplicators.Add((upgradedQueenNotNull, upgradedQueenReplicatorComp));

//             if (!mindContainer.HasMind)
//                 _actions.AddAction(upgradedQueenNotNull, ent.Comp.SpawnNewNestAction);
//             else
//                 _actionContainer.AddAction(mind, upgradedComp.SpawnNewNestAction);
//         }
//     }

//     private void OnRoundEndTextAppend(RoundEndTextAppendEvent args)
//     {
//         List<Entity<ReplicatorNestPointsStorageComponent>> nests = [];

//         var query = AllEntityQuery<ReplicatorNestPointsStorageComponent>();
//         while (query.MoveNext(out var uid, out var comp))
//             nests.Add((uid, comp));

//         if (nests.Count == 0)
//             return;

//         args.AddLine("");

//         var totalPoints = 0;
//         var totalSpawned = 0;
//         HashSet<int> levels = [];
//         var locationsList = "";

//         var i = 0;
//         foreach (var ent in nests)
//         {
//             i++;
//             var pointsStorage = ent.Comp;
//             var location = "Unknown";
//             var mapCoords = _xform.ToMapCoordinates(Transform(ent).Coordinates);
//             if (_navMap.TryGetNearestBeacon(mapCoords, out var beacon, out _) && beacon?.Comp.Text != null)
//                 location = beacon?.Comp.Text!;

//             if (nests.Count == 1)
//                 locationsList = string.Concat(locationsList, "[color=#d70aa0]", location, "[/color].");
//             else if (nests.Count == 2 && i == 1)
//                 locationsList = string.Concat(locationsList, "[color=#d70aa0]", location, " ");
//             else if (i != nests.Count)
//                 locationsList = string.Concat(locationsList, "[color=#d70aa0]", location, "[/color], ");
//             else
//                 locationsList = string.Concat(locationsList, $"and [color=#d70aa0]{location}[/color].");

//             totalPoints += pointsStorage.TotalPoints / 10;
//             totalSpawned += pointsStorage.TotalReplicators;
//             levels.Add(pointsStorage.Level);
//         }

//         var highestLevel = levels.Max();

//         args.AddLine(Loc.GetString("replicator-nest-end-of-round", ("location", locationsList), ("level", highestLevel), ("points", totalPoints), ("replicators", totalSpawned)));
//         args.AddLine("");
//     }
// }
