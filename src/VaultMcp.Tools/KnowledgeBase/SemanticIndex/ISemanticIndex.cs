namespace VaultMcp.Tools.KnowledgeBase.SemanticIndex;

public interface ISemanticIndex
{
    void Rebuild();
    void UpsertFile(string relativePath);
    void DeleteFile(string relativePath);
    IReadOnlyList<SemanticSearchHit> Search(string query, int limit = 10);
    SemanticIndexStatus GetStatus();
}
