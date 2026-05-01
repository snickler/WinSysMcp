using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class EventLogTools
{
    [McpServerTool(Name = "get_event_logs"), Description("Fetches recent entries from a Windows Event Log (e.g., Application, System). Parameters: logName (default 'Application'), maxEvents (default 10), entryType (optional filter like 'Error'/'Warning'/'Information'). Read-only; may require administrative privileges. Example: logName='System', maxEvents=20, entryType='Error'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"logName\":{\"type\":\"string\"},\"maxEvents\":{\"type\":\"integer\"},\"entryType\":{\"type\":\"string\"}}}")]
    public static List<EventLogEntryModel> GetEventLogs(
        [System.ComponentModel.DescriptionAttribute("The name of the log to query (e.g., 'Application', 'System'). Default is 'Application'.")] string logName = "Application",
        [System.ComponentModel.DescriptionAttribute("The maximum number of events to return. Default is 10.")] int maxEvents = 10,
        [System.ComponentModel.DescriptionAttribute("Filter by entry type (e.g., 'Error', 'Warning', 'Information'). Optional.")] string? entryType = null)
    {
        var results = new List<EventLogEntryModel>();

        try
        {
            if (string.IsNullOrWhiteSpace(logName)) return results; // return empty list for invalid log name
            if (maxEvents <= 0) maxEvents = 10;
            if (maxEvents > 1000) maxEvents = 1000; // cap to reasonable number
            if (!EventLog.Exists(logName))
            {
                throw new ArgumentException($"Event log '{logName}' does not exist.");
            }

            using var eventLog = new EventLog(logName);
            var entries = eventLog.Entries;
            int count = entries.Count;
            int added = 0;

            // Iterate backwards to get the most recent events
            for (int i = count - 1; i >= 0 && added < maxEvents; i--)
            {
                var entry = entries[i];

                if (!string.IsNullOrEmpty(entryType))
                {
                    if (!entry.EntryType.ToString().Equals(entryType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
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
            // Return a single error entry if something goes wrong (e.g. permissions)
            // This is better than crashing the tool execution
            results.Add(new EventLogEntryModel
            {
                Message = $"Error retrieving logs: {ex.Message}",
                EntryType = "Error"
            });
        }

        return results;
    }

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
