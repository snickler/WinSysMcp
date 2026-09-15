using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
#if WINDOWS_APIS
using System.ServiceProcess;
#endif

namespace WinSysMcp.Tools;

[McpServerToolType]
public class ServiceTools
{
    [McpServerTool(Name = "list_services"), Description("Lists system services (Windows Service Controller or systemd units on Linux) with name, display name, and status. Optional filters: status and nameFilter. Read-only overview; may require elevated privileges. JSON input schema example: {\"type\":\"object\",\"properties\":{\"status\":{\"type\":\"string\"},\"nameFilter\":{\"type\":\"string\"}}}")]
    public static List<ServiceInfoModel> ListServices(
        [Description("Filter by status (e.g., 'Running', 'Stopped'). Optional.")] string? status = null,
        [Description("Filter by service name (partial match). Optional.")] string? nameFilter = null)
    {
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return ListWindowsServices(status, nameFilter);
#endif
        return ListSystemdServices(status, nameFilter);
    }

    [McpServerTool(Name = "get_service_details"), Description("Returns detailed data for a service (Windows or systemd unit). Parameter: serviceName. Read-only; may require elevation. Example: serviceName='wuauserv' or 'ssh.service'.")]
    public static ServiceInfoModel? GetServiceDetails(
        [Description("The exact name of the service.")] string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) return null;
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return GetWindowsServiceDetails(serviceName);
#endif
        return GetSystemdServiceDetails(serviceName);
    }

    [McpServerTool(Name = "start_service"), Description("Attempts to start a service (Windows SCM or systemctl start). Requires privilege. Example: serviceName='Spooler' or 'nginx.service'.")]
    public static string StartService(
        [Description("The name of the service to start.")] string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) return "Error: serviceName is required.";
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return StartWindowsService(serviceName);
#endif
        var result = OsProcess.Run("systemctl", $"start {Quote(serviceName)}");
        return result.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || result.StartsWith("Exception", StringComparison.OrdinalIgnoreCase)
            ? result
            : $"Successfully started service '{serviceName}'.";
    }

    [McpServerTool(Name = "stop_service"), Description("Attempts to stop a service (Windows SCM or systemctl stop). Requires privilege. Example: serviceName='Spooler' or 'nginx.service'.")]
    public static string StopService(
        [Description("The name of the service to stop.")] string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) return "Error: serviceName is required.";
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return StopWindowsService(serviceName);
#endif
        var result = OsProcess.Run("systemctl", $"stop {Quote(serviceName)}");
        return result.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || result.StartsWith("Exception", StringComparison.OrdinalIgnoreCase)
            ? result
            : $"Successfully stopped service '{serviceName}'.";
    }

#if WINDOWS_APIS
    private static List<ServiceInfoModel> ListWindowsServices(string? status, string? nameFilter)
    {
        var results = new List<ServiceInfoModel>();
        foreach (var service in ServiceController.GetServices())
        {
            if (!string.IsNullOrEmpty(status)
                && !service.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(nameFilter)
                && !service.ServiceName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)
                && !service.DisplayName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(new ServiceInfoModel
            {
                ServiceName = service.ServiceName,
                DisplayName = service.DisplayName,
                Status = service.Status.ToString(),
                ServiceType = service.ServiceType.ToString()
            });
        }

        return results.OrderBy(s => s.ServiceName).ToList();
    }

    private static ServiceInfoModel? GetWindowsServiceDetails(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            return new ServiceInfoModel
            {
                ServiceName = service.ServiceName,
                DisplayName = service.DisplayName,
                Status = service.Status.ToString(),
                ServiceType = service.ServiceType.ToString(),
                CanStop = service.CanStop,
                CanPauseAndContinue = service.CanPauseAndContinue,
                CanShutdown = service.CanShutdown
            };
        }
        catch
        {
            return null;
        }
    }

    private static string StartWindowsService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status == ServiceControllerStatus.Running)
                return $"Service '{serviceName}' is already running.";
            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            return $"Successfully started service '{serviceName}'.";
        }
        catch (Exception ex)
        {
            return $"Failed to start service '{serviceName}': {ex.Message}";
        }
    }

    private static string StopWindowsService(string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status == ServiceControllerStatus.Stopped)
                return $"Service '{serviceName}' is already stopped.";
            if (!service.CanStop)
                return $"Service '{serviceName}' cannot be stopped.";
            service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            return $"Successfully stopped service '{serviceName}'.";
        }
        catch (Exception ex)
        {
            return $"Failed to stop service '{serviceName}': {ex.Message}";
        }
    }
