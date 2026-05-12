namespace VaultMcp.Tools.KnowledgeBase.Vault;

internal static class VaultPathGuard
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static string ResolvePath(string rootPath, string relativePath, params string[] allowedExtensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("Only vault-relative note paths are allowed.", nameof(relativePath));

        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        var rootPrefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, PathComparison) &&
            !string.Equals(fullPath, rootPath, PathComparison))
        {
            throw new ArgumentException("The requested note path escapes the configured vault root.", nameof(relativePath));
        }

        if (allowedExtensions.Length > 0 &&
            !allowedExtensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only markdown note paths are allowed.", nameof(relativePath));
        }

        return fullPath;
    }
}
