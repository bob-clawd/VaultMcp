using System.Text;

namespace VaultMcp.Tools.KnowledgeBase.Search.Lexical;

internal static class LexicalSearchTextExtensions
{
    private static readonly HashSet<string> StopWords =
    [
        "the", "and", "oder", "und", "der", "die", "das", "with", "when", "from", "into", "this", "that", "than", "then",
        "have", "must", "should", "will", "uses", "used", "using", "after", "before", "through", "across", "flow", "note",
        "kind", "captured", "summary", "details", "domain"
    ];

    public static string? FindBestSharedTerm(this VaultIndexedNote source, VaultIndexedNote candidate)
    {
        var titleTerms = candidate.Title.ExtractTerms();
        foreach (var term in source.ExtractedTerms.OrderByDescending(t => t.Length))
        {
            if (titleTerms.Contains(term))
                return term;
        }

        foreach (var term in source.ExtractedTerms.OrderByDescending(t => t.Length))
        {
            if (candidate.BodyContent.Contains(term, StringComparison.OrdinalIgnoreCase))
                return term;
        }

        return null;
    }

    public static string BuildExcerpt(this string content, string query)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var index = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return FirstNonEmptyLine(content);

        var start = Math.Max(0, index - LexicalSearchScoringOptions.Default.ExcerptRadius);
        var end = Math.Min(content.Length, index + query.Length + LexicalSearchScoringOptions.Default.ExcerptRadius);
        var snippet = content[start..end].Trim();
        var collapsed = CollapseWhitespace(snippet);

        if (start > 0)
            collapsed = "…" + collapsed;
        if (end < content.Length)
            collapsed += "…";

        return collapsed;
    }

    public static HashSet<string> ExtractTerms(this string value)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                continue;
            }

            AddToken(builder, terms);
        }

        AddToken(builder, terms);
        return terms;
    }

    internal static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasWhitespace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (lastWasWhitespace)
                    continue;

                builder.Append(' ');
                lastWasWhitespace = true;
                continue;
            }

            builder.Append(ch);
            lastWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    private static string FirstNonEmptyLine(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                return CollapseWhitespace(trimmed);
        }

        return string.Empty;
    }

    private static void AddToken(StringBuilder builder, HashSet<string> terms)
    {
        if (builder.Length == 0)
            return;

        var token = builder.ToString();
        builder.Clear();

        if (token.Length < 4 || StopWords.Contains(token))
            return;

        terms.Add(token);
    }
}
