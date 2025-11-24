using ModelContextProtocol.Server;
using System.ComponentModel;

namespace WinSysMcp.Tools;

[McpServerToolType]
public static class DiskTools
{
    [McpServerTool(Name = "get_drives")]
    public static List<DriveInfoModel> GetDrives()
    {
        var results = new List<DriveInfoModel>();
        var drives = DriveInfo.GetDrives();

        foreach (var drive in drives)
        {
            try
            {
                if (drive.IsReady)
                {
                    results.Add(new DriveInfoModel
                    {
                        Name = drive.Name,
                        DriveType = drive.DriveType.ToString(),
                        VolumeLabel = drive.VolumeLabel,
                        DriveFormat = drive.DriveFormat,
                        TotalSizeGB = Math.Round(drive.TotalSize / 1024.0 / 1024.0 / 1024.0, 2),
                        AvailableFreeSpaceGB = Math.Round(drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 2),
                        IsReady = true
                    });
                }
                else
                {
                    results.Add(new DriveInfoModel
                    {
                        Name = drive.Name,
                        DriveType = drive.DriveType.ToString(),
                        IsReady = false
                    });
                }
            }
            catch (Exception ex)
            {
                results.Add(new DriveInfoModel
                {
                    Name = drive.Name,
                    IsReady = false,
                    Error = ex.Message
                });
            }
        }

        return results;
    }

    [McpServerTool(Name = "get_folder_size")]
    public static string GetFolderSize(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the folder.")] string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return $"Directory '{path}' not found.";
            }

            var dirInfo = new DirectoryInfo(path);
            long size = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
            
            double sizeMB = Math.Round(size / 1024.0 / 1024.0, 2);
            if (sizeMB > 1024)
            {
                return $"{Math.Round(sizeMB / 1024.0, 2)} GB";
            }
            return $"{sizeMB} MB";
        }
        catch (Exception ex)
        {
            return $"Error calculating size: {ex.Message}";
        }
    }

    public class DriveInfoModel
    {
        public string Name { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public string VolumeLabel { get; set; } = string.Empty;
        public string DriveFormat { get; set; } = string.Empty;
        public double TotalSizeGB { get; set; }
        public double AvailableFreeSpaceGB { get; set; }
        public bool IsReady { get; set; }
        public string? Error { get; set; }
    }
}
