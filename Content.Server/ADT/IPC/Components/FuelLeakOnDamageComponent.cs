using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared.FixedPoint;

namespace Content.Shared.ADT.Silicon.Components;

[RegisterComponent]
public sealed partial class FuelLeakOnDamageComponent : Component
{
    [DataField] public FixedPoint2 Amount = 1;
    [DataField] public string FuelReagent = "WeldingFuel";
    [DataField] public FixedPoint2 MinDamage = 10;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(1.5);

    [ViewVariables] public TimeSpan NextLeakTime = TimeSpan.Zero;
}
