using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class ProcessTools
{
    private static readonly HashSet<string> BlockedExecutableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "cmd.exe",
        "powershell", "powershell.exe", "pwsh", "pwsh.exe",
        "wscript", "wscript.exe", "cscript", "cscript.exe",
        "mshta", "mshta.exe", "regsvr32", "regsvr32.exe",
        "rundll32", "rundll32.exe", "msiexec", "msiexec.exe"
    };

    [McpServerTool(Name = "get_top_processes"), Description("Returns the top N running processes sorted by memory (RSS). Parameter: count (default 10). Read-only; may skip system processes due to access restrictions. Example: count=5. JSON input schema example: {\"type\":\"object\",\"properties\":{\"count\":{\"type\":\"integer\"}}}")]
    public static List<ProcessInfoModel> GetTopProcesses(
        [System.ComponentModel.DescriptionAttribute("The number of processes to return. Default is 10.")] int count = 10)
    {
        if (count <= 0) count = 10;
        if (count > 200) count = 200; // don't allow huge values
        var processes = Process.GetProcesses();
        try
        {
            return processes
                .OrderByDescending(p => p.WorkingSet64)
                .Take(count)
                .Select(p => {
                    try
                    {
                        return new ProcessInfoModel
                        {
                            Id = p.Id,
                            ProcessName = p.ProcessName,
                            WorkingSet64 = p.WorkingSet64,
                            PrivateMemorySize64 = p.PrivateMemorySize64,
                            StartTime = TryGetStartTime(p),
                            Responding = p.Responding
                        };
                    }
                    catch
                    {
                        // Handle access denied for some system processes
                        return new ProcessInfoModel
                        {
                            Id = p.Id,
                            ProcessName = p.ProcessName,
                            WorkingSet64 = p.WorkingSet64,
                            Note = "Access Denied to details"
                        };
                    }
                })
                .ToList();
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    private static DateTime? TryGetStartTime(Process p)
    {
        try { return p.StartTime; } catch { return null; }
    }

    [McpServerTool(Name = "kill_process"), Description("Terminates a process by PID. Parameter: processId. Destructive and requires permissions; can fail for protected or system processes. Use cautiously. Example: processId=1234. JSON input schema example: {\"type\":\"object\",\"properties\":{\"processId\":{\"type\":\"integer\"}}}")]
    public static string KillProcess(
        [System.ComponentModel.DescriptionAttribute("The ID of the process to terminate.")] int processId)
    {
        try
        {
            if (processId <= 0) return "Error: processId must be a positive integer.";
            using var process = Process.GetProcessById(processId);
            process.Kill();
            return $"Successfully terminated process {processId} ({process.ProcessName}).";
        }
        catch (ArgumentException)
        {
            return $"Process {processId} not found.";
        }
        catch (Exception ex)
        {
            return $"Failed to terminate process {processId}: {ex.Message}";
        }
    }

    [McpServerTool(Name = "start_process"), Description("Starts a new process with the given executable path and optional arguments. Parameters: fileName, arguments (optional). Use caution launching untrusted executables; process runs under server user account. Example: fileName='C:\\Program Files\\MyApp\\app.exe', arguments='--verbose'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"fileName\":{\"type\":\"string\"},\"arguments\":{\"type\":\"string\"}}}")]
    public static string StartProcess(
        [System.ComponentModel.DescriptionAttribute("The path to the executable.")] string fileName,
        [System.ComponentModel.DescriptionAttribute("Arguments to pass.")] string arguments = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "Error: fileName is required.";
            if (!Path.IsPathRooted(fileName)) return "Error: fileName must be an absolute path to the executable.";
            var executableName = Path.GetFileName(fileName);
            if (BlockedExecutableNames.Contains(executableName)) return $"Error: Launching '{executableName}' is blocked for security reasons.";
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false
            };
            var process = Process.Start(startInfo);
            return process != null ? $"Started process {process.Id}." : "Failed to start process.";
        }
        catch (Exception ex)
        {
            return $"Error starting process: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_process_details"), Description("Returns detailed metadata for a process by PID: name, memory details, start time, module path when accessible. Parameter: processId. Read-only; may fail on protected processes. Example: processId=1234. JSON input schema example: {\"type\":\"object\",\"properties\":{\"processId\":{\"type\":\"integer\"}}}")]
    public static ProcessInfoModel? GetProcessDetails(
        [System.ComponentModel.DescriptionAttribute("The ID of the process.")] int processId)
    {
        try
        {
            if (processId <= 0) return null;
            using var p = Process.GetProcessById(processId);
            return new ProcessInfoModel
            {
                Id = p.Id,
                ProcessName = p.ProcessName,
                WorkingSet64 = p.WorkingSet64,
                PrivateMemorySize64 = p.PrivateMemorySize64,
                StartTime = TryGetStartTime(p),
                Responding = p.Responding,
                Note = p.MainModule?.FileName
            };
        }
        catch
        {
            return null;
        }
    }

    public class ProcessInfoModel
    {
        public int Id { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public long WorkingSet64 { get; set; }
        public long PrivateMemorySize64 { get; set; }
        public DateTime? StartTime { get; set; }
        public bool Responding { get; set; }
        public string? Note { get; set; }
    }
}
