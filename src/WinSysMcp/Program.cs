using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly);

var app = builder.Build();
await app.RunAsync();
