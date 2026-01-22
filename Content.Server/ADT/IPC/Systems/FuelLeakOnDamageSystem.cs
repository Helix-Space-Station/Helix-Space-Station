using Content.Shared.Damage;
using Content.Server.Fluids.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using Content.Shared.ADT.Silicon.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

namespace Content.Server.ADT.IPC.Systems;

public sealed class FuelLeakOnDamageSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PuddleSystem _puddleSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FuelLeakOnDamageComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(EntityUid uid, FuelLeakOnDamageComponent comp, DamageChangedEvent args)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        var damageDict = damageable.Damage.DamageDict;

        FixedPoint2 physDamage = FixedPoint2.Zero;

        if (damageDict.TryGetValue("Blunt", out var blunt))
            physDamage += blunt;

        if (damageDict.TryGetValue("Slash", out var slash))
            physDamage += slash;

        if (damageDict.TryGetValue("Piercing", out var piercing))
            physDamage += piercing;

        if (physDamage < comp.MinDamage)
            return;

        if (_timing.CurTime < comp.NextLeakTime)
            return;

        var solution = new Solution();
        solution.AddReagent(comp.FuelReagent, comp.Amount);

        _puddleSystem.TrySpillAt(uid, solution, out _, sound: false);

        comp.NextLeakTime = _timing.CurTime + comp.Cooldown;
    }
}
