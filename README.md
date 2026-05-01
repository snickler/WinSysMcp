# WinSysMcp

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that exposes Windows system diagnostics and management tools to AI agents and MCP clients.

Built with .NET 10 and the official [`ModelContextProtocol`](https://github.com/modelcontextprotocol/csharp-sdk) SDK. Supports both standard JIT and **Native AOT** compilation for fast startup and low memory overhead.

---

## Features

60+ tools organized into focused groups:

| Group | Tools |
|---|---|
| **Disk** | Drive info, folder size |
| **Event Logs** | Query Application/System/Security logs with filters |
| **Files** | Read, write, search, copy, move, diff, hash, encoding-aware edits |
| **Network** | Ping, DNS lookup, TCP check, interfaces, ARP, firewall rules, route table, DNS cache |
| **Performance** | CPU %, available memory, uptime |
| **Processes** | List top processes, details by PID, start/kill |
| **Registry** | Read, write, list, delete keys and values |
| **Reliability** | WMI reliability records (crashes, failures) |
| **Services** | List, get details, start, stop |
| **Software** | Installed programs from registry |
| **System** | OS version, system info, environment variables, startup apps, uptime, shutdown/restart |
| **Task Scheduler** | Query scheduled tasks |
| **Power & Security** | Battery status, current user, local users/groups, group membership |
| **WMI** | BIOS, CPU, GPU, printers, sound devices, startup commands |

See [`docs/TOOLS.md`](docs/TOOLS.md) for the complete annotated tool catalog, or [`docs/TOOLS_SCHEMA.json`](docs/TOOLS_SCHEMA.json) for the machine-readable schema.

---

## Requirements

- Windows 10/11 or Windows Server 2019+
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for building from source)
- Some tools require **Administrator privileges** (Event Logs, WMI, Service control, etc.)

---

## Quick Start

### Option A — Download a release binary

Download the latest `winsysmcp.exe` from the [Releases](../../releases) page. No .NET runtime required — it's a self-contained Native AOT executable.

> **Note:** The AOT build omits three tool groups that depend on WMI reflection (`WmiTools`, `ReliabilityTools`, `PowerAndSecurityTools`). Use the JIT build if you need those tools.

### Option B — Run from source

```powershell
git clone https://github.com/<your-org>/winsysmcp.git
cd winsysmcp
dotnet run --project src/WinSysMcp
```

The server communicates over **stdio** using JSON-RPC (standard MCP transport). It does not open any network ports.

---

## MCP Client Configuration

### Claude Desktop (`claude_desktop_config.json`)

```json
{
  "mcpServers": {
    "winsysmcp": {
      "command": "C:\\path\\to\\winsysmcp.exe"
    }
  }
}
```

Or if running from source:

```json
{
  "mcpServers": {
    "winsysmcp": {
      "command": "dotnet",
      "args": ["run", "--project", "C:\\path\\to\\winsysmcp\\src\\WinSysMcp"]
    }
  }
}
```

### VS Code / GitHub Copilot (`mcp.json`)

```json
{
  "servers": {
    "winsysmcp": {
      "type": "stdio",
      "command": "C:\\path\\to\\winsysmcp.exe"
    }
  }
}
```

---

## Building

### Debug (JIT) — includes all tools

```powershell
dotnet build src/WinSysMcp
```

### Release — Native AOT (win-arm64 by default)

```powershell
dotnet publish src/WinSysMcp -c Release
```

To target x64 instead, override the runtime identifier:

```powershell
dotnet publish src/WinSysMcp -c Release -r win-x64
```

---

## Running Tests

```powershell
dotnet test tests/WinSysMcp.Tests
```

GitHub Actions CI/CD now runs the test suite and Native AOT publishes as matching runtime matrices: `win-arm64` tests/publishes run on `windows-11-arm64`, `win-x64` tests/publishes run on the standard Windows runner, and tagged releases wait for both the test and AOT matrices before publishing.

---

## Regenerating the Tool Catalog

The `docs/TOOLS.md` and `docs/TOOLS_SCHEMA.json` files are generated from source metadata:

```powershell
dotnet run --project tools/WinSysMcp.ToolSchemaGen
```

---

## Project Structure

```
winsysmcp/
├── src/WinSysMcp/
│   ├── Tools/          # One file per tool group
│   ├── Serialization/  # AOT-safe JSON serialization context
│   └── Program.cs      # Host setup and tool registration
├── tests/WinSysMcp.Tests/
├── tools/WinSysMcp.ToolSchemaGen/   # Doc generator
└── docs/
    ├── TOOLS.md         # Human-readable tool catalog
    └── TOOLS_SCHEMA.json
```

---

## Security Considerations

- The server runs under whatever account launches it. Sensitive tools (registry writes, service control, shutdown) will succeed or fail based on that account's permissions.
- `get_environment_variables` may expose secrets present in the process environment — use with care.
- File write and registry write tools block access to a set of protected system paths.
- For read-only use cases, consider running under a restricted user account.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

---

## License

[MIT](LICENSE)
