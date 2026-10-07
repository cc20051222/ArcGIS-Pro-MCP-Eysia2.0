using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>
/// D-104 X15: read-only inspection of the deliberately narrow LAS 1.2 / point format 0 subset.
/// LAZ, COPC, other LAS versions and other point formats remain outside this verified subset.
/// </summary>
public sealed class InspectPointCloudTool : McpToolBase
{
    private const long DefaultMaxPoints = 10_000_000;

    public override string Name => "inspect_point_cloud";

    public override string Description =>
        "只读检查未压缩 LAS 1.2 点格式 0，返回点数、分类、回波、范围、CRS 元数据与 XY 范围密度；LAZ/COPC 不推定支持。";

    protected override string CategoryName => ToolCategories.Quality;

    public override IReadOnlyDictionary<string, object?> InputSchema => D104PointCloudSchemas.InspectPointCloud;

    public override async Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        var input = ToolArgs.GetString(context, "input");
        if (string.IsNullOrWhiteSpace(input))
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "input must be a non-empty point-cloud path.");

        var maxPoints = DefaultMaxPoints;
        if (context.Arguments?.TryGetValue("maxPoints", out var maxValue) == true
            && !TryReadInteger(maxValue, out maxPoints))
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxPoints must be a positive integer.");
        }

        if (maxPoints < 1)
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "maxPoints must be a positive integer.");

        var computeDensity = true;
        if (context.Arguments?.TryGetValue("computeDensity", out var densityValue) == true)
        {
            if (densityValue is not bool requestedDensity)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "computeDensity must be a boolean.");
            computeDensity = requestedDensity;
        }

        try
        {
            return await Las12PointCloudReader.ReadAsync(
                input.Trim(), maxPoints, computeDensity, context.CancellationToken).ConfigureAwait(false);
        }
        catch (UnsupportedPointCloudException ex)
        {
            return OperationResult<object?>.Fail(ErrorCodes.NotImplemented, ex.Message);
        }
        catch (FileNotFoundException)
        {
            return OperationResult<object?>.Fail(ErrorCodes.NotFound, "The point-cloud input file was not found.");
        }
        catch (DirectoryNotFoundException)
        {
            return OperationResult<object?>.Fail(ErrorCodes.NotFound, "The point-cloud input directory was not found.");
        }
        catch (UnauthorizedAccessException)
        {
            return OperationResult<object?>.Fail(ErrorCodes.PermissionDenied, "The point-cloud input cannot be read with the current file permissions.");
        }
        catch (InvalidDataException ex)
        {
            return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, ex.Message);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<object?>.Fail(ErrorCodes.Cancelled, "Point-cloud inspection was cancelled; no output file was written.");
        }
        catch (IOException ex)
        {
            return OperationResult<object?>.Fail(ErrorCodes.ExecutionFailed, "The point-cloud input could not be read.", ex.Message);
        }
    }

    private static bool TryReadInteger(object? value, out long result)
    {
        switch (value)
        {
            case byte v: result = v; return true;
            case sbyte v: result = v; return true;
            case short v: result = v; return true;
            case ushort v: result = v; return true;
            case int v: result = v; return true;
            case uint v: result = v; return true;
            case long v: result = v; return true;
            case ulong v when v <= long.MaxValue: result = (long)v; return true;
            case double v when double.IsFinite(v) && Math.Truncate(v) == v && v >= long.MinValue && v <= long.MaxValue:
                result = (long)v;
                return true;
            case decimal v when decimal.Truncate(v) == v && v >= long.MinValue && v <= long.MaxValue:
                result = (long)v;
                return true;
            case System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.Number:
                return element.TryGetInt64(out result);
            default:
                result = 0;
                return false;
        }
    }
}

