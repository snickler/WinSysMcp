using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Management;

namespace WinSysMcp.Tools;

[McpServerToolType]
public static class WmiTools
{
    [McpServerTool(Name = "get_bios_info")]
    [System.ComponentModel.Description("Retrieves BIOS information using WMI (Win32_BIOS).")]
    public static object GetBiosInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            using var collection = searcher.Get();
            
            foreach (ManagementObject obj in collection)
            {
                return new
                {
                    Manufacturer = obj["Manufacturer"]?.ToString(),
                    Name = obj["Name"]?.ToString(),
                    SerialNumber = obj["SerialNumber"]?.ToString(),
                    Version = obj["Version"]?.ToString(),
                    ReleaseDate = obj["ReleaseDate"]?.ToString()
                };
            }
            return "No BIOS info found.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_processor_info")]
    [System.ComponentModel.Description("Retrieves Processor information using WMI (Win32_Processor).")]
    public static List<object> GetProcessorInfo()
    {
        var results = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                results.Add(new
                {
                    Name = obj["Name"]?.ToString(),
                    Manufacturer = obj["Manufacturer"]?.ToString(),
                    MaxClockSpeed = obj["MaxClockSpeed"]?.ToString(),
                    NumberOfCores = obj["NumberOfCores"]?.ToString(),
                    NumberOfLogicalProcessors = obj["NumberOfLogicalProcessors"]?.ToString(),
                    ProcessorId = obj["ProcessorId"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            results.Add($"Error: {ex.Message}");
        }
        return results;
    }

    [McpServerTool(Name = "get_printer_info")]
    [System.ComponentModel.Description("Retrieves installed printers using WMI (Win32_Printer).")]
    public static List<object> GetPrinterInfo()
    {
        var results = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                results.Add(new
                {
                    Name = obj["Name"]?.ToString(),
                    DriverName = obj["DriverName"]?.ToString(),
                    PortName = obj["PortName"]?.ToString(),
                    HorizontalResolution = obj["HorizontalResolution"]?.ToString(),
                    VerticalResolution = obj["VerticalResolution"]?.ToString(),
                    Local = obj["Local"]?.ToString(),
                    Network = obj["Network"]?.ToString(),
                    Shared = obj["Shared"]?.ToString(),
                    ShareName = obj["ShareName"]?.ToString(),
                    Status = obj["Status"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            results.Add($"Error: {ex.Message}");
        }
        return results;
    }

    [McpServerTool(Name = "get_sound_devices")]
    [System.ComponentModel.Description("Retrieves sound devices using WMI (Win32_SoundDevice).")]
    public static List<object> GetSoundDevices()
    {
        var results = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SoundDevice");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                results.Add(new
                {
                    Name = obj["Name"]?.ToString(),
                    Manufacturer = obj["Manufacturer"]?.ToString(),
                    Status = obj["Status"]?.ToString(),
                    DeviceId = obj["DeviceID"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            results.Add($"Error: {ex.Message}");
        }
        return results;
    }

    [McpServerTool(Name = "get_video_controllers")]
    [System.ComponentModel.Description("Retrieves video controller (GPU) information using WMI (Win32_VideoController).")]
    public static List<object> GetVideoControllers()
    {
        var results = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                results.Add(new
                {
                    Name = obj["Name"]?.ToString(),
                    AdapterRAM = obj["AdapterRAM"]?.ToString(),
                    DriverVersion = obj["DriverVersion"]?.ToString(),
                    VideoProcessor = obj["VideoProcessor"]?.ToString(),
                    CurrentHorizontalResolution = obj["CurrentHorizontalResolution"]?.ToString(),
                    CurrentVerticalResolution = obj["CurrentVerticalResolution"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            results.Add($"Error: {ex.Message}");
        }
        return results;
    }

    [McpServerTool(Name = "get_startup_commands")]
    [System.ComponentModel.Description("Retrieves startup commands using WMI (Win32_StartupCommand).")]
    public static List<object> GetStartupCommands()
    {
        var results = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_StartupCommand");
            using var collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                results.Add(new
                {
                    Name = obj["Name"]?.ToString(),
                    Command = obj["Command"]?.ToString(),
                    Location = obj["Location"]?.ToString(),
                    User = obj["User"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            results.Add($"Error: {ex.Message}");
        }
        return results;
    }
}
