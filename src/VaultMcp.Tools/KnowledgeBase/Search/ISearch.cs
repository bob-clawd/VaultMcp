namespace VaultMcp.Tools.KnowledgeBase.Search;

internal interface ISearch
{
    IReadOnlyList<VaultSearchResult> SearchNotes(IReadOnlyList<VaultIndexedNote> notes, string query, int maxCount = 10);
    IReadOnlyList<VaultSearchResult> FindTerm(IReadOnlyList<VaultIndexedNote> notes, string term, int maxCount = 10);
    IReadOnlyList<VaultSearchResult> FindRelatedNotes(IReadOnlyList<VaultIndexedNote> notes, VaultIndexedNote source, int maxCount = 5);
}
