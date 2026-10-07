using System.Text.Json;
using ArcGISProMCP.Configuration;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

public sealed class D102PsToolTests
{
    private static readonly string[] ReadNames =
    [
        "ps_compare_document_versions",
        "ps_get_document_info",
        "ps_get_layer_info",
        "ps_list_documents",
        "ps_list_layers",
        "ps_validate_document",
    ];

    private static readonly string[] SessionNames =
    [
        "ps_create_adjustment_layer",
        "ps_manage_artboards",
        "ps_place_design_asset",
        "ps_restore_document_snapshot",
        "ps_set_layer_mask",
        "ps_set_layer_properties",
        "ps_set_text_properties",
    ];

    private static readonly string[] WriteNames =
    [
        "ps_create_document_snapshot",
        "ps_preview_document",
    ];

    public static IEnumerable<object[]> ValidToolCases =>
    [
        Case("ps_compare_document_versions", ("leftRef", "snapshot-left"), ("rightRef", "snapshot-right")),
        Case("ps_get_document_info", ("documentId", "doc-1")),
        Case("ps_get_layer_info", ("documentId", "doc-1"), ("layerId", "layer-1")),
        Case("ps_list_documents"),
        Case("ps_list_layers", ("documentId", "doc-1")),
        Case("ps_validate_document", ("documentId", "doc-1")),
        Case("ps_create_adjustment_layer", ("documentId", "doc-1"), ("kind", "levels"), ("apsReference", new Dictionary<string, object?>())),
        Case("ps_manage_artboards", ("documentId", "doc-1"), ("action", "list"), ("apsReference", new Dictionary<string, object?>())),
        Case("ps_place_design_asset", ("documentId", "doc-1"), ("assetPath", "D:/owned/asset.png"), ("apsReference", new Dictionary<string, object?>())),
        Case("ps_restore_document_snapshot", ("documentId", "doc-1"), ("snapshotId", "snap-1"), ("apsReference", new Dictionary<string, object?>())),
        Case("ps_set_layer_mask", ("documentId", "doc-1"), ("layerId", "layer-1"), ("maskSource", new Dictionary<string, object?>()), ("apsReference", new Dictionary<string, object?>())),
        Case("ps_set_layer_properties", ("documentId", "doc-1"), ("layerId", "layer-1"), ("properties", new Dictionary<string, object?>()), ("apsReference", new Dictionary<string, object?>())),
        Case("ps_set_text_properties", ("documentId", "doc-1"), ("layerId", "text-1"), ("properties", new Dictionary<string, object?>()), ("apsReference", new Dictionary<string, object?>())),
        Case("ps_create_document_snapshot", ("documentId", "doc-1"), ("outputPath", "D:/owned/snapshot.psd")),
        Case("ps_preview_document", ("documentId", "doc-1"), ("outputPath", "D:/owned/preview.png")),
    ];

    public static IEnumerable<object[]> SessionCases =>
        SessionNames.Select(name =>
        {
            var item = ValidToolCases.Single(test => StringComparer.Ordinal.Equals((string)test[0], name));
            return item;
        });

    public static IEnumerable<object[]> WriteCases =>
        WriteNames.Select(name =>
        {
            var item = ValidToolCases.Single(test => StringComparer.Ordinal.Equals((string)test[0], name));
            return item;
        });

