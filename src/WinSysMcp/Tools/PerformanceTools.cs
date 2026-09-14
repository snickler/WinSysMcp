using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
#if WINDOWS_APIS
using System.Diagnostics;
#endif

namespace WinSysMcp.Tools;

[McpServerToolType]
public class PerformanceTools
{
    [McpServerTool(Name = "get_system_metrics"), Description("Returns real-time system metrics (CPU percentage, available memory MB, system up time seconds). Uses Performance Counters on Windows and /proc on Linux.")]
    public static SystemMetricsModel GetSystemMetrics()
    {
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return GetWindowsMetrics();
#endif
        return GetLinuxMetrics();
    }

#if WINDOWS_APIS
    private static SystemMetricsModel GetWindowsMetrics()
    {
        var metrics = new SystemMetricsModel();
        try
        {
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue();
            Thread.Sleep(100);
            metrics.CpuUsagePercent = Math.Round(cpuCounter.NextValue(), 2);

            using var ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            metrics.AvailableMemoryMB = ramCounter.NextValue();

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
#endif

    private static SystemMetricsModel GetLinuxMetrics()
    {
        var metrics = new SystemMetricsModel();
        try
        {
            metrics.CpuUsagePercent = SampleCpuPercent();
            metrics.AvailableMemoryMB = ReadAvailableMemoryMb();
            metrics.SystemUpTimeSeconds = ReadUptimeSeconds();
        }
        catch (Exception ex)
        {
            metrics.Error = $"Failed to retrieve metrics: {ex.Message}";
        }

        return metrics;
    }

    private static double SampleCpuPercent()
    {
        var (idle1, total1) = ReadCpuTimes();
        Thread.Sleep(100);
        var (idle2, total2) = ReadCpuTimes();
        var idleDelta = idle2 - idle1;
        var totalDelta = total2 - total1;
        if (totalDelta <= 0) return 0;
        var busy = 1.0 - (idleDelta / (double)totalDelta);
        return Math.Round(Math.Clamp(busy * 100.0, 0, 100), 2);
    }

    private static (long Idle, long Total) ReadCpuTimes()
    {
        var line = File.ReadLines("/proc/stat").First();
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // cpu user nice system idle iowait irq softirq steal ...
        long total = 0;
        for (var i = 1; i < parts.Length; i++)
        {
            if (long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                total += v;
        }

        long idle = 0;
        if (parts.Length > 4
            && long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var idleVal))
        {
            idle = idleVal;
            if (parts.Length > 5
                && long.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iowait))
            {
                idle += iowait;
            }
        }

        return (idle, total);
    }

    private static float ReadAvailableMemoryMb()
    {
        long? memAvailable = null;
        long? memFree = null;
        long? buffers = null;
        long? cached = null;

        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                memAvailable = ParseKb(line);
            else if (line.StartsWith("MemFree:", StringComparison.Ordinal))
                memFree = ParseKb(line);
            else if (line.StartsWith("Buffers:", StringComparison.Ordinal))
                buffers = ParseKb(line);
            else if (line.StartsWith("Cached:", StringComparison.Ordinal))
                cached = ParseKb(line);
        }

        var kb = memAvailable
            ?? ((memFree ?? 0) + (buffers ?? 0) + (cached ?? 0));
        return (float)Math.Round(kb / 1024.0, 2);
    }

    private static float ReadUptimeSeconds()
    {
        var text = File.ReadAllText("/proc/uptime").Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : 0;
    }

    private static long ParseKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb)
            ? kb
            : 0;
    }

    public class SystemMetricsModel
    {
        public double CpuUsagePercent { get; set; }
        public float AvailableMemoryMB { get; set; }
        public float SystemUpTimeSeconds { get; set; }
        public string? Error { get; set; }
    }
}
