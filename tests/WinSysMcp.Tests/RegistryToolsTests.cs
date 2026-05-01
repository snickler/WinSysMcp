using Xunit;
using WinSysMcp.Tools;

namespace WinSysMcp.Tests;

public class RegistryToolsTests
{
    [Fact]
    public void ReadRegistryValue_MissingParams_ReturnsError()
    {
        var res = RegistryTools.ReadRegistryValue("","","");
        Assert.Equal("Error: root, keyPath and valueName are required.", res);
    }

    [Fact]
    public void ListRegistryKeys_MissingParams_ReturnsErrorList()
    {
        var res = RegistryTools.ListRegistryKeys("", "");
        Assert.NotEmpty(res);
        Assert.Equal("Error: root and keyPath are required.", res[0]);
    }

    [Fact]
    public void WriteRegistryValue_MissingParams_ReturnsError()
    {
        var res = RegistryTools.WriteRegistryValue("", "", "", "");
        Assert.Equal("Error: root, keyPath and valueName are required.", res);
    }

    [Fact]
    public void DeleteRegistryValue_MissingParams_ReturnsError()
    {
        var res = RegistryTools.DeleteRegistryValue("", "", "");
        Assert.Equal("Error: root, keyPath and valueName are required.", res);
    }
}
