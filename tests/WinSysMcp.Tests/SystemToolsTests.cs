using Xunit;
using WinSysMcp.Tools;

namespace WinSysMcp.Tests;

public class SystemToolsTests
{
    [Fact]
    public void Echo_ReturnsMessage()
    {
        var result = SystemTools.Echo("ping");
        Assert.Equal("Echo: ping", result);
    }

    [Fact]
    public void GetSystemInfo_NotEmpty()
    {
        var info = SystemTools.GetSystemInfo();
        Assert.False(string.IsNullOrWhiteSpace(info));
    }
}
