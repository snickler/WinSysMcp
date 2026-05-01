using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Management;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class ReliabilityTools
{
    [McpServerTool(Name = "get_reliability_records")]
    public static List<ReliabilityRecordModel> GetReliabilityRecords(
        [System.ComponentModel.DescriptionAttribute("The maximum number of records to return. Default is 10.")] int maxEvents = 10)
    {
        var results = new List<ReliabilityRecordModel>();

        try
        {
            var scope = new ManagementScope("\\\\.\\root\\cimv2");
            scope.Connect();

            // Check if class exists first to avoid ugly errors? 
            // Or just try query.
            var query = new ObjectQuery("SELECT * FROM Win32_ReliabilityRecords");
            using var searcher = new ManagementObjectSearcher(scope, query);
            using var collection = searcher.Get();

            var allRecords = new List<ReliabilityRecordModel>();

            foreach (ManagementObject obj in collection)
            {
                var timeStr = obj["TimeGenerated"]?.ToString();
                DateTime time = DateTime.MinValue;
                if (!string.IsNullOrEmpty(timeStr))
                {
                    try { time = ManagementDateTimeConverter.ToDateTime(timeStr); } catch { }
                }

                allRecords.Add(new ReliabilityRecordModel
                {
                    SourceName = obj["SourceName"]?.ToString() ?? "",
                    Message = obj["Message"]?.ToString() ?? "",
                    TimeGenerated = time,
                    EventIdentifier = obj["EventIdentifier"]?.ToString() ?? "",
                    ProductName = obj["ProductName"]?.ToString() ?? "",
                    ComputerName = obj["ComputerName"]?.ToString() ?? "",
                    RecordNumber = obj["RecordNumber"]?.ToString() ?? ""
                });
            }

            results = allRecords.OrderByDescending(r => r.TimeGenerated).Take(maxEvents).ToList();
        }
        catch (Exception ex)
        {
             results.Add(new ReliabilityRecordModel
            {
                Message = $"Error retrieving reliability records: {ex.Message}. Note: This tool requires Admin privileges and the 'Win32_ReliabilityRecords' WMI class."
            });
        }

        return results;
    }

    public class ReliabilityRecordModel
    {
        public DateTime TimeGenerated { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string EventIdentifier { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ComputerName { get; set; } = string.Empty;
        public string RecordNumber { get; set; } = string.Empty;
    }
}
