using VaultMcp.Tools.KnowledgeBase.SemanticIndex;
using VaultMcp.Tools.KnowledgeBase.Vault;

namespace VaultMcp.Tools;

public sealed record ErrorInfo(
    string Message,
    IReadOnlyDictionary<string, string>? Details = null);

internal static class VaultToolErrors
{
    public static ErrorInfo? ValidateReadableVault(IVault vault)
    {
        var status = vault.GetStatus();
        if (status.Exists)
            return null;

        return new ErrorInfo(
            "vault root not found",
            new Dictionary<string, string>
            {
                ["rootPath"] = status.RootPath
            });
    }

    public static ErrorInfo FromException(Exception exception)
    {
        return exception switch
        {
            FileNotFoundException fileNotFound => new ErrorInfo(
                "note not found",
                CreateDetails(("path", fileNotFound.FileName))),

            ArgumentOutOfRangeException outOfRange => new ErrorInfo(
                outOfRange.Message.Split(Environment.NewLine)[0],
                CreateDetails(("parameter", outOfRange.ParamName))),

            ArgumentException argument => new ErrorInfo(
                argument.Message.Split(Environment.NewLine)[0],
                CreateDetails(("parameter", argument.ParamName))),

            DirectoryNotFoundException => new ErrorInfo("vault root not found"),

            IOException io => new ErrorInfo("vault io error", CreateDetails(("details", io.Message))),

            EmbeddingProviderUnavailableException => new ErrorInfo("embedding provider unavailable"),

            SemanticIndexNotBuiltException semanticIndexNotBuilt => new ErrorInfo(
                "semantic index not built",
                CreateDetails(("details", semanticIndexNotBuilt.Message))),

            SemanticIndexModelMismatchException modelMismatch => new ErrorInfo(
                "semantic index model mismatch",
                CreateDetails(("details", modelMismatch.Message))),

            SemanticIndexCorruptException corruptIndex => new ErrorInfo(
                "semantic index corrupt",
                CreateDetails(("details", corruptIndex.Message))),

            SemanticIndexException semanticIndex => new ErrorInfo(
                "semantic index failed",
                CreateDetails(("details", semanticIndex.Message))),

            _ => new ErrorInfo("vault operation failed", CreateDetails(("details", exception.Message)))
        };
    }

    private static IReadOnlyDictionary<string, string>? CreateDetails(params (string Key, string? Value)[] pairs)
    {
        var values = pairs
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.OrdinalIgnoreCase);

        return values.Count == 0 ? null : values;
    }
}
