# Contributing to WinSysMcp

Thank you for considering a contribution! Here's how to get started.

## Development Setup

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. Clone the repo and build:

   ```powershell
   git clone https://github.com/<your-org>/winsysmcp.git
   cd winsysmcp
   dotnet build src/WinSysMcp
   ```

3. Run the tests:

   ```powershell
   dotnet test tests/WinSysMcp.Tests
   ```

## Adding a New Tool

1. Find the appropriate `Tools/*.cs` file, or create a new one for a distinct group.
2. Follow the existing pattern — annotate the method with `[McpServerTool]` and add a clear description.
3. Register the tool class in `Program.cs` with `.WithTools<YourToolClass>(toolJsonOptions)`.
4. If the tool uses WMI or reflection APIs incompatible with AOT, guard it with `#if !AOT_SAFE` and exclude the file in the `Condition="'$(PublishAot)' == 'true'"` item group in `WinSysMcp.csproj`.
5. Regenerate the tool catalog:

   ```powershell
   dotnet run --project tools/WinSysMcp.ToolSchemaGen
   ```

6. Add a unit test in `tests/WinSysMcp.Tests/`.

## Guidelines

- **Read-only first.** Prefer safe, non-destructive tools. Clearly label any tool that modifies system state.
- **Logs to stderr.** Never write to stdout — it carries the MCP JSON-RPC stream.
- **AOT compatibility.** Keep new tools AOT-safe where possible. If not, add the appropriate guards.
- **JSON output.** Return structured data (not raw text dumps) so MCP clients can reason about results.
- **Error handling.** Catch `UnauthorizedAccessException` and other expected OS errors and return a friendly message instead of an exception.

## Pull Requests

- Keep PRs focused on a single change.
- Update `docs/TOOLS.md` and `docs/TOOLS_SCHEMA.json` by running the generator before submitting.
- Ensure `dotnet test` passes.
- GitHub Actions CI validates the Native AOT publish matrix in parallel (`win-arm64` on `windows-11-arm`, `win-x64` on the standard Windows runner), and tagged releases use the same split build before publishing artifacts.
- Follow the existing code style (four-space indentation, nullable enabled, no suppressed warnings without justification).
