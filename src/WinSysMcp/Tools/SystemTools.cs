using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;

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
    public static string Echo([System.ComponentModel.DescriptionAttribute("The message to echo")] string message)
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

    [McpServerTool(Name = "get_startup_apps"), Description("Lists applications configured to start automatically via common Registry Run keys (HKLM/HKCU). Returns name, command string and which hive (HKLM/HKCU). Read-only and safe.")]
    public static List<StartupAppModel> GetStartupApps()
    {
        var apps = new List<StartupAppModel>();
        string[] runKeys = {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
        };

        foreach (var keyPath in runKeys)
        {
            try
            {
                // Check Local Machine
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        foreach (var name in key.GetValueNames())
                        {
                            apps.Add(new StartupAppModel { Name = name, Command = key.GetValue(name)?.ToString() ?? "", Location = "HKLM" });
                        }
                    }
                }

                // Check Current User
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath))
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
            catch { }
        }

        return apps;
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

    [McpServerTool(Name = "lock_workstation"), Description("Locks the currently logged-in user session immediately. Requires interactive desktop; may not work from non-interactive services or remote sessions.")]
    public static string LockWorkstation()
    {
        try
        {
            LockWorkStation();
            return "Workstation locked.";
        }
        catch (Exception ex)
        {
            return $"Error locking workstation: {ex.Message}";
        }
    }

    [McpServerTool(Name = "shutdown_computer"), Description("Schedules a system shutdown. Parameters: delay (seconds, default 30) and comment. Requires privileges; operation is destructive. Example: delay=60, comment='Maintenance'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"delay\":{\"type\":\"integer\"},\"comment\":{\"type\":\"string\"}}}")]
    public static string ShutdownComputer(
        [System.ComponentModel.DescriptionAttribute("Delay in seconds. Default 30.")] int delay = 30,
        [System.ComponentModel.DescriptionAttribute("Comment to display.")] string comment = "Shutdown initiated by MCP.")
    {
        try
        {
            if (delay < 0) return "Error: delay must be >= 0.";
            if (comment?.Length > 200) return "Error: comment too long (max 200 chars).";
            Process.Start("shutdown", $"/s /t {delay} /c \"{comment}\"");
            return $"Shutdown initiated in {delay} seconds.";
        }
        catch (Exception ex)
        {
            return $"Error initiating shutdown: {ex.Message}";
        }
    }

    [McpServerTool(Name = "restart_computer"), Description("Schedules a system restart. Parameters: delay (seconds, default 30) and comment. Requires privileges; this will reboot the machine. Example: delay=30, comment='Patch install'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"delay\":{\"type\":\"integer\"},\"comment\":{\"type\":\"string\"}}}")]
    public static string RestartComputer(
        [System.ComponentModel.DescriptionAttribute("Delay in seconds. Default 30.")] int delay = 30,
        [System.ComponentModel.DescriptionAttribute("Comment to display.")] string comment = "Restart initiated by MCP.")
    {
        try
        {
            if (delay < 0) return "Error: delay must be >= 0.";
            if (comment?.Length > 200) return "Error: comment too long (max 200 chars).";
            Process.Start("shutdown", $"/r /t {delay} /c \"{comment}\"");
            return $"Restart initiated in {delay} seconds.";
        }
        catch (Exception ex)
        {
            return $"Error initiating restart: {ex.Message}";
        }
    }

    [McpServerTool(Name = "abort_shutdown"), Description("Attempts to cancel a pending shutdown or restart initiated by the OS shutdown command. Only affects a scheduled shutdown/restart that is currently pending and requires appropriate privileges.")]
    public static string AbortShutdown()
    {
        try
        {
            Process.Start("shutdown", "/a");
            return "Shutdown aborted.";
        }
        catch (Exception ex)
        {
            return $"Error aborting shutdown: {ex.Message}";
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    public class StartupAppModel
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }
}
