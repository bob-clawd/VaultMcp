using Is.Assertions;
using Microsoft.ML.OnnxRuntime.Tensors;
using VaultMcp.Tools.KnowledgeBase.SemanticIndex;
using Xunit;

namespace VaultMcp.Tools.Tests.KnowledgeBase.SemanticIndex;

public sealed class OnnxBertEmbeddingProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "VaultMcp.OnnxEmbedding.Tests", Guid.NewGuid().ToString("N"));

    public OnnxBertEmbeddingProviderTests()
        => Directory.CreateDirectory(_root);

    [Fact]
    public void MeanPoolAndNormalize_averages_masked_tokens_and_normalizes()
    {
        var tensor = new DenseTensor<float>(new[] { 1, 2, 2 });
        tensor[0, 0, 0] = 3f;
        tensor[0, 0, 1] = 0f;
        tensor[0, 1, 0] = 0f;
        tensor[0, 1, 1] = 4f;

        var pooled = OnnxBertEmbeddingProvider.MeanPoolAndNormalize(tensor, new long[] { 1, 1 });

        pooled.Length.Is(2);
        pooled[0].Is(0.6f);
        pooled[1].Is(0.8f);
    }

    [Fact]
    public void Embed_throws_clear_error_when_local_assets_are_missing()
    {
        var provider = new OnnxBertEmbeddingProvider(
            Path.Combine(_root, "onnx", "model_qint8_arm64.onnx"),
            Path.Combine(_root, "vocab.txt"),
            "all-MiniLM-L6-v2");

        var exception = Record.Exception(() => provider.Embed("invoice approval"));

        exception.IsNotNull();
        exception.Is<EmbeddingProviderUnavailableException>();
        exception.Message.Contains("assets missing", StringComparison.OrdinalIgnoreCase).IsTrue();
    }

    [Fact]
    public void ResolveModelPath_prefers_quantized_arm64_model_when_present()
    {
        var modelDirectory = EmbeddingModelPaths.GetDefaultModelDirectory(_root, "all-MiniLM-L6-v2");
        Directory.CreateDirectory(Path.Combine(modelDirectory, "onnx"));
        File.WriteAllText(Path.Combine(modelDirectory, "onnx", "model_qint8_arm64.onnx"), string.Empty);

        var modelPath = EmbeddingModelPaths.ResolveModelPath(_root, "all-MiniLM-L6-v2", null);

        modelPath.EndsWith(Path.Combine("onnx", "model_qint8_arm64.onnx"), StringComparison.Ordinal).IsTrue();
    }

    [Fact]
    public void ResolveVocabPath_prefers_model_root_vocab()
    {
        var modelDirectory = EmbeddingModelPaths.GetDefaultModelDirectory(_root, "all-MiniLM-L6-v2");
        Directory.CreateDirectory(Path.Combine(modelDirectory, "onnx"));
        var modelPath = Path.Combine(modelDirectory, "onnx", "model_qint8_arm64.onnx");
        File.WriteAllText(modelPath, string.Empty);
        File.WriteAllText(Path.Combine(modelDirectory, "vocab.txt"), string.Empty);

        var vocabPath = EmbeddingModelPaths.ResolveVocabPath(_root, "all-MiniLM-L6-v2", null, modelPath);

        vocabPath.EndsWith("vocab.txt", StringComparison.Ordinal).IsTrue();
        vocabPath.Contains("all-MiniLM-L6-v2", StringComparison.Ordinal).IsTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
