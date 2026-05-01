using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ServiceProcess;

namespace WinSysMcp.Tools;

[McpServerToolType]
public static class ServiceTools
{
    [McpServerTool(Name = "list_services")]
    public static List<ServiceInfoModel> ListServices(
        [System.ComponentModel.DescriptionAttribute("Filter by status (e.g., 'Running', 'Stopped'). Optional.")] string? status = null,
        [System.ComponentModel.DescriptionAttribute("Filter by service name (partial match). Optional.")] string? nameFilter = null)
    {
        var results = new List<ServiceInfoModel>();
        var services = ServiceController.GetServices();

        foreach (var service in services)
        {
            // Filter by status if provided
            if (!string.IsNullOrEmpty(status))
            {
                if (!service.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            // Filter by name if provided
            if (!string.IsNullOrEmpty(nameFilter))
            {
                if (!service.ServiceName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) &&
                    !service.DisplayName.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
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

    [McpServerTool(Name = "get_service_details")]
    public static ServiceInfoModel? GetServiceDetails(
        [System.ComponentModel.DescriptionAttribute("The exact name of the service.")] string serviceName)
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

    [McpServerTool(Name = "start_service")]
    public static string StartService(
        [System.ComponentModel.DescriptionAttribute("The name of the service to start.")] string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status == ServiceControllerStatus.Running)
            {
                return $"Service '{serviceName}' is already running.";
            }
            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            return $"Successfully started service '{serviceName}'.";
        }
        catch (Exception ex)
        {
            return $"Failed to start service '{serviceName}': {ex.Message}";
        }
    }

    [McpServerTool(Name = "stop_service")]
    public static string StopService(
        [System.ComponentModel.DescriptionAttribute("The name of the service to stop.")] string serviceName)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                return $"Service '{serviceName}' is already stopped.";
            }
            if (!service.CanStop)
            {
                return $"Service '{serviceName}' cannot be stopped.";
            }
            service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            return $"Successfully stopped service '{serviceName}'.";
        }
        catch (Exception ex)
        {
            return $"Failed to stop service '{serviceName}': {ex.Message}";
        }
    }

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
