using Is.Assertions;
using VaultMcp.Tools.KnowledgeBase;
using VaultMcp.Tools.KnowledgeBase.SemanticIndex;
using VaultMcp.Tools.KnowledgeBase.Vault;
using VaultMcp.Tools.Tools;
using Xunit;

namespace VaultMcp.Tools.Tests.Tools;

public sealed class RecallContextToolTests
{
    [Fact]
    public void Execute_combines_matches_loads_top_notes_and_expands_related_context()
    {
        var termResults = new[]
        {
            new VaultSearchResult("glossary/order.md", "Order", "# Order", 1200)
        };
        var searchResults = new[]
        {
            new VaultSearchResult("glossary/order.md", "Order", "# Order", 950),
            new VaultSearchResult("workflows/order-flow.md", "Order Flow", "…order flow…", 320)
        };
        var relatedResults = new[]
        {
            new VaultSearchResult("invariants/order-boundary.md", "Order Boundary", "…boundary…", 210)
        };
        var documents = new Dictionary<string, VaultNoteDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["glossary/order.md"] = new("glossary/order.md", "Order", "# Order\n\nCanonical order term."),
            ["workflows/order-flow.md"] = new("workflows/order-flow.md", "Order Flow", "# Order Flow\n\nStep 1")
        };

        var tool = new RecallContextTool(new StubKnowledgeVault(
            new VaultStatus("/repo/docs/domain", true, 2, [".md"]),
            [],
            searchResults: searchResults,
            termResults: termResults,
            relatedResults: relatedResults,
            documentsByPath: documents));

        var response = tool.Execute("order", maxCharsPerNote: 6000);

        response.Error.IsNull();
        response.TermMatches.Count.Is(1);
        response.SearchMatches.Count.Is(2);
        response.Notes.Count.Is(2);
        response.Notes[0].Path.Is("glossary/order.md");
        response.Notes[1].Path.Is("workflows/order-flow.md");
        response.RelatedNotes.Count.Is(1);
        response.RelatedNotes[0].Path.Is("invariants/order-boundary.md");
    }

    [Fact]
    public void Execute_excludes_already_loaded_notes_from_related_notes()
    {
        var termResults = new[]
        {
            new VaultSearchResult("glossary/order.md", "Order", "# Order", 1200)
        };
        var searchResults = new[]
        {
            new VaultSearchResult("workflows/order-flow.md", "Order Flow", "…order flow…", 320)
        };
        var relatedResults = new[]
        {
            new VaultSearchResult("workflows/order-flow.md", "Order Flow", "…order flow…", 500),
            new VaultSearchResult("invariants/order-boundary.md", "Order Boundary", "…boundary…", 210)
        };
        var documents = new Dictionary<string, VaultNoteDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["glossary/order.md"] = new("glossary/order.md", "Order", "# Order\n\nCanonical order term."),
            ["workflows/order-flow.md"] = new("workflows/order-flow.md", "Order Flow", "# Order Flow\n\nStep 1")
        };

        var tool = new RecallContextTool(new StubKnowledgeVault(
            new VaultStatus("/repo/docs/domain", true, 2, [".md"]),
            [],
            searchResults: searchResults,
            termResults: termResults,
            relatedResults: relatedResults,
            documentsByPath: documents));

        var response = tool.Execute("order", maxCharsPerNote: 6000);

        response.RelatedNotes.Count.Is(1);
        response.RelatedNotes[0].Path.Is("invariants/order-boundary.md");
    }

    [Fact]
    public void Execute_returns_structured_error_when_vault_root_is_missing()
    {
        var tool = new RecallContextTool(new StubKnowledgeVault(
            new VaultStatus("/repo/docs/domain", false, 0, [".md"]),
            []));

        var response = tool.Execute("order");

        response.Error!.Message.Is("vault root not found");
        response.Notes.Count.Is(0);
    }

    [Fact]
    public void Execute_deduplicates_semantic_and_lexical_matches_case_insensitively()
    {
        var searchResults = new[]
        {
            new VaultSearchResult("glossary/order.md", "Order", "# Order", 950)
        };
        var documents = new Dictionary<string, VaultNoteDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["glossary/order.md"] = new("glossary/order.md", "Order", "# Order\n\nCanonical order term.")
        };
        var semanticIndex = new StubSemanticIndex(
            new SemanticIndexStatus("/repo/docs/domain", "/repo/docs/domain/.vault", true, "test", "test-model", true, "test-model", 3, 1, 1, DateTimeOffset.UtcNow),
            [new SemanticSearchHit("chunk-1", "Glossary/Order.md", "Order", null, 0.91f, "Canonical order term.")]);

        var tool = new RecallContextTool(new StubKnowledgeVault(
            new VaultStatus("/repo/docs/domain", true, 1, [".md"]),
            [],
            searchResults: searchResults,
            documentsByPath: documents), semanticIndex);

        var response = tool.Execute("order", loadTopNotes: 5, maxCharsPerNote: 6000);

        response.Notes.Count.Is(1);
        response.Notes[0].Path.Is("glossary/order.md");
    }
}
