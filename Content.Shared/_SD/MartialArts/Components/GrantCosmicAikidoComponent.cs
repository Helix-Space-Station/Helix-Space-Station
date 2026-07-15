using Content.Shared.ADT.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Shared.ADT.MartialArts;

[RegisterComponent]
public sealed partial class GrantCosmicAikidoComponent : GrantMartialArtKnowledgeComponent
{
    [DataField]
    public override MartialArtsForms MartialArtsForm { get; set; } = MartialArtsForms.CosmicAikido;

    public override LocId? LearnMessage { get; set; } = "aikido-success-learned";
}
