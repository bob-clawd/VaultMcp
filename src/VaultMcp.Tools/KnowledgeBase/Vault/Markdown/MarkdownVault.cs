using System.Text;
using VaultMcp.Tools.KnowledgeBase.Vault;
using VaultMcp.Tools.KnowledgeBase.Search;
using VaultMcp.Tools.KnowledgeBase.Search.Lexical;

namespace VaultMcp.Tools.KnowledgeBase.Vault.Markdown;

public sealed class MarkdownVault : IVault, IDisposable
{
    private const int DefaultGetNoteMaxChars = 12000;
    private const int MaxIndexedCharacters = 64000;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly string[] Extensions = [".md", ".markdown"];

    private readonly ISearch _search;
    private readonly object _sync = new();
    private readonly string _rootPath;
    private readonly StringComparer _pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private Dictionary<string, VaultIndexedNote> _index = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private FileSystemWatcher? _watcher;
    private bool _indexDirty = true;

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

        EnsureWatcher();
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
        Dictionary<string, VaultIndexedNote> currentIndex;

        lock (_sync)
        {
            EnsureWatcher();

            if (!_indexDirty)
                return _index.Values.ToArray();

            currentIndex = new Dictionary<string, VaultIndexedNote>(_index, _pathComparer);
        }

        var nextIndex = BuildIndexSnapshot(currentIndex);

        lock (_sync)
        {
            EnsureWatcher();

            if (_indexDirty)
            {
                _index = nextIndex;
                _indexDirty = false;
            }

            return _index.Values.ToArray();
        }
    }

    private Dictionary<string, VaultIndexedNote> BuildIndexSnapshot(IReadOnlyDictionary<string, VaultIndexedNote> currentIndex)
    {
        var currentFiles = EnumerateFiles().ToArray();
        var nextIndex = new Dictionary<string, VaultIndexedNote>(_pathComparer);

        foreach (var path in currentFiles)
        {
            var info = new FileInfo(path);
            if (currentIndex.TryGetValue(path, out var existing) &&
                existing.LastWriteTimeUtc == info.LastWriteTimeUtc &&
                existing.FileSizeBytes == info.Length)
            {
                nextIndex[path] = existing;
                continue;
            }

            nextIndex[path] = LoadIndexedNote(path, info);
        }

        return nextIndex;
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
        => VaultPathGuard.ResolvePath(_rootPath, relativePath, Extensions);

    private void RefreshIndex(string fullPath)
    {
        lock (_sync)
        {
            EnsureWatcher();

            if (!File.Exists(fullPath))
            {
                _index.Remove(fullPath);
                _indexDirty = true;
                return;
            }

            var info = new FileInfo(fullPath);
            _index[fullPath] = LoadIndexedNote(fullPath, info);
            _indexDirty = true;
        }
    }

    private void EnsureWatcher()
    {
        if (_watcher is not null || !Directory.Exists(_rootPath))
            return;

        _watcher = new FileSystemWatcher(_rootPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Changed += OnFileSystemChanged;
        _watcher.Created += OnFileSystemChanged;
        _watcher.Deleted += OnFileSystemChanged;
        _watcher.Renamed += OnFileSystemRenamed;
        _watcher.Error += OnWatcherError;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs args)
    {
        if (ShouldInvalidateForPath(args.FullPath))
            MarkIndexDirty();
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs args)
    {
        if (ShouldInvalidateForPath(args.FullPath) || ShouldInvalidateForPath(args.OldFullPath))
            MarkIndexDirty();
    }

    private void OnWatcherError(object sender, ErrorEventArgs args)
    {
        lock (_sync)
        {
            if (_watcher is not null)
            {
                _watcher.Dispose();
                _watcher = null;
            }

            _indexDirty = true;
        }
    }

    private void MarkIndexDirty()
    {
        lock (_sync)
        {
            _indexDirty = true;
        }
    }

    private static bool ShouldInvalidateForPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        var extension = Path.GetExtension(path);
        return string.IsNullOrEmpty(extension) || Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_watcher is null)
                return;

            _watcher.Dispose();
            _watcher = null;
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
