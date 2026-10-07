namespace ArcGISProMCP.UnitTests;

/// <summary>
/// D-055：**环境变量敏感**用例的隔离集合。
/// 允许范围 / 受保护根（<c>ARCGIS_PRO_MCP_ALLOWED_ROOTS</c> / <c>ARCGIS_PRO_MCP_PROTECTED_ROOTS</c>）是**进程级**状态，
/// 若与其它测试并行运行会互相干扰（实测：D-053 的 append 用例因允许范围生效而被误拒为 PATH_ESCAPE_REJECTED）。
/// <c>DisableParallelization = true</c> ⇒ 本集合不与任何其它集合并行执行（xUnit v2 语义），从根上消除该类干扰。
/// </summary>
[CollectionDefinition(EnvSensitiveCollection.Name, DisableParallelization = true)]
public sealed class EnvSensitiveCollection
{
    public const string Name = "D055EnvSensitive";
}
