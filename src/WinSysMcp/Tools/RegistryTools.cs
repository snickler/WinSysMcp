using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Win32;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class RegistryTools
{
    [McpServerTool(Name = "read_registry_value"), Description("Reads a value from the Windows Registry. Parameters: root (HKLM/HKCU/etc), keyPath, valueName. Read-only — returns the value or an explanatory error message. Example: root='HKLM', keyPath='SOFTWARE\\MyApp', valueName='InstallPath'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"root\":{\"type\":\"string\"},\"keyPath\":{\"type\":\"string\"},\"valueName\":{\"type\":\"string\"}}}")]
    public static string ReadRegistryValue(
        [System.ComponentModel.DescriptionAttribute("Root key (HKLM, HKCU).")] string root,
        [System.ComponentModel.DescriptionAttribute("Subkey path.")] string keyPath,
        [System.ComponentModel.DescriptionAttribute("Value name.")] string valueName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(valueName)) return "Error: root, keyPath and valueName are required.";
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

    [McpServerTool(Name = "write_registry_value"), Description("Writes a value to the Windows Registry. Parameters: root, keyPath, valueName, valueData. Destructive operation — modifies system configuration and requires appropriate privileges. Use carefully. Example: root='HKLM', keyPath='SOFTWARE\\MyApp', valueName='Setting', valueData='1'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"root\":{\"type\":\"string\"},\"keyPath\":{\"type\":\"string\"},\"valueName\":{\"type\":\"string\"},\"valueData\":{\"type\":\"string\"}}}")]
    public static string WriteRegistryValue(
        [System.ComponentModel.DescriptionAttribute("Root key (HKLM, HKCU).")] string root,
        [System.ComponentModel.DescriptionAttribute("Subkey path.")] string keyPath,
        [System.ComponentModel.DescriptionAttribute("Value name.")] string valueName,
        [System.ComponentModel.DescriptionAttribute("Value data.")] string valueData)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(valueName)) return "Error: root, keyPath and valueName are required.";
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

    [McpServerTool(Name = "delete_registry_value"), Description("Deletes a registry value. Parameters: root, keyPath, valueName. Destructive — use with care and expect permission errors without elevation. Example: root='HKCU', keyPath='SOFTWARE\\MyApp', valueName='Setting'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"root\":{\"type\":\"string\"},\"keyPath\":{\"type\":\"string\"},\"valueName\":{\"type\":\"string\"}}}")]
    public static string DeleteRegistryValue(
        [System.ComponentModel.DescriptionAttribute("Root key (HKLM, HKCU).")] string root,
        [System.ComponentModel.DescriptionAttribute("Subkey path.")] string keyPath,
        [System.ComponentModel.DescriptionAttribute("Value name.")] string valueName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(valueName)) return "Error: root, keyPath and valueName are required.";
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

    [McpServerTool(Name = "list_registry_keys"), Description("Lists subkeys under a registry key. Parameters: root, keyPath. Read-only listing; will return helpful messages when keys are missing or inaccessible. Example: root='HKLM', keyPath='SOFTWARE'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"root\":{\"type\":\"string\"},\"keyPath\":{\"type\":\"string\"}}}")]
    public static List<string> ListRegistryKeys(
        [System.ComponentModel.DescriptionAttribute("Root key (HKLM, HKCU).")] string root,
        [System.ComponentModel.DescriptionAttribute("Subkey path.")] string keyPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(keyPath)) return new List<string> { "Error: root and keyPath are required." };
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
