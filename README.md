# WinSysMcp

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that exposes common system diagnostics and management tools to AI agents and MCP clients.

Built with .NET 10 and the official [`ModelContextProtocol`](https://github.com/modelcontextprotocol/csharp-sdk) SDK. Supports both standard JIT and **Native AOT** compilation for fast startup and low memory overhead on **Windows and Linux**.

---

## Features

60+ tools organized into focused groups. Most groups have OS-native backends with the **same tool names**:

| Group | Windows | Linux |
|---|---|---|
| **Disk / Files / Processes / Network** | .NET BCL | .NET BCL |
| **Performance** | Performance Counters | `/proc` |
| **Event Logs** | Windows Event Log | `journalctl` |
| **Services** | Service Controller | `systemctl` |
| **Software** | Uninstall registry | `dpkg` / `rpm` |
| **Startup apps** | Run keys | `.desktop` + systemd user units |
| **Task Scheduler** | `schtasks` | systemd timers + crontab |
| **Network advanced** | `arp` / `route` / `netsh` | `ip` / `nft` / `resolvectl` |
| **Registry / WMI** | Full | Not registered (Windows-only) |

See [`docs/TOOLS.md`](docs/TOOLS.md) for the complete annotated tool catalog, or [`docs/TOOLS_SCHEMA.json`](docs/TOOLS_SCHEMA.json) for the machine-readable schema.

---

## Requirements

- Windows 10/11 / Windows Server 2019+, **or** a modern Linux distro with the usual userspace tools (`systemctl`, `journalctl`, `ip`, …)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for building from source)
- Some tools require elevated privileges (Event Logs / journal, service control, firewall, shutdown)

---

## Quick Start

### Option A — Download a release binary

Download the latest RID-matched binary from [Releases](../../releases) (e.g. `winsysmcp-win-x64.exe` or `winsysmcp-linux-x64`). No .NET runtime required — self-contained Native AOT.

> **Note:** The AOT build omits WMI/reliability/account-management tool groups that depend on Windows reflection APIs. Use the JIT build on Windows if you need those tools.

### Option B — Run from source

```powershell
git clone https://github.com/snickler/WinSysMcp.git
cd WinSysMcp
dotnet run --project src/WinSysMcp
```

The server communicates over **stdio** using JSON-RPC (standard MCP transport). It does not open any network ports.

---

## MCP Client Configuration

### Claude Desktop / VS Code / Copilot

Point `command` at the AOT binary (`.exe` on Windows, no extension on Linux), or use `dotnet run --project …` from source.

Orchesttui looks for `winsysmcp` / `winsysmcp.exe` next to its own binary, or `ORCHESTTUI_WINSYS_MCP` for an override path.

---

## Building

### Debug (JIT)

```powershell
dotnet build src/WinSysMcp
```

On Windows this enables the full Windows API surface (registry, services, event log). On Linux the same project builds with Linux backends and omits registry/WMI sources.

### Release — Native AOT

```powershell
# Windows host
dotnet publish src/WinSysMcp -c Release -r win-x64
dotnet publish src/WinSysMcp -c Release -r win-arm64

# Linux host (Native AOT cannot cross-OS compile)
dotnet publish src/WinSysMcp -c Release -r linux-x64
dotnet publish src/WinSysMcp -c Release -r linux-arm64
```

---

## Running Tests

```powershell
dotnet test tests/WinSysMcp.Tests
```

CI runs tests on `win-x64`, `win-arm64`, and `linux-x64`, and publishes AOT artifacts for those RIDs plus `linux-arm64`.

---

## Regenerating the Tool Catalog

```powershell
dotnet run --project tools/WinSysMcp.ToolSchemaGen
```

---

## Project Structure

```
winsysmcp/
├── src/WinSysMcp/
│   ├── Tools/          # One file per tool group (Win + Linux backends)
│   ├── Serialization/  # AOT-safe JSON serialization context
│   ├── OsProcess.cs    # Shared process helper
│   └── Program.cs      # Host setup and tool registration
├── tests/WinSysMcp.Tests/
├── tools/WinSysMcp.ToolSchemaGen/
└── docs/
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
