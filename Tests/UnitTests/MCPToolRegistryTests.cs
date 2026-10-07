using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

public class MCPToolRegistryTests
{
    [Fact]
    public void Register_Then_Get_Returns_Instance()
    {
        var registry = new MCPToolRegistry();
        var ping = new PingTool();

        registry.Register(ping);

        Assert.Same(ping, registry.Get("ping"));
        Assert.True(registry.Contains("ping"));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void Register_Same_Name_Twice_Throws()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new PingTool());

        var ex = Assert.Throws<InvalidOperationException>(() => registry.Register(new PingTool()));
        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public void List_Is_Sorted_By_Name()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new GetProjectInfoTool());
        registry.Register(new GetLayersTool());
        registry.Register(new PingTool());

        var names = registry.List().Select(t => t.Name).ToList();
        Assert.Equal("get_layers", names[0]);
        Assert.Equal("get_project_info", names[1]);
        Assert.Equal("ping", names[2]);
    }

    [Fact]
    public void Unregister_Removes_Tool()
    {
        var registry = new MCPToolRegistry();
        registry.Register(new PingTool());

        Assert.True(registry.Unregister("ping"));
        Assert.False(registry.Contains("ping"));
        Assert.Equal(0, registry.Count);
        Assert.Null(registry.Get("ping"));
    }

    [Fact]
    public void Get_Unknown_Returns_Null()
    {
        var registry = new MCPToolRegistry();
        Assert.Null(registry.Get("does-not-exist"));
    }

    [Fact]
    public void Register_Null_Throws()
    {
        var registry = new MCPToolRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }
}
