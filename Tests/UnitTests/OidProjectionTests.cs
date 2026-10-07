using ArcGISProMCP.Core.Services;
using Xunit;

namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-022 / F2：<c>query_attributes</c> 的 OID 投影构造单测（Shared/Core，无 SDK 依赖）。
/// Keeper 裁定：OID **始终携带**；仅当数据源客观无 OID 时才允许 <c>oid:-1</c>，且必须显式披露。
/// </summary>
public sealed class OidProjectionTests
{
    private const string Oid = "OBJECTID";

    [Fact]
    public void Requested_Fields_Without_Oid_Get_Oid_Prepended()
    {
        var result = OidProjection.Build(new[] { "NAME", "POP" }, Oid);

        Assert.True(result.OidAvailable);
        Assert.True(result.OidAdded);
        Assert.Equal(Oid, result.OidField);
        // OID 置首位，且不得引入任何未请求的业务字段。
        Assert.Equal(new[] { "OBJECTID", "NAME", "POP" }, result.SubFields);
    }

    [Fact]
    public void Requested_Fields_Already_Containing_Oid_Are_Not_Duplicated()
    {
        var result = OidProjection.Build(new[] { "NAME", "objectid" }, Oid);

        Assert.True(result.OidAvailable);
        Assert.False(result.OidAdded);
        Assert.Equal(new[] { "NAME", "objectid" }, result.SubFields);
    }

    [Fact]
    public void Blank_Entries_Are_Filtered_And_Oid_Still_Applied()
    {
        var result = OidProjection.Build(new[] { "NAME", "  ", string.Empty, null! }, Oid);

        Assert.Equal(new[] { "OBJECTID", "NAME" }, result.SubFields);
    }

    [Fact]
    public void Null_Or_Empty_Request_Falls_Back_To_Oid_Only_Projection()
    {
        var empty = OidProjection.Build(Array.Empty<string>(), Oid);
        Assert.Equal(new[] { "OBJECTID" }, empty.SubFields);

        var nullRequest = OidProjection.Build(null, Oid);
        Assert.Equal(new[] { "OBJECTID" }, nullRequest.SubFields);
    }

    [Fact]
    public void Missing_Oid_Field_Is_Reported_As_Unavailable_Without_Inventing_A_Field()
    {
        var result = OidProjection.Build(new[] { "NAME" }, null);

        Assert.False(result.OidAvailable);
        Assert.False(result.OidAdded);
        Assert.Null(result.OidField);
        Assert.Equal(new[] { "NAME" }, result.SubFields);
    }

    [Fact]
    public void Non_Objectid_Oid_Field_Name_Is_Honoured()
    {
        // shapefage / 某些数据源的 OID 字段名可能不是 OBJECTID，须以实际字段名为准。
        var result = OidProjection.Build(new[] { "NAME" }, "FID");

        Assert.True(result.OidAvailable);
        Assert.Equal(new[] { "FID", "NAME" }, result.SubFields);
    }

    [Fact]
    public void Unavailable_Message_Is_Explicit_And_Mentions_Minus_One()
    {
        var message = OidProjection.UnavailableMessage();

        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.Contains("-1", message);
        Assert.Contains("ObjectID", message);
    }
}
