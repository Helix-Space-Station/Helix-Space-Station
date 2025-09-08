using Robust.Shared.GameStates;

namespace Content.Server.SD.Dissolvement
{
    [RegisterComponent]
    public sealed partial class DissolvementOnAttackComponent : Component
    {
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