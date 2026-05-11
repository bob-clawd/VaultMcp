using System.Text;
using VaultMcp.Tools.KnowledgeBase.Vault;

namespace VaultMcp.Tools.KnowledgeBase.Vault.Markdown;

internal sealed record VaultParsedNote(
    string RawContent,
    string BodyContent,
    string Title,
    IReadOnlyList<string> Headings,
    VaultFrontmatter Frontmatter);

internal static class VaultMarkdownParser
{
    public static VaultParsedNote Parse(string rawContent, string fallbackTitle)
    {
        var bodyContent = rawContent;
        var frontmatter = VaultFrontmatter.Empty;

        if (TryExtractFrontmatter(rawContent, out var frontmatterText, out var remainingBody))
        {
            frontmatter = ParseFrontmatter(frontmatterText);
            bodyContent = remainingBody;
        }

        var headings = ExtractHeadings(bodyContent);
        var title = headings.FirstOrDefault() ?? fallbackTitle;
        return new VaultParsedNote(rawContent, bodyContent, title, headings, frontmatter);
    }

    private static bool TryExtractFrontmatter(string rawContent, out string frontmatterText, out string remainingBody)
    {
        frontmatterText = string.Empty;
        remainingBody = rawContent;

        using var reader = new StringReader(rawContent);
        var firstLine = reader.ReadLine();
        if (!string.Equals(firstLine?.Trim(), "---", StringComparison.Ordinal))
            return false;

        var builder = new StringBuilder();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.Equals(line.Trim(), "---", StringComparison.Ordinal))
            {
                frontmatterText = builder.ToString();
                remainingBody = reader.ReadToEnd().TrimStart('\r', '\n');
                return true;
            }

            builder.AppendLine(line);
        }

        return false;
    }

    private static VaultFrontmatter ParseFrontmatter(string frontmatterText)
    {
        string? kind = null;
        string? confidence = null;
        var tags = new List<string>();
        var aliases = new List<string>();
        var related = new List<string>();
        string? currentListKey = null;

        foreach (var rawLine in frontmatterText.Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) && currentListKey is not null)
            {
                AddListValue(currentListKey, trimmed[2..]);
                continue;
            }

            currentListKey = null;
            var separatorIndex = trimmed.IndexOf(':');
            if (separatorIndex <= 0)
                continue;

            var key = trimmed[..separatorIndex].Trim().ToLowerInvariant();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                if (IsListKey(key))
                    currentListKey = key;
                continue;
            }

            if (IsInlineList(value))
            {
                foreach (var item in ParseInlineList(value))
                    AddListValue(key, item);

                continue;
            }

            var normalizedValue = Unquote(value);
            switch (key)
            {
                case "kind":
                    kind = normalizedValue;
                    break;
                case "confidence":
                    confidence = normalizedValue;
                    break;
                case "tags":
                case "aliases":
                case "related":
                    AddListValue(key, normalizedValue);
                    break;
            }
        }

        return new VaultFrontmatter(
            kind,
            NormalizeList(tags),
            NormalizeList(aliases),
            NormalizeList(related),
            confidence);

        void AddListValue(string key, string value)
        {
            var normalizedValue = Unquote(value);
            if (string.IsNullOrWhiteSpace(normalizedValue))
                return;

            switch (key)
            {
                case "tags":
                    tags.Add(normalizedValue);
                    break;
                case "aliases":
                    aliases.Add(normalizedValue);
                    break;
                case "related":
                    related.Add(normalizedValue);
                    break;
            }
        }
    }

    private static IReadOnlyList<string> ExtractHeadings(string bodyContent)
    {
        var headings = new List<string>();
        foreach (var rawLine in bodyContent.Split('\n'))
        {
            var trimmed = rawLine.Trim();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
                continue;

            var heading = trimmed.TrimStart('#', ' ').Trim();
            if (!string.IsNullOrWhiteSpace(heading))
                headings.Add(heading);
        }

        return headings;
    }

    private static bool IsListKey(string key) =>
        key is "tags" or "aliases" or "related";

    private static bool IsInlineList(string value) =>
        value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal);

    private static IEnumerable<string> ParseInlineList(string value)
    {
        var inner = value[1..^1];
        foreach (var item in inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            yield return item;
    }

    private static string Unquote(string value) => value.Trim().Trim('"', '\'');

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
