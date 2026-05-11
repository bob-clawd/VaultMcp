namespace VaultMcp.Tools.KnowledgeBase;

public sealed record VaultStatus(
    string RootPath,
    bool Exists,
    int NoteCount,
    IReadOnlyList<string> SupportedExtensions);

public sealed record VaultNote(
    string Path,
    string Title);

public sealed record VaultNoteDocument(
    string Path,
    string Title,
    string Content,
    bool IsTruncated = false,
    string? Kind = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<string>? Headings = null);

public sealed record VaultSearchResult(
    string Path,
    string Title,
    string Excerpt,
    int Score,
    string? Kind = null,
    IReadOnlyList<string>? Tags = null);

public sealed record VaultCaptureResult(
    string Path,
    string Title,
    string Kind,
    bool Created,
    bool Appended,
    bool Unchanged,
    string Message);

public sealed record VaultLearningCapture(
    string Kind,
    string Title,
    string Summary,
    string? Details,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<string>? Examples = null,
    IReadOnlyList<string>? Steps = null,
    string? Source = null,
    string? Sink = null,
    string? Scope = null,
    string? FailureMode = null,
    string? Symptom = null,
    string? Cause = null,
    string? Fix = null,
    string? Context = null,
    string? Choice = null,
    string? Consequence = null);

internal sealed record VaultFrontmatter(
    string? Kind,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Related,
    string? Confidence)
{
    public static VaultFrontmatter Empty { get; } = new(null, [], [], [], null);
}

internal sealed record VaultIndexedNote(
    string FullPath,
    string RelativePath,
    string Title,
    string RawContent,
    string BodyContent,
    IReadOnlyList<string> Headings,
    VaultFrontmatter Frontmatter,
    HashSet<string> ExtractedTerms,
    long FileSizeBytes,
    DateTime LastWriteTimeUtc,
    bool IsIndexTruncated);

