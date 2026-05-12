namespace VaultMcp.Tools.KnowledgeBase.SemanticIndex;

public sealed record SemanticSearchScoringOptions(
    float SemanticWeight,
    float LexicalWeight,
    float MetadataWeight)
{
    public static SemanticSearchScoringOptions Default { get; } = new(0.75f, 0.15f, 0.10f);
}
