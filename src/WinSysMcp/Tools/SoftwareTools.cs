using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Win32;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class SoftwareTools
{
    [McpServerTool(Name = "get_installed_programs")]
    public static List<InstalledProgramModel> GetInstalledPrograms(
        [System.ComponentModel.DescriptionAttribute("Filter by program name (partial match). Optional.")] string? nameFilter = null)
    {
        var programs = new List<InstalledProgramModel>();
        string registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        // Search Current User
        GetProgramsFromRegistry(Registry.CurrentUser, registryKey, programs, nameFilter);
        
        // Search Local Machine (32-bit and 64-bit)
        GetProgramsFromRegistry(Registry.LocalMachine, registryKey, programs, nameFilter);
        GetProgramsFromRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", programs, nameFilter);

        return programs.DistinctBy(p => p.DisplayName).OrderBy(p => p.DisplayName).ToList();
    }

    private static void GetProgramsFromRegistry(RegistryKey root, string keyPath, List<InstalledProgramModel> programs, string? nameFilter)
    {
        try
        {
            using var key = root.OpenSubKey(keyPath);
            if (key == null) return;

            foreach (var subkeyName in key.GetSubKeyNames())
            {
                using var subkey = key.OpenSubKey(subkeyName);
                if (subkey == null) continue;

                var displayName = subkey.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(displayName)) continue;

                if (!string.IsNullOrEmpty(nameFilter) && 
                    !displayName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                programs.Add(new InstalledProgramModel
                {
                    DisplayName = displayName,
                    DisplayVersion = subkey.GetValue("DisplayVersion") as string ?? "",
                    Publisher = subkey.GetValue("Publisher") as string ?? "",
                    InstallDate = subkey.GetValue("InstallDate") as string ?? ""
                });
            }
        }
        catch
        {
            // Ignore permission errors or missing keys
        }
    }

    public class InstalledProgramModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string DisplayVersion { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string InstallDate { get; set; } = string.Empty;
    }
}
