namespace VaultMcp.Tools.KnowledgeBase.SemanticIndex;

internal sealed class UnavailableEmbeddingProvider(string message, string providerName = "none", string modelName = "unconfigured") : IEmbeddingProvider
{
    public string ProviderName { get; } = providerName;
    public string ModelName { get; } = modelName;
    public bool IsConfigured => false;

    public float[] Embed(string text)
        => throw new EmbeddingProviderUnavailableException(message);
}
