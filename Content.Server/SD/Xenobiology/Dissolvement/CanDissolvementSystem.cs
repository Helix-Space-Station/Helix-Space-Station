using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Systems;

namespace Content.Server.SD.Dissolvement
{
    public sealed class CanDissolvementSystem : EntitySystem
    {
        [Dependency] private readonly DissolvementOnAttackSystem _dissolvementSystem = default!;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<CanDissolvementComponent, MobStateChangedEvent>(OnMobStateChanged);
        }

        private void OnMobStateChanged(EntityUid uid, CanDissolvementComponent component, MobStateChangedEvent args)
        {
            foreach (var follower in component.Followers)
            {
                _dissolvementSystem.StopDissolvement(follower);
            }
        }
    }
}