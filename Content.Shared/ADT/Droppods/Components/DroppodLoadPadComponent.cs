namespace Content.Shared.ADT.Droppods.Components;

/// <summary>
/// Marks a floor pad / zone used by <see cref="DroppodDispatchConsoleComponent"/> to collect cargo.
/// If any pad is in console range, only entities near pads are loaded.
/// </summary>
[RegisterComponent]
public sealed partial class DroppodLoadPadComponent : Component;
