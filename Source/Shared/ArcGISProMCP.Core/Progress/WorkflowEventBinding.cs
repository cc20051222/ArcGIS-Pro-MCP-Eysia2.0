using System.Text.Json;

namespace ArcGISProMCP.Core.Progress;

/// <summary>Only exact request IDs bind execution to a business step. Never grants verification.</summary>
public static class WorkflowEventBinding
{
    public static ToolProgressEvent? Latest(JsonElement step, IReadOnlyList<ToolProgressEvent> events)
    {
        if (!step.TryGetProperty("requestIds", out var ids) || ids.ValueKind != JsonValueKind.Array) return null;
        var requests = ids.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()).ToHashSet(StringComparer.Ordinal);
        return events.Where(e => e.RequestId is not null && requests.Contains(e.RequestId))
            .OrderBy(e => e.ObservedAt).ThenBy(e => e.Sequence).LastOrDefault();
    }

    public static string EffectiveState(JsonElement step, ToolProgressEvent? latest)
    {
        var recorded = step.TryGetProperty("state", out var s) ? s.GetString() ?? "UNKNOWN" : "UNKNOWN";
        if (latest is null || Latest(step, new[] { latest }) is null) return recorded;
        if (step.TryGetProperty("observedAt", out var stamp) && stamp.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(stamp.GetString(), out var observed) && observed >= latest.ObservedAt)
            return recorded;
        return latest.State is "RUNNING" or "QUEUED" or "PRODUCED" or "FAILED" or "UNKNOWN"
            ? latest.State : "UNKNOWN";
    }

    public static bool ChecksNeedRefresh(JsonElement step, ToolProgressEvent? latest)
        => latest is not null && step.TryGetProperty("state", out var state) && state.GetString() == "VERIFIED"
           && EffectiveState(step, latest) != "VERIFIED";
}
