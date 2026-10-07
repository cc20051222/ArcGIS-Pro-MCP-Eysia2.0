using ArcGISProMCP.Core.Versioning;

namespace ArcGISProMCP.UnitTests;

/// <summary>D-070（P-23）：VersionIdentity 目标框架映射双口径单测。</summary>
public sealed class VersionIdentityTests
{
    [Fact]
    public void MapTargetFramework_Net8Attribute_ReturnsNet8()
    {
        Assert.Equal("net8.0", VersionIdentity.MapTargetFramework(".NETCoreApp,Version=v8.0"));
        Assert.Equal("net8.0", VersionIdentity.MapTargetFramework("net8.0-windows"));
    }

    [Fact]
    public void MapTargetFramework_Net6Attribute_ReturnsNet6()
    {
        Assert.Equal("net6.0", VersionIdentity.MapTargetFramework(".NETCoreApp,Version=v6.0"));
        Assert.Equal("net6.0", VersionIdentity.MapTargetFramework("net6.0-windows"));
    }

    [Fact]
    public void MapTargetFramework_UnknownOrEmpty_ReturnsNet6Fallback()
    {
        Assert.Equal("net6.0", VersionIdentity.MapTargetFramework(null));
        Assert.Equal("net6.0", VersionIdentity.MapTargetFramework(string.Empty));
        Assert.Equal("net6.0", VersionIdentity.MapTargetFramework(".NETCoreApp,Version=v7.0"));
    }
}
