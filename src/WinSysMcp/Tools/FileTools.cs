using ModelContextProtocol.Server;
using System.Buffers;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class FileTools
{
    private const int MaxReadFileLines = 10000;
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly UnicodeEncoding Utf16LeNoBom = new(bigEndian: false, byteOrderMark: false);
    private static readonly UnicodeEncoding Utf16BeNoBom = new(bigEndian: true, byteOrderMark: false);
    private static readonly UTF32Encoding Utf32LeNoBom = new(bigEndian: false, byteOrderMark: false);
    private static readonly UTF32Encoding Utf32BeNoBom = new(bigEndian: true, byteOrderMark: false);

    private static readonly string[] WriteDenylist =
    [
        @"C:\Windows\System32",
        @"C:\Windows\SysWOW64",
        @"C:\Windows\Boot",
        @"C:\Windows\servicing",
        @"C:\EFI"
    ];

    private static readonly string[] FullWriteDenylist = WriteDenylist.Select(Path.GetFullPath).ToArray();

    static FileTools()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [McpServerTool(Name = "list_directory"), Description("Lists files and subdirectories inside a directory. Parameters: path (absolute). Returns entries (name, type, size and timestamps). Read-only; access may be restricted by file permissions.")]
    public static List<FileSystemEntryModel> ListDirectory(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the directory to list.")] string path)
    {
        var results = new List<FileSystemEntryModel>();

        try
        {
            if (string.IsNullOrWhiteSpace(path)) return new List<FileSystemEntryModel> { new FileSystemEntryModel { Name = "Error", Type = "Error", FullName = "path parameter is required" } };
            if (!Path.IsPathRooted(path)) return new List<FileSystemEntryModel> { new FileSystemEntryModel { Name = "Error", Type = "Error", FullName = "path must be absolute" } };
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

    [McpServerTool(Name = "read_file_tail"), Description("Reads the last N lines of a text file. Parameters: path (absolute), lineCount (default 20), encoding (optional override). Returns newest lines. Large files are handled but may be slow; this is read-only. JSON input schema example: {\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"lineCount\":{\"type\":\"integer\"},\"encoding\":{\"type\":\"string\"}}}")]
    public static List<string> ReadFileTail(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to read.")] string path,
        [System.ComponentModel.DescriptionAttribute("The number of lines to read from the end. Default is 20.")] int lineCount = 20,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return new List<string> { "Error: path parameter is required." };
            if (!Path.IsPathRooted(path)) return new List<string> { "Error: path must be absolute." };
            if (lineCount < 1) return new List<string> { "Error: lineCount must be >= 1." };
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return new List<string> { encodingError! };
            if (!File.Exists(path))
            {
                return new List<string> { $"Error: File '{path}' not found." };
            }

            var queue = new Queue<string>(Math.Min(lineCount, 256));
            using var reader = OpenDetectedTextReader(path, encodingOverride);

            while (reader.ReadLine() is { } line)
            {
                if (queue.Count == lineCount)
                {
                    queue.Dequeue();
                }

                queue.Enqueue(line);
            }

            return queue.ToList();
        }
        catch (Exception ex)
        {
            return new List<string> { $"Error reading file: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "read_file_head"), Description("Reads the first N lines of a text file. Parameters: path (absolute), lineCount (default 20), encoding (optional override). Read-only; returns early with a friendly error message if file missing.")]
    public static List<string> ReadFileHead(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to read.")] string path,
        [System.ComponentModel.DescriptionAttribute("The number of lines to read from the beginning. Default is 20.")] int lineCount = 20,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return new List<string> { "Error: path parameter is required." };
            if (!Path.IsPathRooted(path)) return new List<string> { "Error: path must be absolute." };
            if (lineCount < 1) return new List<string> { "Error: lineCount must be >= 1." };
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return new List<string> { encodingError! };
            if (!File.Exists(path))
            {
                return new List<string> { $"Error: File '{path}' not found." };
            }

            var results = new List<string>(Math.Min(lineCount, 256));
            using var reader = OpenDetectedTextReader(path, encodingOverride);
            while (results.Count < lineCount && reader.ReadLine() is { } line)
            {
                results.Add(line);
            }

            return results;
        }
        catch (Exception ex)
        {
            return new List<string> { $"Error reading file: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "get_file_info"), Description("Returns metadata for a file (size, last write time, path). Parameter: path (absolute). Read-only and non-modifying.")]
    public static FileSystemEntryModel? GetFileInfo(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file.")] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!Path.IsPathRooted(path)) return null;
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

    [McpServerTool(Name = "copy_file"), Description("Copies a file from sourcePath to destPath. Parameters: sourcePath, destPath, overwrite (default false). Requires file system permissions; non-idempotent when overwrite=true. JSON input schema example: {\"type\":\"object\",\"properties\":{\"sourcePath\":{\"type\":\"string\"},\"destPath\":{\"type\":\"string\"},\"overwrite\":{\"type\":\"boolean\"}}}")]
    public static string CopyFile(
        [System.ComponentModel.DescriptionAttribute("Source file path.")] string sourcePath,
        [System.ComponentModel.DescriptionAttribute("Destination file path.")] string destPath,
        [System.ComponentModel.DescriptionAttribute("Overwrite if exists. Default false.")] bool overwrite = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destPath)) return "sourcePath and destPath are required.";
            if (!Path.IsPathRooted(sourcePath) || !Path.IsPathRooted(destPath)) return "sourcePath and destPath must be absolute.";
            if (!File.Exists(sourcePath)) return $"Source file '{sourcePath}' not found.";
            if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase)) return "Source and destination paths are the same.";
            File.Copy(sourcePath, destPath, overwrite);
            return $"Successfully copied '{sourcePath}' to '{destPath}'.";
        }
        catch (Exception ex)
        {
            return $"Error copying file: {ex.Message}";
        }
    }

    [McpServerTool(Name = "move_file"), Description("Moves a file from sourcePath to destPath. Parameters: sourcePath, destPath. Requires permissions; operation is destructive to original path and may fail across volumes.")]
    public static string MoveFile(
        [System.ComponentModel.DescriptionAttribute("Source file path.")] string sourcePath,
        [System.ComponentModel.DescriptionAttribute("Destination file path.")] string destPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destPath)) return "sourcePath and destPath are required.";
            if (!Path.IsPathRooted(sourcePath) || !Path.IsPathRooted(destPath)) return "sourcePath and destPath must be absolute.";
            if (!File.Exists(sourcePath)) return $"Source file '{sourcePath}' not found.";
            File.Move(sourcePath, destPath);
            return $"Successfully moved '{sourcePath}' to '{destPath}'.";
        }
        catch (Exception ex)
        {
            return $"Error moving file: {ex.Message}";
        }
    }

    [McpServerTool(Name = "delete_file"), Description("Deletes a file at the given path. Parameter: path. Destructive operation — requires privileges. Returns a success or error message.")]
    public static string DeleteFile(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to delete.")] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!File.Exists(path)) return $"File '{path}' not found.";
            File.Delete(path);
            return $"Successfully deleted '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error deleting file: {ex.Message}";
        }
    }

    [McpServerTool(Name = "create_directory"), Description("Creates a directory and any missing parents. Parameter: path. Non-destructive if existing; requires write permission to the parent folder.")]
    public static string CreateDirectory(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the directory to create.")] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            Directory.CreateDirectory(path);
            return $"Successfully created directory '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error creating directory: {ex.Message}";
        }
    }

    [McpServerTool(Name = "delete_directory"), Description("Deletes a directory. Parameters: path, recursive (default false). Destructive when recursive=true; use with caution and appropriate permissions. JSON input schema example: {\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"recursive\":{\"type\":\"boolean\"}}}")]
    public static string DeleteDirectory(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the directory to delete.")] string path,
        [System.ComponentModel.DescriptionAttribute("Recursive delete. Default false.")] bool recursive = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path required.";
            if (IsRootPath(path)) return "Refusing to delete a root path.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!Directory.Exists(path)) return $"Directory '{path}' not found.";
            Directory.Delete(path, recursive);
            return $"Successfully deleted directory '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error deleting directory: {ex.Message}";
        }
    }

    [McpServerTool(Name = "search_files"), Description("Searches for files matching searchPattern (e.g. '*.log') inside path. Parameters: path, searchPattern, recursive=false. Returns up to first 100 matches. Read-only.")]
    public static List<string> SearchFiles(
        [System.ComponentModel.DescriptionAttribute("The directory to search in.")] string path,
        [System.ComponentModel.DescriptionAttribute("The search pattern (e.g. *.txt).")] string searchPattern,
        [System.ComponentModel.DescriptionAttribute("Search recursively. Default false.")] bool recursive = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(searchPattern)) return new List<string> { "Error: path and searchPattern are required." };
            if (!Path.IsPathRooted(path)) return new List<string> { "Error: path must be absolute." };
            if (!Directory.Exists(path)) return new List<string> { $"Directory '{path}' not found." };
            
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.GetFiles(path, searchPattern, option).Take(100).ToList();
        }
        catch (Exception ex)
        {
            return new List<string> { $"Error searching files: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "get_file_hash"), Description("Computes SHA256 hash of the file at path. Parameter: path. Read-only. Large files processed as streams — may be slow for very large files. JSON input schema example: {\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"}}}")]
    public static string GetFileHash(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file.")] string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
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

    [McpServerTool(Name = "get_temp_path"), Description("Returns the current process's temporary directory path. Read-only diagnostic value; may be useful for creating temp files or debugging.")]
    public static string GetTempPath()
    {
        return Path.GetTempPath();
    }

    [McpServerTool(Name = "read_file_lines"), Description("Reads a range of lines from a text file. Parameters: path (absolute), startLine (1-based, default 1), endLine (inclusive, default -1 for end of file), encoding (optional override). Returns up to 10,000 lines. Read-only.")]
    public static List<string> ReadFileLines(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to read.")] string path,
        [System.ComponentModel.DescriptionAttribute("The 1-based start line to read. Default is 1.")] int startLine = 1,
        [System.ComponentModel.DescriptionAttribute("The 1-based inclusive end line to read. Use -1 for end of file.")] int endLine = -1,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return new List<string> { "Error: path parameter is required." };
            if (!Path.IsPathRooted(path)) return new List<string> { "Error: path must be absolute." };
            if (!File.Exists(path)) return new List<string> { $"Error: File '{path}' not found." };
            if (startLine < 1) return new List<string> { "Error: startLine must be >= 1." };
            if (endLine != -1 && endLine < startLine) return new List<string> { "Error: endLine must be -1 or >= startLine." };
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return new List<string> { encodingError! };

            var maxResultCount = endLine == -1
                ? MaxReadFileLines
                : Math.Min(MaxReadFileLines, endLine - startLine + 1);

            var results = new List<string>(Math.Min(maxResultCount, 256));
            using var reader = OpenDetectedTextReader(path, encodingOverride);

            var currentLine = 0;
            while (results.Count < maxResultCount)
            {
                var line = reader.ReadLine();
                if (line is null) break;

                currentLine++;
                if (currentLine < startLine) continue;
                if (endLine != -1 && currentLine > endLine) break;

                results.Add(line);
            }

            return results;
        }
        catch (Exception ex)
        {
            return new List<string> { $"Error reading file lines: {ex.Message}" };
        }
    }

    [McpServerTool(Name = "write_file"), Description("Writes text content to a file. Parameters: path (absolute), content, overwrite (default true), encoding name or code page (for example utf-8, utf-8-bom, utf-16, utf-16be, windows-1252, shift_jis). Creates or overwrites the target file. Write access to protected system locations is blocked.")]
    public static string WriteFile(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to write.")] string path,
        [System.ComponentModel.DescriptionAttribute("The content to write.")] string content,
        [System.ComponentModel.DescriptionAttribute("Whether to overwrite an existing file. Default true.")] bool overwrite = true,
        [System.ComponentModel.DescriptionAttribute("Text encoding name or code page. Examples: utf-8, utf-8-bom, utf-16, utf-16be, windows-1252, shift_jis. Default utf-8.")] string encoding = "utf-8")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path parameter is required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!IsSafeToWrite(path)) return $"Error: Writing to protected system path is blocked: '{path}'.";

            var fullPath = Path.GetFullPath(path);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            if (!overwrite && File.Exists(fullPath))
            {
                return $"Error: File '{fullPath}' already exists and overwrite=false.";
            }

            var selectedEncoding = GetEncodingFromName(encoding);
            if (selectedEncoding is null) return $"Error: encoding '{encoding}' is not supported on this system.";

            var safeContent = content ?? string.Empty;
            File.WriteAllText(fullPath, safeContent, selectedEncoding);
            var byteCount = selectedEncoding.GetByteCount(safeContent);
            return $"Success: wrote {byteCount} bytes to '{fullPath}'.";
        }
        catch (Exception ex)
        {
            return $"Error writing file: {ex.Message}";
        }
    }

    [McpServerTool(Name = "insert_at_line"), Description("Inserts content at a line position in a text file. Parameters: path (absolute), lineNumber (1-based insertion point), content, encoding (optional override). lineNumber=1 prepends; values beyond end append.")]
    public static string InsertAtLine(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to edit.")] string path,
        [System.ComponentModel.DescriptionAttribute("The 1-based line number where content will be inserted.")] int lineNumber,
        [System.ComponentModel.DescriptionAttribute("Content to insert. Multi-line content is supported.")] string content,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path parameter is required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!IsSafeToWrite(path)) return $"Error: Writing to protected system path is blocked: '{path}'.";
            if (lineNumber < 1) return "Error: lineNumber must be >= 1.";
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return encodingError!;
            if (!File.Exists(path)) return $"Error: File '{path}' not found.";

            var fileState = ReadExistingFileState(path, encodingOverride);
            var insertLines = SplitContentIntoLines(content);
            var insertIndex = Math.Min(lineNumber - 1, fileState.Lines.Count);
            fileState.Lines.InsertRange(insertIndex, insertLines);
            WriteExistingFileState(path, fileState);

            return $"Success: inserted {insertLines.Count} line(s) at line {lineNumber} in '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error inserting lines: {ex.Message}";
        }
    }

    [McpServerTool(Name = "insert_at_line_guarded"), Description("Inserts content at a line position only when the surrounding line anchors match the current file. Parameters: path (absolute), lineNumber (1-based insertion point), content, expectedBeforeLine (optional), expectedAfterLine (optional), encoding (optional override). At least one expected anchor must be provided. This is safer for LLM-driven edits because it refuses insertions when nearby lines drift.")]
    public static string InsertAtLineGuarded(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to edit.")] string path,
        [System.ComponentModel.DescriptionAttribute("The 1-based line number where content will be inserted.")] int lineNumber,
        [System.ComponentModel.DescriptionAttribute("Content to insert. Multi-line content is supported.")] string content,
        [System.ComponentModel.DescriptionAttribute("The exact line expected immediately before the insertion point. Optional, but at least one anchor is required.")] string? expectedBeforeLine = null,
        [System.ComponentModel.DescriptionAttribute("The exact line expected immediately after the insertion point. Optional, but at least one anchor is required.")] string? expectedAfterLine = null,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path parameter is required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!IsSafeToWrite(path)) return $"Error: Writing to protected system path is blocked: '{path}'.";
            if (lineNumber < 1) return "Error: lineNumber must be >= 1.";
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return encodingError!;
            if (expectedBeforeLine is null && expectedAfterLine is null)
            {
                return "Error: expectedBeforeLine and/or expectedAfterLine must be provided for guarded insert.";
            }

            if (!File.Exists(path)) return $"Error: File '{path}' not found.";

            var fileState = ReadExistingFileState(path, encodingOverride);
            if (lineNumber > fileState.Lines.Count + 1)
            {
                return $"Error: lineNumber {lineNumber} is beyond the valid guarded insertion range ({fileState.Lines.Count + 1}).";
            }

            var insertIndex = lineNumber - 1;
            if (!InsertAnchorsMatch(fileState.Lines, insertIndex, expectedBeforeLine, expectedAfterLine, out var mismatchReason))
            {
                return $"Error: {mismatchReason}. No changes were made.";
            }

            var insertLines = SplitContentIntoLines(content);
            fileState.Lines.InsertRange(insertIndex, insertLines);
            WriteExistingFileState(path, fileState);

            return $"Success: inserted {insertLines.Count} guarded line(s) at line {lineNumber} in '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error inserting guarded lines: {ex.Message}";
        }
    }

    [McpServerTool(Name = "replace_lines"), Description("Replaces an inclusive line range in a text file. Parameters: path (absolute), startLine, endLine, newContent, encoding (optional override). Line numbers are 1-based and inclusive.")]
    public static string ReplaceLines(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to edit.")] string path,
        [System.ComponentModel.DescriptionAttribute("The 1-based start line (inclusive).") ] int startLine,
        [System.ComponentModel.DescriptionAttribute("The 1-based end line (inclusive).") ] int endLine,
        [System.ComponentModel.DescriptionAttribute("Replacement content. Multi-line content is supported.")] string newContent,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path parameter is required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!IsSafeToWrite(path)) return $"Error: Writing to protected system path is blocked: '{path}'.";
            if (!File.Exists(path)) return $"Error: File '{path}' not found.";
            if (startLine < 1 || endLine < startLine) return "Error: startLine/endLine must define a valid inclusive range.";
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return encodingError!;

            var fileState = ReadExistingFileState(path, encodingOverride);
            if (startLine > fileState.Lines.Count)
            {
                return $"Error: startLine {startLine} is beyond end of file ({fileState.Lines.Count} lines).";
            }

            var replacementLines = SplitContentIntoLines(newContent);

            var effectiveEnd = Math.Min(endLine, fileState.Lines.Count);
            fileState.Lines.RemoveRange(startLine - 1, effectiveEnd - startLine + 1);
            fileState.Lines.InsertRange(startLine - 1, replacementLines);
            WriteExistingFileState(path, fileState);

            return $"Success: replaced lines {startLine}-{effectiveEnd} with {replacementLines.Count} line(s) in '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error replacing lines: {ex.Message}";
        }
    }

    [McpServerTool(Name = "delete_lines"), Description("Deletes an inclusive line range in a text file. Parameters: path (absolute), startLine, endLine, encoding (optional override). Line numbers are 1-based and inclusive.")]
    public static string DeleteLines(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to edit.")] string path,
        [System.ComponentModel.DescriptionAttribute("The 1-based start line (inclusive).") ] int startLine,
        [System.ComponentModel.DescriptionAttribute("The 1-based end line (inclusive).") ] int endLine,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path parameter is required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!IsSafeToWrite(path)) return $"Error: Writing to protected system path is blocked: '{path}'.";
            if (!File.Exists(path)) return $"Error: File '{path}' not found.";
            if (startLine < 1 || endLine < startLine) return "Error: startLine/endLine must define a valid inclusive range.";
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return encodingError!;

            var fileState = ReadExistingFileState(path, encodingOverride);
            if (startLine > fileState.Lines.Count)
            {
                return $"Error: startLine {startLine} is beyond end of file ({fileState.Lines.Count} lines).";
            }

            var effectiveEnd = Math.Min(endLine, fileState.Lines.Count);
            fileState.Lines.RemoveRange(startLine - 1, effectiveEnd - startLine + 1);
            WriteExistingFileState(path, fileState);

            return $"Success: deleted lines {startLine}-{effectiveEnd} from '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error deleting lines: {ex.Message}";
        }
    }

    [McpServerTool(Name = "replace_lines_guarded"), Description("Replaces an inclusive line range only when the current file content exactly matches the expected content for that range. Parameters: path (absolute), startLine, endLine, expectedContent, newContent, encoding (optional override). This is safer for LLM-driven edits because it refuses stale or drifted line-number edits.")]
    public static string ReplaceLinesGuarded(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to edit.")] string path,
        [System.ComponentModel.DescriptionAttribute("The 1-based start line (inclusive).") ] int startLine,
        [System.ComponentModel.DescriptionAttribute("The 1-based end line (inclusive).") ] int endLine,
        [System.ComponentModel.DescriptionAttribute("The exact content currently expected in the target line range.")] string expectedContent,
        [System.ComponentModel.DescriptionAttribute("Replacement content. Multi-line content is supported.")] string newContent,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        return ReplaceLinesGuardedCore(path, startLine, endLine, expectedContent, newContent, deleteLines: false, encoding);
    }

    [McpServerTool(Name = "delete_lines_guarded"), Description("Deletes an inclusive line range only when the current file content exactly matches the expected content for that range. Parameters: path (absolute), startLine, endLine, expectedContent, encoding (optional override). This is safer for cleanup of stale or duplicated edits because it refuses mismatched ranges.")]
    public static string DeleteLinesGuarded(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to edit.")] string path,
        [System.ComponentModel.DescriptionAttribute("The 1-based start line (inclusive).") ] int startLine,
        [System.ComponentModel.DescriptionAttribute("The 1-based end line (inclusive).") ] int endLine,
        [System.ComponentModel.DescriptionAttribute("The exact content currently expected in the target line range.")] string expectedContent,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        return ReplaceLinesGuardedCore(path, startLine, endLine, expectedContent, string.Empty, deleteLines: true, encoding);
    }

    [McpServerTool(Name = "replace_text"), Description("Replaces text in a file. Parameters: path (absolute), oldText, newText, replaceAll (default true), encoding (optional override). Returns replacement count. Write access to protected system locations is blocked.")]
    public static string ReplaceText(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to edit.")] string path,
        [System.ComponentModel.DescriptionAttribute("The text to find.")] string oldText,
        [System.ComponentModel.DescriptionAttribute("The replacement text.")] string newText,
        [System.ComponentModel.DescriptionAttribute("Replace all occurrences when true; otherwise only first occurrence. Default true.")] bool replaceAll = true,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path parameter is required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!IsSafeToWrite(path)) return $"Error: Writing to protected system path is blocked: '{path}'.";
            if (!File.Exists(path)) return $"Error: File '{path}' not found.";
            if (string.IsNullOrEmpty(oldText)) return "Error: oldText must not be empty.";
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return encodingError!;

            var fileTextState = ReadFileTextState(path, encodingOverride);
            var content = fileTextState.Content;
            int replacements;
            string updated;
            var safeNewText = newText ?? string.Empty;

            if (replaceAll)
            {
                replacements = CountOccurrences(content, oldText);
                if (replacements == 0) return $"No occurrences found for '{oldText}'.";
                updated = ReplaceAllOrdinalPooled(content, oldText, safeNewText, replacements);
            }
            else
            {
                var index = content.IndexOf(oldText, StringComparison.Ordinal);
                if (index < 0) return $"No occurrences found for '{oldText}'.";

                updated = ReplaceSingleOrdinalPooled(content, oldText, safeNewText, index);
                replacements = 1;
            }

            WriteTextWithEncoding(path, fileTextState.Encoding, updated);
            return $"Success: replaced {replacements} occurrence(s) in '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error replacing text: {ex.Message}";
        }
    }

    [McpServerTool(Name = "replace_exact_chunk"), Description("Replaces an exact text chunk in a file and fails if the number of matches is not exactly what you expect. Parameters: path (absolute), oldText, newText, expectedOccurrences (default 1), encoding (optional override). Safer for LLM-driven edits than line-number edits because it avoids editing the wrong location when line numbers drift.")]
    public static string ReplaceExactChunk(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to edit.")] string path,
        [System.ComponentModel.DescriptionAttribute("The exact text chunk to replace.")] string oldText,
        [System.ComponentModel.DescriptionAttribute("The replacement text chunk.")] string newText,
        [System.ComponentModel.DescriptionAttribute("How many matches must exist for the edit to proceed. Default 1.")] int expectedOccurrences = 1,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        return ReplaceExactChunkCore(path, oldText, newText, expectedOccurrences, "replaced", encoding);
    }

    [McpServerTool(Name = "delete_exact_chunk"), Description("Deletes an exact text chunk from a file and fails if the number of matches is not exactly what you expect. Parameters: path (absolute), text, expectedOccurrences (default 1), encoding (optional override). Useful when a line-based edit created duplicates and you want a safe cleanup.")]
    public static string DeleteExactChunk(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to edit.")] string path,
        [System.ComponentModel.DescriptionAttribute("The exact text chunk to delete.")] string text,
        [System.ComponentModel.DescriptionAttribute("How many matches must exist for the delete to proceed. Default 1.")] int expectedOccurrences = 1,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        return ReplaceExactChunkCore(path, text, string.Empty, expectedOccurrences, "deleted", encoding);
    }

    [McpServerTool(Name = "apply_diff"), Description("Applies edits from a diff payload to a text file. Parameters: path (absolute), diff, encoding (optional override). Supports unified diff hunks (@@) and JSON operation arrays. Write access to protected system locations is blocked.")]
    public static string ApplyDiff(
        [System.ComponentModel.DescriptionAttribute("The absolute path of the file to edit.")] string path,
        [System.ComponentModel.DescriptionAttribute("Unified diff text or JSON operations array.")] string diff,
        [System.ComponentModel.DescriptionAttribute("Optional encoding name or code page override, e.g. utf-8, utf-16be, windows-1252, shift_jis.")] string? encoding = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path parameter is required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!IsSafeToWrite(path)) return $"Error: Writing to protected system path is blocked: '{path}'.";
            if (!File.Exists(path)) return $"Error: File '{path}' not found.";
            if (string.IsNullOrWhiteSpace(diff)) return "Error: diff parameter is required.";
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return encodingError!;

            var trimmed = diff.AsSpan().TrimStart();
            if (trimmed.StartsWith("[".AsSpan(), StringComparison.Ordinal))
            {
                var operationCount = ApplyJsonDiffOperations(path, diff, encodingOverride);
                return $"Success: applied {operationCount} operation(s) to '{path}'.";
            }

            if (trimmed.IndexOf("@@".AsSpan(), StringComparison.Ordinal) >= 0)
            {
                var hunkCount = ApplyUnifiedDiff(path, diff, encodingOverride);
                return $"Success: applied {hunkCount} hunk(s) to '{path}'.";
            }

            return "Error: diff format not recognized. Provide unified diff hunks (@@) or a JSON operations array.";
        }
        catch (Exception ex)
        {
            return $"Error applying diff: {ex.Message}";
        }
    }

    private static int ApplyJsonDiffOperations(string path, string diffJson, Encoding? encodingOverride)
    {
        var operations = ParseDiffOperations(diffJson);
        if (operations is null || operations.Count == 0) throw new InvalidOperationException("No diff operations were provided.");

        var fileState = ReadExistingFileState(path, encodingOverride);
        var lines = fileState.Lines;

        operations.Sort(static (a, b) => GetOperationSortKey(b).CompareTo(GetOperationSortKey(a)));

        foreach (var operation in operations)
        {
            if (string.IsNullOrWhiteSpace(operation.Operation)) throw new InvalidOperationException("Diff operation is missing 'operation'.");
            var kind = operation.Operation.Trim().ToLowerInvariant();

            switch (kind)
            {
                case "insert":
                    {
                        var afterLine = operation.AfterLine.GetValueOrDefault(lines.Count);
                        if (afterLine < 0) throw new InvalidOperationException("insert operation requires afterLine >= 0.");
                        var index = Math.Clamp(afterLine, 0, lines.Count);
                        var insertLines = SplitContentIntoLines(operation.Content);
                        lines.InsertRange(index, insertLines);
                        break;
                    }
                case "replace":
                    {
                        var start = operation.StartLine ?? throw new InvalidOperationException("replace operation requires startLine.");
                        var end = operation.EndLine ?? start;
                        if (start < 1 || end < start) throw new InvalidOperationException("replace operation has invalid startLine/endLine.");
                        if (start > lines.Count) throw new InvalidOperationException($"replace operation startLine {start} is beyond file length {lines.Count}.");

                        var effectiveEnd = Math.Min(end, lines.Count);
                        var index = start - 1;
                        var removeCount = effectiveEnd - start + 1;
                        var replacementLines = SplitContentIntoLines(operation.Content);

                        lines.RemoveRange(index, removeCount);
                        lines.InsertRange(index, replacementLines);
                        break;
                    }
                case "delete":
                    {
                        var start = operation.StartLine ?? throw new InvalidOperationException("delete operation requires startLine.");
                        var end = operation.EndLine ?? start;
                        if (start < 1 || end < start) throw new InvalidOperationException("delete operation has invalid startLine/endLine.");
                        if (start > lines.Count) throw new InvalidOperationException($"delete operation startLine {start} is beyond file length {lines.Count}.");

                        var effectiveEnd = Math.Min(end, lines.Count);
                        var index = start - 1;
                        var removeCount = effectiveEnd - start + 1;
                        lines.RemoveRange(index, removeCount);
                        break;
                    }
                default:
                    throw new InvalidOperationException($"Unsupported operation '{operation.Operation}'. Supported: insert, replace, delete.");
            }
        }

        WriteExistingFileState(path, fileState);
        return operations.Count;
    }

    private static string ReplaceExactChunkCore(string path, string oldText, string newText, int expectedOccurrences, string operationVerb, string? encoding)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path parameter is required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!IsSafeToWrite(path)) return $"Error: Writing to protected system path is blocked: '{path}'.";
            if (!File.Exists(path)) return $"Error: File '{path}' not found.";
            if (string.IsNullOrEmpty(oldText)) return "Error: target text must not be empty.";
            if (expectedOccurrences < 1) return "Error: expectedOccurrences must be >= 1.";
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return encodingError!;

            var fileTextState = ReadFileTextState(path, encodingOverride);
            var content = fileTextState.Content;
            var occurrences = CountOccurrences(content, oldText);
            if (occurrences != expectedOccurrences)
            {
                return $"Error: expected {expectedOccurrences} occurrence(s) of the target text, but found {occurrences}. No changes were made.";
            }

            var safeNewText = newText ?? string.Empty;
            string updated;

            if (occurrences == 1)
            {
                var matchIndex = content.IndexOf(oldText, StringComparison.Ordinal);
                updated = ReplaceSingleOrdinalPooled(content, oldText, safeNewText, matchIndex);
            }
            else
            {
                updated = ReplaceAllOrdinalPooled(content, oldText, safeNewText, occurrences);
            }

            WriteTextWithEncoding(path, fileTextState.Encoding, updated);
            return $"Success: {operationVerb} {occurrences} exact chunk occurrence(s) in '{path}'.";
        }
        catch (Exception ex)
        {
            return $"Error performing exact chunk edit: {ex.Message}";
        }
    }

    private static List<DiffOperation> ParseDiffOperations(string diffJson)
    {
        if (string.IsNullOrWhiteSpace(diffJson))
        {
            throw new InvalidOperationException("Diff JSON payload is empty.");
        }

        var maxByteCount = Encoding.UTF8.GetMaxByteCount(diffJson.Length);
        var rentedBytes = ArrayPool<byte>.Shared.Rent(maxByteCount);

        try
        {
            var byteCount = Encoding.UTF8.GetBytes(diffJson.AsSpan(), rentedBytes.AsSpan());
            var reader = new Utf8JsonReader(rentedBytes.AsSpan(0, byteCount), isFinalBlock: true, state: default);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
            {
                throw new InvalidOperationException("Diff JSON must be an array of operations.");
            }

            var operations = new List<DiffOperation>(8);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new InvalidOperationException("Each diff operation must be a JSON object.");
                }

                var operation = default(DiffOperation);

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        operations.Add(operation);
                        break;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new InvalidOperationException("Expected a property name in diff operation object.");
                    }

                    var propertyName = reader.ValueTextEquals("operation"u8) ? DiffPropertyName.Operation
                        : reader.ValueTextEquals("startLine"u8) ? DiffPropertyName.StartLine
                        : reader.ValueTextEquals("endLine"u8) ? DiffPropertyName.EndLine
                        : reader.ValueTextEquals("afterLine"u8) ? DiffPropertyName.AfterLine
                        : reader.ValueTextEquals("content"u8) ? DiffPropertyName.Content
                        : DiffPropertyName.Unknown;

                    if (!reader.Read())
                    {
                        throw new InvalidOperationException("Unexpected end of JSON while reading diff operation property value.");
                    }

                    switch (propertyName)
                    {
                        case DiffPropertyName.Operation:
                            operation.Operation = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                            break;
                        case DiffPropertyName.StartLine:
                            operation.StartLine = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32();
                            break;
                        case DiffPropertyName.EndLine:
                            operation.EndLine = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32();
                            break;
                        case DiffPropertyName.AfterLine:
                            operation.AfterLine = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32();
                            break;
                        case DiffPropertyName.Content:
                            operation.Content = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            return operations;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBytes);
        }
    }

    private static int ApplyUnifiedDiff(string path, string diffText, Encoding? encodingOverride)
    {
        var fileState = ReadExistingFileState(path, encodingOverride);
        var originalLines = fileState.Lines;
        var resultLines = new List<string>();

        var originalIndex = 0;
        var hunkCount = 0;
        var pendingHeaderStart = -1;
        var pendingHeaderLength = 0;
        var cursor = 0;

        while (true)
        {
            if (!TryReadLine(diffText, ref cursor, pendingHeaderStart, pendingHeaderLength, out var line, out pendingHeaderStart, out pendingHeaderLength))
            {
                break;
            }

            if (line.StartsWith("diff ".AsSpan(), StringComparison.Ordinal) ||
                line.StartsWith("index ".AsSpan(), StringComparison.Ordinal) ||
                line.StartsWith("--- ".AsSpan(), StringComparison.Ordinal) ||
                line.StartsWith("+++ ".AsSpan(), StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryParseUnifiedDiffHunkHeader(line.AsSpan(), out var oldStart))
            {
                continue;
            }

            hunkCount++;

            while (originalIndex < oldStart - 1 && originalIndex < originalLines.Count)
            {
                resultLines.Add(originalLines[originalIndex]);
                originalIndex++;
            }

            while (true)
            {
                if (!TryReadLine(diffText, ref cursor, -1, 0, out var hunkLine, out var _, out var _))
                {
                    break;
                }

                if (TryParseUnifiedDiffHunkHeader(hunkLine.AsSpan(), out _))
                {
                    pendingHeaderStart = hunkLine.Start;
                    pendingHeaderLength = hunkLine.Length;
                    break;
                }

                if (hunkLine.StartsWith("\\ No newline at end of file".AsSpan(), StringComparison.Ordinal))
                {
                    continue;
                }

                if (hunkLine.Length == 0)
                {
                    // Empty line in diff body is interpreted as context line with empty content.
                    if (originalIndex >= originalLines.Count)
                        throw new InvalidOperationException("Unified diff context line exceeds original file length.");
                    resultLines.Add(originalLines[originalIndex]);
                    originalIndex++;
                    continue;
                }

                var prefix = hunkLine[0];
                var payload = hunkLine.Length > 1 ? hunkLine.Slice(1) : ReadOnlySpan<char>.Empty;

                switch (prefix)
                {
                    case ' ':
                        if (originalIndex >= originalLines.Count)
                            throw new InvalidOperationException("Unified diff context line exceeds original file length.");
                        resultLines.Add(originalLines[originalIndex]);
                        originalIndex++;
                        break;
                    case '-':
                        if (originalIndex >= originalLines.Count)
                            throw new InvalidOperationException("Unified diff delete line exceeds original file length.");
                        originalIndex++;
                        break;
                    case '+':
                        resultLines.Add(payload.ToString());
                        break;
                    default:
                        throw new InvalidOperationException($"Invalid unified diff hunk line: '{hunkLine.ToString()}'.");
                }
            }
        }

        while (originalIndex < originalLines.Count)
        {
            resultLines.Add(originalLines[originalIndex]);
            originalIndex++;
        }

        if (hunkCount == 0)
        {
            throw new InvalidOperationException("No unified diff hunks found.");
        }

        fileState.Lines.Clear();
        fileState.Lines.AddRange(resultLines);
        WriteExistingFileState(path, fileState);
        return hunkCount;
    }

    private static string ReplaceLinesGuardedCore(string path, int startLine, int endLine, string expectedContent, string newContent, bool deleteLines, string? encoding)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "Error: path parameter is required.";
            if (!Path.IsPathRooted(path)) return "Error: path must be absolute.";
            if (!IsSafeToWrite(path)) return $"Error: Writing to protected system path is blocked: '{path}'.";
            if (!File.Exists(path)) return $"Error: File '{path}' not found.";
            if (startLine < 1 || endLine < startLine) return "Error: startLine/endLine must define a valid inclusive range.";
            if (TryValidateEncodingOverride(encoding, out var encodingOverride, out var encodingError)) return encodingError!;

            var fileState = ReadExistingFileState(path, encodingOverride);
            if (startLine > fileState.Lines.Count)
            {
                return $"Error: startLine {startLine} is beyond end of file ({fileState.Lines.Count} lines).";
            }

            if (endLine > fileState.Lines.Count)
            {
                return $"Error: endLine {endLine} is beyond end of file ({fileState.Lines.Count} lines).";
            }

            var startIndex = startLine - 1;
            var rangeLength = endLine - startLine + 1;
            var expectedLines = SplitContentIntoLines(expectedContent);
            if (!LineRangeMatches(fileState.Lines, startIndex, rangeLength, expectedLines))
            {
                return $"Error: expected content did not match lines {startLine}-{endLine}. No changes were made.";
            }

            fileState.Lines.RemoveRange(startIndex, rangeLength);
            if (!deleteLines)
            {
                fileState.Lines.InsertRange(startIndex, SplitContentIntoLines(newContent));
            }

            WriteExistingFileState(path, fileState);

            return deleteLines
                ? $"Success: deleted guarded lines {startLine}-{endLine} from '{path}'."
                : $"Success: replaced guarded lines {startLine}-{endLine} in '{path}'.";
        }
        catch (Exception ex)
        {
            return deleteLines
                ? $"Error deleting guarded lines: {ex.Message}"
                : $"Error replacing guarded lines: {ex.Message}";
        }
    }

    private static int GetOperationSortKey(DiffOperation operation)
    {
        return operation.StartLine
            ?? operation.AfterLine
            ?? int.MaxValue;
    }

    private static List<string> SplitContentIntoLines(string? content)
    {
        var source = content ?? string.Empty;
        if (source.Length == 0)
        {
            return new List<string> { string.Empty };
        }

        var span = source.AsSpan();
        var estimatedLineCount = 1;
        var newlineIndex = 0;
        while (newlineIndex < span.Length)
        {
            var rel = span[newlineIndex..].IndexOfAny('\r', '\n');
            if (rel < 0) break;
            estimatedLineCount++;
            newlineIndex += rel + 1;
        }

        var result = new List<string>(estimatedLineCount);
        var start = 0;
        for (var i = 0; i < span.Length; i++)
        {
            var ch = span[i];
            if (ch != '\r' && ch != '\n') continue;

            result.Add(span[start..i].ToString());
            if (ch == '\r' && i + 1 < span.Length && span[i + 1] == '\n')
            {
                i++;
            }
            start = i + 1;
        }

        if (start <= span.Length)
        {
            result.Add(span[start..].ToString());
        }

        return result;
    }

    private static int CountOccurrences(string source, string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;

        var sourceSpan = source.AsSpan();
        var valueSpan = value.AsSpan();
        var count = 0;
        var index = 0;
        while (index <= sourceSpan.Length - valueSpan.Length)
        {
            var rel = sourceSpan[index..].IndexOf(valueSpan);
            if (rel < 0) break;
            count++;
            index += rel + valueSpan.Length;
        }

        return count;
    }

    private static string ReplaceAllOrdinalPooled(string source, string oldValue, string newValue, int replacementCount)
    {
        var oldSpan = oldValue.AsSpan();
        var newSpan = newValue.AsSpan();

        var finalLength = checked(source.Length + replacementCount * (newSpan.Length - oldSpan.Length));
        if (finalLength == 0) return string.Empty;

        var rented = ArrayPool<char>.Shared.Rent(finalLength);
        try
        {
            var destination = rented.AsSpan(0, finalLength);
            var sourceSpan = source.AsSpan();
            var srcIndex = 0;
            var dstIndex = 0;

            while (true)
            {
                var relative = sourceSpan[srcIndex..].IndexOf(oldSpan);
                if (relative < 0) break;

                var matchIndex = srcIndex + relative;
                var prefix = sourceSpan[srcIndex..matchIndex];
                prefix.CopyTo(destination[dstIndex..]);
                dstIndex += prefix.Length;

                newSpan.CopyTo(destination[dstIndex..]);
                dstIndex += newSpan.Length;

                srcIndex = matchIndex + oldSpan.Length;
            }

            var tail = sourceSpan[srcIndex..];
            tail.CopyTo(destination[dstIndex..]);
            return new string(rented, 0, finalLength);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    private static string ReplaceSingleOrdinalPooled(string source, string oldValue, string newValue, int matchIndex)
    {
        var finalLength = checked(source.Length - oldValue.Length + newValue.Length);
        if (finalLength == 0) return string.Empty;

        var rented = ArrayPool<char>.Shared.Rent(finalLength);
        try
        {
            var destination = rented.AsSpan(0, finalLength);
            var sourceSpan = source.AsSpan();
            var newSpan = newValue.AsSpan();

            var prefix = sourceSpan[..matchIndex];
            prefix.CopyTo(destination);

            var writeIndex = prefix.Length;
            newSpan.CopyTo(destination[writeIndex..]);
            writeIndex += newSpan.Length;

            var suffixStart = matchIndex + oldValue.Length;
            sourceSpan[suffixStart..].CopyTo(destination[writeIndex..]);

            return new string(rented, 0, finalLength);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    private static bool TryReadLine(
        string source,
        ref int cursor,
        int pendingStart,
        int pendingLength,
        out LineSlice line,
        out int nextPendingStart,
        out int nextPendingLength)
    {
        if (pendingStart >= 0)
        {
            line = new LineSlice(source, pendingStart, pendingLength);
            nextPendingStart = -1;
            nextPendingLength = 0;
            return true;
        }

        if (cursor >= source.Length)
        {
            line = default;
            nextPendingStart = -1;
            nextPendingLength = 0;
            return false;
        }

        var start = cursor;
        var span = source.AsSpan();
        while (cursor < source.Length)
        {
            var ch = span[cursor];
            if (ch == '\r' || ch == '\n')
            {
                var length = cursor - start;
                if (ch == '\r' && cursor + 1 < source.Length && span[cursor + 1] == '\n')
                {
                    cursor += 2;
                }
                else
                {
                    cursor++;
                }

                line = new LineSlice(source, start, length);
                nextPendingStart = -1;
                nextPendingLength = 0;
                return true;
            }

            cursor++;
        }

        line = new LineSlice(source, start, source.Length - start);
        nextPendingStart = -1;
        nextPendingLength = 0;
        return true;
    }

    private static bool TryParseUnifiedDiffHunkHeader(ReadOnlySpan<char> line, out int oldStart)
    {
        oldStart = 0;

        if (!line.StartsWith("@@ -".AsSpan(), StringComparison.Ordinal))
        {
            return false;
        }

        var index = 4;
        if (index >= line.Length || !char.IsDigit(line[index]))
        {
            return false;
        }

        var value = 0;
        while (index < line.Length && char.IsDigit(line[index]))
        {
            value = checked(value * 10 + (line[index] - '0'));
            index++;
        }

        if (index < line.Length && line[index] == ',')
        {
            index++;
            while (index < line.Length && char.IsDigit(line[index]))
            {
                index++;
            }
        }

        if (index + 1 >= line.Length || line[index] != ' ' || line[index + 1] != '+')
        {
            return false;
        }

        oldStart = value;
        return true;
    }

    private static Encoding? GetEncodingFromName(string? encoding)
    {
        if (string.IsNullOrWhiteSpace(encoding)) return Utf8NoBom;

        var normalized = encoding.Trim().ToLowerInvariant();

        return normalized switch
        {
            "utf-8" or "utf8" => Utf8NoBom,
            "utf-8-bom" or "utf8-bom" => Utf8Bom,
            "utf-16" or "utf16" or "unicode" => Encoding.Unicode,
            "utf-16le" or "utf16le" => Encoding.Unicode,
            "utf-16be" or "utf16be" or "bigendianunicode" => Encoding.BigEndianUnicode,
            "utf-32" or "utf32" => new UTF32Encoding(bigEndian: false, byteOrderMark: true),
            "utf-32le" or "utf32le" => new UTF32Encoding(bigEndian: false, byteOrderMark: true),
            "utf-32be" or "utf32be" => new UTF32Encoding(bigEndian: true, byteOrderMark: true),
            "ascii" => Encoding.ASCII,
            _ => TryGetNamedEncoding(encoding.Trim())
        };
    }

    private static Encoding TryDetectFileEncoding(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return TryDetectEncoding(bytes, out _, out var encoding) ? encoding : Encoding.Default;
    }

    private static StreamReader OpenDetectedTextReader(string path, Encoding? encodingOverride = null)
    {
        return encodingOverride is null
            ? new StreamReader(path, TryDetectFileEncoding(path), detectEncodingFromByteOrderMarks: true)
            : new StreamReader(path, encodingOverride, detectEncodingFromByteOrderMarks: false);
    }

    private static bool IsSafeToWrite(string path)
    {
        var fullPath = Path.GetFullPath(path);
        foreach (var fullDeniedRoot in FullWriteDenylist)
        {
            if (PathEqualsOrIsChildOf(fullPath, fullDeniedRoot))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsRootPath(string path)
    {
        if (LooksLikeWindowsDriveRoot(path))
        {
            return true;
        }

        if (!Path.IsPathRooted(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }

        return string.Equals(
            fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeWindowsDriveRoot(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length < 3 || !char.IsAsciiLetter(trimmed[0]) || trimmed[1] != ':')
        {
            return false;
        }

        if (trimmed[2] is not ('\\' or '/'))
        {
            return false;
        }

        return trimmed[3..].Trim('\\', '/').Length == 0;
    }

    private static bool PathEqualsOrIsChildOf(string candidate, string parent)
    {
        if (string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedParent = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static FileTextState ReadFileTextState(string path, Encoding? encodingOverride = null)
    {
        var bytes = File.ReadAllBytes(path);
        Encoding encoding;
        int preambleLength;

        if (encodingOverride is not null)
        {
            encoding = encodingOverride;
            preambleLength = GetPreambleLength(bytes, encodingOverride);
        }
        else if (!TryDetectEncoding(bytes, out preambleLength, out encoding))
        {
            encoding = Encoding.Default;
            preambleLength = 0;
        }

        var content = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
        return new FileTextState(content, encoding);
    }

    private static ExistingFileState ReadExistingFileState(string path, Encoding? encodingOverride = null)
    {
        var fileTextState = ReadFileTextState(path, encodingOverride);
        var newLine = DetectNewLine(fileTextState.Content);
        var endsWithNewLine = HasTrailingNewLine(fileTextState.Content);
        var lines = ParseExistingFileLines(fileTextState.Content);

        return new ExistingFileState(fileTextState.Encoding, newLine, endsWithNewLine, lines);
    }

    private static void WriteExistingFileState(string path, ExistingFileState state)
    {
        var fullPath = Path.GetFullPath(path);
        var tempPath = CreateSiblingTempPath(fullPath);

        try
        {
            using (var writer = new StreamWriter(tempPath, append: false, state.Encoding))
            {
                for (var i = 0; i < state.Lines.Count; i++)
                {
                    writer.Write(state.Lines[i]);
                    if (i + 1 < state.Lines.Count || state.EndsWithNewLine)
                    {
                        writer.Write(state.NewLine);
                    }
                }
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            SafeDeleteFile(tempPath);
            throw;
        }
    }

    private static void WriteTextWithEncoding(string path, Encoding encoding, string content)
    {
        var fullPath = Path.GetFullPath(path);
        var tempPath = CreateSiblingTempPath(fullPath);

        try
        {
            File.WriteAllText(tempPath, content, encoding);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            SafeDeleteFile(tempPath);
            throw;
        }
    }

    private static string DetectNewLine(string content)
    {
        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];
            if (ch == '\r')
            {
                return i + 1 < content.Length && content[i + 1] == '\n' ? "\r\n" : "\r";
            }

            if (ch == '\n')
            {
                return "\n";
            }
        }

        return Environment.NewLine;
    }

    private static bool TryValidateEncodingOverride(string? encodingName, out Encoding? encoding, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(encodingName))
        {
            encoding = null;
            errorMessage = null;
            return false;
        }

        encoding = GetEncodingFromName(encodingName);
        if (encoding is not null)
        {
            errorMessage = null;
            return false;
        }

        errorMessage = $"Error: encoding '{encodingName}' is not supported on this system.";
        return true;
    }

    private static int GetPreambleLength(ReadOnlySpan<byte> bytes, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        if (preamble.Length == 0 || bytes.Length < preamble.Length)
        {
            return 0;
        }

        for (var i = 0; i < preamble.Length; i++)
        {
            if (bytes[i] != preamble[i])
            {
                return 0;
            }
        }

        return preamble.Length;
    }

    private static Encoding? TryGetNamedEncoding(string name)
    {
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryDetectEncoding(ReadOnlySpan<byte> bytes, out int preambleLength, out Encoding encoding)
    {
        if (TryDetectBomEncoding(bytes, out preambleLength, out encoding))
        {
            return true;
        }

        preambleLength = 0;

        if (bytes.Length == 0)
        {
            encoding = Utf8NoBom;
            return true;
        }

        if (IsValidUtf8(bytes))
        {
            encoding = Utf8NoBom;
            return true;
        }

        if (LooksLikeUtf32Le(bytes))
        {
            encoding = Utf32LeNoBom;
            return true;
        }

        if (LooksLikeUtf32Be(bytes))
        {
            encoding = Utf32BeNoBom;
            return true;
        }

        if (LooksLikeUtf16Le(bytes))
        {
            encoding = Utf16LeNoBom;
            return true;
        }

        if (LooksLikeUtf16Be(bytes))
        {
            encoding = Utf16BeNoBom;
            return true;
        }

        encoding = Encoding.Default;
        return true;
    }

    private static bool TryDetectBomEncoding(ReadOnlySpan<byte> bytes, out int preambleLength, out Encoding encoding)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            preambleLength = 3;
            encoding = Utf8Bom;
            return true;
        }

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            preambleLength = 4;
            encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true);
            return true;
        }

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            preambleLength = 4;
            encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            preambleLength = 2;
            encoding = Encoding.Unicode;
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            preambleLength = 2;
            encoding = Encoding.BigEndianUnicode;
            return true;
        }

        preambleLength = 0;
        encoding = Utf8NoBom;
        return false;
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        var i = 0;
        while (i < bytes.Length)
        {
            var current = bytes[i];
            if (current <= 0x7F)
            {
                i++;
                continue;
            }

            int additionalCount;
            int minCodePoint;

            if ((current & 0xE0) == 0xC0)
            {
                additionalCount = 1;
                minCodePoint = 0x80;
            }
            else if ((current & 0xF0) == 0xE0)
            {
                additionalCount = 2;
                minCodePoint = 0x800;
            }
            else if ((current & 0xF8) == 0xF0)
            {
                additionalCount = 3;
                minCodePoint = 0x10000;
            }
            else
            {
                return false;
            }

            if (i + additionalCount >= bytes.Length)
            {
                return false;
            }

            var codePoint = current & (0x7F >> additionalCount);
            for (var j = 1; j <= additionalCount; j++)
            {
                var continuation = bytes[i + j];
                if ((continuation & 0xC0) != 0x80)
                {
                    return false;
                }

                codePoint = (codePoint << 6) | (continuation & 0x3F);
            }

            if (codePoint < minCodePoint || codePoint is > 0x10FFFF or >= 0xD800 and <= 0xDFFF)
            {
                return false;
            }

            i += additionalCount + 1;
        }

        return true;
    }

    private static bool LooksLikeUtf16Le(ReadOnlySpan<byte> bytes)
    {
        return LooksLikeUtf16(bytes, evenZeroExpected: false);
    }

    private static bool LooksLikeUtf16Be(ReadOnlySpan<byte> bytes)
    {
        return LooksLikeUtf16(bytes, evenZeroExpected: true);
    }

    private static bool LooksLikeUtf16(ReadOnlySpan<byte> bytes, bool evenZeroExpected)
    {
        if (bytes.Length < 4 || (bytes.Length & 1) != 0)
        {
            return false;
        }

        var zeroOnExpectedSide = 0;
        var zeroOnOtherSide = 0;
        var samplePairs = 0;

        for (var i = 0; i + 1 < bytes.Length && samplePairs < 64; i += 2, samplePairs++)
        {
            var even = bytes[i] == 0;
            var odd = bytes[i + 1] == 0;
            if (evenZeroExpected)
            {
                if (even) zeroOnExpectedSide++;
                if (odd) zeroOnOtherSide++;
            }
            else
            {
                if (odd) zeroOnExpectedSide++;
                if (even) zeroOnOtherSide++;
            }
        }

        return samplePairs >= 2 && zeroOnExpectedSide >= samplePairs / 2 && zeroOnOtherSide <= samplePairs / 8;
    }

    private static bool LooksLikeUtf32Le(ReadOnlySpan<byte> bytes)
    {
        return LooksLikeUtf32(bytes, littleEndian: true);
    }

    private static bool LooksLikeUtf32Be(ReadOnlySpan<byte> bytes)
    {
        return LooksLikeUtf32(bytes, littleEndian: false);
    }

    private static bool LooksLikeUtf32(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        if (bytes.Length < 8 || bytes.Length % 4 != 0)
        {
            return false;
        }

        var expectedZeroCount = 0;
        var sampleUnits = 0;

        for (var i = 0; i + 3 < bytes.Length && sampleUnits < 32; i += 4, sampleUnits++)
        {
            if (littleEndian)
            {
                if (bytes[i + 1] == 0 && bytes[i + 2] == 0 && bytes[i + 3] == 0)
                {
                    expectedZeroCount++;
                }
            }
            else
            {
                if (bytes[i] == 0 && bytes[i + 1] == 0 && bytes[i + 2] == 0)
                {
                    expectedZeroCount++;
                }
            }
        }

        return sampleUnits >= 2 && expectedZeroCount >= sampleUnits / 2;
    }

    private static bool HasTrailingNewLine(string content)
    {
        return content.Length > 0 && (content[^1] == '\n' || content[^1] == '\r');
    }

    private static List<string> ParseExistingFileLines(string content)
    {
        var result = new List<string>();
        if (content.Length == 0)
        {
            return result;
        }

        var span = content.AsSpan();
        var start = 0;

        for (var i = 0; i < span.Length; i++)
        {
            var ch = span[i];
            if (ch != '\r' && ch != '\n')
            {
                continue;
            }

            result.Add(span[start..i].ToString());

            if (ch == '\r' && i + 1 < span.Length && span[i + 1] == '\n')
            {
                i++;
            }

            start = i + 1;
        }

        if (start < span.Length)
        {
            result.Add(span[start..].ToString());
        }

        return result;
    }

    private static string CreateSiblingTempPath(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath) ?? Path.GetTempPath();
        var fileName = Path.GetFileName(fullPath);
        return Path.Combine(directory, $".{fileName}.winsysmcp.{Guid.NewGuid():N}.tmp");
    }

    private static void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private static bool LineRangeMatches(IReadOnlyList<string> lines, int startIndex, int count, IReadOnlyList<string> expectedLines)
    {
        if (expectedLines.Count != count)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            if (!string.Equals(lines[startIndex + i], expectedLines[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool InsertAnchorsMatch(IReadOnlyList<string> lines, int insertIndex, string? expectedBeforeLine, string? expectedAfterLine, out string mismatchReason)
    {
        if (expectedBeforeLine is not null)
        {
            if (insertIndex == 0)
            {
                mismatchReason = "expectedBeforeLine was provided, but the insertion point is at the start of the file";
                return false;
            }

            if (!string.Equals(lines[insertIndex - 1], expectedBeforeLine, StringComparison.Ordinal))
            {
                mismatchReason = $"expectedBeforeLine did not match the current line {insertIndex}";
                return false;
            }
        }

        if (expectedAfterLine is not null)
        {
            if (insertIndex >= lines.Count)
            {
                mismatchReason = "expectedAfterLine was provided, but the insertion point is at the end of the file";
                return false;
            }

            if (!string.Equals(lines[insertIndex], expectedAfterLine, StringComparison.Ordinal))
            {
                mismatchReason = $"expectedAfterLine did not match the current line {insertIndex + 1}";
                return false;
            }
        }

        mismatchReason = string.Empty;
        return true;
    }

    private struct DiffOperation
    {
        public string? Operation { get; set; }
        public int? StartLine { get; set; }
        public int? EndLine { get; set; }
        public int? AfterLine { get; set; }
        public string? Content { get; set; }
    }

    private enum DiffPropertyName : byte
    {
        Unknown = 0,
        Operation = 1,
        StartLine = 2,
        EndLine = 3,
        AfterLine = 4,
        Content = 5
    }

    private readonly record struct LineSlice(string Source, int Start, int Length)
    {
        public char this[int index] => Source[Start + index];
        public ReadOnlySpan<char> AsSpan() => Source.AsSpan(Start, Length);
        public ReadOnlySpan<char> Slice(int start) => Source.AsSpan(Start + start, Length - start);
        public bool StartsWith(ReadOnlySpan<char> value, StringComparison comparison) => AsSpan().StartsWith(value, comparison);
        public override string ToString() => Source.Substring(Start, Length);
    }

    private readonly record struct FileTextState(string Content, Encoding Encoding);

    private sealed class ExistingFileState
    {
        public ExistingFileState(Encoding encoding, string newLine, bool endsWithNewLine, List<string> lines)
        {
            Encoding = encoding;
            NewLine = newLine;
            EndsWithNewLine = endsWithNewLine;
            Lines = lines;
        }

        public Encoding Encoding { get; }
        public string NewLine { get; }
        public bool EndsWithNewLine { get; }
        public List<string> Lines { get; }
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
