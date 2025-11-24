using ModelContextProtocol.Server;
using System.ComponentModel;

namespace WinSysMcp.Tools;

[McpServerToolType]
public static class FileTools
{
    [McpServerTool(Name = "list_directory")]
    public static List<FileSystemEntryModel> ListDirectory(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the directory to list.")] string path)
    {
        var results = new List<FileSystemEntryModel>();

        try
        {
            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                throw new DirectoryNotFoundException($"Directory '{path}' not found.");
            }

            foreach (var dir in dirInfo.GetDirectories())
            {
                results.Add(new FileSystemEntryModel
                {
                    Name = dir.Name,
                    FullName = dir.FullName,
                    Type = "Directory",
                    LastWriteTime = dir.LastWriteTime
                });
            }

            foreach (var file in dirInfo.GetFiles())
            {
                results.Add(new FileSystemEntryModel
                {
                    Name = file.Name,
                    FullName = file.FullName,
                    Type = "File",
                    Size = file.Length,
                    LastWriteTime = file.LastWriteTime
                });
            }
        }
        catch (Exception ex)
        {
            results.Add(new FileSystemEntryModel
            {
                Name = "Error",
                Type = "Error",
                FullName = ex.Message
            });
        }

        return results;
    }

    [McpServerTool(Name = "read_file_tail")]
    public static List<string> ReadFileTail(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to read.")] string path,
        [System.ComponentModel.DescriptionAttribute("The number of lines to read from the end. Default is 20.")] int lineCount = 20)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new List<string> { $"Error: File '{path}' not found." };
            }

            // This is a simple implementation. For huge files, a reverse stream reader would be better.
            // But for typical logs, this is acceptable.
            return File.ReadLines(path).TakeLast(lineCount).ToList();
        }
        catch (Exception ex)
        {
            return new List<string> { $"Error reading file: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "read_file_head")]
    public static List<string> ReadFileHead(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to read.")] string path,
        [System.ComponentModel.DescriptionAttribute("The number of lines to read from the beginning. Default is 20.")] int lineCount = 20)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new List<string> { $"Error: File '{path}' not found." };
            }
            return File.ReadLines(path).Take(lineCount).ToList();
        }
        catch (Exception ex)
        {
            return new List<string> { $"Error reading file: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "get_file_info")]
    public static FileSystemEntryModel? GetFileInfo(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file.")] string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists) return null;

            return new FileSystemEntryModel
            {
                Name = file.Name,
                FullName = file.FullName,
                Type = "File",
                Size = file.Length,
                LastWriteTime = file.LastWriteTime
            };
        }
        catch
        {
            return null;
        }
    }

    [McpServerTool(Name = "copy_file")]
    public static string CopyFile(
        [System.ComponentModel.DescriptionAttribute("Source file path.")] string sourcePath,
        [System.ComponentModel.DescriptionAttribute("Destination file path.")] string destPath,
        [System.ComponentModel.DescriptionAttribute("Overwrite if exists. Default false.")] bool overwrite = false)
    {
        try
        {
            File.Copy(sourcePath, destPath, overwrite);
            return $"Successfully copied '{sourcePath}' to '{destPath}'.";
        }
        catch (Exception ex)
        {
            return $"Error copying file: {ex.Message}";
        }
    }

    [McpServerTool(Name = "move_file")]
    public static string MoveFile(
        [System.ComponentModel.DescriptionAttribute("Source file path.")] string sourcePath,
        [System.ComponentModel.DescriptionAttribute("Destination file path.")] string destPath)
    {
        try
        {
            File.Move(sourcePath, destPath);
            return $"Successfully moved '{sourcePath}' to '{destPath}'.";
        }
        catch (Exception ex)
        {
            return $"Error moving file: {ex.Message}";
        }
    }

    [McpServerTool(Name = "delete_file")]
    public static string DeleteFile(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to delete.")] string path)
    {
        try
        {
            if (!File.Exists(path)) return $"File '{path}' not found.";
            File.Delete(path);
            return $"Successfully deleted '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error deleting file: {ex.Message}";
        }
    }

    [McpServerTool(Name = "create_directory")]
    public static string CreateDirectory(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the directory to create.")] string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return $"Successfully created directory '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error creating directory: {ex.Message}";
        }
    }

    [McpServerTool(Name = "delete_directory")]
    public static string DeleteDirectory(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the directory to delete.")] string path,
        [System.ComponentModel.DescriptionAttribute("Recursive delete. Default false.")] bool recursive = false)
    {
        try
        {
            if (!Directory.Exists(path)) return $"Directory '{path}' not found.";
            Directory.Delete(path, recursive);
            return $"Successfully deleted directory '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error deleting directory: {ex.Message}";
        }
    }

    [McpServerTool(Name = "search_files")]
    public static List<string> SearchFiles(
        [System.ComponentModel.DescriptionAttribute("The directory to search in.")] string path,
        [System.ComponentModel.DescriptionAttribute("The search pattern (e.g. *.txt).")] string searchPattern,
        [System.ComponentModel.DescriptionAttribute("Search recursively. Default false.")] bool recursive = false)
    {
        try
        {
            if (!Directory.Exists(path)) return new List<string> { $"Directory '{path}' not found." };
            
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.GetFiles(path, searchPattern, option).Take(100).ToList();
        }
        catch (Exception ex)
        {
            return new List<string> { $"Error searching files: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "get_file_hash")]
    public static string GetFileHash(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file.")] string path)
    {
        try
        {
            if (!File.Exists(path)) return $"File '{path}' not found.";
            
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            return $"Error calculating hash: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_temp_path")]
    public static string GetTempPath()
    {
        return Path.GetTempPath();
    }

    public class FileSystemEntryModel
    {
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long? Size { get; set; }
        public DateTime? LastWriteTime { get; set; }
    }
}
