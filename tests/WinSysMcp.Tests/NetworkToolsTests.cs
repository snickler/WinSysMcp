using Xunit;
using WinSysMcp.Tools;

namespace WinSysMcp.Tests;

public class NetworkToolsTests
{
    [Fact]
    public void CheckPortOpen_ValidationErrors()
    {
        var missingHost = NetworkTools.CheckPortOpen("", 80);
        Assert.Equal("Error: host parameter is required.", missingHost);

        var invalidPort = NetworkTools.CheckPortOpen("localhost", 70000);
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
    public void DownloadFile_InvalidUrl_ReturnsError()
    {
        var res = NetworkTools.DownloadFile("ftp://example.com/file.txt", "C:\\temp\\file.txt");
        Assert.StartsWith("Error: url must be a valid absolute HTTP/HTTPS URL.", res);
    }
}
