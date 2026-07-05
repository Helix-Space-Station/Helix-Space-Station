using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.HunterEye;
using Robust.Shared.GameObjects;

namespace Content.Shared.HunterEye;

public sealed class HunterEyeDamageReductionSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterEyeDamageReductionComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(EntityUid uid, HunterEyeDamageReductionComponent comp, ref DamageModifyEvent args)
    {
        if (!TryComp<DamageableComponent>(uid, out var dmgComp))
            return;

        var allDamage = _damageable.GetAllDamage((uid, dmgComp));
        var modify = new DamageModifierSet();
        foreach (var key in allDamage.DamageDict.Keys)
        {
            modify.Coefficients.TryAdd(key, 0.1f);
        }
        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modify);
    }
}
