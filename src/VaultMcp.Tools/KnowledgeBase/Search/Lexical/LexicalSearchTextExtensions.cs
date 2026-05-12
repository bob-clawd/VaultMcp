using System.Globalization;
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

        var comparableBody = candidate.BodyContent.NormalizeForComparison();
        foreach (var term in source.ExtractedTerms.OrderByDescending(t => t.Length))
        {
            if (comparableBody.Contains(term, StringComparison.OrdinalIgnoreCase))
                return term;
        }

        return null;
    }

    public static string BuildExcerpt(this string content, string query)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var index = FindComparableIndex(content, query);
        if (index < 0)
            return FirstNonEmptyLine(content);

        var start = Math.Max(0, index - LexicalSearchScoringOptions.Default.ExcerptRadius);
        var end = Math.Min(content.Length, index + query.Length + LexicalSearchScoringOptions.Default.ExcerptRadius);
        var snippet = content[start..end].Trim();
        var collapsed = snippet.CollapseWhitespace();

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

        foreach (var ch in value.NormalizeForComparison())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                continue;
            }

            AddToken(builder, terms);
        }

        AddToken(builder, terms);
        return terms;
    }

    internal static string CollapseWhitespace(this string value)
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

    internal static string NormalizeForComparison(this string value)
        => BuildComparableText(value).Text.Trim();

    private static string FirstNonEmptyLine(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                return trimmed.CollapseWhitespace();
        }

        return string.Empty;
    }

    private static int FindComparableIndex(string text, string query)
    {
        var directIndex = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (directIndex >= 0)
            return directIndex;

        var normalizedQuery = query.NormalizeForComparison();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return -1;

        var comparable = BuildComparableText(text);
        var comparableIndex = comparable.Text.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase);
        return comparableIndex < 0 ? -1 : comparable.IndexMap[comparableIndex];
    }

    private static (string Text, List<int> IndexMap) BuildComparableText(string value)
    {
        var builder = new StringBuilder(value.Length);
        var indexMap = new List<int>(value.Length);
        var lastWasWhitespace = false;

        for (var index = 0; index < value.Length; index++)
        {
            var ch = value[index];
            if (char.IsWhiteSpace(ch))
            {
                if (lastWasWhitespace)
                    continue;

                builder.Append(' ');
                indexMap.Add(index);
                lastWasWhitespace = true;
                continue;
            }

            AppendComparableCharacter(builder, indexMap, ch, index);
            lastWasWhitespace = false;
        }

        return (builder.ToString(), indexMap);
    }

    private static void AppendComparableCharacter(StringBuilder builder, List<int> indexMap, char ch, int sourceIndex)
    {
        switch (char.ToLowerInvariant(ch))
        {
            case 'ä':
                builder.Append("ae");
                indexMap.Add(sourceIndex);
                indexMap.Add(sourceIndex);
                return;
            case 'ö':
                builder.Append("oe");
                indexMap.Add(sourceIndex);
                indexMap.Add(sourceIndex);
                return;
            case 'ü':
                builder.Append("ue");
                indexMap.Add(sourceIndex);
                indexMap.Add(sourceIndex);
                return;
            case 'ß':
                builder.Append("ss");
                indexMap.Add(sourceIndex);
                indexMap.Add(sourceIndex);
                return;
        }

        foreach (var normalizedChar in ch.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(normalizedChar) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(char.ToLowerInvariant(normalizedChar));
            indexMap.Add(sourceIndex);
        }
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
