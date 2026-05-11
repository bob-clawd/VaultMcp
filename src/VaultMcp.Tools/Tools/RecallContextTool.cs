using System.ComponentModel;
using ModelContextProtocol.Server;
using VaultMcp.Tools;
using VaultMcp.Tools.KnowledgeBase;
using VaultMcp.Tools.KnowledgeBase.SemanticIndex;
using VaultMcp.Tools.KnowledgeBase.Vault;

namespace VaultMcp.Tools.Tools;

public sealed record RecallContextResponse(
    string Query,
    IReadOnlyList<VaultSearchResult> TermMatches,
    IReadOnlyList<VaultSearchResult> SearchMatches,
    IReadOnlyList<SemanticSearchHit> SemanticMatches,
    IReadOnlyList<VaultNoteDocument> Notes,
    IReadOnlyList<VaultSearchResult> RelatedNotes,
    ErrorInfo? Error = null,
    ErrorInfo? SemanticError = null)
{
    public static RecallContextResponse AsError(string query, ErrorInfo error)
        => new(query, [], [], [], [], [], error);
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
        [Description("Maximum number of candidate matches to inspect from term lookup and search. Default: 5.")]
        int maxMatches = 5,
        [Description("Maximum number of full notes to load into the response. Default: 2.")]
        int loadTopNotes = 2,
        [Description("Maximum number of characters to load per note. Default: 8000.")]
        int maxCharsPerNote = 8000)
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

            ErrorInfo? semanticError = null;
            IReadOnlyList<SemanticSearchHit> semanticMatches = [];
            if (_semanticIndex is not null)
            {
                try
                {
                    semanticMatches = _semanticIndex.Search(query, maxMatches);
                }
                catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or IOException or SemanticIndexException)
                {
                    semanticError = VaultToolErrors.FromException(exception);
                }
            }

            var notePaths = new List<string>();

            foreach (var semanticMatch in semanticMatches)
            {
                if (!notePaths.Any(path => string.Equals(path, semanticMatch.Path, StringComparison.OrdinalIgnoreCase)))
                    notePaths.Add(semanticMatch.Path);
            }

            foreach (var result in termMatches
                         .Concat(searchMatches)
                         .GroupBy(match => match.Path, StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.OrderByDescending(match => match.Score).First())
                         .OrderByDescending(match => match.Score)
                         .ThenBy(match => match.Title, StringComparer.OrdinalIgnoreCase))
            {
                if (!notePaths.Any(path => string.Equals(path, result.Path, StringComparison.OrdinalIgnoreCase)))
                    notePaths.Add(result.Path);
            }

            var notes = notePaths
                .Take(loadTopNotes)
                .Select(path => _vault.GetNote(path, maxCharsPerNote))
                .ToArray();

            var relatedNotes = notes.Length == 0
                ? []
                : notes
                    .SelectMany(note => _vault.FindRelatedNotes(note.Path, maxMatches))
                    .GroupBy(result => result.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group
                        .OrderByDescending(result => result.Score)
                        .First())
                    .OrderByDescending(result => result.Score)
                    .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
                    .Take(maxMatches)
                    .ToArray();

            return new RecallContextResponse(query, termMatches, searchMatches, semanticMatches, notes, relatedNotes, SemanticError: semanticError);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or FileNotFoundException or DirectoryNotFoundException or IOException)
        {
            return RecallContextResponse.AsError(query, VaultToolErrors.FromException(exception));
        }
    }
}
