namespace VaultMcp.Tools.KnowledgeBase.SemanticIndex;

public sealed record SemanticSearchHit(
    string ChunkId,
    string Path,
    string Title,
    string? Heading,
    float Score,
    string TextPreview);

public sealed record SemanticIndexStatus(
    string RootPath,
    string IndexDirectory,
    bool ProviderConfigured,
    string ProviderName,
    string? ConfiguredEmbeddingModel,
    bool IndexPresent,
    string? IndexedEmbeddingModel,
    int? EmbeddingDimensions,
    int ChunkCount,
    int IndexedFileCount,
    DateTimeOffset? LastIndexedAt,
    string? Warning = null);

internal sealed record NoteChunk(
    string Id,
    string Path,
    string Title,
    string? Heading,
    string Text,
    string TextPreview,
    string[] Tags,
    string[] Aliases,
    string ContentHash,
    DateTimeOffset ModifiedAt);

internal sealed record ChunkIndexEntry(
    string Id,
    string Path,
    string Title,
    string? Heading,
    string TextPreview,
    string[] Tags,
    string[] Aliases,
    string ContentHash,
    int EmbeddingOffset,
    int EmbeddingLength);

internal sealed record SemanticIndexFile(
    int SchemaVersion,
    string ProviderName,
    string EmbeddingModel,
    int EmbeddingDimensions,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ChunkIndexEntry> Chunks);

internal sealed record IndexedFileState(
    string ContentHash,
    DateTimeOffset IndexedAt);

internal sealed record SemanticIndexStateFile(
    int SchemaVersion,
    IReadOnlyDictionary<string, IndexedFileState> Files);

internal sealed record SemanticIndexSnapshot(
    SemanticIndexFile Index,
    SemanticIndexStateFile State,
    IReadOnlyList<float[]> Vectors);
