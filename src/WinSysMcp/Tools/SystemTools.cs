using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace WinSysMcp.Tools;

[McpServerToolType]
public static class SystemTools
{
    [McpServerTool(Name = "get_system_info")]
    public static string GetSystemInfo()
    {
        return $"OS: {RuntimeInformation.OSDescription}, Machine: {Environment.MachineName}, .NET: {Environment.Version}";
    }

    [McpServerTool(Name = "echo_message")]
    public static string Echo([System.ComponentModel.DescriptionAttribute("The message to echo")] string message)
    {
        return $"Echo: {message}";
    }

    [McpServerTool(Name = "get_environment_variables")]
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

    [McpServerTool(Name = "get_startup_apps")]
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

    [McpServerTool(Name = "get_uptime")]
    public static string GetUptime()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return $"{uptime.Days} days, {uptime.Hours} hours, {uptime.Minutes} minutes, {uptime.Seconds} seconds";
    }

    [McpServerTool(Name = "get_os_version")]
    public static string GetOsVersion()
    {
        return $"{Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})";
    }

    [McpServerTool(Name = "lock_workstation")]
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

    [McpServerTool(Name = "shutdown_computer")]
    public static string ShutdownComputer(
        [System.ComponentModel.DescriptionAttribute("Delay in seconds. Default 30.")] int delay = 30,
        [System.ComponentModel.DescriptionAttribute("Comment to display.")] string comment = "Shutdown initiated by MCP.")
    {
        try
        {
            Process.Start("shutdown", $"/s /t {delay} /c \"{comment}\"");
            return $"Shutdown initiated in {delay} seconds.";
        }
        catch (Exception ex)
        {
            return $"Error initiating shutdown: {ex.Message}";
        }
    }

    [McpServerTool(Name = "restart_computer")]
    public static string RestartComputer(
        [System.ComponentModel.DescriptionAttribute("Delay in seconds. Default 30.")] int delay = 30,
        [System.ComponentModel.DescriptionAttribute("Comment to display.")] string comment = "Restart initiated by MCP.")
    {
        try
        {
            Process.Start("shutdown", $"/r /t {delay} /c \"{comment}\"");
            return $"Restart initiated in {delay} seconds.";
        }
        catch (Exception ex)
        {
            return $"Error initiating restart: {ex.Message}";
        }
    }

    [McpServerTool(Name = "abort_shutdown")]
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
