using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Server.SD.Dissolvement
{
    [RegisterComponent, AutoGenerateComponentPause]
    public sealed partial class CanDissolvementComponent : Component
    {
        [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
        [AutoPausedField]
        public TimeSpan NextUpdate = TimeSpan.Zero;

        /// <summary>
        /// Максимальное количество существ, которые могут залезть на это существо.
        /// </summary>
        [DataField("maxDissolvement")]
        public int MaxDissolvement = 1;

        /// <summary>
        /// Текущее количество существ, залезших на это существо.
        /// </summary>
        [ViewVariables]
        public int CurrentDissolvements = 0;

        /// <summary>
        /// Список существ, которые залезли на это существо.
        /// </summary>
        [ViewVariables]
        public HashSet<EntityUid> Followers = new();
    }
}