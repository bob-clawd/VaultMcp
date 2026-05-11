using Is.Assertions;
using VaultMcp.Host;
using Xunit;

namespace VaultMcp.Tools.Tests.Host;

public sealed class McpServerHostTests
{
    [Fact]
    public void ParseOptions_UsesDocsDomainDefault_WhenRootArgumentIsNotProvided()
    {
        var startupDirectory = Path.Combine(Path.GetTempPath(), "VaultMcp.Tests", Guid.NewGuid().ToString("N"));

        var options = McpServerHost.ParseOptions([], startupDirectory);

        options.RootPath.Is(Path.Combine(startupDirectory, "docs", "domain"));
    }

    [Fact]
    public void ParseOptions_UsesExplicitRoot_WhenProvided()
    {
        const string rootPath = "some/domain/path";

        var options = McpServerHost.ParseOptions(["--root", rootPath], "/workspace");

        options.RootPath.Is(rootPath);
    }

    [Fact]
    public void ParseOptions_ThrowsClearError_WhenRootValueIsMissing()
    {
        var exception = Record.Exception(() => McpServerHost.ParseOptions(["--root"], "/workspace"));

        exception.IsNotNull();
        exception.Is<ArgumentException>();
        exception.Message.Contains("Missing value for '--root'", StringComparison.Ordinal).IsTrue();
    }

    [Fact]
    public void ParseOptions_ThrowsClearError_ForUnknownArguments()
    {
        var exception = Record.Exception(() => McpServerHost.ParseOptions(["--wat"], "/workspace"));

        exception.IsNotNull();
        exception.Is<ArgumentException>();
        exception.Message.Contains("Unknown argument '--wat'", StringComparison.Ordinal).IsTrue();
    }
}
