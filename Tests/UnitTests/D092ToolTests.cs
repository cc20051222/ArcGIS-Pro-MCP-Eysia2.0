using ArcGISProMCP.Core.Hosting;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.TestSupport;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

/// <summary>D-092: M2 P3/P4 domain governance and data management tool contract paths and fail-closed cases.</summary>
public sealed class D092ToolTests
{
    private static ToolExecutionContext Ctx(
        Dictionary<string, object?>? args = null, IArcGISHost? host = null, bool readOnly = false)
    {
        var mode = new ReadOnlyModeService();
        if (readOnly) mode.Set(true);
        return new ToolExecutionContext
        {
            Host = host ?? new FakeArcGISHost(),
            Arguments = args ?? new Dictionary<string, object?>(),
            ReadOnly = mode,
        };
    }

    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] values)
        => values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

    private static string? Code(OperationResult<object?> result)
        => result.Success ? null : result.Errors.FirstOrDefault()?.Code;

    private static IArcGISHost HostWithDomain(DomainFake? fake = null)
        => new FakeArcGISHost(d092Domain: fake ?? new DomainFake());

    // ═══════════════════ create_domain ═══════════════════

    [Fact]
    public async Task CreateDomain_DelegatesValidCodedValueAndRejectsMissingWorkspace()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var codedValues = new object?[] { Args(("code", "A"), ("value", "Alpha")) };
        var good = await new CreateDomainTool().ExecuteAsync(
            Ctx(Args(("workspace", "C:\\data.gdb"), ("domainName", "Category"), ("domainType", "CodedValue"), ("codedValues", codedValues)), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.CreateDomainCalls);

        var bad = await new CreateDomainTool().ExecuteAsync(
            Ctx(Args(("domainName", "X"), ("domainType", "Range")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task CreateDomain_RejectsInvalidDomainType()
    {
        var host = HostWithDomain();
        var bad = await new CreateDomainTool().ExecuteAsync(
            Ctx(Args(("workspace", "W"), ("domainName", "D"), ("domainType", "Invalid")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task CreateDomain_CodedValueRequiresValues()
    {
        var host = HostWithDomain();
        var bad = await new CreateDomainTool().ExecuteAsync(
            Ctx(Args(("workspace", "W"), ("domainName", "D"), ("domainType", "CodedValue")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task CreateDomain_ReadOnlyRefuses()
    {
        var host = HostWithDomain();
        var result = await new CreateDomainTool().ExecuteAsync(
            Ctx(Args(("workspace", "W"), ("domainName", "D"), ("domainType", "Range"), ("range", Args(("min", 0), ("max", 100)))), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ update_domain ═══════════════════

    [Fact]
    public async Task UpdateDomain_DelegatesValidInputAndRejectsEmptyUpdate()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var codedValues = new object?[] { Args(("code", "B")) };
        var good = await new UpdateDomainTool().ExecuteAsync(
            Ctx(Args(("workspace", "W"), ("domainName", "D"), ("codedValues", codedValues)), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.UpdateDomainCalls);

        var bad = await new UpdateDomainTool().ExecuteAsync(
            Ctx(Args(("workspace", "W"), ("domainName", "D")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ delete_domain ═══════════════════

    [Fact]
    public async Task DeleteDomain_DelegatesValidInputAndRejectsMissingName()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var good = await new DeleteDomainTool().ExecuteAsync(
            Ctx(Args(("workspace", "W"), ("domainName", "D")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.DeleteDomainCalls);

        var bad = await new DeleteDomainTool().ExecuteAsync(Ctx(Args(("workspace", "W")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ assign_domain_to_field ═══════════════════

    [Fact]
    public async Task AssignDomainToField_DelegatesValidInputAndRejectsMissingField()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var good = await new AssignDomainToFieldTool().ExecuteAsync(
            Ctx(Args(("workspace", "W"), ("dataset", "D"), ("field", "F"), ("domainName", "Dom")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.AssignDomainCalls);

        var bad = await new AssignDomainToFieldTool().ExecuteAsync(
            Ctx(Args(("workspace", "W"), ("dataset", "D"), ("domainName", "Dom")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ remove_domain_from_field ═══════════════════

    [Fact]
    public async Task RemoveDomainFromField_DelegatesValidInputAndRejectsMissingDataset()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var good = await new RemoveDomainFromFieldTool().ExecuteAsync(
            Ctx(Args(("workspace", "W"), ("dataset", "D"), ("field", "F")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.RemoveDomainCalls);

        var bad = await new RemoveDomainFromFieldTool().ExecuteAsync(
            Ctx(Args(("workspace", "W"), ("field", "F")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ configure_subtypes ═══════════════════

    [Fact]
    public async Task ConfigureSubtypes_DelegatesValidInputAndRejectsEmptySubtypes()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var subtypes = new object?[] { Args(("code", 1), ("name", "Type1")) };
        var good = await new ConfigureSubtypesTool().ExecuteAsync(
            Ctx(Args(("dataset", "D"), ("subtypeField", "CATEGORY"), ("subtypes", subtypes)), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.ConfigureSubtypesCalls);

        var bad = await new ConfigureSubtypesTool().ExecuteAsync(
            Ctx(Args(("dataset", "D"), ("subtypeField", "F"), ("subtypes", Array.Empty<object?>())), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ create_relationship_class ═══════════════════

    [Fact]
    public async Task CreateRelationshipClass_DelegatesValidInputAndRejectsInvalidCardinality()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var good = await new CreateRelationshipClassTool().ExecuteAsync(
            Ctx(Args(("originTable", "A"), ("destinationTable", "B"), ("relationshipName", "R"), ("cardinality", "one_to_many")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.CreateRelClassCalls);

        var bad = await new CreateRelationshipClassTool().ExecuteAsync(
            Ctx(Args(("originTable", "A"), ("destinationTable", "B"), ("relationshipName", "R"), ("cardinality", "invalid")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ calculate_geometry_attributes ═══════════════════

    [Fact]
    public async Task CalculateGeometryAttributes_DelegatesValidInputAndRejectsEmptyFields()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var fields = new object?[] { Args(("field", "LENGTH"), ("property", "length")) };
        var good = await new CalculateGeometryAttributesTool().ExecuteAsync(
            Ctx(Args(("dataset", "D"), ("fields", fields), ("units", "meters")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.CalcGeomCalls);

        var bad = await new CalculateGeometryAttributesTool().ExecuteAsync(
            Ctx(Args(("dataset", "D"), ("fields", Array.Empty<object?>()), ("units", "m")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task CalculateGeometryAttributes_ReadOnlyRefuses()
    {
        var host = HostWithDomain();
        var fields = new object?[] { Args(("field", "X"), ("property", "x")) };
        var result = await new CalculateGeometryAttributesTool().ExecuteAsync(
            Ctx(Args(("dataset", "D"), ("fields", fields), ("units", "m")), host, readOnly: true));
        Assert.Equal(ErrorCodes.PermissionDenied, Code(result));
    }

    // ═══════════════════ define_projection ═══════════════════

    [Fact]
    public async Task DefineProjection_DelegatesValidInputAndRejectsMissingConfirm()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var good = await new DefineProjectionTool().ExecuteAsync(
            Ctx(Args(("dataset", "D"), ("crs", "4326"), ("confirmNoTransform", true)), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.DefineProjectionCalls);

        var bad = await new DefineProjectionTool().ExecuteAsync(
            Ctx(Args(("dataset", "D"), ("crs", "4326")), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task DefineProjection_RejectsConfirmFalse()
    {
        var host = HostWithDomain();
        var bad = await new DefineProjectionTool().ExecuteAsync(
            Ctx(Args(("dataset", "D"), ("crs", "4326"), ("confirmNoTransform", false)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    // ═══════════════════ validate_relationship_class ═══════════════════

    [Fact]
    public async Task ValidateRelationshipClass_DelegatesValidInputAndRejectsMissingName()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var good = await new ValidateRelationshipClassTool().ExecuteAsync(
            Ctx(Args(("relationshipName", "R1")), host));
        Assert.True(good.Success);
        Assert.Equal(1, fake.ValidateRelClassCalls);

        var bad = await new ValidateRelationshipClassTool().ExecuteAsync(Ctx(Args(), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
    }

    [Fact]
    public async Task ValidateRelationshipClass_RejectsInvalidMaxItems()
    {
        var host = HostWithDomain();
        var bad = await new ValidateRelationshipClassTool().ExecuteAsync(
            Ctx(Args(("relationshipName", "R"), ("maxItems", 0)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad));
        var bad2 = await new ValidateRelationshipClassTool().ExecuteAsync(
            Ctx(Args(("relationshipName", "R"), ("maxItems", 1001)), host));
        Assert.Equal(ErrorCodes.InvalidArgument, Code(bad2));
    }

    [Fact]
    public async Task ValidateRelationshipClass_NotRefusedInReadOnly()
    {
        var fake = new DomainFake();
        var host = HostWithDomain(fake);
        var good = await new ValidateRelationshipClassTool().ExecuteAsync(
            Ctx(Args(("relationshipName", "R1")), host, readOnly: true));
        Assert.True(good.Success); // R-tier: not refused in read-only
    }

    // ═══════════════════ Cross-cutting ═══════════════════

    [Fact]
    public async Task AllWriteTools_NoServiceReturnsNotImplemented()
    {
        var host = new FakeArcGISHost(); // no D092Domain
        var tools = new (McpToolBase tool, Dictionary<string, object?> args)[]
        {
            (new CreateDomainTool(), Args(("workspace", "W"), ("domainName", "D"), ("domainType", "Range"), ("range", Args(("min", 0))))),
            (new UpdateDomainTool(), Args(("workspace", "W"), ("domainName", "D"), ("range", Args(("min", 0))))),
            (new DeleteDomainTool(), Args(("workspace", "W"), ("domainName", "D"))),
            (new AssignDomainToFieldTool(), Args(("workspace", "W"), ("dataset", "D"), ("field", "F"), ("domainName", "Dom"))),
            (new RemoveDomainFromFieldTool(), Args(("workspace", "W"), ("dataset", "D"), ("field", "F"))),
            (new ConfigureSubtypesTool(), Args(("dataset", "D"), ("subtypeField", "F"), ("subtypes", new object?[] { Args(("code", 1)) }))),
            (new CreateRelationshipClassTool(), Args(("originTable", "A"), ("destinationTable", "B"), ("relationshipName", "R"), ("cardinality", "one_to_one"))),
            (new CalculateGeometryAttributesTool(), Args(("dataset", "D"), ("fields", new object?[] { Args(("field", "X")) }), ("units", "m"))),
            (new DefineProjectionTool(), Args(("dataset", "D"), ("crs", "4326"), ("confirmNoTransform", true))),
            (new ValidateRelationshipClassTool(), Args(("relationshipName", "R"))),
        };
        foreach (var (tool, args) in tools)
        {
            var result = await tool.ExecuteAsync(Ctx(args, host));
            Assert.Equal(ErrorCodes.NotImplemented, Code(result));
        }
    }

    [Fact]
    public void AllTenTools_HaveCorrectMetadata()
    {
        var tools = new McpToolBase[]
        {
            new CreateDomainTool(), new UpdateDomainTool(), new DeleteDomainTool(),
            new AssignDomainToFieldTool(), new RemoveDomainFromFieldTool(),
            new ConfigureSubtypesTool(), new CreateRelationshipClassTool(),
            new CalculateGeometryAttributesTool(), new DefineProjectionTool(),
            new ValidateRelationshipClassTool(),
        };
        Assert.Equal(10, tools.Length);
        foreach (var t in tools)
        {
            Assert.Equal(ToolCategories.DataManagement, t.Metadata.Category);
            Assert.True(t.Metadata.RequiresArcGIS);
            Assert.NotNull(t.InputSchema);
            Assert.Equal("object", t.InputSchema["type"]);
            Assert.Equal(false, t.InputSchema["additionalProperties"]);
        }
        // 8 GP + 2 Native
        Assert.Equal(8, tools.Count(t => t.Metadata.ExecutionType == ExecutionTypes.Geoprocessing));
        Assert.Equal(2, tools.Count(t => t.Metadata.ExecutionType == ExecutionTypes.Native));
    }

    [Fact]
    public void AllTenTools_InContractSnapshotWith224Total()
    {
        var snapshot = ProductionToolContractSnapshot.Tools;
        Assert.Equal(239, snapshot.Count);
        var names = new[]
        {
            "create_domain", "update_domain", "delete_domain", "assign_domain_to_field",
            "remove_domain_from_field", "configure_subtypes", "create_relationship_class",
            "calculate_geometry_attributes", "define_projection", "validate_relationship_class",
        };
        foreach (var name in names)
            Assert.Contains(snapshot, t => t.Name == name);
        Assert.Equal(snapshot.Count, snapshot.Select(t => t.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AllTenTools_ClassifiedCorrectlyInRoster()
    {
        var writeTools = new[]
        {
            "create_domain", "update_domain", "delete_domain", "assign_domain_to_field",
            "remove_domain_from_field", "configure_subtypes", "create_relationship_class",
            "calculate_geometry_attributes", "define_projection",
        };
        foreach (var name in writeTools)
            Assert.Equal(ToolWriteTier.Write, ToolWriteClassification.TierOf(name));
        Assert.Equal(ToolWriteTier.Read, ToolWriteClassification.TierOf("validate_relationship_class"));
    }

    // ═══════════════════ Fake ═══════════════════

    private sealed class DomainFake : ID092DomainService
    {
        public int CreateDomainCalls { get; private set; }
        public int UpdateDomainCalls { get; private set; }
        public int DeleteDomainCalls { get; private set; }
        public int AssignDomainCalls { get; private set; }
        public int RemoveDomainCalls { get; private set; }
        public int ConfigureSubtypesCalls { get; private set; }
        public int CreateRelClassCalls { get; private set; }
        public int CalcGeomCalls { get; private set; }
        public int DefineProjectionCalls { get; private set; }
        public int ValidateRelClassCalls { get; private set; }

        private static OperationResult<IReadOnlyDictionary<string, object?>> Ok()
            => OperationResult<IReadOnlyDictionary<string, object?>>.Ok(new Dictionary<string, object?> { ["status"] = "ok" });

        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CreateDomainAsync(string w, string d, string t, IReadOnlyList<object?> c, IReadOnlyDictionary<string, object?>? r, CancellationToken ct = default)
        { CreateDomainCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> UpdateDomainAsync(string w, string d, IReadOnlyList<object?>? c, IReadOnlyDictionary<string, object?>? r, CancellationToken ct = default)
        { UpdateDomainCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> DeleteDomainAsync(string w, string d, bool f, CancellationToken ct = default)
        { DeleteDomainCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> AssignDomainToFieldAsync(string w, string d, string f, string dom, bool ow, CancellationToken ct = default)
        { AssignDomainCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> RemoveDomainFromFieldAsync(string w, string d, string f, CancellationToken ct = default)
        { RemoveDomainCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ConfigureSubtypesAsync(string d, string sf, IReadOnlyList<object?> st, bool ce, CancellationToken ct = default)
        { ConfigureSubtypesCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CreateRelationshipClassAsync(string o, string dest, string rn, string card, string? fl, string? bl, bool attr, CancellationToken ct = default)
        { CreateRelClassCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> CalculateGeometryAttributesAsync(string d, IReadOnlyList<object?> f, string u, bool ow, CancellationToken ct = default)
        { CalcGeomCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> DefineProjectionAsync(string d, string crs, CancellationToken ct = default)
        { DefineProjectionCalls++; return Task.FromResult(Ok()); }
        public Task<OperationResult<IReadOnlyDictionary<string, object?>>> ValidateRelationshipClassAsync(string rn, string? ws, int max, CancellationToken ct = default)
        { ValidateRelClassCalls++; return Task.FromResult(Ok()); }
    }
}
