using System.Text;
using VaultMcp.Tools.KnowledgeBase.Vault;
using System.Security.Cryptography;

namespace VaultMcp.Tools.KnowledgeBase.Vault.Markdown;

internal static class VaultCaptureExtensions
{
    private static readonly string[] CaptureKinds = ["term", "workflow", "data-flow", "invariant", "pitfall", "decision"];

    public static string NormalizeKind(this string kind)
    {
        var normalized = kind.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
        return normalized switch
        {
            "term" or "terms" or "glossary" or "domain-term" or "concept" or "concepts" => "term",
            "workflow" or "workflows" => "workflow",
            "data-flow" or "dataflow" or "flow" => "data-flow",
            "invariant" or "rule" or "rules" => "invariant",
            "pitfall" or "gotcha" => "pitfall",
            "decision" or "adr" => "decision",
            _ when CaptureKinds.Contains(normalized, StringComparer.OrdinalIgnoreCase) => normalized,
            _ => throw new ArgumentException($"Unsupported learning kind '{kind}'. Allowed kinds: {string.Join(", ", CaptureKinds)}.", nameof(kind))
        };
    }

    public static string[] NormalizeTags(this IReadOnlyList<string>? tags, string kind)
    {
        var normalized = (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToSlug())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var required in new[] { "domain", kind })
        {
            if (!normalized.Contains(required, StringComparer.OrdinalIgnoreCase))
                normalized.Add(required);
        }

