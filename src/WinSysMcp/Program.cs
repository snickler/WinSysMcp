using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Reflection;
using System.Text.Json;
using WinSysMcp.Serialization;
using WinSysMcp.Tools;

// Only start the server when running as the entry assembly. This prevents the
// Program's top-level startup from executing during unit tests or when the
// assembly is referenced by other projects.
if (Assembly.GetEntryAssembly() == Assembly.GetExecutingAssembly())
{
    var builder = Host.CreateApplicationBuilder(args);

    // Configure logging to stderr to avoid interfering with MCP stdio transport
    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    var toolJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    toolJsonOptions.TypeInfoResolverChain.Insert(0, WinSysMcpToolJsonContext.Default);

    var mcp = builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<DiskTools>(toolJsonOptions)
        .WithTools<EventLogTools>(toolJsonOptions)
        .WithTools<FileTools>(toolJsonOptions)
        .WithTools<NetworkAdvancedTools>(toolJsonOptions)
        .WithTools<NetworkTools>(toolJsonOptions)
        .WithTools<PerformanceTools>(toolJsonOptions)
        .WithTools<ProcessTools>(toolJsonOptions)
        .WithTools<ServiceTools>(toolJsonOptions)
        .WithTools<SoftwareTools>(toolJsonOptions)
        .WithTools<SystemTools>(toolJsonOptions)
        .WithTools<TaskSchedulerTools>(toolJsonOptions);

#if WINDOWS_APIS
    mcp = mcp.WithTools<RegistryTools>(toolJsonOptions);
#if !AOT_SAFE
    mcp = mcp
        .WithTools<PowerTools>(toolJsonOptions)
        .WithTools<SecurityTools>(toolJsonOptions)
        .WithTools<ReliabilityTools>(toolJsonOptions)
        .WithTools<WmiTools>(toolJsonOptions);
#endif
#endif

    _ = mcp;

    var app = builder.Build();
    await app.RunAsync();
}
