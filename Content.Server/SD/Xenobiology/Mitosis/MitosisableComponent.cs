using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Server.SD.Mitosis;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class MitosisableComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// </summary>
    /// Порог голода для митоза
    /// </summary>
    [DataField("hungerThreshold"), ViewVariables(VVAccess.ReadWrite)]
    public int HungerThreshold = 200;

    /// </summary>
    /// Шанс мутации при делении
    /// </summary>  
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Mutationchance = 0.3f;

    /// </summary>
    /// Количество получаемых существ при делении
    /// </summary>  
    [DataField("breedCount"), ViewVariables(VVAccess.ReadWrite)]
    public int BreedCount = 3;

    /// </summary>
    /// Список прототипов существ, которые могут появиться при мутации
    /// </summary>
    [DataField("mutations", customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> Mutagen = new() { "MobSlimesPet" };

    /// </summary>
    /// Флаг, указывающий что существо сейчас проходит митоз
    /// </summary>
    [ViewVariables]
    public bool IsMitosising = false;
}