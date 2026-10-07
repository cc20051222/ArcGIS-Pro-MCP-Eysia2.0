using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.TestSupport;

/// <summary>
/// Test-only GP recorder. It captures the production request exactly as received
/// and returns a configured result; it never invokes ArcGIS Pro.
/// </summary>
public sealed class RecordingGeoprocessingService : IGeoprocessingService
{
    public OperationResult<GeoprocessingResult> Result { get; set; } =
        OperationResult<GeoprocessingResult>.Ok(new GeoprocessingResult
        {
            ToolName = "test",
            Result = "ok"
        });

    public List<GeoprocessingRequest> Requests { get; } = new();
    public List<CancellationToken> CancellationTokens { get; } = new();

    /// <summary>D-021：存在性探测的返回值（默认 NotExists，等价于"输出不存在"的历史行为）。</summary>
    public OutputExistence ExistenceResult { get; set; } = OutputExistence.NotExists;

    /// <summary>D-021：探测附带说明（判定来源）。</summary>
    public string? ExistenceDetail { get; set; } = "fake-probe";

    /// <summary>D-021：置 true → 探测本身失败（验证"判定不可得 → 保守拒绝"）。</summary>
    public bool ExistenceProbeFails { get; set; }

    /// <summary>D-021：被探测过的输出路径（断言"进入 GP 之前确实探测过"）。</summary>
    public List<string> ExistenceChecks { get; } = new();

    public Task<OperationResult<OutputExistence>> CheckOutputExistsAsync(string outputPath, CancellationToken ct = default)
    {
        ExistenceChecks.Add(outputPath);
        return Task.FromResult(ExistenceProbeFails
            ? OperationResult<OutputExistence>.Fail(ErrorCodes.InternalError, "existence probe failed (test)")
            : OperationResult<OutputExistence>.Ok(ExistenceResult, ExistenceDetail));
    }

    public Task<OperationResult<GeoprocessingResult>> RunToolAsync(
        GeoprocessingRequest request,
        CancellationToken ct = default)
    {
        Requests.Add(new GeoprocessingRequest
        {
            ToolName = request.ToolName,
            Values = request.Values?.ToArray()
        });
        CancellationTokens.Add(ct);
        return Task.FromResult(Result);
    }

    public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByAttributeAsync(string? mapName, string layerName, string mode, System.Collections.Generic.IReadOnlyList<long>? oidList, string? where, CancellationToken ct = default)
        => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NotImplemented, "not implemented"));

    // ── D-062 · A 段录制/脚本 ──
    public GpRunRequest? LastRunRequest { get; private set; }
    public OperationResult<GpRunResult> RunResult { get; set; } =
        OperationResult<GpRunResult>.Ok(new GpRunResult { ToolName = "analysis.Buffer", DurationMs = 5, AuditPath = "audit", AuditEntryIndex = 1, ParameterForm = "named" });
    public GpWhitelist? WhitelistData { get; set; }
    public OperationResult<GpWhitelist>? WhitelistResult { get; set; }
    public OperationResult<GpMessagesInfo> MessagesResult { get; set; } =
        OperationResult<GpMessagesInfo>.Ok(new GpMessagesInfo());

    public Task<OperationResult<GpRunResult>> RunWhitelistedAsync(GpRunRequest request, CancellationToken ct = default)
    {
        LastRunRequest = request;
        return Task.FromResult(RunResult);
    }

    public Task<OperationResult<GpWhitelist>> GetWhitelistAsync(CancellationToken ct = default)
        => Task.FromResult(WhitelistResult
            ?? (WhitelistData is not null
                ? OperationResult<GpWhitelist>.Ok(WhitelistData)
                : OperationResult<GpWhitelist>.Fail(ErrorCodes.NotFound, "whitelist not scripted")));

    public Task<OperationResult<GpMessagesInfo>> GetLastMessagesAsync(CancellationToken ct = default)
        => Task.FromResult(MessagesResult);

    public Task<OperationResult<System.Text.Json.JsonElement?>> SelectLayerByLocationAsync(string? mapName, string layerName, string? selectingLayerName, string overlapType, double? searchDistance, string? searchDistanceUnit, string mode, CancellationToken ct = default)
        => Task.FromResult(OperationResult<System.Text.Json.JsonElement?>.Fail(ErrorCodes.NotImplemented, "not implemented"));
}
