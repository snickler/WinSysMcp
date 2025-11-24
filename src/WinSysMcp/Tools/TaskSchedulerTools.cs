using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace WinSysMcp.Tools;

[McpServerToolType]
public static class TaskSchedulerTools
{
    private static string RunCommand(string command, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return $"Error (Exit Code {process.ExitCode}): {error}";
            }

            return output;
        }
        catch (Exception ex)
        {
            return $"Exception running command: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_scheduled_tasks")]
    [System.ComponentModel.Description("Retrieves scheduled tasks using 'schtasks /query'.")]
    public static string GetScheduledTasks(
        [System.ComponentModel.Description("Output format: TABLE, LIST, or CSV. Default is CSV.")] string format = "CSV")
    {
        if (format.ToUpper() != "TABLE" && format.ToUpper() != "LIST" && format.ToUpper() != "CSV")
        {
            return "Invalid format. Use TABLE, LIST, or CSV.";
        }
        return RunCommand("schtasks", $"/query /FO {format} /V");
    }
}
