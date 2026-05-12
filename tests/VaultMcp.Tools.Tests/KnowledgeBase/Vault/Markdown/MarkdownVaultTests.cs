using Is.Assertions;
using VaultMcp.Tools.KnowledgeBase;
using VaultMcp.Tools.KnowledgeBase.Vault;
using VaultMcp.Tools.KnowledgeBase.Vault.Markdown;
using VaultMcp.Tools.KnowledgeBase.Search;
using Xunit;

namespace VaultMcp.Tools.Tests.KnowledgeBase.Vault.Markdown;

public sealed class MarkdownVaultTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "VaultMcp.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void GetStatus_counts_markdown_files_only()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.md"), "# A");
        File.WriteAllText(Path.Combine(_root, "b.markdown"), "# B");
        File.WriteAllText(Path.Combine(_root, "c.txt"), "ignore");

        var vault = new MarkdownVault(_root);

        var status = vault.GetStatus();

        status.Exists.IsTrue();
        status.NoteCount.Is(2);
    }

    [Fact]
    public void ListNotes_uses_first_heading_as_title_and_relative_paths()
    {
        Directory.CreateDirectory(Path.Combine(_root, "concepts"));
        File.WriteAllText(Path.Combine(_root, "concepts", "aggregate.md"), "# Aggregate Root\n\nBody");

        var vault = new MarkdownVault(_root);

        var notes = vault.ListNotes();

        notes.Count.Is(1);
        notes[0].Path.Is(Path.Combine("concepts", "aggregate.md"));
        notes[0].Title.Is("Aggregate Root");
    }

    [Fact]
    public void ListNotes_falls_back_to_file_name_when_heading_is_missing()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "pricing.md"), "No heading here");

        var vault = new MarkdownVault(_root);

        var notes = vault.ListNotes();

        notes[0].Title.Is("pricing");
    }

    [Fact]
    public void GetNote_returns_content_and_title_for_relative_path()
    {
        Directory.CreateDirectory(Path.Combine(_root, "workflows"));
        File.WriteAllText(Path.Combine(_root, "workflows", "invoice-flow.md"), "# Invoice Flow\n\nStep 1");

        var vault = new MarkdownVault(_root);

        var note = vault.GetNote(Path.Combine("workflows", "invoice-flow.md"));

        note.Path.Is(Path.Combine("workflows", "invoice-flow.md"));
        note.Title.Is("Invoice Flow");
        note.Content.Is("# Invoice Flow\n\nStep 1");
    }

    [Fact]
    public void GetNote_respects_frontmatter_and_max_chars_budget()
    {
        Directory.CreateDirectory(Path.Combine(_root, "glossary"));
        File.WriteAllText(
            Path.Combine(_root, "glossary", "order.md"),
            "---\nkind: term\ntags:\n - ordering\naliases:\n - Sales Order\n---\n# Order\n\nThis is a long body section for context budgeting.");

        var vault = new MarkdownVault(_root);

        var note = vault.GetNote(Path.Combine("glossary", "order.md"), maxChars: 30);

        note.Title.Is("Order");
        note.Kind.Is("term");
        note.Tags!.Contains("ordering", StringComparer.OrdinalIgnoreCase).IsTrue();
        note.Aliases!.Contains("Sales Order", StringComparer.OrdinalIgnoreCase).IsTrue();
        note.IsTruncated.IsTrue();
        note.Content.Length.Is(30);
    }

    [Fact]
    public void GetNote_hides_internal_learning_hash_markers_from_returned_content()
    {
        Directory.CreateDirectory(Path.Combine(_root, "glossary"));
        File.WriteAllText(
            Path.Combine(_root, "glossary", "order.md"),
            "# Order\n\n<!-- vaultmcp:learning-hash=abc123 -->\n\nUseful content.");

        var vault = new MarkdownVault(_root);

        var note = vault.GetNote(Path.Combine("glossary", "order.md"));

        note.Content.Contains("vaultmcp:learning-hash", StringComparison.OrdinalIgnoreCase).IsFalse();
        note.Content.Contains("Useful content.", StringComparison.Ordinal).IsTrue();
    }

    [Fact]
    public void GetNote_normalizes_crlf_when_stripping_internal_learning_hash_markers()
    {
        Directory.CreateDirectory(Path.Combine(_root, "glossary"));
        File.WriteAllText(
            Path.Combine(_root, "glossary", "order.md"),
            "# Order\r\n\r\n<!-- vaultmcp:learning-hash=abc123 -->\r\n\r\nUseful content.\r\n");

        var vault = new MarkdownVault(_root);

        var note = vault.GetNote(Path.Combine("glossary", "order.md"));

        note.Content.Contains('\r').IsFalse();
        note.Content.Contains("Useful content.", StringComparison.Ordinal).IsTrue();
    }

    [Fact]
    public void GetNote_rejects_path_traversal()
    {
        Directory.CreateDirectory(_root);
        var vault = new MarkdownVault(_root);

        var exception = Record.Exception(() => vault.GetNote(Path.Combine("..", "secrets.md")));

        exception.IsNotNull();
        exception.Is<ArgumentException>();
        exception.Message.Contains("escapes the configured vault root", StringComparison.Ordinal).IsTrue();
    }

    [Fact]
    public void SearchNotes_prefers_title_matches_over_body_only_matches()
    {
        Directory.CreateDirectory(Path.Combine(_root, "concepts"));
        Directory.CreateDirectory(Path.Combine(_root, "pitfalls"));
        File.WriteAllText(Path.Combine(_root, "concepts", "order.md"), "# Order\n\nOrder aggregate overview.");
        File.WriteAllText(Path.Combine(_root, "pitfalls", "shipping.md"), "# Shipping\n\nThis flow mentions order twice. Order must be validated.");

        var vault = new MarkdownVault(_root);

        var results = vault.SearchNotes("order");

        results.Count.Is(2);
        results[0].Path.Is(Path.Combine("concepts", "order.md"));
        results[1].Path.Is(Path.Combine("pitfalls", "shipping.md"));
    }

    [Fact]
    public void SearchNotes_delegates_to_configured_search_implementation()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "pricing.md"), "# Pricing\n\nBody");

        var expected = new[]
        {
            new VaultSearchResult("custom.md", "Custom", "Injected", 123)
        };

        var search = new StubSearch(searchNotes: expected);
        var vault = new MarkdownVault(_root, search);

        var results = vault.SearchNotes("pricing");

        search.SearchNotesCalls.Is(1);
        results.Count.Is(1);
        results[0].Path.Is("custom.md");
    }

    [Fact]
    public void SearchNotes_returns_excerpt_around_match()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "pricing.md"), "# Pricing\n\nThe surcharge rule applies when the fragile order exceeds the threshold.");

        var vault = new MarkdownVault(_root);

        var results = vault.SearchNotes("fragile");

        results.Count.Is(1);
        results[0].Excerpt.Contains("fragile order", StringComparison.OrdinalIgnoreCase).IsTrue();
    }

    [Fact]
    public void SearchNotes_omits_internal_learning_hash_markers_from_excerpts()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(
            Path.Combine(_root, "pricing.md"),
            "# Pricing\n\n<!-- vaultmcp:learning-hash=abc123 -->\n\nThe fragile order exceeds the threshold.");

        var vault = new MarkdownVault(_root);

        var results = vault.SearchNotes("fragile");

        results.Count.Is(1);
        results[0].Excerpt.Contains("vaultmcp:learning-hash", StringComparison.OrdinalIgnoreCase).IsFalse();
        results[0].Excerpt.Contains("fragile order", StringComparison.OrdinalIgnoreCase).IsTrue();
    }

    [Fact]
    public void SearchNotes_prefers_heading_matches_over_body_only_matches()
    {
        Directory.CreateDirectory(Path.Combine(_root, "workflows"));
        Directory.CreateDirectory(Path.Combine(_root, "notes"));
        File.WriteAllText(Path.Combine(_root, "workflows", "invoice-correction.md"), "# Invoice Correction\n\nShort summary.");
        File.WriteAllText(Path.Combine(_root, "notes", "shipping.md"), "# Shipping\n\nInvoice appears here. Much later the correction process is mentioned too.");

        var vault = new MarkdownVault(_root);

        var results = vault.SearchNotes("invoice correction");

        results.Count.Is(2);
        results[0].Path.Is(Path.Combine("workflows", "invoice-correction.md"));
    }

    [Fact]
    public void SearchNotes_matches_transliterated_query_against_umlaut_note()
    {
        Directory.CreateDirectory(Path.Combine(_root, "glossary"));
        File.WriteAllText(Path.Combine(_root, "glossary", "fundstück.md"), "# Fundstück\n\nDas Fundstück wird im Archiv erfasst.");

        var vault = new MarkdownVault(_root);

        var results = vault.SearchNotes("Fundstueck");

        results.Count.Is(1);
        results[0].Path.Is(Path.Combine("glossary", "fundstück.md"));
    }

    [Fact]
    public void SearchNotes_prefers_exact_phrase_over_separate_term_hits()
    {
        Directory.CreateDirectory(Path.Combine(_root, "workflows"));
        Directory.CreateDirectory(Path.Combine(_root, "notes"));
        File.WriteAllText(Path.Combine(_root, "workflows", "invoice-correction.md"), "# Billing\n\nThe invoice correction path starts after rejection.");
        File.WriteAllText(Path.Combine(_root, "notes", "invoice-and-correction.md"), "# Billing Retry\n\nAn invoice may fail. A manual correction happens later.");

        var vault = new MarkdownVault(_root);

        var results = vault.SearchNotes("invoice correction");

        results.Count.Is(2);
        results[0].Path.Is(Path.Combine("workflows", "invoice-correction.md"));
    }

    [Fact]
    public void ListNotes_refreshes_cached_index_after_external_file_creation()
    {
        Directory.CreateDirectory(_root);
        using var vault = new MarkdownVault(_root);

        vault.ListNotes().Count.Is(0);

        Directory.CreateDirectory(Path.Combine(_root, "glossary"));
        File.WriteAllText(Path.Combine(_root, "glossary", "order.md"), "# Order\n\nCanonical domain term.");

        WaitUntil(() => vault.ListNotes().Any(note => string.Equals(note.Path, Path.Combine("glossary", "order.md"), StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void SearchNotes_refreshes_cached_index_after_external_file_change()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "pricing.md"), "# Pricing\n\nalpha token");
        using var vault = new MarkdownVault(_root);

        vault.SearchNotes("alpha").Count.Is(1);

        File.WriteAllText(Path.Combine(_root, "pricing.md"), "# Pricing\n\nbeta token");

        WaitUntil(() =>
        {
            var betaResults = vault.SearchNotes("beta");
            var alphaResults = vault.SearchNotes("alpha");
            return betaResults.Count == 1 && alphaResults.Count == 0;
        });
    }

    [Fact]
    public void SearchNotes_prefers_tag_matches_over_body_only_matches()
    {
        Directory.CreateDirectory(Path.Combine(_root, "data-flows"));
        Directory.CreateDirectory(Path.Combine(_root, "notes"));
        File.WriteAllText(
            Path.Combine(_root, "data-flows", "invoice-export.md"),
            "---\nkind: data-flow\ntags:\n - reporting\n---\n# Invoice Export\n\nExports approved invoices.");
        File.WriteAllText(Path.Combine(_root, "notes", "misc.md"), "# Misc\n\nThis note mentions reporting in passing.");

        var vault = new MarkdownVault(_root);

        var results = vault.SearchNotes("reporting");

        results.Count.Is(2);
        results[0].Path.Is(Path.Combine("data-flows", "invoice-export.md"));
    }

    [Fact]
    public void FindTerm_prefers_exact_title_match()
    {
        Directory.CreateDirectory(Path.Combine(_root, "glossary"));
        Directory.CreateDirectory(Path.Combine(_root, "workflows"));
        File.WriteAllText(Path.Combine(_root, "glossary", "order.md"), "# Order\n\nCanonical domain term.");
        File.WriteAllText(Path.Combine(_root, "workflows", "invoice.md"), "# Invoice Flow\n\nOrder gets processed during invoicing.");

        var vault = new MarkdownVault(_root);

        var results = vault.FindTerm("Order");

        results.Count.Is(2);
        results[0].Path.Is(Path.Combine("glossary", "order.md"));
        results[0].Title.Is("Order");
    }

    [Fact]
    public void FindTerm_uses_frontmatter_aliases()
    {
        Directory.CreateDirectory(Path.Combine(_root, "glossary"));
        File.WriteAllText(
            Path.Combine(_root, "glossary", "order.md"),
            "---\nkind: term\naliases:\n - Sales Order\n---\n# Order\n\nCanonical domain term.");

        var vault = new MarkdownVault(_root);

        var results = vault.FindTerm("Sales Order");

        results.Count.Is(1);
        results[0].Path.Is(Path.Combine("glossary", "order.md"));
    }

    [Fact]
    public void FindTerm_prefers_alias_match_on_term_notes()
    {
        Directory.CreateDirectory(Path.Combine(_root, "glossary"));
        Directory.CreateDirectory(Path.Combine(_root, "workflows"));
        File.WriteAllText(
            Path.Combine(_root, "glossary", "order.md"),
            "---\nkind: term\naliases:\n - Sales Order\n---\n# Order\n\nCanonical domain term.");
        File.WriteAllText(
            Path.Combine(_root, "workflows", "sales-order.md"),
            "---\nkind: workflow\naliases:\n - Sales Order\n---\n# Sales Order Workflow\n\nDescribes the process.");

        var vault = new MarkdownVault(_root);

        var results = vault.FindTerm("Sales Order");

        results.Count.Is(2);
        results[0].Path.Is(Path.Combine("glossary", "order.md"));
    }

    [Fact]
    public void FindTerm_matches_umlaut_query_against_transliterated_note()
    {
        Directory.CreateDirectory(Path.Combine(_root, "glossary"));
        File.WriteAllText(
            Path.Combine(_root, "glossary", "fundstueck.md"),
            "---\nkind: term\n---\n# Fundstueck\n\nDas Fundstueck wird im Archiv erfasst.");

        var vault = new MarkdownVault(_root);

        var results = vault.FindTerm("Fundstück");

        results.Count.Is(1);
        results[0].Path.Is(Path.Combine("glossary", "fundstueck.md"));
    }

    [Fact]
    public void FindTerm_prefers_transliterated_alias_match_on_term_notes()
    {
        Directory.CreateDirectory(Path.Combine(_root, "glossary"));
        Directory.CreateDirectory(Path.Combine(_root, "workflows"));
        File.WriteAllText(
            Path.Combine(_root, "glossary", "fundstueck.md"),
            "---\nkind: term\naliases:\n - Fundstueck\n---\n# Fundstueck\n\nCanonical domain term.");
        File.WriteAllText(
            Path.Combine(_root, "workflows", "fundstueck-flow.md"),
            "---\nkind: workflow\naliases:\n - Fundstueck\n---\n# Fundstueck Workflow\n\nDescribes the process.");

        var vault = new MarkdownVault(_root);

        var results = vault.FindTerm("Fundstück");

        results.Count.Is(2);
        results[0].Path.Is(Path.Combine("glossary", "fundstueck.md"));
    }

    [Fact]
    public void FindRelatedNotes_prefers_shared_terms_and_same_directory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "workflows"));
        Directory.CreateDirectory(Path.Combine(_root, "concepts"));
        File.WriteAllText(Path.Combine(_root, "workflows", "invoice-flow.md"), "# Invoice Flow\n\nOrder validation happens before invoice correction.");
        File.WriteAllText(Path.Combine(_root, "workflows", "invoice-correction.md"), "# Invoice Correction Flow\n\nInvoice correction starts after order validation.");
        File.WriteAllText(Path.Combine(_root, "concepts", "tenant-boundary.md"), "# Tenant Boundary\n\nTenant segregation across exports.");

        var vault = new MarkdownVault(_root);

        var results = vault.FindRelatedNotes(Path.Combine("workflows", "invoice-flow.md"));

        results.Count.Is(1);
        results[0].Path.Is(Path.Combine("workflows", "invoice-correction.md"));
        (results[0].Score > 0).IsTrue();
    }

    [Fact]
    public void FindRelatedNotes_prefers_explicit_related_links_over_shared_terms()
    {
        Directory.CreateDirectory(Path.Combine(_root, "workflows"));
        Directory.CreateDirectory(Path.Combine(_root, "decisions"));
        Directory.CreateDirectory(Path.Combine(_root, "notes"));
        File.WriteAllText(
            Path.Combine(_root, "workflows", "invoice-flow.md"),
            "---\nkind: workflow\nrelated:\n - decisions/invoice-policy.md\n---\n# Invoice Flow\n\nInvoice correction starts after validation.");
        File.WriteAllText(
            Path.Combine(_root, "decisions", "invoice-policy.md"),
            "---\nkind: decision\n---\n# Invoice Policy\n\nExplains why invoice correction needs approval.");
        File.WriteAllText(
            Path.Combine(_root, "notes", "invoice-terms.md"),
            "# Invoice Notes\n\nInvoice correction validation invoice correction validation invoice correction validation.");

        var vault = new MarkdownVault(_root);

        var results = vault.FindRelatedNotes(Path.Combine("workflows", "invoice-flow.md"));

        results.Count.Is(2);
        results[0].Path.Is(Path.Combine("decisions", "invoice-policy.md"));
    }

    [Fact]
    public void CaptureLearning_creates_new_note_in_bucket_directory()
    {
        Directory.CreateDirectory(_root);
        var vault = new MarkdownVault(_root);

        var result = vault.CaptureLearning(new VaultLearningCapture(
            "term",
            "Order Aggregate",
            "Defines the aggregate boundary for orders.",
            "Used in pricing and fulfillment flows.",
            ["ddd", "orders"],
            Aliases: ["Order Root"],
            Examples: ["Pricing loads the aggregate before discount evaluation."]));

        result.Created.IsTrue();
        result.Path.Is(Path.Combine("glossary", "order-aggregate.md"));
        result.Kind.Is("term");

        var fileContent = File.ReadAllText(Path.Combine(_root, "glossary", "order-aggregate.md"));
        fileContent.Contains("---", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("kind: term", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("tags:", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("aliases:", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("# Order Aggregate", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("vaultmcp:learning-hash=", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("Defines the aggregate boundary for orders.", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("## Aliases", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("Order Root", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("## Examples", StringComparison.Ordinal).IsTrue();
        fileContent.Contains(" - domain", StringComparison.OrdinalIgnoreCase).IsTrue();
    }

    [Fact]
    public void CaptureLearning_appends_to_existing_note_for_same_title()
    {
        Directory.CreateDirectory(_root);
        var vault = new MarkdownVault(_root);

        vault.CaptureLearning(new VaultLearningCapture(
            "workflow",
            "Invoice Correction Flow",
            "Starts after invoice rejection.",
            null,
            []));

        var second = vault.CaptureLearning(new VaultLearningCapture(
            "workflow",
            "Invoice Correction Flow",
            "Requires customer notification before resubmission.",
            "Edge case: credit note already issued.",
            [],
            Steps: ["Notify customer", "Re-open invoice", "Re-submit for approval"]));

        second.Appended.IsTrue();
        second.Created.IsFalse();

        var fileContent = File.ReadAllText(Path.Combine(_root, "workflows", "invoice-correction-flow.md"));
        fileContent.Contains("## Learned", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("Requires customer notification before resubmission.", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("### Steps", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("1. Notify customer", StringComparison.Ordinal).IsTrue();
    }

    [Fact]
    public void CaptureLearning_returns_unchanged_when_same_learning_is_already_present()
    {
        Directory.CreateDirectory(_root);
        var vault = new MarkdownVault(_root);

        vault.CaptureLearning(new VaultLearningCapture(
            "invariant",
            "Tenant Boundary",
            "Tenant data must never cross account boundaries.",
            "Applies to exports and background jobs.",
            []));

        var second = vault.CaptureLearning(new VaultLearningCapture(
            "rule",
            "Tenant Boundary",
            "Tenant data must never cross account boundaries.",
            "Applies to exports and background jobs.",
            []));

        second.Unchanged.IsTrue();
        second.Created.IsFalse();
        second.Appended.IsFalse();
    }

    [Fact]
    public void CaptureLearning_uses_deterministic_hash_for_equivalent_content()
    {
        Directory.CreateDirectory(_root);
        var vault = new MarkdownVault(_root);

        vault.CaptureLearning(new VaultLearningCapture(
            "term",
            "Order Aggregate",
            "Defines the aggregate boundary for orders.",
            "Used in pricing and fulfillment flows.",
            [],
            Aliases: ["Order Root", "Sales Aggregate"],
            Examples: ["Pricing loads the aggregate before discount evaluation."]));

        var second = vault.CaptureLearning(new VaultLearningCapture(
            "term",
            "Order Aggregate",
            " defines   the aggregate boundary for orders. ",
            "Used in pricing and fulfillment flows.",
            [],
            Aliases: ["sales aggregate", "order root"],
            Examples: ["Pricing loads the aggregate before discount evaluation."]));

        second.Unchanged.IsTrue();
        second.Appended.IsFalse();
    }

    [Fact]
    public void CaptureLearning_normalizes_legacy_aliases_to_canonical_kind()
    {
        Directory.CreateDirectory(_root);
        var vault = new MarkdownVault(_root);

        var result = vault.CaptureLearning(new VaultLearningCapture(
            "glossary",
            "Payment Allocation",
            "Canonical meaning of payment allocation.",
            null,
            []));

        result.Kind.Is("term");
        result.Path.Is(Path.Combine("glossary", "payment-allocation.md"));

        var fileContent = File.ReadAllText(Path.Combine(_root, "glossary", "payment-allocation.md"));
        fileContent.Contains("kind: term", StringComparison.Ordinal).IsTrue();
    }

    [Fact]
    public void CaptureLearning_renders_structured_sections_for_data_flow()
    {
        Directory.CreateDirectory(_root);
        var vault = new MarkdownVault(_root);

        var result = vault.CaptureLearning(new VaultLearningCapture(
            "data-flow",
            "Invoice Export",
            "Moves approved invoices into the reporting pipeline.",
            null,
            [],
            Steps: ["Read approved invoices", "Map to export DTO", "Publish to reporting queue"],
            Source: "Billing database",
            Sink: "Reporting queue"));

        result.Path.Is(Path.Combine("data-flows", "invoice-export.md"));

        var fileContent = File.ReadAllText(Path.Combine(_root, "data-flows", "invoice-export.md"));
        fileContent.Contains("## Source", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("Billing database", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("## Sink", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("Reporting queue", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("## Steps", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("1. Read approved invoices", StringComparison.Ordinal).IsTrue();
    }

    [Fact]
    public void CaptureLearning_renders_structured_sections_for_decision()
    {
        Directory.CreateDirectory(_root);
        var vault = new MarkdownVault(_root);

        vault.CaptureLearning(new VaultLearningCapture(
            "decision",
            "Use Append-Only Learning Notes",
            "Keep learned knowledge reviewable in git.",
            null,
            [],
            Context: "Agents learn small domain facts during implementation.",
            Choice: "Append structured markdown instead of storing opaque blobs.",
            Consequence: "Humans can review diffs, but note growth must stay controlled."));

        var fileContent = File.ReadAllText(Path.Combine(_root, "decisions", "use-append-only-learning-notes.md"));
        fileContent.Contains("## Context", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("## Choice", StringComparison.Ordinal).IsTrue();
        fileContent.Contains("## Consequence", StringComparison.Ordinal).IsTrue();
    }

    [Fact]
    public void CaptureLearning_rejects_unknown_kind()
    {
        Directory.CreateDirectory(_root);
        var vault = new MarkdownVault(_root);

        var exception = Record.Exception(() => vault.CaptureLearning(new VaultLearningCapture(
            "random",
            "Something",
            "Summary",
            null,
            [])));

        exception.IsNotNull();
        exception.Is<ArgumentException>();
        exception.Message.Contains("Unsupported learning kind", StringComparison.Ordinal).IsTrue();
        exception.Message.Contains("term, workflow, data-flow, invariant, pitfall, decision", StringComparison.Ordinal).IsTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static void WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            Thread.Sleep(50);
        }

        Assert.True(condition());
    }
}

internal sealed class StubSearch(
    IReadOnlyList<VaultSearchResult>? searchNotes = null,
    IReadOnlyList<VaultSearchResult>? findTerm = null,
    IReadOnlyList<VaultSearchResult>? findRelatedNotes = null) : ISearch
{
    public int SearchNotesCalls { get; private set; }

    public IReadOnlyList<VaultSearchResult> SearchNotes(IReadOnlyList<VaultIndexedNote> notes, string query, int maxCount = 10)
    {
        SearchNotesCalls++;
        return (searchNotes ?? []).Take(maxCount).ToArray();
    }

    public IReadOnlyList<VaultSearchResult> FindTerm(IReadOnlyList<VaultIndexedNote> notes, string term, int maxCount = 10)
        => (findTerm ?? []).Take(maxCount).ToArray();

    public IReadOnlyList<VaultSearchResult> FindRelatedNotes(IReadOnlyList<VaultIndexedNote> notes, VaultIndexedNote source, int maxCount = 5)
        => (findRelatedNotes ?? []).Take(maxCount).ToArray();
}
