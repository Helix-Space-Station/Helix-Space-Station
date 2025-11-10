using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Server.SD.Dissolvement
{
    [RegisterComponent, AutoGenerateComponentPause]
    public sealed partial class DissolvementOnAttackComponent : Component
    {
        [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
        [AutoPausedField]
        public TimeSpan NextUpdate = TimeSpan.Zero;

        /// <summary>
        /// Существо, на которое залезли.
        /// </summary>
        [ViewVariables]
        public EntityUid? DissolvementOn;

        /// <summary>
        /// Получаемое количество питательных веществ за каждый тик переваривания
        /// </summary>
        [ViewVariables]
        public int AddNutriements = 1;

        /// <summary>
        /// Шанс, при котором существо слезает с другого при атаке
        /// </summary>
        [ViewVariables]
        public float StopDissolvmentOnAttacked = 0.3f;
    }
}