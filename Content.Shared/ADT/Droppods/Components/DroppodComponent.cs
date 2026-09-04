using Robust.Shared.Prototypes;

namespace Content.Shared.ADT.Droppods.Components;

/// <summary>
/// When a <c>TimedDespawnComponent</c> despawns, cargo is dumped and listed prototypes are spawned.
/// </summary>
[RegisterComponent]
public sealed partial class DroppodComponent : Component
{
    public const string CargoContainerId = "droppod-cargo";

    /// <summary>
    /// Prototypes spawned at the landing site when the falling pod despawns (ghost-role spawners, mobs, crates).
    /// </summary>
    [DataField]
    public List<EntProtoId> Prototypes { get; set; } = new();
}
