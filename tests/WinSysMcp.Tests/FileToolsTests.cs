using System;
using System.IO;
using System.Text;
using Xunit;
using WinSysMcp.Tools;

namespace WinSysMcp.Tests;

public class FileToolsTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testFile;

    public FileToolsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "WinSysMcpTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        _testFile = Path.Combine(_testDir, "test.txt");
        File.WriteAllLines(_testFile, new[] { "line1", "line2", "line3", "line4" });
    }

    [Fact]
    public void ListDirectory_ReturnsEntries()
    {
        var results = FileTools.ListDirectory(_testDir);
        Assert.Contains(results, e => e.FullName == _testFile && e.Type == "File");
    }

    [Fact]
    public void ReadFileHead_Tail_Work()
    {
        var head = FileTools.ReadFileHead(_testFile, 2);
        Assert.Equal(new[] { "line1", "line2" }, head);

        var tail = FileTools.ReadFileTail(_testFile, 2);
        Assert.Equal(new[] { "line3", "line4" }, tail);
    }

    [Fact]
    public void GetFileHash_IsHexString()
    {
        var hash = FileTools.GetFileHash(_testFile);
        Assert.False(string.IsNullOrWhiteSpace(hash));
        // SHA256 hex length is 64
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void CopyFile_Validation_MissingParams()
    {
        var res = FileTools.CopyFile("", "");
        Assert.Equal("sourcePath and destPath are required.", res);
    }

    [Fact]
    public void CopyFile_SamePaths_ReturnsError()
    {
        var res = FileTools.CopyFile(_testFile, _testFile);
        Assert.Equal("Source and destination paths are the same.", res);
    }

    [Fact]
    public void DeleteDirectory_RefuseRoot()
    {
        var res = FileTools.DeleteDirectory("C:\\");
        Assert.Equal("Refusing to delete a root path.", res);
    }

    [Fact]
    public void ReplaceExactChunk_ReplacesUniqueChunk()
    {
        var oldChunk = $"line2{Environment.NewLine}line3";
        var newChunk = $"updated2{Environment.NewLine}updated3";

        var result = FileTools.ReplaceExactChunk(_testFile, oldChunk, newChunk);

        Assert.Contains("Success:", result);
        var contents = File.ReadAllText(_testFile);
        Assert.Contains(newChunk, contents);
        Assert.DoesNotContain(oldChunk, contents);
    }

    [Fact]
    public void DeleteExactChunk_FailsWhenMatchCountIsAmbiguous()
    {
        File.WriteAllLines(_testFile, new[] { "dup", "middle", "dup" });

        var result = FileTools.DeleteExactChunk(_testFile, "dup", expectedOccurrences: 1);

        Assert.Equal("Error: expected 1 occurrence(s) of the target text, but found 2. No changes were made.", result);
        Assert.Equal(new[] { "dup", "middle", "dup" }, File.ReadAllLines(_testFile));
    }

    [Fact]
    public void DeleteExactChunk_DeletesUniqueChunk()
    {
        var result = FileTools.DeleteExactChunk(_testFile, $"line2{Environment.NewLine}");

        Assert.Contains("Success:", result);
        Assert.Equal(new[] { "line1", "line3", "line4" }, File.ReadAllLines(_testFile));
    }

    [Fact]
    public void ReplaceLines_InvalidRange_DoesNotModifyFile()
    {
        var original = File.ReadAllText(_testFile);

        var result = FileTools.ReplaceLines(_testFile, 99, 100, "new line");

        Assert.Equal("Error: startLine 99 is beyond end of file (4 lines).", result);
        Assert.Equal(original, File.ReadAllText(_testFile));
    }

    [Fact]
    public void DeleteLines_InvalidRange_DoesNotModifyFile()
    {
        var original = File.ReadAllText(_testFile);

        var result = FileTools.DeleteLines(_testFile, 99, 100);

        Assert.Equal("Error: startLine 99 is beyond end of file (4 lines).", result);
        Assert.Equal(original, File.ReadAllText(_testFile));
    }

    [Fact]
    public void ReplaceLines_PreservesLfNewlines()
    {
        File.WriteAllText(_testFile, "line1\nline2\nline3\n");

        var result = FileTools.ReplaceLines(_testFile, 2, 2, "updated");

        Assert.Contains("Success:", result);
        Assert.Equal("line1\nupdated\nline3\n", File.ReadAllText(_testFile));
    }

    [Fact]
    public void ReplaceLines_PreservesCrOnlyNewlines()
    {
        File.WriteAllText(_testFile, "line1\rline2\rline3\r", Encoding.ASCII);

        var result = FileTools.ReplaceLines(_testFile, 2, 2, "updated");

        Assert.Contains("Success:", result);
        Assert.Equal("line1\rupdated\rline3\r", File.ReadAllText(_testFile, Encoding.ASCII));
    }

    [Fact]
    public void DeleteLines_PreservesNoTrailingNewline()
    {
        File.WriteAllText(_testFile, "line1\nline2\nline3");

        var result = FileTools.DeleteLines(_testFile, 2, 2);

        Assert.Contains("Success:", result);
        Assert.Equal("line1\nline3", File.ReadAllText(_testFile));
    }

    [Fact]
    public void InsertAtLine_PreservesLfNewlines()
    {
        File.WriteAllText(_testFile, "line1\nline2\nline3\n");

        var result = FileTools.InsertAtLine(_testFile, 2, "inserted");

        Assert.Contains("Success:", result);
        Assert.Equal("line1\ninserted\nline2\nline3\n", File.ReadAllText(_testFile));
    }

    [Fact]
    public void ReplaceLinesGuarded_FailsWhenExpectedContentDoesNotMatch()
    {
        var original = File.ReadAllText(_testFile);

        var result = FileTools.ReplaceLinesGuarded(_testFile, 2, 3, "wrong\ncontent", "updated");

        Assert.Equal("Error: expected content did not match lines 2-3. No changes were made.", result);
        Assert.Equal(original, File.ReadAllText(_testFile));
    }

    [Fact]
    public void ReplaceLinesGuarded_ReplacesMatchingRange()
    {
        File.WriteAllText(_testFile, "line1\nline2\nline3\n");

        var result = FileTools.ReplaceLinesGuarded(_testFile, 2, 3, "line2\nline3", "updated2\nupdated3");

        Assert.Equal($"Success: replaced guarded lines 2-3 in '{_testFile}'.", result);
        Assert.Equal("line1\nupdated2\nupdated3\n", File.ReadAllText(_testFile));
    }

    [Fact]
    public void DeleteLinesGuarded_FailsWhenEndLineIsBeyondEndOfFile()
    {
        var original = File.ReadAllText(_testFile);

        var result = FileTools.DeleteLinesGuarded(_testFile, 3, 99, "line3\nline4");

        Assert.Equal("Error: endLine 99 is beyond end of file (4 lines).", result);
        Assert.Equal(original, File.ReadAllText(_testFile));
    }

    [Fact]
    public void ReplaceText_PreservesUtf16Encoding()
    {
        File.WriteAllText(_testFile, "alpha\r\nbeta\r\n", Encoding.Unicode);

        var result = FileTools.ReplaceText(_testFile, "beta", "gamma");

        Assert.Contains("Success:", result);
        using var reader = new StreamReader(_testFile, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();
        Assert.Equal("alpha\r\ngamma\r\n", content);
        Assert.Equal(Encoding.Unicode.CodePage, reader.CurrentEncoding.CodePage);
    }

    [Fact]
    public void ReplaceText_PreservesUtf8Bom()
    {
        File.WriteAllText(_testFile, "alpha\nbeta\n", Encoding.UTF8);

        var result = FileTools.ReplaceText(_testFile, "beta", "gamma");

        Assert.Contains("Success:", result);
        var bytes = File.ReadAllBytes(_testFile);
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
        Assert.Equal("alpha\ngamma\n", File.ReadAllText(_testFile, Encoding.UTF8));
    }

    [Fact]
    public void ReadFileHead_DetectsUtf16BeWithoutBom()
    {
        var utf16BeNoBom = new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
        File.WriteAllText(_testFile, "line1\nline2\n", utf16BeNoBom);

        var result = FileTools.ReadFileHead(_testFile, 2);

        Assert.Equal(new[] { "line1", "line2" }, result);
    }

    [Fact]
    public void ReadFileHead_UsesEncodingOverride()
    {
        var shiftJis = GetEncoding("shift_jis");
        File.WriteAllText(_testFile, "挨拶\nこんにちは\n", shiftJis);

        var result = FileTools.ReadFileHead(_testFile, 2, encoding: "shift_jis");

        Assert.Equal(new[] { "挨拶", "こんにちは" }, result);
    }

    [Fact]
    public void WriteFile_SupportsNamedCodePageEncoding()
    {
        var expectedEncoding = GetEncoding("windows-1252");

        var result = FileTools.WriteFile(_testFile, "café", encoding: "windows-1252");

        Assert.Contains("Success:", result);
        Assert.Equal(expectedEncoding.GetBytes("café"), File.ReadAllBytes(_testFile));
    }

    [Fact]
    public void ReplaceLines_UsesEncodingOverride()
    {
        var shiftJis = GetEncoding("shift_jis");
        File.WriteAllText(_testFile, "挨拶\nこんにちは\nさようなら\n", shiftJis);

        var result = FileTools.ReplaceLines(_testFile, 2, 2, "こんばんは", encoding: "shift_jis");

        Assert.Contains("Success:", result);
        Assert.Equal("挨拶\nこんばんは\nさようなら\n", File.ReadAllText(_testFile, shiftJis));
    }

    [Fact]
    public void ReplaceText_UsesEncodingOverride()
    {
        var shiftJis = GetEncoding("shift_jis");
        File.WriteAllText(_testFile, "挨拶=こんにちは\n", shiftJis);

        var result = FileTools.ReplaceText(_testFile, "こんにちは", "こんばんは", encoding: "shift_jis");

        Assert.Contains("Success:", result);
        Assert.Equal("挨拶=こんばんは\n", File.ReadAllText(_testFile, shiftJis));
    }

    [Fact]
    public void ReadFileHead_ReturnsErrorForUnsupportedEncodingOverride()
    {
        var result = FileTools.ReadFileHead(_testFile, 1, encoding: "definitely-not-a-real-encoding");

        Assert.Equal(new[] { "Error: encoding 'definitely-not-a-real-encoding' is not supported on this system." }, result);
    }

    [Fact]
    public void ApplyDiff_PreservesLfNewlines()
    {
        File.WriteAllText(_testFile, "line1\nline2\nline3\n");
        const string diff = "@@ -2,1 +2,1 @@\n-line2\n+updated\n";

        var result = FileTools.ApplyDiff(_testFile, diff);

        Assert.Contains("Success:", result);
        Assert.Equal("line1\nupdated\nline3\n", File.ReadAllText(_testFile));
    }

    [Fact]
    public void InsertAtLineGuarded_InsertsWhenAnchorsMatch()
    {
        File.WriteAllText(_testFile, "line1\nline2\nline3\n");

        var result = FileTools.InsertAtLineGuarded(_testFile, 2, "inserted", expectedBeforeLine: "line1", expectedAfterLine: "line2");

        Assert.Equal($"Success: inserted 1 guarded line(s) at line 2 in '{_testFile}'.", result);
        Assert.Equal("line1\ninserted\nline2\nline3\n", File.ReadAllText(_testFile));
    }

    [Fact]
    public void InsertAtLineGuarded_FailsWhenAfterAnchorMismatches()
    {
        var original = File.ReadAllText(_testFile);

        var result = FileTools.InsertAtLineGuarded(_testFile, 2, "inserted", expectedAfterLine: "not-line2");

        Assert.Equal("Error: expectedAfterLine did not match the current line 2. No changes were made.", result);
        Assert.Equal(original, File.ReadAllText(_testFile));
    }

    [Fact]
    public void InsertAtLineGuarded_RequiresAtLeastOneAnchor()
    {
        var original = File.ReadAllText(_testFile);

        var result = FileTools.InsertAtLineGuarded(_testFile, 2, "inserted");

        Assert.Equal("Error: expectedBeforeLine and/or expectedAfterLine must be provided for guarded insert.", result);
        Assert.Equal(original, File.ReadAllText(_testFile));
    }

    public void Dispose()
    {
        try { File.Delete(_testFile); } catch { }
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private static Encoding GetEncoding(string name)
    {
        _ = FileTools.GetTempPath();
        return Encoding.GetEncoding(name);
    }
}
