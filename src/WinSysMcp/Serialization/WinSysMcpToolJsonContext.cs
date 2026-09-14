using System.Text.Json.Serialization;
using WinSysMcp.Tools;

namespace WinSysMcp.Serialization;

[JsonSerializable(typeof(FileTools.FileSystemEntryModel))]
[JsonSerializable(typeof(List<FileTools.FileSystemEntryModel>))]
[JsonSerializable(typeof(DiskTools.DriveInfoModel))]
[JsonSerializable(typeof(List<DiskTools.DriveInfoModel>))]
[JsonSerializable(typeof(EventLogTools.EventLogEntryModel))]
[JsonSerializable(typeof(List<EventLogTools.EventLogEntryModel>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(NetworkTools.NetworkInterfaceModel))]
[JsonSerializable(typeof(List<NetworkTools.NetworkInterfaceModel>))]
[JsonSerializable(typeof(NetworkTools.TcpConnectionModel))]
[JsonSerializable(typeof(List<NetworkTools.TcpConnectionModel>))]
[JsonSerializable(typeof(PerformanceTools.SystemMetricsModel))]
[JsonSerializable(typeof(ProcessTools.ProcessInfoModel))]
[JsonSerializable(typeof(List<ProcessTools.ProcessInfoModel>))]
[JsonSerializable(typeof(ServiceTools.ServiceInfoModel))]
[JsonSerializable(typeof(List<ServiceTools.ServiceInfoModel>))]
[JsonSerializable(typeof(SoftwareTools.InstalledProgramModel))]
[JsonSerializable(typeof(List<SoftwareTools.InstalledProgramModel>))]
[JsonSerializable(typeof(SystemTools.StartupAppModel))]
[JsonSerializable(typeof(List<SystemTools.StartupAppModel>))]
#if WINDOWS_APIS && !AOT_SAFE
[JsonSerializable(typeof(ReliabilityTools.ReliabilityRecordModel))]
[JsonSerializable(typeof(List<ReliabilityTools.ReliabilityRecordModel>))]
#endif
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
public partial class WinSysMcpToolJsonContext : JsonSerializerContext
{
}
