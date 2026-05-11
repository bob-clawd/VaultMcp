using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace VaultMcp.Tools.KnowledgeBase.SemanticIndex;

internal sealed class OnnxBertEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private const int MaxTokenCount = 256;

    private readonly string _modelPath;
    private readonly string _vocabPath;
    private readonly Lazy<InferenceSession> _session;
    private readonly Lazy<BertTokenizer> _tokenizer;

    public OnnxBertEmbeddingProvider(string modelPath, string vocabPath, string modelName)
    {
        _modelPath = Path.GetFullPath(modelPath);
        _vocabPath = Path.GetFullPath(vocabPath);
        ModelName = string.IsNullOrWhiteSpace(modelName) ? "all-MiniLM-L6-v2" : modelName.Trim();
        _session = new Lazy<InferenceSession>(CreateSession, LazyThreadSafetyMode.ExecutionAndPublication);
        _tokenizer = new Lazy<BertTokenizer>(CreateTokenizer, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string ProviderName => "onnx";
    public string ModelName { get; }
    public bool IsConfigured => File.Exists(_modelPath) && File.Exists(_vocabPath);

    public float[] Embed(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        EnsureFilesPresent();

        try
        {
            return EmbedCore(text);
        }
        catch (EmbeddingProviderUnavailableException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or OnnxRuntimeException)
        {
            throw new EmbeddingProviderUnavailableException($"local onnx embedding provider failed: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_session.IsValueCreated)
            _session.Value.Dispose();
    }

    internal static float[] MeanPoolAndNormalize(Tensor<float> embeddings, ReadOnlySpan<long> attentionMask)
    {
        if (embeddings.Rank != 3)
            throw new InvalidOperationException("expected [batch, tokens, hidden] embedding tensor.");

        var batchSize = embeddings.Dimensions[0];
        var tokenCount = embeddings.Dimensions[1];
        var hiddenSize = embeddings.Dimensions[2];

        if (batchSize != 1)
            throw new InvalidOperationException("only batch size 1 is supported for local semantic embeddings.");

        if (attentionMask.Length != tokenCount)
            throw new InvalidOperationException("attention mask length does not match embedding token count.");

        var pooled = new float[hiddenSize];
        var includedTokens = 0f;

        for (var tokenIndex = 0; tokenIndex < tokenCount; tokenIndex++)
        {
            if (attentionMask[tokenIndex] == 0)
                continue;

            includedTokens += 1f;
            for (var hiddenIndex = 0; hiddenIndex < hiddenSize; hiddenIndex++)
                pooled[hiddenIndex] += embeddings[0, tokenIndex, hiddenIndex];
        }

        if (includedTokens <= 0f)
            throw new EmbeddingProviderUnavailableException("local onnx embedding provider produced no active tokens.");

        for (var hiddenIndex = 0; hiddenIndex < hiddenSize; hiddenIndex++)
            pooled[hiddenIndex] /= includedTokens;

        var norm = MathF.Sqrt(pooled.Sum(value => value * value));
        if (norm <= 0f)
            return pooled;

        for (var hiddenIndex = 0; hiddenIndex < hiddenSize; hiddenIndex++)
            pooled[hiddenIndex] /= norm;

        return pooled;
    }

    private float[] EmbedCore(string text)
    {
        var tokenizer = _tokenizer.Value;
        var session = _session.Value;
        string? normalizedText;
        var tokenIds = tokenizer.EncodeToIds(text, MaxTokenCount, true, out normalizedText, out _, true, true);

        if (tokenIds.Count == 0)
            throw new EmbeddingProviderUnavailableException("local onnx embedding provider tokenized the input into zero tokens.");

        var sequenceLength = tokenIds.Count;
        var inputIds = new DenseTensor<long>(new[] { 1, sequenceLength });
        var attentionMask = new DenseTensor<long>(new[] { 1, sequenceLength });
        var tokenTypeIds = new DenseTensor<long>(new[] { 1, sequenceLength });

        for (var index = 0; index < sequenceLength; index++)
        {
            inputIds[0, index] = tokenIds[index];
            attentionMask[0, index] = 1;
            tokenTypeIds[0, index] = 0;
        }

        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
        };

        using var results = session.Run(inputs);
        var hiddenState = results.FirstOrDefault(result => string.Equals(result.Name, "last_hidden_state", StringComparison.Ordinal))
            ?? results.FirstOrDefault()
            ?? throw new EmbeddingProviderUnavailableException("local onnx embedding provider returned no tensors.");

        var embeddings = hiddenState.AsTensor<float>();
        var mask = new long[sequenceLength];
        for (var index = 0; index < sequenceLength; index++)
            mask[index] = attentionMask[0, index];

        return MeanPoolAndNormalize(embeddings, mask);
    }

    private BertTokenizer CreateTokenizer()
        => BertTokenizer.Create(_vocabPath, new BertOptions
        {
            ApplyBasicTokenization = true,
            LowerCaseBeforeTokenization = true,
            RemoveNonSpacingMarks = false,
            SplitOnSpecialTokens = true
        });

    private InferenceSession CreateSession()
    {
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        return new InferenceSession(_modelPath, options);
    }

    private void EnsureFilesPresent()
    {
        if (File.Exists(_modelPath) && File.Exists(_vocabPath))
            return;

        throw new EmbeddingProviderUnavailableException(
            $"local onnx embedding assets missing. Expected model at '{_modelPath}' and vocab at '{_vocabPath}'. Download all-MiniLM-L6-v2 into the vault .vaultmcp/models directory or configure VAULTMCP_EMBEDDINGS_MODEL_PATH and VAULTMCP_EMBEDDINGS_VOCAB_PATH.");
    }
}
