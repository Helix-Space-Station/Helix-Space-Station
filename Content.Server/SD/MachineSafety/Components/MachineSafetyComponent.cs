using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.SD.MachineSafety.Components;

[RegisterComponent]
public sealed partial class MachineSafetyComponent : Component
{
    /// <summary>
    /// Critical temperature when machine explodes (K)
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CriticalTemperature = 423f;

    /// <summary>
    /// How long machine can work without cooling before meltdown (seconds)
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxOverheatTimeSeconds = 600f;

    /// <summary>
    /// Max overheat time in ticks (calculated from seconds)
    /// </summary>
    [ViewVariables]
    public float MaxOverheatTimeTicks;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan OverheatTimer = TimeSpan.Zero;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan LastAlertTime = TimeSpan.Zero;

    [ViewVariables]
    public bool IsOverheating = false;

    [ViewVariables]
    public bool HasAtmosphere = true;

    [ViewVariables]
    public bool HasSentCriticalAlert = false;

    // Warning flags for time-based alerts
    [ViewVariables]
    public bool Warned5Min = false;

    [ViewVariables]
    public bool Warned3Min = false;

    [ViewVariables]
    public bool Warned1Min = false;

    [ViewVariables]
    public bool Warned30Sec = false;

    [ViewVariables]
    public bool Warned10Sec = false;

    /// <summary>
    /// Radio channel for safety alerts
    /// </summary>
    [DataField]
    public string AlertChannel = "Engineering";
}
