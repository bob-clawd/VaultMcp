using VaultMcp.Tools.KnowledgeBase.SemanticIndex;

namespace VaultMcp.Tools.Tests.Tools;

internal sealed class StubSemanticIndex(
    SemanticIndexStatus status,
    IReadOnlyList<SemanticSearchHit>? searchResults = null,
    Exception? searchException = null,
    Exception? rebuildException = null,
    Exception? upsertException = null) : ISemanticIndex
{
    public string? LastUpsertPath { get; private set; }
    public int RebuildCalls { get; private set; }

    public void Rebuild()
    {
        RebuildCalls++;
        if (rebuildException is not null)
            throw rebuildException;
    }

    public void UpsertFile(string relativePath)
    {
        LastUpsertPath = relativePath;
        if (upsertException is not null)
            throw upsertException;
    }

    public void DeleteFile(string relativePath)
    {
    }

    public IReadOnlyList<SemanticSearchHit> Search(string query, int limit = 10)
    {
        if (searchException is not null)
            throw searchException;

        return (searchResults ?? []).Take(limit).ToArray();
    }

    public SemanticIndexStatus GetStatus() => status;
}
