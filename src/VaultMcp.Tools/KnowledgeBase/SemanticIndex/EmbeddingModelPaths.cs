using System.Runtime.InteropServices;
using System.Reflection;

namespace VaultMcp.Tools.KnowledgeBase.SemanticIndex;

internal static class EmbeddingModelPaths
{
    public static string GetDefaultModelDirectory(string rootPath, string modelName)
        => Path.Combine(rootPath, ".vault", "models", modelName);

    public static string GetBundledModelDirectory(string modelName)
    {
        // For dotnet tools, model assets can be shipped inside the tool package.
        // Keep this stable across working directories.
        var baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;

        return Path.Combine(baseDir, "models", modelName);
    }

    public static string ResolveModelPath(string rootPath, string modelName, string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        var rootModelDirectory = GetDefaultModelDirectory(rootPath, modelName);
        var bundledModelDirectory = GetBundledModelDirectory(modelName);

        var directories = new[] { rootModelDirectory, bundledModelDirectory }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidates = directories
            .SelectMany(modelDirectory => RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? new[]
                {
                    Path.Combine(modelDirectory, "onnx", "model_qint8_arm64.onnx"),
                    Path.Combine(modelDirectory, "model_qint8_arm64.onnx"),
                    Path.Combine(modelDirectory, "onnx", "model.onnx"),
                    Path.Combine(modelDirectory, "model.onnx")
                }
                : new[]
                {
                    Path.Combine(modelDirectory, "onnx", "model.onnx"),
                    Path.Combine(modelDirectory, "model.onnx"),
                    Path.Combine(modelDirectory, "onnx", "model_qint8_arm64.onnx"),
                    Path.Combine(modelDirectory, "model_qint8_arm64.onnx")
                })
            .ToArray();

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    public static string ResolveVocabPath(string rootPath, string modelName, string? configuredPath, string modelPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        var modelDirectory = GetDefaultModelDirectory(rootPath, modelName);
        var bundledDirectory = GetBundledModelDirectory(modelName);
        var modelFileDirectory = Path.GetDirectoryName(modelPath) ?? modelDirectory;
        var modelRootDirectory = Directory.GetParent(modelFileDirectory)?.FullName;

        var candidates = new[]
        {
            Path.Combine(modelDirectory, "vocab.txt"),
            Path.Combine(bundledDirectory, "vocab.txt"),
            Path.Combine(modelFileDirectory, "vocab.txt"),
            modelRootDirectory is null ? string.Empty : Path.Combine(modelRootDirectory, "vocab.txt")
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }
}
