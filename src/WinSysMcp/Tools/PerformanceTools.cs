using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class PerformanceTools
{
    [McpServerTool(Name = "get_system_metrics"), Description("Returns real-time system metrics (CPU percentage, available memory MB, system up time seconds). No parameters. Uses Performance Counters which may require permissions and can take a short sample period; first measurement may be delayed briefly. Example: no parameters.")]
    public static SystemMetricsModel GetSystemMetrics()
    {
        var metrics = new SystemMetricsModel();

        try
        {
            // CPU Usage
            // Note: The first call to NextValue() often returns 0, so we might need to sleep briefly or accept it.
            // For a stateless tool, this is tricky. We'll try to get a value.
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue(); // First call always 0
            Thread.Sleep(100); // Brief pause to let counter accumulate
            metrics.CpuUsagePercent = Math.Round(cpuCounter.NextValue(), 2);

            // Available Memory
            using var ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            metrics.AvailableMemoryMB = ramCounter.NextValue();

            // System Up Time
            using var uptimeCounter = new PerformanceCounter("System", "System Up Time");
            uptimeCounter.NextValue();
            metrics.SystemUpTimeSeconds = uptimeCounter.NextValue();
        }
        catch (Exception ex)
        {
            metrics.Error = $"Failed to retrieve metrics: {ex.Message}";
        }

        return metrics;
    }

    public class SystemMetricsModel
    {
        public double CpuUsagePercent { get; set; }
        public float AvailableMemoryMB { get; set; }
        public float SystemUpTimeSeconds { get; set; }
        public string? Error { get; set; }
    }
}
