using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using VaultMcp.Tools;

namespace VaultMcp.Host;

public static class HostExtensions
{
    internal static string ServerVersion => Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(HostExtensions).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static IServiceCollection Compose(this IServiceCollection services, VaultRootOptions options)
    {
        services.AddSingleton(options);
        services.AddVaultMcp(options.RootPath);
        services.AddMcpRuntime();
        return services;
    }

    private static void AddMcpRuntime(this IServiceCollection services)
    {
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            WriteIndented = true
        };

        var builder = services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = "VaultMcp",
                Version = ServerVersion
            };
        });

        builder.WithStdioServerTransport();
        builder.WithTools(ServiceCollectionExtensions.GetTools(), serializerOptions);
    }
}
