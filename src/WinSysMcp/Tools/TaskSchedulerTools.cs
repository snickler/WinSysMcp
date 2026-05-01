using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class TaskSchedulerTools
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

    [McpServerTool(Name = "get_scheduled_tasks"), Description("Retrieves scheduled tasks via 'schtasks /query'. Parameter: format (TABLE|LIST|CSV, default CSV). Output is raw command text; permissions and scheduler service state affect results. Example: format='CSV'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"format\":{\"type\":\"string\"}}}")]
    public static string GetScheduledTasks(
        [System.ComponentModel.Description("Output format: TABLE, LIST, or CSV. Default is CSV.")] string format = "CSV")
    {
        var fmt = (format ?? string.Empty).ToUpper();
        if (fmt != "TABLE" && fmt != "LIST" && fmt != "CSV")
        {
            return "Invalid format. Use TABLE, LIST, or CSV.";
        }
        return RunCommand("schtasks", $"/query /FO {fmt} /V");
    }
}
