namespace ArcGISProMCP.Core.Services;

/// <summary>不含原始异常、路径、用户名、payload 或 GIS 值的状态展示结果。</summary>
public sealed record StatusPresentation(
    string Title,
    string Summary,
    string Details,
    string RecommendedAction)
{
    public string ToDisplayText()
        => string.Join(
            Environment.NewLine,
            Title,
            Summary,
            string.Empty,
            Details,
            string.Empty,
            "Action: " + RecommendedAction);
}