#endif

    private static List<ServiceInfoModel> ListSystemdServices(string? status, string? nameFilter)
    {
        var (exit, stdout, stderr) = OsProcess.RunRaw(
            "systemctl",
            "list-units --type=service --all --no-pager --no-legend --plain");
        if (exit != 0)
        {
            return
            [
                new ServiceInfoModel
                {
                    ServiceName = "error",
                    DisplayName = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr,
                    Status = "Error"
                }
            ];
        }

        var results = new List<ServiceInfoModel>();
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            var unit = parts[0];
            var active = parts[2];
            var sub = parts[3];
            var description = parts.Length > 4 ? string.Join(' ', parts.Skip(4)) : unit;
            var mappedStatus = MapSystemdStatus(active, sub);

            if (!string.IsNullOrEmpty(status)
                && !mappedStatus.Equals(status, StringComparison.OrdinalIgnoreCase)
                && !active.Equals(status, StringComparison.OrdinalIgnoreCase)
                && !sub.Equals(status, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(nameFilter)
                && !unit.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)
                && !description.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(new ServiceInfoModel
            {
                ServiceName = unit,
                DisplayName = description,
                Status = mappedStatus,
                ServiceType = "systemd"
            });
        }

        return results.OrderBy(s => s.ServiceName).ToList();
    }

    private static ServiceInfoModel? GetSystemdServiceDetails(string serviceName)
    {
        var unit = serviceName.EndsWith(".service", StringComparison.OrdinalIgnoreCase)
            ? serviceName
            : serviceName + ".service";
        var (exit, stdout, stderr) = OsProcess.RunRaw(
            "systemctl",
            $"show {Quote(unit)} --no-pager --property=Id,Description,ActiveState,SubState,CanStop,FragmentPath");
        if (exit != 0) return null;

        var map = ParseSystemdShow(stdout);
        if (map.Count == 0) return null;
        var active = map.GetValueOrDefault("ActiveState", "");
        var sub = map.GetValueOrDefault("SubState", "");
        return new ServiceInfoModel
        {
            ServiceName = map.GetValueOrDefault("Id", unit),
            DisplayName = map.GetValueOrDefault("Description", unit),
            Status = MapSystemdStatus(active, sub),
            ServiceType = "systemd",
            CanStop = string.Equals(map.GetValueOrDefault("CanStop"), "yes", StringComparison.OrdinalIgnoreCase),
            CanPauseAndContinue = false,
            CanShutdown = false
        };
    }

    private static Dictionary<string, string> ParseSystemdShow(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = line.IndexOf('=');
            if (idx <= 0) continue;
            map[line[..idx]] = line[(idx + 1)..];
        }
        return map;
    }

    private static string MapSystemdStatus(string active, string sub)
        => active.Equals("active", StringComparison.OrdinalIgnoreCase) && sub.Equals("running", StringComparison.OrdinalIgnoreCase)
            ? "Running"
            : active.Equals("inactive", StringComparison.OrdinalIgnoreCase)
                ? "Stopped"
                : $"{active}/{sub}";

    private static string Quote(string value)
        => value.Any(char.IsWhiteSpace) ? $"\"{value.Replace("\"", "\\\"")}\"" : value;

    public class ServiceInfoModel
    {
        public string ServiceName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public bool CanStop { get; set; }
        public bool CanPauseAndContinue { get; set; }
        public bool CanShutdown { get; set; }
    }
}
