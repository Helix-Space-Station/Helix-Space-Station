using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.ADT.Droppods.Components;

/// <summary>
/// Console that loads nearby entities (or entities on load pads) into a cargo droppod
/// and launches it at a station beacon. Extra prototypes (ghost roles / mobs) spawn on landing.
/// </summary>
[RegisterComponent]
public sealed partial class DroppodDispatchConsoleComponent : Component
{
    /// <summary>
    /// Falling droppod prototype. Must have <see cref="DroppodComponent"/>.
    /// </summary>
    [DataField]
    public EntProtoId DroppodPrototype = "ADTDroppodDropping";

    /// <summary>
    /// How far from the console to look for load pads and, if none exist, for cargo.
    /// </summary>
    [DataField]
    public float ConsoleScanRange = 8f;

    /// <summary>
    /// How far from a load pad to pick up cargo.
    /// </summary>
    [DataField]
    public float PadLoadRange = 1.5f;

    [DataField]
    public int MaxCargo = 8;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan LastLaunchTime = TimeSpan.Zero;

    /// <summary>
    /// Optional whitelist. If set, only matching entities can be loaded.
    /// </summary>
    [DataField]
    public EntityWhitelist? CargoWhitelist;

    [DataField]
    public EntityWhitelist? CargoBlacklist;

    /// <summary>
    /// Prototypes the operator may add to the pod (ghost-role spawners, NPCs, crates).
    /// The client can only request IDs from this list.
    /// </summary>
    [DataField]
    public List<EntProtoId> ExtraSpawnOptions = new();

    /// <summary>
    /// Beacon prototype IDs that cannot be targeted.
    /// </summary>
    [DataField]
    public List<string> BeaconBlacklist = new();

    [DataField]
    public SoundSpecifier? LaunchSound = new SoundPathSpecifier("/Audio/ADT/Misc/droppod_landing.ogg");

    /// <summary>
    /// If true and the console has an APC receiver, it must be powered.
    /// </summary>
    [DataField]
    public bool NeedsPower = true;

    /// <summary>
    /// Next time the open UI should refresh cargo (server-only).
    /// </summary>
    public TimeSpan NextUiRefresh;
}
