using System.ComponentModel;
using ModelContextProtocol.Server;
using VaultMcp.Tools;
using VaultMcp.Tools.KnowledgeBase;
using VaultMcp.Tools.KnowledgeBase.Search.Lexical;
using VaultMcp.Tools.KnowledgeBase.SemanticIndex;
using VaultMcp.Tools.KnowledgeBase.Vault;

namespace VaultMcp.Tools.Tools;

public sealed record RecallContextResponse(
    string Query,
    IReadOnlyList<VaultMatch> Matches,
    IReadOnlyList<VaultSemanticMatch> SemanticMatches,
    IReadOnlyList<VaultContextNote> Notes,
    IReadOnlyList<VaultMatch> RelatedNotes,
    ErrorInfo? Error = null,
    ErrorInfo? SemanticError = null)
{
    public static RecallContextResponse AsError(string query, ErrorInfo error)
        => new(query, [], [], [], [], error);
}

[McpServerToolType]
public sealed class RecallContextTool
{
    private readonly IVault _vault;
    private readonly ISemanticIndex? _semanticIndex;

    public RecallContextTool(IVault vault)
        : this(vault, null)
    {
    }

    public RecallContextTool(IVault vault, ISemanticIndex? semanticIndex)
    {
        _vault = vault;
        _semanticIndex = semanticIndex;
    }
    [McpServerTool(Name = "recall_context", Title = "Recall Context")]
    [Description("Default first retrieval tool for project, domain, and architecture knowledge. Combines term lookup, lexical note search, full note loading, related-note expansion, and optional semantic matches. Use this before asking the user to repeat domain context.")]
    public RecallContextResponse Execute(
        [Description("Domain term, workflow, rule, subsystem, or architecture concept to recall.")]
        string query,
        [Description("Maximum number of lexical candidate matches to inspect. Default: 3.")]
        int maxMatches = 3,
        [Description("Maximum number of full notes to load into the response. Default: 1.")]
        int loadTopNotes = 1,
        [Description("Maximum number of characters to load per note. Default: 4000.")]
        int maxCharsPerNote = 4000)
    {
        if (VaultToolErrors.ValidateReadableVault(_vault) is { } vaultError)
            return RecallContextResponse.AsError(query, vaultError);

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query);
            if (maxMatches <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxMatches), "maxMatches must be greater than zero.");
            if (loadTopNotes <= 0)
                throw new ArgumentOutOfRangeException(nameof(loadTopNotes), "loadTopNotes must be greater than zero.");
            if (maxCharsPerNote <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCharsPerNote), "maxCharsPerNote must be greater than zero.");

            var termMatches = _vault.FindTerm(query, maxMatches);
            var searchMatches = _vault.SearchNotes(query, maxMatches);

            var exactTermLookup = IsExactTermLookup(query, termMatches);
            var semanticLimit = exactTermLookup ? 0 : Math.Min(maxMatches, 3);
            var effectiveLoadTopNotes = exactTermLookup ? Math.Min(loadTopNotes, 1) : loadTopNotes;
            var effectiveRelatedNotes = exactTermLookup ? Math.Min(maxMatches, 2) : maxMatches;

            ErrorInfo? semanticError = null;
            IReadOnlyList<SemanticSearchHit> semanticMatches = [];
            if (_semanticIndex is not null && semanticLimit > 0)
            {
                try
                {
                    semanticMatches = _semanticIndex.Search(query, semanticLimit);
                }
                catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or IOException or SemanticIndexException)
                {
                    semanticError = VaultToolErrors.FromException(exception);
                }
            }

            var lexicalMatches = termMatches
                .Concat(searchMatches)
                .GroupBy(match => match.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(match => match.Score)
                    .ThenBy(match => match.Title, StringComparer.OrdinalIgnoreCase)
                    .First())
                .OrderByDescending(match => match.Score)
                .ThenBy(match => match.Title, StringComparer.OrdinalIgnoreCase)
                .Take(maxMatches)
                .ToArray();

            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var notePaths = new List<string>();

            if (!exactTermLookup)
            {
                foreach (var semanticMatch in semanticMatches)
                {
                    if (seenPaths.Add(semanticMatch.Path))
                        notePaths.Add(semanticMatch.Path);
                }
            }

            foreach (var result in lexicalMatches)
            {
                if (seenPaths.Add(result.Path))
                    notePaths.Add(result.Path);
            }

            if (exactTermLookup)
            {
                foreach (var semanticMatch in semanticMatches)
                {
                    if (seenPaths.Add(semanticMatch.Path))
                        notePaths.Add(semanticMatch.Path);
                }
            }

            var notes = notePaths
                .Take(effectiveLoadTopNotes)
                .Select(path => _vault.GetNote(path, maxCharsPerNote))
                .ToArray();

            var loadedPaths = notes
                .Select(note => note.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var relatedNotes = notes.Length == 0
                ? []
                : notes
                    .SelectMany(note => _vault.FindRelatedNotes(note.Path, maxMatches))
                    .Where(result => !loadedPaths.Contains(result.Path))
                    .Where(result => !lexicalMatches.Any(match => string.Equals(match.Path, result.Path, StringComparison.OrdinalIgnoreCase)))
                    .GroupBy(result => result.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group
                        .OrderByDescending(result => result.Score)
                        .First())
                    .OrderByDescending(result => result.Score)
                    .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
                    .Take(effectiveRelatedNotes)
                    .ToArray();

            return new RecallContextResponse(
                query,
                VaultToolPayloads.FromSearchResults(lexicalMatches),
                VaultToolPayloads.FromSemanticHits(semanticMatches.Take(semanticLimit)),
                VaultToolPayloads.FromContextDocuments(notes, query, maxCharsPerNote),
                VaultToolPayloads.FromSearchResults(relatedNotes),
                SemanticError: semanticError);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or FileNotFoundException or DirectoryNotFoundException or IOException)
        {
            return RecallContextResponse.AsError(query, VaultToolErrors.FromException(exception));
        }
    }

    private static bool IsExactTermLookup(string query, IReadOnlyList<VaultSearchResult> termMatches)
    {
        if (termMatches.Count == 0)
            return false;

        var normalizedQuery = query.NormalizeForComparison();
        var queryTerms = query.ExtractTerms();
        if (queryTerms.Count > 3)
            return false;

        var top = termMatches[0];
        if (!string.Equals(top.Kind, "term", StringComparison.OrdinalIgnoreCase))
            return false;

        if (top.Title.NormalizeForComparison().Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            return true;

        var fileName = Path.GetFileNameWithoutExtension(top.Path);
        return fileName.NormalizeForComparison().Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }
}