    [Fact]
    public void ProductionRegistry_ContainsAllFifteenTools_AndReaches239()
    {
        var tools = ProductionCompositionAccess.BuildRegistry().List();
        var names = tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(239, tools.Count);
        Assert.Equal(239, names.Count);
        Assert.Equal(285, ToolWriteClassification.Total);
        Assert.Equal(94, ToolWriteClassification.ReadTools.Count);
        Assert.Equal(33, ToolWriteClassification.SessionTools.Count);
        Assert.Equal(158, ToolWriteClassification.WriteTools.Count);

        foreach (var name in ReadNames)
            Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf(name));
        foreach (var name in SessionNames)
            Assert.Equal(ToolWriteTier.SessionState, ToolWriteClassification.TierOf(name));
        foreach (var name in WriteNames)
            Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf(name));
    }

    [Fact]
    public void InputSchemas_MatchFrozenF03Schemas_ExceptGovernanceMetadata()
    {
        var tools = ProductionCompositionAccess.BuildRegistry().List()
            .Where(tool => ReadNames.Concat(SessionNames).Concat(WriteNames).Contains(tool.Name, StringComparer.Ordinal))
            .ToDictionary(tool => tool.Name, StringComparer.Ordinal);
        var schemaRoot = Path.Combine(
            FindWorkspaceRoot(), ".runtime", "evolution", "v5-f", "run-20260928-d082", "f03b-5-schemas");

        Assert.Equal(15, tools.Count);
        foreach (var name in ReadNames.Concat(SessionNames).Concat(WriteNames))
        {
            using var frozen = JsonDocument.Parse(File.ReadAllText(Path.Combine(schemaRoot, name + ".schema.json")));
            using var actual = JsonDocument.Parse(JsonSerializer.Serialize(tools[name].InputSchema));
            Assert.Equal(Canonical(frozen.RootElement), Canonical(actual.RootElement));
            Assert.False(actual.RootElement.GetProperty("additionalProperties").GetBoolean());
        }
    }

    [Theory]
    [MemberData(nameof(ValidToolCases))]
    public async Task EveryD102Tool_FailsClosedWithoutSendingOrApplyingRequests(
        string name,
        Dictionary<string, object?> arguments)
    {
        var tool = ProductionCompositionAccess.BuildRegistry().List().Single(item => item.Name == name);
        var result = await tool.ExecuteAsync(Context(arguments));

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.NotImplemented, error.Code);
        using var details = JsonDocument.Parse(error.Details!);
        Assert.Equal("NOT_VERIFIED", details.RootElement.GetProperty("status").GetString());
        Assert.Equal(name, details.RootElement.GetProperty("operation").GetString());
        Assert.False(details.RootElement.GetProperty("psRequestSent").GetBoolean());
        Assert.False(details.RootElement.GetProperty("sideEffects").GetBoolean());
    }

    [Theory]
    [MemberData(nameof(WriteCases))]
    public async Task WriteTools_AreRefusedInReadOnlyMode(
        string name,
        Dictionary<string, object?> arguments)
    {
        var readOnly = new ReadOnlyModeService();
        readOnly.Set(true);
        var tool = ProductionCompositionAccess.BuildRegistry().List().Single(item => item.Name == name);

        var result = await ToolInvoker.Default.InvokeAsync(tool, Context(arguments, readOnly));

        Assert.Equal(ErrorCodes.PermissionDenied, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [MemberData(nameof(SessionCases))]
    public async Task SessionStateTools_RemainAllowedInReadOnlyModeButStillFailClosed(
        string name,
        Dictionary<string, object?> arguments)
    {
        var readOnly = new ReadOnlyModeService();
        readOnly.Set(true);
        var tool = ProductionCompositionAccess.BuildRegistry().List().Single(item => item.Name == name);

        var result = await ToolInvoker.Default.InvokeAsync(tool, Context(arguments, readOnly));

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.NotImplemented, error.Code);
        using var details = JsonDocument.Parse(error.Details!);
        Assert.Equal("NOT_VERIFIED", details.RootElement.GetProperty("status").GetString());
        Assert.False(details.RootElement.GetProperty("psRequestSent").GetBoolean());
        Assert.False(details.RootElement.GetProperty("sideEffects").GetBoolean());
    }

    [Fact]
    public async Task ListDocuments_EnforcesFrozenNumericBoundsAndRejectsUnknownArguments()
    {
        var tool = ProductionCompositionAccess.BuildRegistry().List().Single(item => item.Name == "ps_list_documents");

        var outOfRange = await tool.ExecuteAsync(Context(new Dictionary<string, object?>
        {
            ["maxItems100"] = 501,
        }));
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(outOfRange.Errors).Code);

        var unknown = await tool.ExecuteAsync(Context(new Dictionary<string, object?>
        {
            ["unexpected"] = true,
        }));
        Assert.Equal(ErrorCodes.InvalidArgument, Assert.Single(unknown.Errors).Code);
    }

    private static object[] Case(string name, params (string Key, object? Value)[] pairs)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in pairs)
            arguments.Add(pair.Key, pair.Value);
        return [name, arguments];
    }

    private static ToolExecutionContext Context(
        IReadOnlyDictionary<string, object?> arguments,
        IReadOnlyModeService? readOnly = null)
        => new()
        {
            Arguments = arguments,
            Settings = new MCPSettings { Host = "127.0.0.1", Port = 6520 },
            ReadOnly = readOnly,
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
