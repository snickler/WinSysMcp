using ModelContextProtocol.Server;
using System.ComponentModel;
#if WINDOWS_APIS
using Microsoft.Win32;
#endif

namespace WinSysMcp.Tools;

[McpServerToolType]
public class SoftwareTools
{
    [McpServerTool(Name = "get_installed_programs"), Description("Lists installed programs. On Windows: Add/Remove Programs registry. On Linux: dpkg/rpm package database. Parameter: nameFilter (optional). Read-only.")]
    public static List<InstalledProgramModel> GetInstalledPrograms(
        [Description("Filter by program name (partial match). Optional.")] string? nameFilter = null)
    {
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return GetWindowsPrograms(nameFilter);
#endif
        return GetLinuxPackages(nameFilter);
    }

#if WINDOWS_APIS
    private static List<InstalledProgramModel> GetWindowsPrograms(string? nameFilter)
    {
        var programs = new List<InstalledProgramModel>();
        const string registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        GetProgramsFromRegistry(Registry.CurrentUser, registryKey, programs, nameFilter);
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

                if (!string.IsNullOrEmpty(nameFilter)
                    && !displayName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
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
#endif

    private static List<InstalledProgramModel> GetLinuxPackages(string? nameFilter)
    {
        var programs = new List<InstalledProgramModel>();

        var (dpkgExit, dpkgOut, _) = OsProcess.RunRaw(
            "dpkg-query",
            "-W -f=${Package}\\t${Version}\\t${Maintainer}\\t${Installed-Size}\\n");
        if (dpkgExit == 0)
        {
            foreach (var line in dpkgOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split('\t');
                var name = parts.ElementAtOrDefault(0) ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!string.IsNullOrEmpty(nameFilter)
                    && !name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                programs.Add(new InstalledProgramModel
                {
                    DisplayName = name,
                    DisplayVersion = parts.ElementAtOrDefault(1) ?? "",
                    Publisher = parts.ElementAtOrDefault(2) ?? "",
                    InstallDate = ""
                });
            }

            return programs.OrderBy(p => p.DisplayName).ToList();
        }

        var (rpmExit, rpmOut, _) = OsProcess.RunRaw("rpm", "-qa --qf '%{NAME}\\t%{VERSION}-%{RELEASE}\\t%{VENDOR}\\t%{INSTALLTIME:date}\\n'");
        if (rpmExit == 0)
        {
            foreach (var line in rpmOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split('\t');
                var name = parts.ElementAtOrDefault(0) ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!string.IsNullOrEmpty(nameFilter)
                    && !name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                programs.Add(new InstalledProgramModel
                {
                    DisplayName = name,
                    DisplayVersion = parts.ElementAtOrDefault(1) ?? "",
                    Publisher = parts.ElementAtOrDefault(2) ?? "",
                    InstallDate = parts.ElementAtOrDefault(3) ?? ""
                });
            }
        }
        else
        {
            programs.Add(new InstalledProgramModel
            {
                DisplayName = "error",
                Publisher = "Neither dpkg-query nor rpm is available on this host."
            });
        }

        return programs.OrderBy(p => p.DisplayName).ToList();
    }

    public class InstalledProgramModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string DisplayVersion { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string InstallDate { get; set; } = string.Empty;
    }
}
