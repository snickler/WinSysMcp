using System.Text.Json.Nodes;

namespace WinSysMcp.ToolSchemaGen;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
            var schemaOutputPath = Path.Combine(repositoryRoot, "docs", "TOOLS_SCHEMA.json");
            var markdownOutputPath = Path.Combine(repositoryRoot, "docs", "TOOLS.md");
            var checkOnly = false;
            var writeSchemaOnly = false;

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--check":
                        checkOnly = true;
                        break;
                    case "--output" when i + 1 < args.Length:
                        schemaOutputPath = Path.GetFullPath(args[++i]);
                        writeSchemaOnly = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument '{args[i]}'. Supported: --check, --output <path>.");
                }
            }

            var normalizedGenerated = ToolSchemaGenerator.BuildCatalog(includeGeneratedAt: false);

            if (checkOnly)
            {
                var normalizedExisting = ToolSchemaGenerator.LoadNormalizedCatalog(schemaOutputPath);
                if (!JsonNode.DeepEquals(normalizedGenerated, normalizedExisting))
                {
                    Console.Error.WriteLine($"Tool schema is out of date: '{schemaOutputPath}'. Regenerate it with the tool schema generator.");
                    return 1;
                }

                if (!writeSchemaOnly)
                {
                    var generatedMarkdown = ToolSchemaGenerator.BuildMarkdownDocument().Replace("\r\n", "\n", StringComparison.Ordinal);
                    var existingMarkdown = ToolSchemaGenerator.LoadTextDocument(markdownOutputPath);
                    if (!string.Equals(generatedMarkdown, existingMarkdown, StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine($"Tool catalog markdown is out of date: '{markdownOutputPath}'. Regenerate it with the tool schema generator.");
                        return 1;
                    }

                    Console.WriteLine($"Tool catalog markdown is up to date: '{markdownOutputPath}'.");
                }

                Console.WriteLine($"Tool schema is up to date: '{schemaOutputPath}'.");
                return 0;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(schemaOutputPath)!);
            var generatedWithStamp = ToolSchemaGenerator.BuildCatalog(includeGeneratedAt: true);
            File.WriteAllText(schemaOutputPath, ToolSchemaGenerator.Serialize(generatedWithStamp));
            Console.WriteLine($"Wrote tool schema to '{schemaOutputPath}'.");

            if (!writeSchemaOnly)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(markdownOutputPath)!);
                File.WriteAllText(markdownOutputPath, ToolSchemaGenerator.BuildMarkdownDocument());
                Console.WriteLine($"Wrote tool catalog markdown to '{markdownOutputPath}'.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            if (current.GetDirectories(".git").Length > 0 && current.GetDirectories("src").Length > 0 && current.GetDirectories("docs").Length > 0)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the current directory.");
    }
}