internal static class D104PointCloudSchemas
{
    public static readonly IReadOnlyDictionary<string, object?> InspectPointCloud =
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["input"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["type"] = "string",
                    ["description"] = "点云路径（LAS/LAZ/COPC 分别声明支持，不相互推定）。",
                },
                ["computeDensity"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "是否计算密度（可能耗时）。",
                },
                ["maxPoints"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["type"] = "integer",
                    ["default"] = 10_000_000,
                    ["description"] = "采样/读取点数上限。",
                },
            },
            ["required"] = new[] { "input" },
            ["additionalProperties"] = false,
        };
}

internal static class Las12PointCloudReader
{
    private const int MinimumPublicHeaderLength = 227;
    private const int Las12PublicHeaderLength = 227;
    private const int VlrHeaderLength = 54;
    private const ushort PointFormat0RecordLength = 20;
    private const int PointReadBufferLength = 65_520; // divisible by the supported 20-byte record length

    public static async Task<OperationResult<object?>> ReadAsync(
        string path, long maxPoints, bool computeDensity, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: PointReadBufferLength,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (stream.Length < MinimumPublicHeaderLength)
            throw new InvalidDataException("The input is shorter than a LAS public header.");

        var header = new byte[MinimumPublicHeaderLength];
        await ReadExactlyAsync(stream, header, cancellationToken).ConfigureAwait(false);

        if (header[0] != (byte)'L' || header[1] != (byte)'A' || header[2] != (byte)'S' || header[3] != (byte)'F')
            throw new UnsupportedPointCloudException("The input does not have a LASF signature; no format support is inferred from its filename.");

        var headerSize = ReadUInt16(header, 94);
        var pointDataOffset = ReadUInt32(header, 96);
        var vlrCount = ReadUInt32(header, 100);
        var rawPointFormat = header[104];
        var pointRecordLength = ReadUInt16(header, 105);
        var legacyPointCount = ReadUInt32(header, 107);

        if (headerSize < MinimumPublicHeaderLength || headerSize > stream.Length)
            throw new InvalidDataException("The LAS public-header size is outside the input file.");
        if (pointDataOffset < headerSize || pointDataOffset > stream.Length)
            throw new InvalidDataException("The LAS point-data offset is outside the input file.");

        var vlrs = await ReadVlrsAsync(stream, headerSize, pointDataOffset, vlrCount, cancellationToken).ConfigureAwait(false);
        if (vlrs.IsCopc)
            throw new UnsupportedPointCloudException("COPC metadata was detected. COPC is not supported by this reader and is not inferred from LAS support.");
        if (vlrs.IsLaszip || (rawPointFormat & 0x80) != 0)
            throw new UnsupportedPointCloudException("LASzip-compressed point data was detected. LAZ is not supported by this reader and is not inferred from LAS support.");

        if (header[24] != 1 || header[25] != 2 || headerSize != Las12PublicHeaderLength)
            throw new UnsupportedPointCloudException("Only LAS version 1.2 with a 227-byte public header is supported by this reader.");
        if ((rawPointFormat & 0xC0) != 0 || rawPointFormat != 0)
            throw new UnsupportedPointCloudException("Only uncompressed LAS point data record format 0 is supported by this reader.");
        if (pointRecordLength != PointFormat0RecordLength)
            throw new UnsupportedPointCloudException("LAS point format 0 records with extra bytes are not supported by this reader.");

        var pointCount = (long)legacyPointCount;
        if (pointCount > maxPoints)
            return OperationResult<object?>.Fail(
                ErrorCodes.InvalidArgument,
                "The file point count exceeds maxPoints; no partial summary was returned.");

        long expectedEnd;
        try
        {
            expectedEnd = checked((long)pointDataOffset + pointCount * pointRecordLength);
        }
        catch (OverflowException)
        {
            throw new InvalidDataException("The LAS point-data extent overflows the supported file range.");
        }

        if (expectedEnd > stream.Length)
            throw new InvalidDataException("The LAS point records are truncated or inconsistent with the header count.");

        var scale = new[] { ReadDouble(header, 131), ReadDouble(header, 139), ReadDouble(header, 147) };
        var offset = new[] { ReadDouble(header, 155), ReadDouble(header, 163), ReadDouble(header, 171) };
        if (scale.Any(v => !double.IsFinite(v) || v <= 0) || offset.Any(v => !double.IsFinite(v)))
            throw new InvalidDataException("The LAS coordinate scale or offset is not finite and positive.");

        stream.Position = pointDataOffset;
        var classes = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var returnNumbers = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var numberOfReturns = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var pointFlags = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var bounds = new PointBounds();
        var buffer = new byte[PointReadBufferLength];
        var remaining = pointCount;
        var recordsPerChunk = buffer.Length / PointFormat0RecordLength;

        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recordsThisChunk = (int)Math.Min(recordsPerChunk, remaining);
            var bytesThisChunk = recordsThisChunk * PointFormat0RecordLength;
            await ReadExactlyAsync(stream, buffer.AsMemory(0, bytesThisChunk), cancellationToken).ConfigureAwait(false);

            for (var recordIndex = 0; recordIndex < recordsThisChunk; recordIndex++)
            {
                var recordOffset = recordIndex * PointFormat0RecordLength;
                var x = ReadInt32(buffer, recordOffset) * scale[0] + offset[0];
                var y = ReadInt32(buffer, recordOffset + 4) * scale[1] + offset[1];
                var z = ReadInt32(buffer, recordOffset + 8) * scale[2] + offset[2];
                if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
                    throw new InvalidDataException("A LAS point coordinate is not finite.");

                bounds.Add(x, y, z);
                var returnBits = buffer[recordOffset + 14];
                Add(returnNumbers, (byte)(returnBits & 0x07));
                Add(numberOfReturns, (byte)((returnBits >> 3) & 0x07));

                var rawClassification = buffer[recordOffset + 15];
                Add(classes, (byte)(rawClassification & 0x1F));
                Add(pointFlags, "synthetic", (rawClassification & 0x20) != 0);
                Add(pointFlags, "keyPoint", (rawClassification & 0x40) != 0);
                Add(pointFlags, "withheld", (rawClassification & 0x80) != 0);
            }

            remaining -= recordsThisChunk;
        }

