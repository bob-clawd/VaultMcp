using System.Text;
using VaultMcp.Tools.KnowledgeBase.Vault;
using VaultMcp.Tools.KnowledgeBase.Search;
using VaultMcp.Tools.KnowledgeBase.Search.Lexical;

namespace VaultMcp.Tools.KnowledgeBase.Vault.Markdown;

public sealed class MarkdownVault : IVault
{
    private const int DefaultGetNoteMaxChars = 12000;
    private const int MaxIndexedCharacters = 64000;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly string[] Extensions = [".md", ".markdown"];

    private readonly ISearch _search;
    private readonly object _sync = new();
    private readonly string _rootPath;
    private readonly StringComparer _pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly StringComparison _pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private Dictionary<string, VaultIndexedNote> _index = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public MarkdownVault(string rootPath)
        : this(rootPath, new LexicalSearch())
    {
    }

    internal MarkdownVault(string rootPath, ISearch search)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(search);

        _rootPath = Path.GetFullPath(rootPath);
        _search = search;
    }

    public VaultStatus GetStatus()
    {
        var notes = GetIndexedNotes();
        return new VaultStatus(_rootPath, Directory.Exists(_rootPath), notes.Count, Extensions);
    }

    public IReadOnlyList<VaultNote> ListNotes(int maxCount = 100)
    {
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "maxCount must be greater than zero.");

        return GetIndexedNotes()
            .OrderBy(note => note.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .Select(note => new VaultNote(note.RelativePath, note.Title))
            .ToArray();
    }

    public VaultNoteDocument GetNote(string relativePath, int maxChars = DefaultGetNoteMaxChars)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (maxChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChars), "maxChars must be greater than zero.");

        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Note '{relativePath}' was not found in the vault.", relativePath);

        var entry = GetIndexedNote(fullPath);
        var content = VaultMarkdownParser.StripInternalMarkers(ReadTextUtf8(fullPath, maxChars, out var truncated));
        return new VaultNoteDocument(
            entry.RelativePath,
            entry.Title,
            content,
            truncated,
            entry.Frontmatter.Kind,
            entry.Frontmatter.Tags,
            entry.Frontmatter.Aliases,
            entry.Headings);
    }

    public IReadOnlyList<VaultSearchResult> SearchNotes(string query, int maxCount = 10)
        => _search.SearchNotes(GetIndexedNotes(), query, maxCount);

    public IReadOnlyList<VaultSearchResult> FindTerm(string term, int maxCount = 10)
        => _search.FindTerm(GetIndexedNotes(), term, maxCount);

    public IReadOnlyList<VaultSearchResult> FindRelatedNotes(string relativePath, int maxCount = 5)
        => _search.FindRelatedNotes(GetIndexedNotes(), GetIndexedNote(ResolvePath(relativePath)), maxCount);

    public VaultCaptureResult CaptureLearning(VaultLearningCapture learning)
    {
        ArgumentNullException.ThrowIfNull(learning);
        ArgumentException.ThrowIfNullOrWhiteSpace(learning.Kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(learning.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(learning.Summary);

        var normalizedKind = learning.Kind.NormalizeKind();
        var title = learning.Title.Trim();
        var tags = learning.Tags.NormalizeTags(normalizedKind);
        var relativePath = Path.Combine(normalizedKind.MapKindToDirectory(), title.ToSlug() + ".md");
        var fullPath = ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        if (!File.Exists(fullPath))
        {
            File.WriteAllText(fullPath, learning.BuildNewNote(title, normalizedKind, tags, DateTimeOffset.UtcNow), Utf8);
            RefreshIndex(fullPath);
            return new VaultCaptureResult(ToRelativePath(fullPath), title, normalizedKind, Created: true, Appended: false, Unchanged: false, Message: "Created new knowledge note.");
        }

        var existing = File.ReadAllText(fullPath, Utf8);
        if (existing.ContainsLearning(learning, normalizedKind, title))
            return new VaultCaptureResult(ToRelativePath(fullPath), title, normalizedKind, Created: false, Appended: false, Unchanged: true, Message: "Learning already present in note.");

        var separator = existing.EndsWith(Environment.NewLine, StringComparison.Ordinal)
            ? Environment.NewLine
            : Environment.NewLine + Environment.NewLine;
        var updated = existing + separator + learning.BuildAppendBlock(normalizedKind, title, DateTimeOffset.UtcNow);
        File.WriteAllText(fullPath, updated, Utf8);
        RefreshIndex(fullPath);
        return new VaultCaptureResult(ToRelativePath(fullPath), title, normalizedKind, Created: false, Appended: true, Unchanged: false, Message: "Appended learning to existing note.");
    }

    private IReadOnlyList<VaultIndexedNote> GetIndexedNotes()
    {
        lock (_sync)
        {
            var currentFiles = EnumerateFiles().ToArray();
            var nextIndex = new Dictionary<string, VaultIndexedNote>(_pathComparer);

            foreach (var path in currentFiles)
            {
                var info = new FileInfo(path);
                if (_index.TryGetValue(path, out var existing) &&
                    existing.LastWriteTimeUtc == info.LastWriteTimeUtc &&
                    existing.FileSizeBytes == info.Length)
                {
                    nextIndex[path] = existing;
                    continue;
                }

                nextIndex[path] = LoadIndexedNote(path, info);
            }

            _index = nextIndex;
            return _index.Values.ToArray();
        }
    }

    private VaultIndexedNote GetIndexedNote(string fullPath)
    {
        var notes = GetIndexedNotes();
        if (_index.TryGetValue(fullPath, out var note))
            return note;

        throw new FileNotFoundException($"Note '{ToRelativePath(fullPath)}' was not found in the vault.", ToRelativePath(fullPath));
    }

    private VaultIndexedNote LoadIndexedNote(string path, FileInfo info)
    {
        var rawContent = ReadTextUtf8(path, MaxIndexedCharacters, out var truncated);
        var parsed = VaultMarkdownParser.Parse(rawContent, Path.GetFileNameWithoutExtension(path));
        var terms = string.Join(
                Environment.NewLine,
                new[]
                {
                    parsed.Title,
                    parsed.BodyContent,
                    string.Join(Environment.NewLine, parsed.Headings),
                    string.Join(Environment.NewLine, parsed.Frontmatter.Aliases),
                    string.Join(Environment.NewLine, parsed.Frontmatter.Tags)
                })
            .ExtractTerms();

        return new VaultIndexedNote(
            path,
            ToRelativePath(path),
            parsed.Title,
            parsed.RawContent,
            parsed.BodyContent,
            parsed.Headings,
            parsed.Frontmatter,
            terms,
            info.Length,
            info.LastWriteTimeUtc,
            truncated);
    }

    private IEnumerable<string> EnumerateFiles()
    {
        if (!Directory.Exists(_rootPath))
            return [];

        return Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories)
            .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));
    }

    private string ResolvePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("Only vault-relative note paths are allowed.", nameof(relativePath));

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, _pathComparison) &&
            !string.Equals(fullPath, _rootPath, _pathComparison))
        {
            throw new ArgumentException("The requested note path escapes the configured vault root.", nameof(relativePath));
        }

        if (!Extensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Only markdown note paths are allowed.", nameof(relativePath));

        return fullPath;
    }

    private void RefreshIndex(string fullPath)
    {
        lock (_sync)
        {
            if (!File.Exists(fullPath))
            {
                _index.Remove(fullPath);
                return;
            }

            var info = new FileInfo(fullPath);
            _index[fullPath] = LoadIndexedNote(fullPath, info);
        }
    }

    private string ToRelativePath(string path) => Path.GetRelativePath(_rootPath, path);

    private static string ReadTextUtf8(string path, int maxChars, out bool truncated)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: true);

        var builder = new StringBuilder(Math.Min(maxChars, 8192));
        var buffer = new char[Math.Min(maxChars, 4096)];
        var remaining = maxChars;

        while (remaining > 0)
        {
            var read = reader.Read(buffer, 0, Math.Min(buffer.Length, remaining));
            if (read <= 0)
                break;

            builder.Append(buffer, 0, read);
            remaining -= read;
        }

        truncated = !reader.EndOfStream;
        return builder.ToString();
    }
}
