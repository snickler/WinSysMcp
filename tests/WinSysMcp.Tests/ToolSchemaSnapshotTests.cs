using System;
using System.IO;
using System.Text.Json.Nodes;
using WinSysMcp.ToolSchemaGen;
using Xunit;

namespace WinSysMcp.Tests;

public class ToolSchemaSnapshotTests
{
    [Fact]
    public void ToolsSchemaJson_IsInSyncWithGeneratedCatalog()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var schemaPath = Path.Combine(repoRoot, "docs", "TOOLS_SCHEMA.json");

        var generated = ToolSchemaGenerator.BuildCatalog(includeGeneratedAt: false);
        var existing = ToolSchemaGenerator.LoadNormalizedCatalog(schemaPath);

        Assert.True(JsonNode.DeepEquals(generated, existing), "docs/TOOLS_SCHEMA.json is out of date. Regenerate it with the tool schema generator.");
    }

    [Fact]
    public void ToolsMarkdown_IsInSyncWithGeneratedCatalog()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var markdownPath = Path.Combine(repoRoot, "docs", "TOOLS.md");

        var generated = ToolSchemaGenerator.BuildMarkdownDocument().Replace("\r\n", "\n", StringComparison.Ordinal);
        var existing = ToolSchemaGenerator.LoadTextDocument(markdownPath);

        Assert.Equal(generated, existing);
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (current.GetDirectories(".git").Length > 0 && current.GetDirectories("docs").Length > 0 && current.GetDirectories("src").Length > 0)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for the tool schema snapshot test.");
    }
}