        var actualBounds = bounds.ToDictionary();
        var xyArea = bounds.HasPoints ? (bounds.MaxX - bounds.MinX) * (bounds.MaxY - bounds.MinY) : 0d;
        var density = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = !computeDensity ? "not_requested" : bounds.HasPoints && xyArea > 0 ? "computed" : "undefined_degenerate_extent",
            ["value"] = computeDensity && bounds.HasPoints && xyArea > 0 ? pointCount / xyArea : null,
            ["method"] = "pointCount / (observed X span * observed Y span)",
            ["xyExtentAreaCoordinateUnitsSquared"] = bounds.HasPoints ? xyArea : null,
            ["units"] = "points per squared coordinate unit; CRS linear/angular units are not normalized",
        };

        var headerBounds = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["minX"] = ReadDouble(header, 187),
            ["maxX"] = ReadDouble(header, 179),
            ["minY"] = ReadDouble(header, 203),
            ["maxY"] = ReadDouble(header, 195),
            ["minZ"] = ReadDouble(header, 219),
            ["maxZ"] = ReadDouble(header, 211),
        };
        var headerBoundsMatch = bounds.HasPoints && bounds.MatchesHeader(header, scale.Max());

        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["format"] = "LAS",
            ["formatVersion"] = "1.2",
            ["pointDataRecordFormat"] = 0,
            ["compression"] = "none",
            ["supportScope"] = "This file was read as uncompressed LAS 1.2 point format 0 with 20-byte records.",
            ["notClaimedFormats"] = new[] { "LAZ", "COPC" },
            ["totalPointCount"] = pointCount,
            ["pointsRead"] = pointCount,
            ["sampled"] = false,
            ["maxPoints"] = maxPoints,
            ["classificationCounts"] = classes,
            ["returnNumberCounts"] = returnNumbers,
            ["numberOfReturnsCounts"] = numberOfReturns,
            ["pointFlags"] = pointFlags,
            ["bounds"] = actualBounds,
            ["headerBounds"] = headerBounds,
            ["headerBoundsMatch"] = headerBoundsMatch,
            ["coordinateScale"] = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["x"] = scale[0], ["y"] = scale[1], ["z"] = scale[2],
            },
            ["coordinateOffset"] = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["x"] = offset[0], ["y"] = offset[1], ["z"] = offset[2],
            },
            ["crs"] = vlrs.CrsSummary,
            ["density"] = density,
            ["fields"] = new[]
            {
                "x", "y", "z", "intensity", "returnNumber", "numberOfReturns",
                "scanDirectionFlag", "edgeOfFlightLine", "classification", "scanAngleRank",
                "userData", "pointSourceId",
            },
        };

        return OperationResult<object?>.Ok(
            result,
            "LAS 1.2 point format 0 inspected read-only. LAZ and COPC support remain unverified and are not inferred.");
    }

    private static async Task<VlrScan> ReadVlrsAsync(
        FileStream stream, long headerSize, long pointDataOffset, uint count, CancellationToken cancellationToken)
    {
        stream.Position = headerSize;
        var vlrHeader = new byte[VlrHeaderLength];
        var isCopc = false;
        var isLaszip = false;
        IReadOnlyDictionary<string, object?> crs = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "not_declared_in_supported_metadata",
            ["authority"] = null,
            ["code"] = null,
        };

        for (uint i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stream.Position + VlrHeaderLength > pointDataOffset)
                throw new InvalidDataException("The LAS VLR count extends past the point-data offset.");

            await ReadExactlyAsync(stream, vlrHeader, cancellationToken).ConfigureAwait(false);
            var userId = Encoding.ASCII.GetString(vlrHeader, 2, 16).Trim('\0', ' ');
            var recordId = ReadUInt16(vlrHeader, 18);
            var payloadLength = ReadUInt16(vlrHeader, 20);
            if (stream.Position + payloadLength > pointDataOffset)
                throw new InvalidDataException("A LAS VLR payload extends past the point-data offset.");

            if (string.Equals(userId, "copc", StringComparison.OrdinalIgnoreCase) && recordId == 1)
                isCopc = true;
            if (string.Equals(userId, "laszip encoded", StringComparison.OrdinalIgnoreCase) || recordId == 22204)
                isLaszip = true;

            if (string.Equals(userId, "LASF_Projection", StringComparison.OrdinalIgnoreCase)
                && recordId is 34735 or 2112)
            {
                var payload = new byte[payloadLength];
                await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
                crs = recordId == 34735 ? ParseGeoKeyDirectory(payload) : WktDeclaredSummary();
            }
            else
            {
                stream.Position += payloadLength;
            }
        }

        if (stream.Position > pointDataOffset)
            throw new InvalidDataException("The LAS VLR records extend past the point-data offset.");

        return new VlrScan(isCopc, isLaszip, crs);
    }

    private static IReadOnlyDictionary<string, object?> ParseGeoKeyDirectory(byte[] payload)
    {
        if (payload.Length < 8)
            throw new InvalidDataException("The GeoTIFF key directory VLR is truncated.");

        var keyCount = ReadUInt16(payload, 6);
        if (payload.Length < 8 + keyCount * 8)
            throw new InvalidDataException("The GeoTIFF key directory VLR has an invalid key count.");

        for (var i = 0; i < keyCount; i++)
        {
            var entryOffset = 8 + i * 8;
            var keyId = ReadUInt16(payload, entryOffset);
            var tiffTagLocation = ReadUInt16(payload, entryOffset + 2);
            var valueCount = ReadUInt16(payload, entryOffset + 4);
            var value = ReadUInt16(payload, entryOffset + 6);
            if (keyId is not (2048 or 3072) || tiffTagLocation != 0 || valueCount != 1)
                continue;

            var keyName = keyId == 3072 ? "ProjectedCSTypeGeoKey" : "GeographicTypeGeoKey";
            if (value > 0 && value < 32767)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "epsg_declared",
                    ["authority"] = "EPSG",
                    ["code"] = value,
                    ["source"] = keyName,
                };
            }

            if (value == 32767)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["status"] = "user_defined_crs",
                    ["authority"] = null,
                    ["code"] = null,
                    ["source"] = keyName,
                };
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "geo_keys_present_epsg_unresolved",
            ["authority"] = null,
            ["code"] = null,
            ["source"] = "GeoKeyDirectoryTag",
        };
    }

    private static IReadOnlyDictionary<string, object?> WktDeclaredSummary()
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "wkt_declared_epsg_unresolved",
            ["authority"] = null,
            ["code"] = null,
            ["source"] = "WKT VLR",
        };

    private static void Add(IDictionary<string, long> counts, byte value)
        => Add(counts, value.ToString(CultureInfo.InvariantCulture), true);

    private static void Add(IDictionary<string, long> counts, string key, bool present)
    {
        if (!present) return;
        counts.TryGetValue(key, out var current);
        counts[key] = current + 1;
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        => await ReadExactlyAsync(stream, buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.Slice(read), cancellationToken).ConfigureAwait(false);
            if (count == 0)
                throw new InvalidDataException("The LAS input ended before the requested header, VLR or point records were read.");
            read += count;
        }
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
        => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));

    private static uint ReadUInt32(byte[] bytes, int offset)
        => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));

    private static int ReadInt32(byte[] bytes, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));

    private static double ReadDouble(byte[] bytes, int offset)
        => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(offset, sizeof(long))));

    private sealed record VlrScan(bool IsCopc, bool IsLaszip, IReadOnlyDictionary<string, object?> CrsSummary);

    private sealed class PointBounds
    {
        public bool HasPoints { get; private set; }
        public double MinX { get; private set; }
        public double MaxX { get; private set; }
        public double MinY { get; private set; }
        public double MaxY { get; private set; }
        public double MinZ { get; private set; }
        public double MaxZ { get; private set; }

        public void Add(double x, double y, double z)
        {
            if (!HasPoints)
            {
                MinX = MaxX = x;
                MinY = MaxY = y;
                MinZ = MaxZ = z;
                HasPoints = true;
                return;
            }

            MinX = Math.Min(MinX, x);
            MaxX = Math.Max(MaxX, x);
            MinY = Math.Min(MinY, y);
            MaxY = Math.Max(MaxY, y);
            MinZ = Math.Min(MinZ, z);
            MaxZ = Math.Max(MaxZ, z);
        }

        public Dictionary<string, object?> ToDictionary()
            => HasPoints
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["minX"] = MinX, ["maxX"] = MaxX,
                    ["minY"] = MinY, ["maxY"] = MaxY,
                    ["minZ"] = MinZ, ["maxZ"] = MaxZ,
                }
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["minX"] = null, ["maxX"] = null,
                    ["minY"] = null, ["maxY"] = null,
                    ["minZ"] = null, ["maxZ"] = null,
                };

        public bool MatchesHeader(byte[] header, double scaleTolerance)
        {
            var tolerance = Math.Max(scaleTolerance, 1e-9);
            return Math.Abs(MinX - ReadDouble(header, 187)) <= tolerance
                && Math.Abs(MaxX - ReadDouble(header, 179)) <= tolerance
                && Math.Abs(MinY - ReadDouble(header, 203)) <= tolerance
                && Math.Abs(MaxY - ReadDouble(header, 195)) <= tolerance
                && Math.Abs(MinZ - ReadDouble(header, 219)) <= tolerance
                && Math.Abs(MaxZ - ReadDouble(header, 211)) <= tolerance;
        }
    }
}

internal sealed class UnsupportedPointCloudException : Exception
{
    public UnsupportedPointCloudException(string message) : base(message) { }
}
