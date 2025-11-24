# Windows System MCP Server Instructions

This project is a Model Context Protocol (MCP) server designed to expose Windows diagnostic tools (Event Viewer, Reliability Monitor, etc.) to AI agents.

## Project Overview
- **Goal**: Provide an interface for AI agents to query and analyze Windows system health and logs.
- **Target OS**: Windows. All tools are expected to run on Windows and interact with Windows APIs.
- **Tech Stack**: .NET 10 (C#), using the official `ModelContextProtocol` SDK.

## Architecture & Patterns
- **MCP Compliance**: The server must strictly adhere to the Model Context Protocol specification for tools and resources.
- **Tool Granularity**: Create focused tools for specific tasks (e.g., `get_event_logs`, `get_reliability_records`). Avoid monolithic "do everything" tools.
- **Data Formatting**: Return structured JSON data from tools to allow easy parsing by the LLM. Avoid returning raw unstructured text dumps unless necessary.
- **Security**: Read-only access to system logs is preferred. Be cautious with tools that modify system state.
- **Project Structure**:
  - `src/WinSysMcp/`: Main project folder.
  - `src/WinSysMcp/Tools/`: Contains static classes defining MCP tools (`EventLogTools.cs`, `ReliabilityTools.cs`, `ServiceTools.cs`, `ProcessTools.cs`, `DiskTools.cs`, `NetworkTools.cs`, `PerformanceTools.cs`, `SoftwareTools.cs`, `FileTools.cs`, `RegistryTools.cs`, `PowerAndSecurityTools.cs`).
  - `Program.cs`: Entry point, configures the Host and Stdio transport.

## Windows Integration
- **APIs**: Use robust libraries for interacting with Windows:
  - `System.Diagnostics.EventLog` for Event Viewer.
  - `System.Management` (WMI) for Reliability Monitor (`Win32_ReliabilityRecords`) and Battery.
  - `System.ServiceProcess` for Windows Services.
  - `System.Diagnostics.Process` for Process management.
  - `System.IO.DriveInfo` for Disk information.
  - `System.Net.NetworkInformation` for Network status.
  - `System.Diagnostics.PerformanceCounter` for real-time metrics.
  - `Microsoft.Win32.Registry` for installed software, startup apps, and registry tools.
  - `System.DirectoryServices.AccountManagement` for user/group management.
- **Performance**: System queries (like Event Viewer) can be heavy. Always implement limits (e.g., `max_results`) and time-window filtering in tools.
- **Platform Targeting**: The project targets `net10.0-windows` to access Windows-specific APIs.

## Development Workflow
- **Build**: `dotnet build src/WinSysMcp`
- **Run**: `dotnet run --project src/WinSysMcp` (Note: This runs over Stdio, so it will expect JSON-RPC input).
- **Mocking**: Since this relies on OS internals, use mocking for unit tests to simulate Windows API responses.
- **Error Handling**: Gracefully handle permission errors (Admin rights might be needed for some logs) and return clear explanations to the user.
- **Logging**: **CRITICAL**: All logs must be directed to Standard Error (`stderr`). Standard Output (`stdout`) is reserved for MCP protocol messages.

