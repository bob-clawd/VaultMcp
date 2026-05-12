using VaultMcp.Tools.KnowledgeBase;
using VaultMcp.Tools.KnowledgeBase.SemanticIndex;
using VaultMcp.Tools.KnowledgeBase.Vault.Markdown;

namespace VaultMcp.Tools.Tools;

public sealed record VaultMatch(
    string Path,
    string Title,
    string Excerpt,
    string? Kind = null);

public sealed record VaultSemanticMatch(
    string Path,
    string Title,
    string? Heading,
    string TextPreview);

public sealed record VaultContextNote(
    string Path,
    string Title,
    string Content,
    bool IsTruncated = false,
    string? Kind = null,
    IReadOnlyList<string>? Aliases = null);

internal static class VaultToolPayloads
{
    public static VaultMatch FromSearchResult(VaultSearchResult result)
        => new(result.Path, result.Title, result.Excerpt, result.Kind);

    public static IReadOnlyList<VaultMatch> FromSearchResults(IEnumerable<VaultSearchResult> results)
        => results.Select(FromSearchResult).ToArray();

    public static VaultSemanticMatch FromSemanticHit(SemanticSearchHit hit)
        => new(hit.Path, hit.Title, hit.Heading, hit.TextPreview);

    public static IReadOnlyList<VaultSemanticMatch> FromSemanticHits(IEnumerable<SemanticSearchHit> hits)
        => hits.Select(FromSemanticHit).ToArray();

    public static VaultContextNote FromDocument(VaultNoteDocument note)
    {
        var parsed = VaultMarkdownParser.Parse(note.Content, note.Title);
        var aliases = note.Aliases is { Count: > 0 } ? note.Aliases : null;
        return new VaultContextNote(note.Path, note.Title, parsed.BodyContent, note.IsTruncated, note.Kind, aliases);
    }

    public static IReadOnlyList<VaultContextNote> FromDocuments(IEnumerable<VaultNoteDocument> notes)
        => notes.Select(FromDocument).ToArray();
}
