namespace VaultMcp.Tools.KnowledgeBase.SemanticIndex;

public interface IEmbeddingProvider
{
    string ProviderName { get; }
    string ModelName { get; }
    bool IsConfigured { get; }
    float[] Embed(string text);
}
