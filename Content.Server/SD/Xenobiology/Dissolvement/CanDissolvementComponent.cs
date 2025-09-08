using Robust.Shared.GameStates;

namespace Content.Server.SD.Dissolvement
{
    [RegisterComponent]
    public sealed partial class CanDissolvementComponent : Component
    {
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