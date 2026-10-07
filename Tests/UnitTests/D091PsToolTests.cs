using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

public sealed class D091PsToolTests
{
    private static readonly string[] ExpectedNames =
    [
        "ps_get_capabilities",
        "ps_apply_design_recipe",
        "ps_import_design_bundle",
        "ps_refresh_design_bundle",
        "ps_export_deliverables",
    ];

    [Fact]
    public void ProductionRegistry_ContainsD091Tools_AndReaches239()
    {
        var registry = ProductionCompositionAccess.BuildRegistry();
        var tools = registry.List();
        var actualNames = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(239, tools.Count);
        Assert.Equal(tools.Count, actualNames.Count);
        foreach (var name in ExpectedNames)
            Assert.Contains(name, actualNames);

        foreach (var name in ExpectedNames)
        {
            var tool = tools.Single(item => item.Name == name);
            Assert.Equal(ToolCategories.Photoshop, tool.Metadata.Category);
            Assert.Equal(ExecutionTypes.Bridge, tool.Metadata.ExecutionType);
            Assert.False(tool.Metadata.RequiresArcGIS);
        }

        Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf("ps_get_capabilities"));
        Assert.Equal(ToolWriteTier.SessionState, ToolWriteClassification.TierOf("ps_apply_design_recipe"));
        Assert.Equal(ToolWriteTier.SessionState, ToolWriteClassification.TierOf("ps_import_design_bundle"));
        Assert.Equal(ToolWriteTier.SessionState, ToolWriteClassification.TierOf("ps_refresh_design_bundle"));
        Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf("ps_export_deliverables"));
        Assert.Equal(285, ToolWriteClassification.Total);
    }

    [Fact]
    public void InputSchemas_MatchTheFrozenF03Schemas_ExceptGovernanceOnlyMetadata()
    {
        var tools = ProductionCompositionAccess.BuildRegistry().List()
            .Where(tool => ExpectedNames.Contains(tool.Name, StringComparer.Ordinal))
            .ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        var schemaRoot = Path.Combine(
            FindWorkspaceRoot(), ".runtime", "evolution", "v5-f", "run-20260928-d082", "f03b-5-schemas");

        Assert.Equal(ExpectedNames.Length, tools.Count);
        foreach (var name in ExpectedNames)
        {
            var frozenPath = Path.Combine(schemaRoot, name + ".schema.json");
            using var frozen = JsonDocument.Parse(File.ReadAllText(frozenPath));
            using var actual = JsonDocument.Parse(JsonSerializer.Serialize(tools[name].InputSchema));
            Assert.Equal(Canonical(frozen.RootElement), Canonical(actual.RootElement));
            Assert.False(actual.RootElement.GetProperty("additionalProperties").GetBoolean());
        }
    }

    [Fact]
    public async Task GetCapabilities_ReportsNotVerified_AndNeverRunsDeepProbe()
    {
        var result = await new PsGetCapabilitiesTool().ExecuteAsync(Context(
            new Dictionary<string, object?> { ["probeDeep"] = true },
            new MCPSettings { Host = "0.0.0.0", Port = 6520 }));

        Assert.True(result.Success);
        var data = Assert.IsType<Dictionary<string, object?>>(result.Data);
        Assert.Equal("NOT_VERIFIED", data["status"]);
        Assert.Equal(true, data["probeDeepRequested"]);
        Assert.Equal(false, data["deepProbePerformed"]);
        Assert.Equal(false, data["psRequestSent"]);
        var routeHealth = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(data["routeHealth"]);
        Assert.Equal("NOT_PROBED", routeHealth["status"]);
        Assert.Equal(false, routeHealth["requestAttempted"]);
    }

    [Theory]
    [InlineData("ps_apply_design_recipe")]
    [InlineData("ps_import_design_bundle")]
    [InlineData("ps_refresh_design_bundle")]
    [InlineData("ps_export_deliverables")]
    public async Task UnverifiedOperations_FailClosedWithoutSendingRequests(string toolName)
    {
        var (tool, args) = ToolAndValidArguments(toolName);
        var result = await tool.ExecuteAsync(Context(args, new MCPSettings { Host = "0.0.0.0" }));

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.NotImplemented, error.Code);
        using var details = JsonDocument.Parse(error.Details!);
        Assert.Equal("NOT_VERIFIED", details.RootElement.GetProperty("status").GetString());
        Assert.False(details.RootElement.GetProperty("psRequestSent").GetBoolean());
        Assert.False(details.RootElement.GetProperty("sideEffects").GetBoolean());
    }

    [Fact]
    public async Task StrictSchemaValidation_RejectsUnknownAndOutOfEnumArguments()
    {
        var unknown = await new PsApplyDesignRecipeTool().ExecuteAsync(Context(new Dictionary<string, object?>
        {
            ["documentId"] = "doc-1",
            ["apsReference"] = new Dictionary<string, object?>(),
            ["recipeId"] = "recipe-1",
            ["url"] = "http://127.0.0.1/anything",
        }));
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(unknown.Errors).Code);

        var invalidEnum = await new PsImportDesignBundleTool().ExecuteAsync(Context(new Dictionary<string, object?>
        {
            ["bundlePath"] = "D:/owned/bundle",
            ["documentId"] = "doc-1",
            ["apsReference"] = new Dictionary<string, object?>(),
            ["placementMode"] = "arbitrary",
        }));
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(invalidEnum.Errors).Code);
    }

    [Fact]
    public async Task Export_IsRefusedByTheExistingReadOnlyGateBeforeItsStubRuns()
    {
        var readOnly = new ReadOnlyModeService();
        readOnly.Set(true);
        var context = Context(new Dictionary<string, object?>
        {
            ["documentId"] = "doc-1",
            ["outputDir"] = "D:/owned/output",
            ["formats"] = new[] { "pdf" },
        }, readOnly: readOnly);

        var result = await ToolInvoker.Default.InvokeAsync(new PsExportDeliverablesTool(), context);
        Assert.Equal(ErrorCodes.PermissionDenied, Assert.Single(result.Errors).Code);
        Assert.Null(context.LastInvocationReceipt);
    }

    private static ToolExecutionContext Context(
        IReadOnlyDictionary<string, object?>? args = null,
        MCPSettings? settings = null,
        IReadOnlyModeService? readOnly = null)
        => new()
        {
            Arguments = args,
            Settings = settings ?? new MCPSettings { Host = "0.0.0.0" },
            ReadOnly = readOnly,
        };

    private static (IMCPTool Tool, Dictionary<string, object?> Args) ToolAndValidArguments(string name)
        => name switch
        {
            "ps_apply_design_recipe" => (new PsApplyDesignRecipeTool(), new Dictionary<string, object?>
            {
                ["documentId"] = "doc-1",
                ["apsReference"] = new Dictionary<string, object?>(),
                ["recipeId"] = "recipe-1",
            }),
            "ps_import_design_bundle" => (new PsImportDesignBundleTool(), new Dictionary<string, object?>
            {
                ["bundlePath"] = "D:/owned/bundle",
                ["documentId"] = "doc-1",
                ["apsReference"] = new Dictionary<string, object?>(),
            }),
            "ps_refresh_design_bundle" => (new PsRefreshDesignBundleTool(), new Dictionary<string, object?>
            {
                ["documentId"] = "doc-1",
                ["bundlePath"] = "D:/owned/bundle",
                ["apsReference"] = new Dictionary<string, object?>(),
            }),
            "ps_export_deliverables" => (new PsExportDeliverablesTool(), new Dictionary<string, object?>
            {
                ["documentId"] = "doc-1",
                ["outputDir"] = "D:/owned/output",
                ["formats"] = new[] { "pdf" },
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".runtime", "evolution", "v5-f", "run-20260928-d082", "f03b-5-schemas");
            if (Directory.Exists(candidate))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("The frozen D-082 PS schema directory was not found above the test output.");
    }

    private static string Canonical(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject()
                .Where(property => property.Name != "x-d082")
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => JsonSerializer.Serialize(property.Name) + ":" + Canonical(property.Value))) + "}",
            JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonical)) + "]",
            JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => throw new InvalidOperationException("Unexpected JSON schema token: " + element.ValueKind),
        };
}
