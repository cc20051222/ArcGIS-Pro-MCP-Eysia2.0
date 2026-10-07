using System.Text.Json;

namespace ArcGISProMCP.Protocol;

/// <summary>MCP 服务器信息。</summary>
public sealed class McpServerInfo
{
    public string Name { get; set; } = "arcgis-pro-mcp";
    public string Version { get; set; } = "0.1.0";
}

/// <summary>MCP 客户端信息。</summary>
public sealed class McpClientInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

/// <summary>MCP tools 能力声明。</summary>
public sealed class McpToolsCapability
{
    public bool ListChanged { get; set; }
}

/// <summary>MCP 服务器能力。</summary>
public sealed class McpServerCapabilities
{
    public McpToolsCapability Tools { get; set; } = new();
}

/// <summary>initialize 请求参数。</summary>
public sealed class McpInitializeParams
{
    public string? ProtocolVersion { get; set; }
    public JsonElement? Capabilities { get; set; }
    public McpClientInfo? ClientInfo { get; set; }
}

/// <summary>initialize 响应结果。</summary>
public sealed class McpInitializeResult
{
    public string ProtocolVersion { get; set; } = string.Empty;
    public McpServerCapabilities Capabilities { get; set; } = new();
    public McpServerInfo ServerInfo { get; set; } = new();
}

/// <summary>
/// MCP 文本内容块。
/// <para>D-063：显式 <c>JsonPropertyName</c> —— MCP wire 规范要求小写键（type/text），
/// 此前依赖调用方的 JsonSerializerOptions；显式声明后**任何序列化路径都合规**。</para>
/// </summary>
public sealed class McpTextContent
{
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// D-063 ★ 新能力面：MCP **图像内容块**（tools/call 结果可回传图像，兼容端 AI 可直接"看"图）。
/// 形态遵循 MCP 规范：{ type: "image", data: &lt;base64&gt;, mimeType: "image/png" }。
/// </summary>
public sealed class McpImageContent
{
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string Type { get; set; } = "image";

    /// <summary>标准 base64 编码的图像字节（**不含** <c>data:</c> 前缀）。</summary>
    [System.Text.Json.Serialization.JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "image/png";
}

/// <summary>tools/call 请求参数。</summary>
public sealed class McpToolCallParams
{
    public string? Name { get; set; }
    public IReadOnlyDictionary<string, object?>? Arguments { get; set; }
}

/// <summary>
/// tools/call 结果。
/// <para>D-063：Content 元素类型放宽为 <see cref="object"/>，以承载**多形态 content**
/// （<see cref="McpTextContent"/> 与 <see cref="McpImageContent"/>）；System.Text.Json 对
/// <c>object</c> 声明按**运行时类型**序列化，故 wire 形态仍严格符合 MCP 规范。</para>
/// </summary>
public sealed class McpToolCallResult
{
    public IReadOnlyList<object> Content { get; set; } = Array.Empty<object>();
    public bool IsError { get; set; }
}

/// <summary>tools/list 结果。</summary>
public sealed class McpToolsListResult
{
    public IReadOnlyList<McpToolDefinition> Tools { get; set; } = Array.Empty<McpToolDefinition>();
}
