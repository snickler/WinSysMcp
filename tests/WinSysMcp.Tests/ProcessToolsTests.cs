using Xunit;
using WinSysMcp.Tools;

namespace WinSysMcp.Tests;

public class ProcessToolsTests
{
    [Fact]
    public void GetTopProcesses_ReturnsCountLimit()
    {
        var list = ProcessTools.GetTopProcesses(2);
        Assert.NotNull(list);
        Assert.True(list.Count <= 2);
    }
}
