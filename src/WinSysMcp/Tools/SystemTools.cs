using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
#if WINDOWS_APIS
using Microsoft.Win32;
#endif

namespace WinSysMcp.Tools;

[McpServerToolType]
public class SystemTools
{
    [McpServerTool(Name = "get_system_info"), Description("Returns key read-only system diagnostics: OS description, machine name, .NET runtime version and architecture. Safe to call — useful for diagnostics. Example: no parameters.")]
    public static string GetSystemInfo()
    {
        return $"OS: {RuntimeInformation.OSDescription}, Machine: {Environment.MachineName}, .NET: {Environment.Version}";
    }

    [McpServerTool(Name = "echo_message"), Description("Echoes back the provided text. Use for connection and health checks. Parameter: message — string returned verbatim. Example: message='ping'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"message\":{\"type\":\"string\"}}}")]
    public static string Echo([Description("The message to echo")] string message)
    {
        return $"Echo: {message}";
    }

    [McpServerTool(Name = "get_environment_variables"), Description("Returns all environment variables visible to the MCP process as a dictionary (key => value). WARNING: may contain sensitive values (API keys, secrets) — treat output carefully.")]
    public static Dictionary<string, string> GetEnvironmentVariables()
    {
        var vars = Environment.GetEnvironmentVariables();
        var result = new Dictionary<string, string>();
        foreach (System.Collections.DictionaryEntry entry in vars)
        {
            result[entry.Key.ToString()!] = entry.Value?.ToString() ?? "";
        }
        return result;
    }

    [McpServerTool(Name = "get_startup_apps"), Description("Lists applications configured to start automatically. Windows: Registry Run keys. Linux: ~/.config/autostart and enabled systemd user units. Read-only and safe.")]
    public static List<StartupAppModel> GetStartupApps()
    {
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return GetWindowsStartupApps();
#endif
        return GetLinuxStartupApps();
    }

    [McpServerTool(Name = "get_uptime"), Description("Returns system uptime (time since last boot) as a human-readable string. Read-only diagnostic information.")]
    public static string GetUptime()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return $"{uptime.Days} days, {uptime.Hours} hours, {uptime.Minutes} minutes, {uptime.Seconds} seconds";
    }

    [McpServerTool(Name = "get_os_version"), Description("Reports detailed OS version information and whether the OS is 32-bit or 64-bit. Useful for troubleshooting and compatibility checks.")]
    public static string GetOsVersion()
    {
        return $"{Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})";
    }

    [McpServerTool(Name = "lock_workstation"), Description("Locks the currently logged-in user session immediately. Windows: LockWorkStation. Linux: loginctl lock-session. May not work from non-interactive services.")]
    public static string LockWorkstation()
    {
        try
        {
#if WINDOWS_APIS
            if (OperatingSystem.IsWindows())
            {
                LockWorkStation();
                return "Workstation locked.";
            }
#endif
            var result = OsProcess.Run("loginctl", "lock-session");
            return result.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || result.StartsWith("Exception", StringComparison.OrdinalIgnoreCase)
                ? result
                : "Workstation locked.";
        }
        catch (Exception ex)
        {
            return $"Error locking workstation: {ex.Message}";
        }
    }

    [McpServerTool(Name = "shutdown_computer"), Description("Schedules a system shutdown. Parameters: delay (seconds, default 30) and comment. Requires privileges; operation is destructive.")]
    public static string ShutdownComputer(
        [Description("Delay in seconds. Default 30.")] int delay = 30,
        [Description("Comment to display.")] string comment = "Shutdown initiated by MCP.")
    {
        try
        {
            if (delay < 0) return "Error: delay must be >= 0.";
            if (comment?.Length > 200) return "Error: comment too long (max 200 chars).";
#if WINDOWS_APIS
            if (OperatingSystem.IsWindows())
            {
                Process.Start("shutdown", $"/s /t {delay} /c \"{comment}\"");
                return $"Shutdown initiated in {delay} seconds.";
            }
#endif
            // GNU shutdown: +minutes; for seconds use systemd-run or sleep+shutdown.
            var minutes = Math.Max(1, (int)Math.Ceiling(delay / 60.0));
            string result;
            if (delay == 0)
            {
                result = OsProcess.Run("shutdown", $"-h now {QuoteArgument(comment ?? "")}");
            }
            else
            {
                result = OsProcess.Run("shutdown", $"-h +{minutes} {QuoteArgument(comment ?? "")}");
            }

            if (IsProcessError(result))
                return result;

            return $"Shutdown initiated in {delay} seconds.";
        }
        catch (Exception ex)
        {
            return $"Error initiating shutdown: {ex.Message}";
        }
    }

    [McpServerTool(Name = "restart_computer"), Description("Schedules a system restart. Parameters: delay (seconds, default 30) and comment. Requires privileges; this will reboot the machine.")]
    public static string RestartComputer(
        [Description("Delay in seconds. Default 30.")] int delay = 30,
        [Description("Comment to display.")] string comment = "Restart initiated by MCP.")
    {
        try
        {
            if (delay < 0) return "Error: delay must be >= 0.";
            if (comment?.Length > 200) return "Error: comment too long (max 200 chars).";
#if WINDOWS_APIS
            if (OperatingSystem.IsWindows())
            {
                Process.Start("shutdown", $"/r /t {delay} /c \"{comment}\"");
                return $"Restart initiated in {delay} seconds.";
            }
#endif
            var minutes = Math.Max(1, (int)Math.Ceiling(delay / 60.0));
            string result;
            if (delay == 0)
            {
                result = OsProcess.Run("shutdown", $"-r now {QuoteArgument(comment ?? "")}");
            }
            else
            {
                result = OsProcess.Run("shutdown", $"-r +{minutes} {QuoteArgument(comment ?? "")}");
            }

            if (IsProcessError(result))
                return result;

            return $"Restart initiated in {delay} seconds.";
        }
        catch (Exception ex)
        {
            return $"Error initiating restart: {ex.Message}";
        }
    }

    [McpServerTool(Name = "abort_shutdown"), Description("Attempts to cancel a pending shutdown or restart. Windows: shutdown /a. Linux: shutdown -c.")]
    public static string AbortShutdown()
    {
        try
        {
#if WINDOWS_APIS
            if (OperatingSystem.IsWindows())
            {
                Process.Start("shutdown", "/a");
                return "Shutdown aborted.";
            }
#endif
            var result = OsProcess.Run("shutdown", "-c");
            return result.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || result.StartsWith("Exception", StringComparison.OrdinalIgnoreCase)
                ? result
                : "Shutdown aborted.";
        }
        catch (Exception ex)
        {
            return $"Error aborting shutdown: {ex.Message}";
        }
    }

