namespace VaultMcp.Tools.KnowledgeBase.SemanticIndex;

public sealed record SemanticIndexOptions(
    string RootPath,
    string IndexDirectory,
    string ProviderName,
    string EmbeddingModel,
    string EmbeddingModelPath,
    string EmbeddingVocabPath,
    int MaxChunkWords,
    int MaxPreviewChars,
    SemanticSearchScoringOptions? Scoring = null)
{
    public SemanticSearchScoringOptions EffectiveScoring => Scoring ?? SemanticSearchScoringOptions.Default;
    public string IndexFilePath => Path.Combine(IndexDirectory, "semantic-index.json");
    public string VectorFilePath => Path.Combine(IndexDirectory, "semantic-vectors.bin");
    public string StateFilePath => Path.Combine(IndexDirectory, "index-state.json");
}
