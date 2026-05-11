using Is.Assertions;
using VaultMcp.Tools.KnowledgeBase;
using VaultMcp.Tools.KnowledgeBase.Vault;
using VaultMcp.Tools.Tools;
using Xunit;

namespace VaultMcp.Tools.Tests.Tools;

public sealed class FindRelatedNotesToolTests
{
    [Fact]
    public void Execute_returns_related_results_from_vault()
    {
        var results = new[]
        {
            new VaultSearchResult("workflows/invoice-correction.md", "Invoice Correction Flow", "…invoice correction…", 310)
        };

        var tool = new FindRelatedNotesTool(new StubKnowledgeVault(
            new VaultStatus("/repo/docs/domain", true, 2, [".md"]),
            [],
            relatedResults: results));

        var response = tool.Execute("workflows/invoice-flow.md");

        response.Error.IsNull();
        response.Results.Count.Is(1);
        response.Results[0].Path.Is("workflows/invoice-correction.md");
        response.Results[0].Score.Is(310);
    }
}
