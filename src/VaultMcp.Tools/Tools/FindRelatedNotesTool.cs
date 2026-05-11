using System.ComponentModel;
using ModelContextProtocol.Server;
using VaultMcp.Tools;
using VaultMcp.Tools.KnowledgeBase;
using VaultMcp.Tools.KnowledgeBase.Vault;

namespace VaultMcp.Tools.Tools;

public sealed record FindRelatedNotesResponse(
    IReadOnlyList<VaultSearchResult> Results,
    ErrorInfo? Error = null)
{
    public static FindRelatedNotesResponse AsError(ErrorInfo error) => new([], error);
}

[McpServerToolType]
public sealed class FindRelatedNotesTool(IVault vault)
{
    [McpServerTool(Name = "find_related_notes", Title = "Find Related Notes")]
    [Description("Find notes related to a given vault note using shared domain terms and directory proximity. Use this after `get_note` when you need broader domain context around a relevant note.")]
    public FindRelatedNotesResponse Execute(
        [Description("Vault-relative markdown path, for example 'workflows/invoice-flow.md'.")]
        string path,
        [Description("Maximum number of related notes to return. Default: 5.")]
        int maxCount = 5)
    {
        if (VaultToolErrors.ValidateReadableVault(vault) is { } vaultError)
            return FindRelatedNotesResponse.AsError(vaultError);

        try
        {
            return new FindRelatedNotesResponse(vault.FindRelatedNotes(path, maxCount));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or FileNotFoundException or DirectoryNotFoundException or IOException)
        {
            return FindRelatedNotesResponse.AsError(VaultToolErrors.FromException(exception));
        }
    }
}
