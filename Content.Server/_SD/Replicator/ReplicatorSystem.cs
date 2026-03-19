// // these are HEAVILY based on the Bingle free-agent ghostrole from GoobStation, but reflavored and reprogrammed to make them more Robust (and less of a meme.)
// // all credit for the core gameplay concepts and a lot of the core functionality of the code goes to the folks over at Goob, but I re-wrote enough of it to justify putting it in our filestructure.
// // the original Bingle PR can be found here: https://github.com/Goob-Station/Goob-Station/pull/1519

// using Content.Server.Actions;
// using Content.Server.Ghost.Roles.Events;
// using Content.Server.Pinpointer;
// using Content.Server.Popups;
// using Content.Shared.Stunnable;
// using Content.Shared.SD.Replicator;
// using Content.Shared.SD.SpawnedFromTracker;
// using Content.Shared.Actions;
// using Content.Shared.CombatMode;
// using Content.Shared.Interaction.Events;
// using Content.Shared.Inventory;
// using Content.Shared.Mind.Components;
// using Content.Shared.Mobs;
// using Content.Shared.Mobs.Systems;
// using Content.Shared.Pinpointer;
// using Content.Shared.Popups;
// using Robust.Server.GameObjects;
// using Robust.Shared.Map;
// using Robust.Shared.Timing;

// namespace Content.Server.SD.Replicator;

// public sealed class ReplicatorSystem : EntitySystem
// {
//     [Dependency] private readonly IGameTiming _timing = default!;

//     [Dependency] private readonly ActionsSystem _actions = default!;
//     [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
//     [Dependency] private readonly AppearanceSystem _appearance = default!;
//     [Dependency] private readonly PopupSystem _popup = default!;
//     [Dependency] private readonly SharedStunSystem _stun = default!;
//     [Dependency] private readonly InventorySystem _inventory = default!;
//     [Dependency] private readonly PinpointerSystem _pinpointer = default!;
//     [Dependency] private readonly SharedReplicatorNestSystem _replicatorNest = default!;
//     [Dependency] private readonly MobStateSystem _mobState = default!;

//     public override void Initialize()
//     {
//         base.Initialize();

//         SubscribeLocalEvent<ReplicatorComponent, MapInitEvent>(OnInit);
//         SubscribeLocalEvent<ReplicatorComponent, MindRemovedMessage>(OnMindRemoved);
//         SubscribeLocalEvent<ReplicatorComponent, AttackAttemptEvent>(OnAttackAttempt);
//         SubscribeLocalEvent<ReplicatorComponent, ToggleCombatActionEvent>(OnCombatToggle);
//         SubscribeLocalEvent<ReplicatorComponent, GhostRoleSpawnerUsedEvent>(OnGhostRoleSpawnerUsed);
//         SubscribeLocalEvent<ReplicatorComponent, ReplicatorSpawnNestActionEvent>(OnSpawnNestAction);
//         // SubscribeLocalEvent<ReplicatorComponent, EmpPulseEvent>(OnEmpPulse); // EmpPulseEvent не существует
//     }

//     private void OnInit(EntityUid uid, ReplicatorComponent component, MapInitEvent args)
//     {
//         if (component.HasSpawnedNest)
//             return;

//         if (component.Queen)
//         {
//             _actions.AddAction(uid, component.SpawnNewNestAction);
//             component.HasSpawnedNest = true;
//         }
//     }

//     private void OnMindRemoved(Entity<ReplicatorComponent> ent, ref MindRemovedMessage args)
//     {
//         foreach (var action in ent.Comp.Actions)
//         {
//             QueueDel(action);
//         }
//     }

//     private void OnSpawnNestAction(Entity<ReplicatorComponent> ent, ref ReplicatorSpawnNestActionEvent args)
//     {
//         if (!_timing.IsFirstTimePredicted)
//             return;

//         var xform = Transform(ent);
//         var coords = xform.Coordinates;

//         if (!coords.IsValid(EntityManager) || xform.MapID == MapId.Nullspace)
//             return;

//         var myNest = Spawn("ReplicatorNest", xform.Coordinates);
//         var myNestComp = EnsureComp<ReplicatorNestComponent>(myNest);

//         if (ent.Comp.RelatedReplicators.Count <= 0 || ent.Comp.Queen && !ent.Comp.RelatedReplicators.Contains(ent))
//             ent.Comp.RelatedReplicators.Add(ent);

//         HashSet<EntityUid> newMinions = [];
//         HashSet<(EntityUid, ReplicatorComponent)> livingReplicators = [];
//         var query = EntityQueryEnumerator<ReplicatorComponent>();
//         while (query.MoveNext(out var uid, out var comp))
//         {
//             livingReplicators.Add((uid, comp));
//         }
//         foreach (var (uid, comp) in livingReplicators)
//         {
//             newMinions.Add(uid);

//             if (!_inventory.TryGetSlotEntity(uid, "pocket1", out var pocket1) || !TryComp<PinpointerComponent>(pocket1, out var pinpointer))
//                 continue;
//             _pinpointer.SetTarget(pocket1.Value, myNest, pinpointer);
//             comp.MyNest = myNest;
//         }
//         myNestComp.SpawnedMinions = newMinions;
//         myNestComp.SpawnedMinions.Add(ent);
//         ent.Comp.MyNest = myNest;
//         ent.Comp.RelatedReplicators.Clear();
//         ent.Comp.Queen = false;

//         _replicatorNest.ForceUpgrade(ent, ent.Comp.FirstStage);

//         QueueDel(args.Action);
//     }

//     private void OnGhostRoleSpawnerUsed(Entity<ReplicatorComponent> ent, ref GhostRoleSpawnerUsedEvent args)
//     {
//         if (!TryComp<SpawnedFromTrackerComponent>(args.Spawner, out var tracker) || !TryComp<ReplicatorNestComponent>(tracker.SpawnedFrom, out var nestComp))
//             return;
//         nestComp.SpawnedMinions.Add(ent);
//         nestComp.UnclaimedSpawners.Remove(args.Spawner);
//         ent.Comp.MyNest = tracker.SpawnedFrom;
//     }

//     private void OnAttackAttempt(Entity<ReplicatorComponent> ent, ref AttackAttemptEvent args)
//     {
//         if (HasComp<ReplicatorComponent>(args.Target))
//         {
//             _popup.PopupEntity(Loc.GetString("replicator-on-replicator-attack-fail"), ent, ent, PopupType.MediumCaution);
//             args.Cancel();
//         }

//         if (HasComp<ReplicatorNestComponent>(args.Target))
//         {
//             _popup.PopupEntity(Loc.GetString("replicator-on-nest-attack-fail"), ent, ent, PopupType.MediumCaution);
//             args.Cancel();
//         }
//     }

//     private void OnCombatToggle(Entity<ReplicatorComponent> ent, ref ToggleCombatActionEvent args)
//     {
//         if (!TryComp<CombatModeComponent>(ent, out var combat))
//             return;
//         _appearance.SetData(ent, ReplicatorVisuals.Combat, combat.IsInCombatMode);
//     }

//     // private void OnEmpPulse(Entity<ReplicatorComponent> ent, ref EmpPulseEvent args)
//     // {
//     //     args.Affected = true;
//     //     args.Disabled = true;
//     //     _stun.TryUpdateParalyzeDuration(ent, ent.Comp.EmpStunTime);
//     // }
// }
