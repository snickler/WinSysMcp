using ModelContextProtocol.Server;
using System.ComponentModel;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class TaskSchedulerTools
{
    [McpServerTool(Name = "get_scheduled_tasks"), Description("Retrieves scheduled tasks. Windows: schtasks /query. Linux: systemctl list-timers plus user crontab. Parameter: format (TABLE|LIST|CSV on Windows; ignored on Linux).")]
    public static string GetScheduledTasks(
        [Description("Output format on Windows: TABLE, LIST, or CSV. Default is CSV. Ignored on Linux.")] string format = "CSV")
    {
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
        {
            var fmt = (format ?? string.Empty).ToUpperInvariant();
            if (fmt != "TABLE" && fmt != "LIST" && fmt != "CSV")
                return "Invalid format. Use TABLE, LIST, or CSV.";
            return OsProcess.Run("schtasks", $"/query /FO {fmt} /V");
        }
#endif
        var timers = OsProcess.Run("systemctl", "list-timers --all --no-pager");
        var crontab = OsProcess.Run("crontab", "-l");
        return $"=== systemd timers ===\n{timers}\n\n=== user crontab ===\n{crontab}";
    }
}
