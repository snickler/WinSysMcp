using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace WinSysMcp.ToolSchemaGen;

internal static class ToolSchemaGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
    private static readonly NullabilityInfoContext NullabilityContext = new();

    public static JsonObject BuildCatalog(bool includeGeneratedAt)
    {
        var toolEntries = GetToolEntries();
        var root = new JsonObject();
        if (includeGeneratedAt)
        {
            root["generatedAt"] = DateTime.UtcNow.ToString("O");
        }

        root["toolset"] = "WinSysMcp";

        var toolsObject = new JsonObject();
        foreach (var tool in toolEntries)
        {
            toolsObject[tool.Name] = tool.Node;
        }

        root["tools"] = toolsObject;
        return root;
    }

    public static string BuildMarkdownDocument()
    {
        var toolEntries = GetToolEntries();
        var builder = new StringBuilder(16_384);

        builder.AppendLine("# WinSysMcp — Tool Catalog");
        builder.AppendLine();
        builder.AppendLine("This document is generated from the MCP tool metadata in `src/WinSysMcp/Tools/`. Do not edit it manually; regenerate it with `tools/WinSysMcp.ToolSchemaGen`.");
        builder.AppendLine();
        builder.AppendLine("## Files");
        builder.AppendLine();
        builder.AppendLine("- `docs/TOOLS.md` — human-readable tool catalog generated from source metadata.");
        builder.AppendLine("- `docs/TOOLS_SCHEMA.json` — machine-readable JSON catalog generated from the same metadata.");
        builder.AppendLine("- `src/WinSysMcp/Tools/*` — C# source files implementing each MCP tool.");
        builder.AppendLine("- `tools/WinSysMcp.ToolSchemaGen` — generator that refreshes both catalog files.");
        builder.AppendLine();
        builder.AppendLine("## Refresh generated docs");
        builder.AppendLine();
        builder.AppendLine("```powershell");
        builder.AppendLine("dotnet run --project tools/WinSysMcp.ToolSchemaGen");
        builder.AppendLine("```" );
        builder.AppendLine();
        builder.AppendLine("## Safety notes");
        builder.AppendLine();
        builder.AppendLine("- Some tools are read-only diagnostics; others modify system state or files. Read each description carefully.");
        builder.AppendLine("- File and registry tools include destructive operations. Use guarded or exact-match file edit tools when safety matters.");
        builder.AppendLine("- The machine-readable schema in `docs/TOOLS_SCHEMA.json` is the best source for client-side validation and code generation.");
        builder.AppendLine();
        builder.AppendLine("## Tool groups");
        builder.AppendLine();

        foreach (var group in toolEntries.GroupBy(static t => t.Source, StringComparer.Ordinal).OrderBy(static g => g.Key, StringComparer.Ordinal))
        {
            var groupName = Path.GetFileNameWithoutExtension(group.Key);
            builder.AppendLine($"### `{groupName}` (`{group.Key}`)");
            builder.AppendLine();

            foreach (var tool in group.OrderBy(static t => t.Name, StringComparer.Ordinal))
            {
                builder.Append("- `").Append(tool.Name).Append("` — ").AppendLine(tool.Description);

                if (tool.Parameters.Count == 0)
                {
                    builder.AppendLine("  - Parameters: none");
                }
                else
                {
                    builder.Append("  - Parameters: ");
                    for (var i = 0; i < tool.Parameters.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        var parameter = tool.Parameters[i];
                        builder.Append('`').Append(parameter.Name).Append(parameter.Optional ? "?: " : ": ").Append(parameter.Type);
                        if (parameter.HasDefaultValue && parameter.DefaultValue is not null)
                        {
                            builder.Append(" = ").Append(FormatDefaultValue(parameter.DefaultValue));
                        }

                        builder.Append('`');
                    }

                    builder.AppendLine();
                }
            }

            builder.AppendLine();
        }

        builder.AppendLine("---");
        builder.AppendLine("Generated from source metadata by `tools/WinSysMcp.ToolSchemaGen`.");

        return builder.ToString();
    }

    public static string Serialize(JsonObject catalog)
    {
        return catalog.ToJsonString(JsonOptions) + Environment.NewLine;
    }

    public static JsonObject LoadNormalizedCatalog(string path)
    {
        var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
            ?? throw new InvalidOperationException($"Schema file '{path}' is not a valid JSON object.");

        node.Remove("generatedAt");
        return node;
    }

    public static string LoadTextDocument(string path)
    {
        return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static List<ToolEntry> GetToolEntries()
    {
        var assembly = typeof(WinSysMcp.Tools.FileTools).Assembly;
        var results = new List<ToolEntry>();

        foreach (var type in assembly.GetTypes()
            .Where(static t => t.IsClass && HasAttribute(t, "McpServerToolTypeAttribute"))
            .OrderBy(static t => t.Name, StringComparer.Ordinal))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(static m => HasAttribute(m, "McpServerToolAttribute"))
                .OrderBy(static m => GetToolName(m), StringComparer.Ordinal))
            {
                var toolName = GetToolName(method);
                if (string.IsNullOrWhiteSpace(toolName))
                {
                    continue;
                }

                results.Add(BuildToolEntry(type, method, toolName));
            }
        }

        return results;
    }

    private static ToolEntry BuildToolEntry(Type type, MethodInfo method, string toolName)
    {
        var parameters = method.GetParameters();
        var toolParameters = parameters.Select(BuildToolParameter).ToList();
        var parameterArray = toolParameters.Count == 0 ? null : new JsonArray(toolParameters.Select(static parameter => parameter.Node).ToArray<JsonNode?>());

        var source = GetSourcePath(type);
        var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
        var node = new JsonObject
        {
            ["source"] = source,
            ["description"] = description,
            ["parameters"] = parameterArray,
            ["jsonInputSchema"] = BuildJsonInputSchema(parameters)
        };

        return new ToolEntry(toolName, source, description, toolParameters, node);
    }

    private static JsonNode? BuildJsonInputSchema(ParameterInfo[] parameters)
    {
        if (parameters.Length == 0)
        {
            return null;
        }

        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var parameter in parameters)
        {
            var property = BuildJsonSchemaProperty(parameter);
            properties[parameter.Name!] = property;

            if (!IsOptional(parameter))
            {
                required.Add(parameter.Name);
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static JsonObject BuildJsonSchemaProperty(ParameterInfo parameter)
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        var property = new JsonObject
        {
            ["type"] = GetJsonTypeName(type)
        };

        if (type.IsEnum)
        {
            property["enum"] = new JsonArray(Enum.GetNames(type).Select(static name => (JsonNode?)name).ToArray());
        }

        if (TryGetNumericMinimum(parameter, type, out var minimum))
        {
            property["minimum"] = minimum;
        }

        if (parameter.HasDefaultValue && parameter.DefaultValue is not DBNull)
        {
            property["default"] = JsonValue.Create(parameter.DefaultValue);
        }

        return property;
    }

    private static ToolParameter BuildToolParameter(ParameterInfo parameter)
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        var node = new JsonObject
        {
            ["name"] = parameter.Name,
            ["type"] = GetSchemaTypeName(type)
        };

        if (IsOptional(parameter))
        {
            node["optional"] = true;
        }

        if (parameter.HasDefaultValue && parameter.DefaultValue is not DBNull)
        {
            node["default"] = JsonValue.Create(parameter.DefaultValue);
        }

        return new ToolParameter(
            parameter.Name ?? string.Empty,
            GetSchemaTypeName(type),
            IsOptional(parameter),
            parameter.HasDefaultValue,
            parameter.HasDefaultValue && parameter.DefaultValue is not DBNull ? parameter.DefaultValue : null,
            node);
    }

    private static bool IsOptional(ParameterInfo parameter)
    {
        return parameter.IsOptional
            || parameter.HasDefaultValue
            || Nullable.GetUnderlyingType(parameter.ParameterType) is not null
            || NullabilityContext.Create(parameter).WriteState is NullabilityState.Nullable;
    }

    private static string GetToolName(MethodInfo method)
    {
        var attribute = method.GetCustomAttributes(inherit: false)
            .FirstOrDefault(static attr => string.Equals(attr.GetType().Name, "McpServerToolAttribute", StringComparison.Ordinal));

        return attribute?.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)?.GetValue(attribute) as string
            ?? method.Name;
    }

    private static bool HasAttribute(MemberInfo member, string attributeTypeName)
    {
        return member.GetCustomAttributes(inherit: false)
            .Any(attr => string.Equals(attr.GetType().Name, attributeTypeName, StringComparison.Ordinal));
    }

    private static string GetSourcePath(Type type)
    {
        return type.Name switch
        {
            "PowerTools" or "SecurityTools" => "src/WinSysMcp/Tools/PowerAndSecurityTools.cs",
            _ => $"src/WinSysMcp/Tools/{type.Name}.cs"
        };
    }

    private static string GetSchemaTypeName(Type type)
    {
        if (type == typeof(string) || type == typeof(char) || type.IsEnum)
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong))
        {
            return "integer";
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return "number";
        }

        if (type.IsArray)
        {
            return "array";
        }

        return "object";
    }

    private static string GetJsonTypeName(Type type)
    {
        return GetSchemaTypeName(type);
    }

    private static bool TryGetNumericMinimum(ParameterInfo parameter, Type type, out int minimum)
    {
        if (parameter.Name is "lineNumber" or "startLine" or "expectedOccurrences")
        {
            minimum = 1;
            return true;
        }

        if (parameter.Name == "endLine" && parameter.HasDefaultValue && Equals(parameter.DefaultValue, -1))
        {
            minimum = -1;
            return true;
        }

        minimum = 0;
        return false;
    }

    private static string FormatDefaultValue(object value)
    {
        return value switch
        {
            string s => $"\"{s}\"",
            char c => $"'{c}'",
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private sealed record ToolEntry(string Name, string Source, string Description, List<ToolParameter> Parameters, JsonObject Node);

    private sealed record ToolParameter(string Name, string Type, bool Optional, bool HasDefaultValue, object? DefaultValue, JsonObject Node);
}