#if WINDOWS_APIS
    private static List<StartupAppModel> GetWindowsStartupApps()
    {
        var apps = new List<StartupAppModel>();
        string[] runKeys =
        [
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
        ];

        foreach (var keyPath in runKeys)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        foreach (var name in key.GetValueNames())
                        {
                            apps.Add(new StartupAppModel { Name = name, Command = key.GetValue(name)?.ToString() ?? "", Location = "HKLM" });
                        }
                    }
                }

                using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        foreach (var name in key.GetValueNames())
                        {
                            apps.Add(new StartupAppModel { Name = name, Command = key.GetValue(name)?.ToString() ?? "", Location = "HKCU" });
                        }
                    }
                }
            }
            catch { /* ignore */ }
        }

        return apps;
    }

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();
#endif

    private static List<StartupAppModel> GetLinuxStartupApps()
    {
        var apps = new List<StartupAppModel>();
        var autostartDirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart"),
            "/etc/xdg/autostart"
        };

        foreach (var dir in autostartDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.desktop"))
            {
                try
                {
                    string? name = null;
                    string? exec = null;
                    foreach (var line in File.ReadLines(file))
                    {
                        if (line.StartsWith("Name=", StringComparison.Ordinal))
                            name = line["Name=".Length..].Trim();
                        else if (line.StartsWith("Exec=", StringComparison.Ordinal))
                            exec = line["Exec=".Length..].Trim();
                    }

                    apps.Add(new StartupAppModel
                    {
                        Name = name ?? Path.GetFileNameWithoutExtension(file),
                        Command = exec ?? file,
                        Location = dir
                    });
                }
                catch { /* ignore */ }
            }
        }

        var (exit, stdout, _) = OsProcess.RunRaw(
            "systemctl",
            "--user list-unit-files --type=service --state=enabled --no-pager --no-legend");
        if (exit == 0)
        {
            foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                apps.Add(new StartupAppModel
                {
                    Name = parts[0],
                    Command = $"systemctl --user start {parts[0]}",
                    Location = "systemd-user"
                });
            }
        }

        return apps;
    }

    private static bool IsProcessError(string result)
        => result.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
            || result.StartsWith("Exception", StringComparison.OrdinalIgnoreCase);

    private static string QuoteArgument(string value)
        => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public class StartupAppModel
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }
}
