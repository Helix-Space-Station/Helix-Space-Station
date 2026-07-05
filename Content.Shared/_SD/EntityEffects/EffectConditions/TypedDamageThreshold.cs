using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityConditions;
using Content.Shared.FixedPoint;
using Content.Shared.Localizations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using System;
using System.Collections.Generic;

namespace Content.Server.EntityConditions;

public sealed partial class TypedDamageThreshold : EntityConditionBase<TypedDamageThreshold>
{
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        var damages = new List<string>();
        var comparison = new DamageSpecifier(Damage);
        foreach (var group in prototype.EnumeratePrototypes<DamageGroupPrototype>())
        {
            var requestedGroup = FixedPoint2.Zero;
            foreach (var damageType in group.DamageTypes)
            {
                if (comparison.DamageDict.TryGetValue(damageType, out var value) && value > FixedPoint2.Zero)
                    requestedGroup += value;
            }
            if (requestedGroup == FixedPoint2.Zero)
                continue;

            damages.Add(
                Loc.GetString("health-change-display",
                    ("kind", group.LocalizedName),
                    ("amount", MathF.Abs(requestedGroup.Float())),
                    ("deltasign", 1))
            );

            foreach (var damageType in group.DamageTypes)
            {
                if (!comparison.DamageDict.TryGetValue(damageType, out var value) || value == FixedPoint2.Zero)
                    continue;

                comparison.DamageDict[damageType] -= value;
                if (MathF.Abs(comparison.DamageDict[damageType].Float() - MathF.Round(comparison.DamageDict[damageType].Float())) < 0.02)
                    comparison.DamageDict[damageType] = MathF.Round(comparison.DamageDict[damageType].Float());
            }
            comparison.ClampMin(0);
            comparison.TrimZeros();
        }

        foreach (var (kind, amount) in comparison.DamageDict)
        {
            damages.Add(
                Loc.GetString("health-change-display",
                    ("kind", prototype.Index<DamageTypePrototype>(kind).LocalizedName),
                    ("amount", MathF.Abs(amount.Float())),
                    ("deltasign", 1))
                );
        }

        return Loc.GetString("reagent-effect-condition-guidebook-typed-damage-threshold",
                ("inverse", Inverted),
                ("changes", ContentLocalizationManager.FormatList(damages))
                );
    }
}

public sealed partial class TypedDamageThresholdSystem :
    EntityConditionSystem<DamageableComponent, TypedDamageThreshold>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    protected override void Condition(
        Entity<DamageableComponent> ent,
        ref EntityConditionEvent<TypedDamageThreshold> args)
    {
        var condition = args.Condition;
        var allDamage = _damageable.GetAllDamage((ent.Owner, (DamageableComponent?)ent.Comp));

        var comparison = new DamageSpecifier(condition.Damage);
        foreach (var group in _proto.EnumeratePrototypes<DamageGroupPrototype>())
        {
            var requestedGroup = FixedPoint2.Zero;
            foreach (var damageType in group.DamageTypes)
            {
                if (comparison.DamageDict.TryGetValue(damageType, out var value) && value > FixedPoint2.Zero)
                    requestedGroup += value;
            }
            if (requestedGroup == FixedPoint2.Zero)
                continue;

            if (allDamage.TryGetDamageInGroup(group, out var total) && total >= requestedGroup)
            {
                args.Result = !condition.Inverted;
                return;
            }

            foreach (var damageType in group.DamageTypes)
            {
                if (!comparison.DamageDict.TryGetValue(damageType, out var value) || value == FixedPoint2.Zero)
                    continue;

                comparison.DamageDict[damageType] -= value;
                if (MathF.Abs(comparison.DamageDict[damageType].Float() - MathF.Round(comparison.DamageDict[damageType].Float())) < 0.02)
                    comparison.DamageDict[damageType] = MathF.Round(comparison.DamageDict[damageType].Float());
            }
            comparison.ClampMin(0);
            comparison.TrimZeros();
        }
        comparison.ExclusiveAdd(-allDamage);
        comparison = -comparison;
        args.Result = comparison.AnyPositive() ^ condition.Inverted;
    }
}
