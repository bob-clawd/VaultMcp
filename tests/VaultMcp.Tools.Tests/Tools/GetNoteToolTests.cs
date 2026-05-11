using Is.Assertions;
using VaultMcp.Tools.KnowledgeBase;
using VaultMcp.Tools.KnowledgeBase.Vault;
using VaultMcp.Tools.Tools;
using Xunit;

namespace VaultMcp.Tools.Tests.Tools;

public sealed class GetNoteToolTests
{
    [Fact]
    public void Execute_returns_document_from_vault()
    {
        var document = new VaultNoteDocument("glossary/order.md", "Order", "# Order\n\nBody");
        var stub = new StubKnowledgeVault(new VaultStatus("/repo/docs/domain", true, 1, [".md"]), [], document);
        var tool = new GetNoteTool(stub);

        var result = tool.Execute("glossary/order.md", 4000);

        result.Error.IsNull();
        result.Note!.Path.Is("glossary/order.md");
        result.Note.Title.Is("Order");
        result.Note.Content.Is("# Order\n\nBody");
        stub.LastGetNoteMaxChars.Is(4000);
    }

    [Fact]
    public void Execute_returns_structured_error_when_note_is_missing()
    {
        var tool = new GetNoteTool(new StubKnowledgeVault(new VaultStatus("/repo/docs/domain", true, 0, [".md"]), [], document: null));

        var result = tool.Execute("glossary/missing.md");

        result.Note.IsNull();
        result.Error!.Message.Is("note not found");
    }
}
