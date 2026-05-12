using System.ComponentModel;
using ModelContextProtocol.Server;
using VaultMcp.Tools;
using VaultMcp.Tools.KnowledgeBase;
using VaultMcp.Tools.KnowledgeBase.Vault;

namespace VaultMcp.Tools.Tools;

public sealed record FindTermResponse(
    IReadOnlyList<VaultMatch> Results,
    ErrorInfo? Error = null)
{
    public static FindTermResponse AsError(ErrorInfo error) => new([], error);
}

[McpServerToolType]
public sealed class FindTermTool(IVault vault)
{
    [McpServerTool(Name = "find_term", Title = "Find Term")]
    [Description("Find glossary-like matches for a domain term, preferring exact or near-exact title matches. Use this before asking the user to re-explain an unfamiliar domain term.")]
    public FindTermResponse Execute(
        [Description("Domain term to look up, for example 'Order Aggregate' or 'Tenant Boundary'.")]
        string term,
        [Description("Maximum number of results to return. Default: 3.")]
        int maxCount = 3)
    {
        if (VaultToolErrors.ValidateReadableVault(vault) is { } vaultError)
            return FindTermResponse.AsError(vaultError);

        try
        {
            return new FindTermResponse(VaultToolPayloads.FromSearchResults(vault.FindTerm(term, maxCount)));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or DirectoryNotFoundException or IOException)
        {
            return FindTermResponse.AsError(VaultToolErrors.FromException(exception));
        }
    }
}
