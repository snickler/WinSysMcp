using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class ProcessTools
{
    [McpServerTool(Name = "get_top_processes")]
    public static List<ProcessInfoModel> GetTopProcesses(
        [System.ComponentModel.DescriptionAttribute("The number of processes to return. Default is 10.")] int count = 10)
    {
        var processes = Process.GetProcesses();
        
        var sorted = processes
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

        return sorted;
    }

    private static DateTime? TryGetStartTime(Process p)
    {
        try { return p.StartTime; } catch { return null; }
    }

    [McpServerTool(Name = "kill_process")]
    public static string KillProcess(
        [System.ComponentModel.DescriptionAttribute("The ID of the process to terminate.")] int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
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

    [McpServerTool(Name = "start_process")]
    public static string StartProcess(
        [System.ComponentModel.DescriptionAttribute("The path to the executable.")] string fileName,
        [System.ComponentModel.DescriptionAttribute("Arguments to pass.")] string arguments = "")
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            };
            var process = Process.Start(startInfo);
            return process != null ? $"Started process {process.Id}." : "Failed to start process.";
        }
        catch (Exception ex)
        {
            return $"Error starting process: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_process_details")]
    public static ProcessInfoModel? GetProcessDetails(
        [System.ComponentModel.DescriptionAttribute("The ID of the process.")] int processId)
    {
        try
        {
            var p = Process.GetProcessById(processId);
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
