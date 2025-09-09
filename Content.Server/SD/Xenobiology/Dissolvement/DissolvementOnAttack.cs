using Content.Server.SD.Mitosis;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Interaction;
using Content.Shared.Damage;
using Content.Shared.Interaction.Events;
using Content.Shared.DoAfter;
using Content.Shared.Buckle.Components;
using Content.Shared.Popups;
using Content.Shared.Climbing.Events;
using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Standing;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.SD.Dissolvement;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Systems;

namespace Content.Server.SD.Dissolvement
{
    public sealed class DissolvementOnAttackSystem : EntitySystem
    {
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] private readonly HungerSystem _hunger = default!;
        [Dependency] private readonly IRobustRandom _robustRandom = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<DissolvementOnAttackComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<DissolvementOnAttackComponent, ActionStartDissolvementEvent>(OnAction);
            SubscribeLocalEvent<DissolvementOnAttackComponent, UpdateCanMoveEvent>(OnMoveAttempt);
            SubscribeLocalEvent<DissolvementOnAttackComponent, AttackAttemptEvent>(OnAttackAttempt);
            SubscribeLocalEvent<DissolvementOnAttackComponent, StandAttemptEvent>(OnStandAttempt);
            SubscribeLocalEvent<DissolvementOnAttackComponent, MobStateChangedEvent>(OnMobStateChanged);
            SubscribeLocalEvent<DissolvementOnAttackComponent, AttackedEvent>(OnAttack);
            SubscribeLocalEvent<DissolvementOnAttackComponent, PullAttemptEvent>(OnPullAttempt);
        }

        private void OnStartup(EntityUid uid, DissolvementOnAttackComponent component, ComponentStartup args)
        {
            Transform(uid).AttachToGridOrMap();
        }

        private void OnAction(EntityUid uid, DissolvementOnAttackComponent component, ActionStartDissolvementEvent args)
        {
            if (component.DissolvementOn != null)
                return;

            if (TryComp<MitosisableComponent>(uid, out var mitosComp) && mitosComp.IsMitosising)
                return;

            var hitEntity = args.Target;

            if (_mobState.IsIncapacitated(hitEntity))
            {
                return;
            }

            if (!TryComp<CanDissolvementComponent>(hitEntity, out var dissolvemented))
            {
                return;
            }

            if (dissolvemented.CurrentDissolvements >= dissolvemented.MaxDissolvement)
            {
                return;
            }

            component.DissolvementOn = hitEntity;

            dissolvemented.Followers.Add(uid);
            dissolvemented.CurrentDissolvements++;

            Transform(uid).AttachToGridOrMap();
            Transform(uid).Coordinates = Transform(hitEntity).Coordinates;
            Transform(uid).AttachParent(hitEntity);

            _actionBlockerSystem.UpdateCanMove(uid);
        }
        public void StopDissolvement(EntityUid uid, DissolvementOnAttackComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            if (component.DissolvementOn == null)
                return;

            if (TryComp<CanDissolvementComponent>(component.DissolvementOn, out var dissolvemented))
            {
                dissolvemented.Followers.Remove(uid);
                dissolvemented.CurrentDissolvements--;
            }

            component.DissolvementOn = null;

            Transform(uid).AttachToGridOrMap();

            _actionBlockerSystem.UpdateCanMove(uid);
        }

        private void OnMobStateChanged(EntityUid uid, DissolvementOnAttackComponent component, MobStateChangedEvent args)
        {
            StopDissolvement(uid, component);
        }

        private void OnAttack(EntityUid uid, DissolvementOnAttackComponent component, ref AttackedEvent args)
        {
            if (_robustRandom.Prob(component.StopDissolvmentOnAttacked))
            {
                StopDissolvement(uid, component);
            }
        }
        private void OnMoveAttempt(EntityUid uid, DissolvementOnAttackComponent component, UpdateCanMoveEvent args)
        {
            if (component.DissolvementOn == null)
                return;

            args.Cancel();
        }

        private void OnAttackAttempt(EntityUid uid, DissolvementOnAttackComponent component, AttackAttemptEvent args)
        {
            if (component.DissolvementOn != null)
            {
                args.Cancel();
            }
        }

        private void OnStandAttempt(EntityUid uid, DissolvementOnAttackComponent component, StandAttemptEvent args)
        {
            if (component.DissolvementOn == null)
                return;

            args.Cancel();
        }

        private void OnPullAttempt(EntityUid uid, DissolvementOnAttackComponent component, PullAttemptEvent args)
        {
            if (component.DissolvementOn == null)
                return;

            args.Cancelled = true;
        }
        public override void Update(float frameTime)
        {
            var xenoQuery = EntityQueryEnumerator<DissolvementOnAttackComponent, TransformComponent>();
            while (xenoQuery.MoveNext(out var uid, out var component, out var transform))
            {
                if (component.DissolvementOn == null)
                    return;

                if (!TryComp<HungerComponent>(uid, out var hungerComp))
                    return;

                if (_robustRandom.Prob(0.03f))
                {
                    _hunger.ModifyHunger(uid, component.AddNutriements, hungerComp);

                    var attacked = component.DissolvementOn; 
                    var damage = new DamageSpecifier();
                    damage.DamageDict.Add("Caustic", 1);
                    _damageableSystem.TryChangeDamage(attacked, damage);
                }
            }
        }
    }
}