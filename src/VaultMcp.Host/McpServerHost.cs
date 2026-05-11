using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VaultMcp.Host;

public static class McpServerHost
{
    public static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = ParseOptions(args, Directory.GetCurrentDirectory());
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();

        builder.Services.Compose(options);

        var host = builder.Build();
        await host.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static VaultRootOptions ParseOptions(string[] args, string startupDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(startupDirectory);

        string? rootPath = null;
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--root":
                    if (rootPath is not null)
                        throw new ArgumentException("The '--root' option may only be specified once.", nameof(args));

                    if (index + 1 >= args.Length)
                        throw new ArgumentException("Missing value for '--root'. Expected '--root <path>'.", nameof(args));

                    rootPath = args[++index];
                    if (string.IsNullOrWhiteSpace(rootPath))
                        throw new ArgumentException("The '--root' value must not be empty or whitespace.", nameof(args));

                    break;

                default:
                    throw new ArgumentException($"Unknown argument '{argument}'. Expected '--root <path>'.", nameof(args));
            }
        }

        rootPath ??= Path.Combine(startupDirectory, "docs", "domain");
        return new VaultRootOptions { RootPath = rootPath };
    }
}
