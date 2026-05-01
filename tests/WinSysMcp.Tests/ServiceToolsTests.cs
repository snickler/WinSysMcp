using Xunit;
using WinSysMcp.Tools;

namespace WinSysMcp.Tests;

public class ServiceToolsTests
{
    [Fact]
    public void GetServiceDetails_Empty_ReturnsNull()
    {
        var details = ServiceTools.GetServiceDetails("");
        Assert.Null(details);
    }

    [Fact]
    public void StartService_Empty_ReturnsValidationError()
    {
        var res = ServiceTools.StartService("");
        Assert.Equal("Error: serviceName is required.", res);
    }

    [Fact]
    public void StopService_Empty_ReturnsValidationError()
    {
        var res = ServiceTools.StopService("");
        Assert.Equal("Error: serviceName is required.", res);
    }
}
