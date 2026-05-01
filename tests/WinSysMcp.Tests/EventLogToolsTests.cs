using Xunit;
using WinSysMcp.Tools;

namespace WinSysMcp.Tests;

public class EventLogToolsTests
{
    [Fact]
    public void GetEventLogs_InvalidLogReturnsErrorEntry()
    {
        var results = EventLogTools.GetEventLogs("ThisLogDoesNotExist_12345", 5);
        Assert.NotEmpty(results);
        Assert.Equal("Error", results[0].EntryType);
    }
}