        return normalized.ToArray();
    }

    public static string MapKindToDirectory(this string kind) => kind switch
    {
        "term" => "glossary",
        "workflow" => "workflows",
        "data-flow" => "data-flows",
        "invariant" => "invariants",
        "pitfall" => "pitfalls",
        "decision" => "decisions",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported learning kind.")
    };

    public static string ToSlug(this string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasDash = false;
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasDash = false;
                continue;
            }

            if (lastWasDash)
                continue;

            builder.Append('-');
            lastWasDash = true;
        }

        return builder.ToString().Trim('-');
    }

    public static string BuildNewNote(this VaultLearningCapture learning, string title, string kind, IReadOnlyList<string> tags, DateTimeOffset timestamp)
    {
        var learningHash = learning.ComputeLearningHash(kind, title);
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"kind: {kind}");
        builder.AppendLine("tags:");
        foreach (var tag in tags)
            builder.AppendLine($" - {tag}");
        var aliases = NormalizeList(learning.Aliases);
        if (aliases.Length > 0)
        {
            builder.AppendLine("aliases:");
            foreach (var alias in aliases)
                builder.AppendLine($" - {alias}");
        }
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {title.Trim()}");
        builder.AppendLine();
        builder.AppendLine(learningHash.ToHashMarker());
        builder.AppendLine($"Captured: `{timestamp:yyyy-MM-dd HH:mm 'UTC'}`");
        builder.AppendLine();
        builder.AppendLine("## Summary");
        builder.AppendLine(learning.Summary.Trim());

        builder.AppendStructuredSections(kind, learning, "##");

        if (!string.IsNullOrWhiteSpace(learning.Details))
        {
            builder.AppendLine();
            builder.AppendLine("## Details");
            builder.AppendLine(learning.Details.Trim());
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildAppendBlock(this VaultLearningCapture learning, string kind, string title, DateTimeOffset timestamp)
    {
        var learningHash = learning.ComputeLearningHash(kind, title);
        var builder = new StringBuilder();
        builder.AppendLine($"## Learned {timestamp:yyyy-MM-dd HH:mm 'UTC'}");
        builder.AppendLine();
        builder.AppendLine(learningHash.ToHashMarker());
        builder.AppendLine("### Summary");
        builder.AppendLine(learning.Summary.Trim());

        builder.AppendStructuredSections(kind, learning, "###");

        if (!string.IsNullOrWhiteSpace(learning.Details))
        {
            builder.AppendLine();
            builder.AppendLine("### Details");
            builder.AppendLine(learning.Details.Trim());
        }

        return builder.ToString().TrimEnd();
    }

    public static bool ContainsLearning(this string existing, VaultLearningCapture learning, string kind, string title)
    {
        if (existing.Contains(learning.ComputeLearningHash(kind, title).ToHashMarker(), StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var fragment in learning.ComparableFragments())
        {
            if (!existing.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static string ComputeLearningHash(this VaultLearningCapture learning, string kind, string title)
    {
        var canonical = string.Join("\n", new[]
        {
            $"kind={NormalizeScalar(kind)}",
            $"title={NormalizeScalar(title)}",
            $"summary={NormalizeScalar(learning.Summary)}",
            $"details={NormalizeScalar(learning.Details)}",
            $"aliases={string.Join("|", NormalizeList(learning.Aliases).Select(NormalizeScalar).OrderBy(x => x, StringComparer.Ordinal))}",
            $"examples={string.Join("|", NormalizeList(learning.Examples).Select(NormalizeScalar).OrderBy(x => x, StringComparer.Ordinal))}",
            $"steps={string.Join("|", NormalizeList(learning.Steps).Select(NormalizeScalar).OrderBy(x => x, StringComparer.Ordinal))}",
            $"source={NormalizeScalar(learning.Source)}",
            $"sink={NormalizeScalar(learning.Sink)}",
            $"scope={NormalizeScalar(learning.Scope)}",
            $"failureMode={NormalizeScalar(learning.FailureMode)}",
            $"symptom={NormalizeScalar(learning.Symptom)}",
            $"cause={NormalizeScalar(learning.Cause)}",
            $"fix={NormalizeScalar(learning.Fix)}",
            $"context={NormalizeScalar(learning.Context)}",
            $"choice={NormalizeScalar(learning.Choice)}",
            $"consequence={NormalizeScalar(learning.Consequence)}"
        });

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hashBytes).ToLowerInvariant()[..12];
    }

    public static string ToHashMarker(this string hash) => $"<!-- vaultmcp:learning-hash={hash} -->";

    private static IEnumerable<string> ComparableFragments(this VaultLearningCapture learning)
    {
        yield return learning.Summary.Trim();

        if (!string.IsNullOrWhiteSpace(learning.Details))
            yield return learning.Details.Trim();

        foreach (var value in NormalizeList(learning.Aliases))
            yield return value;
        foreach (var value in NormalizeList(learning.Examples))
            yield return value;
        foreach (var value in NormalizeList(learning.Steps))
            yield return value;

        foreach (var value in new[]
        {
            learning.Source,
            learning.Sink,
            learning.Scope,
            learning.FailureMode,
            learning.Symptom,
            learning.Cause,
            learning.Fix,
            learning.Context,
            learning.Choice,
            learning.Consequence
        })
        {
            if (!string.IsNullOrWhiteSpace(value))
                yield return value.Trim();
        }
    }

    private static void AppendStructuredSections(this StringBuilder builder, string kind, VaultLearningCapture learning, string headingPrefix)
    {
        switch (kind)
        {
            case "term":
                builder.AppendListSection(headingPrefix, "Aliases", learning.Aliases, ordered: false);
                builder.AppendListSection(headingPrefix, "Examples", learning.Examples, ordered: false);
                break;
            case "workflow":
                builder.AppendListSection(headingPrefix, "Steps", learning.Steps, ordered: true);
                break;
            case "data-flow":
                builder.AppendTextSection(headingPrefix, "Source", learning.Source);
                builder.AppendTextSection(headingPrefix, "Sink", learning.Sink);
                builder.AppendListSection(headingPrefix, "Steps", learning.Steps, ordered: true);
                break;
            case "invariant":
                builder.AppendTextSection(headingPrefix, "Scope", learning.Scope);
                builder.AppendTextSection(headingPrefix, "Failure Mode", learning.FailureMode);
                break;
            case "pitfall":
                builder.AppendTextSection(headingPrefix, "Symptom", learning.Symptom);
                builder.AppendTextSection(headingPrefix, "Cause", learning.Cause);
                builder.AppendTextSection(headingPrefix, "Fix", learning.Fix);
                break;
            case "decision":
                builder.AppendTextSection(headingPrefix, "Context", learning.Context);
                builder.AppendTextSection(headingPrefix, "Choice", learning.Choice);
                builder.AppendTextSection(headingPrefix, "Consequence", learning.Consequence);
                break;
        }
    }

    private static void AppendTextSection(this StringBuilder builder, string headingPrefix, string title, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.AppendLine();
        builder.AppendLine($"{headingPrefix} {title}");
        builder.AppendLine(value.Trim());
    }

    private static void AppendListSection(this StringBuilder builder, string headingPrefix, string title, IReadOnlyList<string>? values, bool ordered)
    {
        var normalized = NormalizeList(values);
        if (normalized.Length == 0)
            return;

        builder.AppendLine();
        builder.AppendLine($"{headingPrefix} {title}");

        for (var i = 0; i < normalized.Length; i++)
        {
            var prefix = ordered ? $"{i + 1}." : "-";
            builder.AppendLine($"{prefix} {normalized[i]}");
        }
    }

    private static string[] NormalizeList(IReadOnlyList<string>? values)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeScalar(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var lastWasWhitespace = false;
        foreach (var ch in value.Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                if (lastWasWhitespace)
                    continue;

                builder.Append(' ');
                lastWasWhitespace = true;
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
            lastWasWhitespace = false;
        }

        return builder.ToString();
    }
}
