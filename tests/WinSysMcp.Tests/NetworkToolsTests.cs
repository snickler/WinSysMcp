using System.Threading.Tasks;
using Xunit;
using WinSysMcp.Tools;

namespace WinSysMcp.Tests;

public class NetworkToolsTests
{
    [Fact]
    public async Task CheckPortOpen_ValidationErrors()
    {
        var missingHost = await NetworkTools.CheckPortOpen("", 80);
        Assert.Equal("Error: host parameter is required.", missingHost);

        var invalidPort = await NetworkTools.CheckPortOpen("localhost", 70000);
        Assert.Equal("Error: port must be between 1 and 65535.", invalidPort);
    }

    [Fact]
    public void PingHost_Localhost_ReturnsStatus()
    {
        var result = NetworkTools.PingHost("127.0.0.1");
        Assert.False(string.IsNullOrWhiteSpace(result));
        // acceptable values: starts with Success, Failed or Error
        Assert.True(result.StartsWith("Success") || result.StartsWith("Failed") || result.StartsWith("Error"));
    }

    [Fact]
    public async Task DownloadFile_InvalidUrl_ReturnsError()
    {
        var res = await NetworkTools.DownloadFile("ftp://example.com/file.txt", @"C:\temp\file.txt");
        Assert.StartsWith("Error: url must be a valid absolute HTTP/HTTPS URL.", res);
    }

    [Fact]
    public async Task DownloadFile_ProtectedDest_ReturnsError()
    {
        var res = await NetworkTools.DownloadFile("https://example.com/file.txt", @"C:\Windows\System32\evil.dll");
        Assert.StartsWith("Error: Writing to protected system path is blocked:", res);
    }
}
