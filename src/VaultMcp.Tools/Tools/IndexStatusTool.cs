using System.ComponentModel;
using ModelContextProtocol.Server;
using VaultMcp.Tools.KnowledgeBase.SemanticIndex;

namespace VaultMcp.Tools.Tools;

public sealed record IndexStatusResponse(
    SemanticIndexStatus Status,
    ErrorInfo? Error = null);

[McpServerToolType]
public sealed class IndexStatusTool(ISemanticIndex semanticIndex)
{
    [McpServerTool(Name = "index_status", Title = "Index Status")]
    [Description("Show semantic index provider and file status, including model, dimensions, chunk count, and warnings.")]
    public IndexStatusResponse Execute()
    {
        try
        {
            return new IndexStatusResponse(semanticIndex.GetStatus());
        }
        catch (Exception exception) when (exception is IOException or SemanticIndexException)
        {
            return new IndexStatusResponse(semanticIndex.GetStatus(), VaultToolErrors.FromException(exception));
        }
    }
}
