using System.Numerics;
using Content.Server.Power.Components;
using Content.Shared.Audio;
using Content.Shared.Climbing.Events;
using Content.Shared.Construction.Components;
using Content.Shared.Containers;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Jittering;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Power;
using Content.Shared.Throwing;
using Robust.Server.Containers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.SD.Server.ExtractGrinder;

public sealed partial class ExtractGrinderSystem : EntitySystem
{
    [Dependency] private readonly SharedJitteringSystem _jitteringSystem = default!;
    [Dependency] private readonly SharedAudioSystem _sharedAudioSystem = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSoundSystem = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedJointSystem _jointSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActiveExtractGrinderComponent, ComponentInit>(OnActiveInit);
        SubscribeLocalEvent<ActiveExtractGrinderComponent, ComponentRemove>(OnActiveShutdown);
        SubscribeLocalEvent<ActiveExtractGrinderComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);

        SubscribeLocalEvent<ExtractGrinderComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ExtractGrinderComponent, ComponentRemove>(OnShutdown);
        SubscribeLocalEvent<ExtractGrinderComponent, BeforeUnanchoredEvent>(OnUnanchored);
        SubscribeLocalEvent<ExtractGrinderComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<ExtractGrinderComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ExtractGrinderComponent, ClimbedOnEvent>(OnClimbedOn);
        SubscribeLocalEvent<ExtractGrinderComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ExtractGrinderComponent, ReclaimerDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<ExtractGrinderComponent, BeforeThrowInsertEvent>(BeforeThrowInsert);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveExtractGrinderComponent, ExtractGrinderComponent>();
        while (query.MoveNext(out var uid, out _, out var grinder))
        {
            grinder.ProcessingTimer -= frameTime;

            if (grinder.ProcessingTimer > 0
                || !TryComp<HaveExtractComponent>(grinder.EntityGrinded, out var extract))
                continue;

            var extractProto = extract.ExtractProto;
            var extractQuantity = extract.ExtractQuantity;

            for (var i = 0; i < extractQuantity; i++)
                SpawnNextToOrDrop(extractProto, uid);

            QueueDel(grinder.EntityGrinded);
            grinder.EntityGrinded = null;

            RemCompDeferred<ActiveExtractGrinderComponent>(uid);
        }
    }

    #region  Active Grinding

    private void OnActiveInit(Entity<ActiveExtractGrinderComponent> activeGrinder, ref ComponentInit args)
    {
        if (!TryComp<ExtractGrinderComponent>(activeGrinder, out var grinder))
            return;

        _jitteringSystem.AddJitter(activeGrinder, -10, 100);
        _sharedAudioSystem.PlayPvs(grinder.GrindSound, activeGrinder);
        _ambientSoundSystem.SetAmbience(activeGrinder, true);
    }

    private void OnActiveShutdown(Entity<ActiveExtractGrinderComponent> activeGrinder, ref ComponentRemove args)
    {
        RemComp<JitteringComponent>(activeGrinder);
        _ambientSoundSystem.SetAmbience(activeGrinder, false);
    }

    private void OnUnanchorAttempt(Entity<ActiveExtractGrinderComponent> activeGrinder, ref UnanchorAttemptEvent args) =>
        args.Cancel();

    private void OnPowerChanged(Entity<ExtractGrinderComponent> grinder, ref PowerChangedEvent args)
    {
        if (args.Powered)
        {
            if (grinder.Comp.ProcessingTimer > 0)
                EnsureComp<ActiveExtractGrinderComponent>(grinder);
        }
        else
        {
            RemCompDeferred<ActiveExtractGrinderComponent>(grinder);
        }
    }

    #endregion

    private void OnInit(Entity<ExtractGrinderComponent> grinder, ref ComponentInit args) =>
        grinder.Comp.GrindedContainer = _container.EnsureContainer<Container>(grinder, "GrindedContainer");

    private void OnShutdown(Entity<ExtractGrinderComponent> grinder, ref ComponentRemove args) =>
        _container.EmptyContainer(grinder.Comp.GrindedContainer);

    private void OnUnanchored(Entity<ExtractGrinderComponent> grinder, ref BeforeUnanchoredEvent args) =>
        _container.EmptyContainer(grinder.Comp.GrindedContainer);

    private void OnAfterInteractUsing(Entity<ExtractGrinderComponent> grinder, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach
            || args.Target == null
            || !TryComp<PhysicsComponent>(args.Used, out var physics)
            || !CanGrind(grinder, args.Used))
            return;

        var delay = grinder.Comp.BaseInsertionDelay * physics.FixturesMass;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            delay,
            new ReclaimerDoAfterEvent(),
            grinder,
            target: args.Target,
            used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void BeforeThrowInsert(Entity<ExtractGrinderComponent> grinder, ref BeforeThrowInsertEvent args)
    {
        if (CanGrind(grinder, args.ThrownEntity))
            return;

        args.Cancelled = true;
    }

    private void OnClimbedOn(Entity<ExtractGrinderComponent> grinder, ref ClimbedOnEvent args)
    {
        if (!CanGrind(grinder, args.Climber))
        {
            var direction = new Vector2(_robustRandom.Next(-2, 2), _robustRandom.Next(-2, 2));
            _throwing.TryThrow(args.Climber, direction, 0.5f);
            return;
        }

        _container.Insert(args.Climber, grinder.Comp.GrindedContainer);
        StartProcessing(args.Climber, grinder);
    }

    private void OnDoAfter(Entity<ExtractGrinderComponent> grinder, ref ReclaimerDoAfterEvent args)
    {
        if (args.Handled
            || args.Cancelled
            || args.Args.Used is not { } toProcess)
            return;

        _container.Insert(toProcess, grinder.Comp.GrindedContainer);
        args.Handled = true;
    }

    private void OnInserted(Entity<ExtractGrinderComponent> grinder, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != grinder.Comp.GrindedContainer.ID)
            return;

        if (!CanGrind(grinder, args.Entity))
        {
            _container.TryRemoveFromContainer(args.Entity, true);
            _throwing.TryThrow(args.Entity, _robustRandom.NextVector2() * 3);

            return;
        }

        _jointSystem.RecursiveClearJoints(args.Entity);
        StartProcessing(args.Entity, grinder);
    }

    private void StartProcessing(EntityUid toProcess, Entity<ExtractGrinderComponent> grinder, PhysicsComponent? physics = null, HaveExtractComponent? slime = null)
    {
        if (!Resolve(toProcess, ref physics, ref slime))
            return;

        EnsureComp<ActiveExtractGrinderComponent>(grinder);
        grinder.Comp.ProcessingTimer = physics.FixturesMass * grinder.Comp.ProcessingTimePerUnitMass;
        grinder.Comp.EntityGrinded = toProcess;
    }

    private bool CanGrind(Entity<ExtractGrinderComponent> grinder, EntityUid dragged)
    {
        if (HasComp<ActiveExtractGrinderComponent>(grinder)
            || !Transform(grinder).Anchored
            || !HasComp<HaveExtractComponent>(dragged)
            || !TryComp<MobStateComponent>(dragged, out var mobState)
            || mobState.CurrentState != MobState.Dead)
            return false;

        return !TryComp<ApcPowerReceiverComponent>(grinder, out var power) || power.Powered;
    }


}
