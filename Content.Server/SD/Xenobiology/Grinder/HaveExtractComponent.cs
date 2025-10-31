using Robust.Shared.Audio;
using Robust.Shared.Containers;

namespace Content.SD.Server.ExtractGrinder;

[RegisterComponent]
public sealed partial class HaveExtractComponent : Component
{
    /// <summary>
    /// Экстракт получаемый при переработке
    /// </summary>
    [DataField("extractProto"), ViewVariables(VVAccess.ReadWrite)]
    public string ExtractProto = "";

    /// </summary>
    /// Количество получаемых экстрактов
    /// </summary>
    [DataField("extractQuantity"), ViewVariables(VVAccess.ReadWrite)]
    public int ExtractQuantity = 1;
}
