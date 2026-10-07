using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Logging;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;
using Xunit;

namespace ArcGISProMCP.UnitTests;

public sealed class D105RasterQualificationGateTests
{
    private static async Task<OperationResult<object?>> CallAsync(
        FakeArcGISHost host,
        FakePythonBridgeService? bridge,
        IMCPTool tool,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var registry = new MCPToolRegistry();
        registry.Register(tool);
        var router = new MCPToolRouter(registry, host, new MCPSettings(), NullLogger.Instance, bridge);
        return await router.ExecuteAsync(new MCPToolCall { Name = tool.Name, Arguments = arguments });
    }

    private static string CreateInputDirectory(bool withSidecar)
    {
        var path = Path.Combine(Path.GetTempPath(), "d105-qualification-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, "input.tif"), new byte[] { 1, 2, 3, 4 });
        if (withSidecar)
        {
            File.WriteAllText(Path.Combine(path, "input_qualification.json"), "{}");
        }

        return path;
    }

    private static OperationResult<JsonElement?> RejectedQualification()
        => OperationResult<JsonElement?>.Fail(ErrorCodes.InvalidArgument, "repeated bands are not qualified");

    private static OperationResult<JsonElement?> AcceptedQualification()
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            applies = true,
            qualified = true,
            inputSha256 = new string('A', 64),
            sidecarSha256 = new string('B', 64),
        }));
        return OperationResult<JsonElement?>.Ok(document.RootElement.Clone());
    }

    [Fact]
    public async Task AddLayer_RejectsUnqualifiedSidecarInputBeforeMapMutation()
    {
        var directory = CreateInputDirectory(withSidecar: true);
        try
        {
            var host = new FakeArcGISHost();
            var bridge = new FakePythonBridgeService { RasterQualificationResult = RejectedQualification() };
            var input = Path.Combine(directory, "input.tif");
            var result = await CallAsync(host, bridge, new AddLayerTool(), new Dictionary<string, object?>
            {
                ["mapName"] = "TestMap",
                ["layerPathOrUri"] = input,
            });

            Assert.False(result.Success);
            Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors!).Code);
            Assert.Equal(0, ((FakeLayerService)host.Layers).AddLayerCallCount);
            Assert.Equal(input, Assert.Single(bridge.RasterQualificationPaths));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AddLayer_WithoutSidecarPreservesOrdinaryInputBehavior()
    {
        var directory = CreateInputDirectory(withSidecar: false);
        try
        {
            var host = new FakeArcGISHost();
            var bridge = new FakePythonBridgeService { RasterQualificationResult = RejectedQualification() };
            var result = await CallAsync(host, bridge, new AddLayerTool(), new Dictionary<string, object?>
            {
                ["mapName"] = "TestMap",
                ["layerPathOrUri"] = Path.Combine(directory, "input.tif"),
            });

            Assert.True(result.Success);
            Assert.Equal(1, ((FakeLayerService)host.Layers).AddLayerCallCount);
            Assert.Empty(bridge.RasterQualificationPaths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CreateLayout_RechecksMapRasterSourcesBeforeCreatingLayout()
    {
        var directory = CreateInputDirectory(withSidecar: true);
        try
        {
            var layout = new NotImplementedService();
            var layers = new FakeLayerService { RasterSourcePaths = new[] { Path.Combine(directory, "input.tif") } };
            var host = new FakeArcGISHost(layers: layers, layout: layout);
            var bridge = new FakePythonBridgeService { RasterQualificationResult = RejectedQualification() };
            var result = await CallAsync(host, bridge, new CreateLayoutTool(), new Dictionary<string, object?>
            {
                ["name"] = "D105_gate_test",
                ["pageWidth"] = 8.5,
                ["pageHeight"] = 11.0,
                ["mapName"] = "TestMap",
            });

            Assert.False(result.Success);
            Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors!).Code);
            Assert.Equal(0, layout.CreateLayoutCallCount);
            Assert.Single(layers.RasterSourcePaths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExportLayout_RechecksMapFrameRasterSourcesBeforeWritingOutput()
    {
        var directory = CreateInputDirectory(withSidecar: true);
        try
        {
            const string output = @"D:\d105-qualification-gate-output.png";
            var layout = new NotImplementedService
            {
                RasterSourcePaths = new[] { Path.Combine(directory, "input.tif") },
            };
            var host = new FakeArcGISHost(layout: layout);
            var bridge = new FakePythonBridgeService { RasterQualificationResult = RejectedQualification() };
            var result = await CallAsync(host, bridge, new ExportLayoutPngTool(), new Dictionary<string, object?>
            {
                ["layoutName"] = "D105_gate_test",
                ["outputPath"] = output,
                ["resolution"] = 150.0,
            });

            Assert.False(result.Success);
            Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(result.Errors!).Code);
            Assert.Equal(0, layout.ExportLayoutCallCount);
            Assert.False(File.Exists(output));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AddLayer_AcceptsHashBoundQualifiedSidecarInput()
    {
        var directory = CreateInputDirectory(withSidecar: true);
        try
        {
            var host = new FakeArcGISHost();
            var bridge = new FakePythonBridgeService { RasterQualificationResult = AcceptedQualification() };
            var result = await CallAsync(host, bridge, new AddLayerTool(), new Dictionary<string, object?>
            {
                ["mapName"] = "TestMap",
                ["layerPathOrUri"] = Path.Combine(directory, "input.tif"),
            });

            Assert.True(result.Success);
            Assert.Equal(1, ((FakeLayerService)host.Layers).AddLayerCallCount);
            Assert.Single(bridge.RasterQualificationPaths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
