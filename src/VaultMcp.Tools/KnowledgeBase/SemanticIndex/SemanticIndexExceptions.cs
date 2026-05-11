namespace VaultMcp.Tools.KnowledgeBase.SemanticIndex;

public class SemanticIndexException(string message) : Exception(message);

public sealed class EmbeddingProviderUnavailableException(string message) : SemanticIndexException(message);

public sealed class SemanticIndexNotBuiltException(string message) : SemanticIndexException(message);

public sealed class SemanticIndexModelMismatchException(string message) : SemanticIndexException(message);

public sealed class SemanticIndexCorruptException(string message) : SemanticIndexException(message);
