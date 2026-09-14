using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
#if WINDOWS_APIS
using System.Diagnostics;
#endif

namespace WinSysMcp.Tools;

[McpServerToolType]
public class EventLogTools
{
    [McpServerTool(Name = "get_event_logs"), Description("Fetches recent log entries. On Windows: Event Log (Application/System/etc). On Linux: journalctl (logName maps to systemd unit or 'system'/'user'). Parameters: logName (default 'Application'/'system'), maxEvents, entryType. Read-only; may require privileges.")]
    public static List<EventLogEntryModel> GetEventLogs(
        [Description("Windows log name (Application/System) or Linux journal scope/unit (system, user, or unit name).")] string logName = "Application",
        [Description("The maximum number of events to return. Default is 10.")] int maxEvents = 10,
        [Description("Filter by entry type (e.g., 'Error', 'Warning', 'Information'). Optional.")] string? entryType = null)
    {
        if (maxEvents <= 0) maxEvents = 10;
        if (maxEvents > 1000) maxEvents = 1000;

#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return GetWindowsEventLogs(logName, maxEvents, entryType);
#endif
        return GetJournalLogs(logName, maxEvents, entryType);
    }

#if WINDOWS_APIS
    private static List<EventLogEntryModel> GetWindowsEventLogs(string logName, int maxEvents, string? entryType)
    {
        var results = new List<EventLogEntryModel>();
        try
        {
            if (string.IsNullOrWhiteSpace(logName)) return results;
            if (!EventLog.Exists(logName))
                throw new ArgumentException($"Event log '{logName}' does not exist.");

            using var eventLog = new EventLog(logName);
            var entries = eventLog.Entries;
            int count = entries.Count;
            int added = 0;

            for (int i = count - 1; i >= 0 && added < maxEvents; i--)
            {
                var entry = entries[i];
                if (!string.IsNullOrEmpty(entryType)
                    && !entry.EntryType.ToString().Equals(entryType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                results.Add(new EventLogEntryModel
                {
                    Index = entry.Index,
                    TimeGenerated = entry.TimeGenerated,
                    Source = entry.Source,
                    EntryType = entry.EntryType.ToString(),
                    Message = entry.Message,
                    InstanceId = entry.InstanceId
                });
                added++;
            }
        }
        catch (Exception ex)
        {
            results.Add(new EventLogEntryModel
            {
                Message = $"Error retrieving logs: {ex.Message}",
                EntryType = "Error"
            });
        }

        return results;
    }
#endif

    private static List<EventLogEntryModel> GetJournalLogs(string logName, int maxEvents, string? entryType)
    {
        var results = new List<EventLogEntryModel>();
        try
        {
            var scope = string.IsNullOrWhiteSpace(logName) ? "system" : logName.Trim();
            // Map common Windows log names to journal defaults for agent familiarity.
            if (scope.Equals("Application", StringComparison.OrdinalIgnoreCase)
                || scope.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                scope = "system";
            }

            var args = $"--no-pager -o json -n {maxEvents}";
            if (scope.Equals("system", StringComparison.OrdinalIgnoreCase))
                args += " --system";
            else if (scope.Equals("user", StringComparison.OrdinalIgnoreCase))
                args += " --user";
            else
                args += $" -u {Quote(scope)}";

            if (!string.IsNullOrEmpty(entryType))
            {
                var priority = MapPriority(entryType);
                if (priority is not null)
                    args += $" -p {priority}";
            }

            var (exit, stdout, stderr) = OsProcess.RunRaw("journalctl", args);
            if (exit != 0)
            {
                results.Add(new EventLogEntryModel
                {
                    Message = $"Error retrieving logs: {(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr)}",
                    EntryType = "Error"
                });
                return results;
            }

            var index = 0;
            foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var priority = root.TryGetProperty("PRIORITY", out var p) ? p.GetString() ?? "" : "";
                    var mappedType = MapEntryType(priority);
                    if (!string.IsNullOrEmpty(entryType)
                        && !mappedType.Equals(entryType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var message = root.TryGetProperty("MESSAGE", out var m) ? m.GetString() ?? "" : line;
                    var source = root.TryGetProperty("SYSLOG_IDENTIFIER", out var s)
                        ? s.GetString() ?? ""
                        : root.TryGetProperty("_SYSTEMD_UNIT", out var u) ? u.GetString() ?? "" : scope;
                    var time = DateTime.UtcNow;
                    if (root.TryGetProperty("__REALTIME_TIMESTAMP", out var ts)
                        && long.TryParse(ts.GetString(), out var micros))
                    {
                        time = DateTimeOffset.FromUnixTimeMilliseconds(micros / 1000).UtcDateTime;
                    }

                    results.Add(new EventLogEntryModel
                    {
                        Index = ++index,
                        TimeGenerated = time,
                        Source = source,
                        EntryType = mappedType,
                        Message = message,
                        InstanceId = root.TryGetProperty("_PID", out var pid) && long.TryParse(pid.GetString(), out var pidVal)
                            ? pidVal
                            : 0
                    });
                }
                catch
                {
                    results.Add(new EventLogEntryModel
                    {
                        Index = ++index,
                        TimeGenerated = DateTime.UtcNow,
                        Source = scope,
                        EntryType = "Information",
                        Message = line
                    });
                }
            }
        }
        catch (Exception ex)
        {
            results.Add(new EventLogEntryModel
            {
                Message = $"Error retrieving logs: {ex.Message}",
                EntryType = "Error"
            });
        }

        return results;
    }

    private static string? MapPriority(string entryType)
        => entryType.ToLowerInvariant() switch
        {
            "error" or "err" or "critical" or "alert" or "emerg" => "err",
            "warning" or "warn" => "warning",
            "information" or "info" or "notice" => "info",
            "debug" => "debug",
            _ => null
        };

    private static string MapEntryType(string priority)
        => priority switch
        {
            "0" or "1" or "2" or "3" => "Error",
            "4" => "Warning",
            "7" => "Verbose",
            _ => "Information"
        };

    private static string Quote(string value)
        => value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;

    public class EventLogEntryModel
    {
        public int Index { get; set; }
        public DateTime TimeGenerated { get; set; }
        public string Source { get; set; } = string.Empty;
        public string EntryType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public long InstanceId { get; set; }
    }
}
