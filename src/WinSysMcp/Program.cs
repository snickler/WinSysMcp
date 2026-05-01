using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Text.Json;
using WinSysMcp.Serialization;
using WinSysMcp.Tools;

using System.Reflection;

// Only start the server when running as the entry assembly. This prevents the
// Program's top-level startup from executing during unit tests or when the
// assembly is referenced by other projects.
if (Assembly.GetEntryAssembly() == Assembly.GetExecutingAssembly())
{
    // Create the Host Application Builder
    var builder = Host.CreateApplicationBuilder(args);

    // Configure logging to stderr to avoid interfering with MCP stdio transport
    // MCP uses standard input/output for JSON-RPC communication.
    // Any logs written to stdout will corrupt the protocol.
    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    // Register MCP Server and Transport
        var toolJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        toolJsonOptions.TypeInfoResolverChain.Insert(0, WinSysMcpToolJsonContext.Default);

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<DiskTools>(toolJsonOptions)
        .WithTools<EventLogTools>(toolJsonOptions)
        .WithTools<FileTools>(toolJsonOptions)
        .WithTools<NetworkAdvancedTools>(toolJsonOptions)
        .WithTools<NetworkTools>(toolJsonOptions)
        .WithTools<PerformanceTools>(toolJsonOptions)
    #if !AOT_SAFE
        .WithTools<PowerTools>(toolJsonOptions)
        .WithTools<SecurityTools>(toolJsonOptions)
    #endif
        .WithTools<ProcessTools>(toolJsonOptions)
        .WithTools<RegistryTools>(toolJsonOptions)
    #if !AOT_SAFE
        .WithTools<ReliabilityTools>(toolJsonOptions)
    #endif
        .WithTools<ServiceTools>(toolJsonOptions)
        .WithTools<SoftwareTools>(toolJsonOptions)
        .WithTools<SystemTools>(toolJsonOptions)
        .WithTools<TaskSchedulerTools>(toolJsonOptions)
    #if !AOT_SAFE
        .WithTools<WmiTools>(toolJsonOptions);
    #else
        ;
    #endif

    var app = builder.Build();
    await app.RunAsync();
}
