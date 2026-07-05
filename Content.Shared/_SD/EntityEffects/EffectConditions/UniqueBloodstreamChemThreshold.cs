using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using System;

namespace Content.Server.EntityConditions;

public sealed partial class UniqueBloodstreamChemThreshold : EntityConditionBase<UniqueBloodstreamChemThreshold>
{
    [DataField]
    public int Max = int.MaxValue;

    [DataField]
    public int Min = -1;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        return Loc.GetString("reagent-effect-condition-guidebook-unique-bloodstream-chem-threshold",
            ("max", Max),
            ("min", Min));
    }
}

public sealed partial class UniqueBloodstreamChemThresholdSystem :
    EntityConditionSystem<BloodstreamComponent, UniqueBloodstreamChemThreshold>
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    protected override void Condition(
        Entity<BloodstreamComponent> ent,
        ref EntityConditionEvent<UniqueBloodstreamChemThreshold> args)
    {
        var condition = args.Condition;

        if (_solution.ResolveSolution(ent.Owner, ent.Comp.MetabolitesSolutionName, ref ent.Comp.MetabolitesSolution, out var chemSolution))
        {
            var count = chemSolution.Contents.Count;
            var result = count > condition.Min && count < condition.Max;
            args.Result = condition.Inverted ? !result : result;
            return;
        }

        args.Result = condition.Inverted;
    }
}
