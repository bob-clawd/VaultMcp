using System.ComponentModel;
using ModelContextProtocol.Server;
using VaultMcp.Tools.KnowledgeBase.SemanticIndex;
using VaultMcp.Tools.KnowledgeBase.Vault;

namespace VaultMcp.Tools.Tools;

public sealed record SemanticSearchNotesResponse(
    IReadOnlyList<SemanticSearchHit> Results,
    ErrorInfo? Error = null)
{
    public static SemanticSearchNotesResponse AsError(ErrorInfo error) => new([], error);
}

[McpServerToolType]
public sealed class SemanticSearchNotesTool(IVault vault, ISemanticIndex semanticIndex)
{
    [McpServerTool(Name = "semantic_search_notes", Title = "Semantic Search Notes")]
    [Description("Search semantic note chunks using the persisted derived index. Use this for natural-language recall after running `reindex_vault`." )]
    public SemanticSearchNotesResponse Execute(
        [Description("Natural-language semantic search query.")]
        string query,
        [Description("Maximum number of grouped note hits to return. Default: 10.")]
        int limit = 10)
    {
        if (VaultToolErrors.ValidateReadableVault(vault) is { } vaultError)
            return SemanticSearchNotesResponse.AsError(vaultError);

        try
        {
            return new SemanticSearchNotesResponse(semanticIndex.Search(query, limit));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or IOException or SemanticIndexException)
        {
            return SemanticSearchNotesResponse.AsError(VaultToolErrors.FromException(exception));
        }
    }
}
