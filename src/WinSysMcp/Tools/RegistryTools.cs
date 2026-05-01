using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Win32;

namespace WinSysMcp.Tools;

[McpServerToolType]
public static class RegistryTools
{
    [McpServerTool(Name = "read_registry_value")]
    public static string ReadRegistryValue(
        [System.ComponentModel.DescriptionAttribute("Root key (HKLM, HKCU).")] string root,
        [System.ComponentModel.DescriptionAttribute("Subkey path.")] string keyPath,
        [System.ComponentModel.DescriptionAttribute("Value name.")] string valueName)
    {
        try
        {
            using var key = GetRootKey(root).OpenSubKey(keyPath);
            if (key == null) return "Key not found.";
            var val = key.GetValue(valueName);
            return val?.ToString() ?? "Value not found or null.";
        }
        catch (Exception ex)
        {
            return $"Error reading registry: {ex.Message}";
        }
    }

    [McpServerTool(Name = "write_registry_value")]
    public static string WriteRegistryValue(
        [System.ComponentModel.DescriptionAttribute("Root key (HKLM, HKCU).")] string root,
        [System.ComponentModel.DescriptionAttribute("Subkey path.")] string keyPath,
        [System.ComponentModel.DescriptionAttribute("Value name.")] string valueName,
        [System.ComponentModel.DescriptionAttribute("Value data.")] string valueData)
    {
        try
        {
            using var key = GetRootKey(root).OpenSubKey(keyPath, true);
            if (key == null) return "Key not found.";
            key.SetValue(valueName, valueData);
            return "Successfully wrote registry value.";
        }
        catch (Exception ex)
        {
            return $"Error writing registry: {ex.Message}";
        }
    }

    [McpServerTool(Name = "delete_registry_value")]
    public static string DeleteRegistryValue(
        [System.ComponentModel.DescriptionAttribute("Root key (HKLM, HKCU).")] string root,
        [System.ComponentModel.DescriptionAttribute("Subkey path.")] string keyPath,
        [System.ComponentModel.DescriptionAttribute("Value name.")] string valueName)
    {
        try
        {
            using var key = GetRootKey(root).OpenSubKey(keyPath, true);
            if (key == null) return "Key not found.";
            key.DeleteValue(valueName);
            return "Successfully deleted registry value.";
        }
        catch (Exception ex)
        {
            return $"Error deleting registry value: {ex.Message}";
        }
    }

    [McpServerTool(Name = "list_registry_keys")]
    public static List<string> ListRegistryKeys(
        [System.ComponentModel.DescriptionAttribute("Root key (HKLM, HKCU).")] string root,
        [System.ComponentModel.DescriptionAttribute("Subkey path.")] string keyPath)
    {
        try
        {
            using var key = GetRootKey(root).OpenSubKey(keyPath);
            if (key == null) return new List<string> { "Key not found." };
            return key.GetSubKeyNames().ToList();
        }
        catch (Exception ex)
        {
            return new List<string> { $"Error listing keys: {ex.Message}" };
        }
    }

    private static RegistryKey GetRootKey(string root)
    {
        return root.ToUpper() switch
        {
            "HKLM" => Registry.LocalMachine,
            "HKCU" => Registry.CurrentUser,
            "HKCR" => Registry.ClassesRoot,
            "HKU" => Registry.Users,
            "HKCC" => Registry.CurrentConfig,
            _ => throw new ArgumentException("Invalid root key. Use HKLM, HKCU, HKCR, HKU, or HKCC.")
        };
    }
}
