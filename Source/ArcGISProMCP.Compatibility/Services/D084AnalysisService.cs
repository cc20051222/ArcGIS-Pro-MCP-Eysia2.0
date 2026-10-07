using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>
/// D-084 read-only data analysis. All ArcGIS SDK object access is contained in QueuedTask.Run (Rule 4).
/// It never edits features or runs geoprocessing.
/// </summary>
public sealed class D084AnalysisService : ID084AnalysisService
{
    private const int MaxReturnedGroups = 1000;
    private const int MaxIssueEvidence = 100;

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> GetGeometryInfoAsync(
        string layerOrPath, string geometryUnit, int maxFeatures, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyDictionary<string, object?>>>(() =>
        {
            if (string.IsNullOrWhiteSpace(layerOrPath) || !Units.Contains(geometryUnit) || maxFeatures is < 1 or > 1_000_000)
                return Fail(ErrorCodes.InvalidArgument, "layer, supported geometryUnit and maxFeatures 1..1000000 are required.");
            return WithFeatureClass(layerOrPath, fc => ReadGeometryInfo(fc, layerOrPath, geometryUnit, maxFeatures, ct), ct);
        }, TaskCreationOptions.None);

    public Task<OperationResult<IReadOnlyDictionary<string, object?>>> FindIdenticalAsync(
        string layerOrPath, string mode, IReadOnlyList<string> fields, double tolerance, int maxFeatures, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<IReadOnlyDictionary<string, object?>>>(() =>
        {
            if (string.IsNullOrWhiteSpace(layerOrPath) || mode is not ("attributes" or "geometry" or "attributes+geometry")
                || !double.IsFinite(tolerance) || tolerance < 0 || maxFeatures is < 1 or > 1_000_000)
                return Fail(ErrorCodes.InvalidArgument, "layer, supported mode, finite non-negative tolerance and maxFeatures 1..1000000 are required.");
            return WithFeatureClass(layerOrPath, fc => ReadIdentical(fc, layerOrPath, mode, fields, tolerance, maxFeatures, ct), ct);
        }, TaskCreationOptions.None);

    public async Task<OperationResult<IReadOnlyDictionary<string, object?>>> GenerateQualityReportAsync(
        string dataset, IReadOnlyList<string> rules, string severityFloor, int maxFeatures, CancellationToken ct = default)
    {
        var supportedRules = new HashSet<string>(StringComparer.Ordinal)
        { "nulls", "duplicates", "domain", "subtype", "geometry", "topology", "units", "outliers", "crs" };
        if (string.IsNullOrWhiteSpace(dataset) || severityFloor is not ("info" or "warning" or "error") || maxFeatures is < 1 or > 1_000_000)
            return Fail(ErrorCodes.InvalidArgument, "dataset, supported severityFloor and maxFeatures 1..1000000 are required.");
        if (rules.Any(r => !supportedRules.Contains(r)))
            return Fail(ErrorCodes.InvalidArgument, "rules contains an unsupported quality rule.");

        var checks = new Dictionary<string, object?>(StringComparer.Ordinal);
        var findings = new List<Dictionary<string, object?>>();
        IReadOnlyDictionary<string, object?>? geometry = null;
        long total = 0;
        var scanned = 0;
        var truncated = false;

        if (rules.Contains("geometry", StringComparer.Ordinal) || rules.Contains("crs", StringComparer.Ordinal) || rules.Contains("units", StringComparer.Ordinal))
        {
            var gr = await GetGeometryInfoAsync(dataset, "layer", maxFeatures, ct).ConfigureAwait(false);
            if (!gr.Success || gr.Data is null) return Propagate(gr);
            geometry = gr.Data;
            total = ReadLong(geometry, "totalFeatureCount");
            scanned = ReadInt(geometry, "scannedCount");
            truncated = ReadBool(geometry, "truncated");
            if (rules.Contains("geometry", StringComparer.Ordinal))
            {
                var empty = ReadInt(geometry, "emptyGeometryCount");
                checks["geometry"] = new Dictionary<string, object?>
                {
                    ["status"] = "evaluated", ["emptyGeometryCount"] = empty,
                    ["sampledCount"] = scanned, ["totalFeatureCount"] = total,
                    ["countBasis"] = ReadString(geometry, "countBasis"),
                };
                if (empty > 0)
                {
                    var emptyIds = geometry["emptyGeometryObjectIds"] as IReadOnlyList<long> ?? Array.Empty<long>();
                    findings.Add(Issue("error", "EMPTY_GEOMETRY", empty, "Features have empty geometry.",
                        ReadString(geometry, "shapeField"), emptyIds, dataset, ReadBool(geometry, "emptyGeometryObjectIdsTruncated")));
                }
            }
            if (rules.Contains("crs", StringComparer.Ordinal))
            {
                var sr = ReadString(geometry, "spatialReference");
                checks["crs"] = new Dictionary<string, object?> { ["status"] = "evaluated", ["spatialReference"] = sr };
                if (string.IsNullOrWhiteSpace(sr)) findings.Add(Issue("warning", "MISSING_SPATIAL_REFERENCE", 1,
                    "Spatial reference is unavailable.", dataset: dataset));
            }
            if (rules.Contains("units", StringComparer.Ordinal))
            {
                checks["units"] = new Dictionary<string, object?> { ["status"] = "evaluated", ["coordinateUnit"] = ReadString(geometry, "coordinateUnit"), ["areaUnit"] = ReadString(geometry, "areaUnit") };
            }
        }

        if (rules.Contains("nulls", StringComparer.Ordinal))
        {
            var nullResult = await QueuedTask.Run<OperationResult<IReadOnlyDictionary<string, object?>>>(() =>
                WithFeatureClass(dataset, fc => ReadNullCounts(fc, maxFeatures, ct), ct), TaskCreationOptions.None).ConfigureAwait(false);
            if (!nullResult.Success || nullResult.Data is null) return Propagate(nullResult);
            var n = nullResult.Data;
            total = ReadLong(n, "totalFeatureCount");
            scanned = ReadInt(n, "scannedCount");
            truncated = ReadBool(n, "truncated");
            checks["nulls"] = new Dictionary<string, object?> { ["status"] = "evaluated", ["fields"] = n["fields"], ["scannedCount"] = scanned };
            if (n["fields"] is IEnumerable<Dictionary<string, object?>> fieldNulls)
            {
                foreach (var f in fieldNulls)
                {
                    var count = Convert.ToInt32(f["nullCount"], CultureInfo.InvariantCulture);
                    if (count > 0)
                    {
                        var ids = f["objectIds"] as IReadOnlyList<long> ?? Array.Empty<long>();
                        findings.Add(Issue("warning", "NULL_VALUES", count, "Null values in field " + f["field"] + ".",
                            f["field"]?.ToString(), ids, dataset, f.TryGetValue("objectIdsTruncated", out var truncatedIds) && truncatedIds is true));
                    }
                }
            }
        }

        if (rules.Contains("duplicates", StringComparer.Ordinal))
        {
            var dr = await FindIdenticalAsync(dataset, "attributes", Array.Empty<string>(), 0, maxFeatures, ct).ConfigureAwait(false);
            if (!dr.Success || dr.Data is null) return Propagate(dr);
            total = Math.Max(total, ReadLong(dr.Data, "totalFeatureCount"));
            scanned = Math.Max(scanned, ReadInt(dr.Data, "scannedCount"));
            truncated |= ReadBool(dr.Data, "truncated");
            checks["duplicates"] = new Dictionary<string, object?>
            {
                ["status"] = "evaluated", ["duplicateGroupCount"] = dr.Data["duplicateGroupCount"],
                ["duplicateFeatureCount"] = dr.Data["duplicateFeatureCount"], ["groups"] = dr.Data["groups"],
            };
            var duplicates = ReadInt(dr.Data, "duplicateFeatureCount");
            if (duplicates > 0)
            {
                var duplicateGroups = dr.Data["groups"] as IEnumerable<Dictionary<string, object?>>
                    ?? Array.Empty<Dictionary<string, object?>>();
                var duplicateIds = duplicateGroups
                    .SelectMany(g => g.TryGetValue("objectIds", out var ids) && ids is IEnumerable<long> values ? values : Array.Empty<long>())
                    .Take(MaxIssueEvidence + 1).ToList();
                var evidenceTruncated = duplicateIds.Count > MaxIssueEvidence;
                findings.Add(Issue("warning", "DUPLICATE_FEATURES", duplicates, "Duplicate attribute rows were found.",
                    dataset: dataset, objectIds: duplicateIds.Take(MaxIssueEvidence).ToList(), objectIdsTruncated: evidenceTruncated));
            }
        }

        if (rules.Contains("domain", StringComparer.Ordinal) || rules.Contains("subtype", StringComparer.Ordinal))
        {
            var metadataResult = await QueuedTask.Run<OperationResult<IReadOnlyDictionary<string, object?>>>(() =>
                WithFeatureClass(dataset, fc => ReadDomainSubtype(fc, maxFeatures, rules.Contains("domain", StringComparer.Ordinal),
                    rules.Contains("subtype", StringComparer.Ordinal), ct), ct), TaskCreationOptions.None).ConfigureAwait(false);
            if (!metadataResult.Success || metadataResult.Data is null) return Propagate(metadataResult);
            var metadata = metadataResult.Data;
            total = Math.Max(total, ReadLong(metadata, "totalFeatureCount"));
            scanned = Math.Max(scanned, ReadInt(metadata, "scannedCount"));
            truncated |= ReadBool(metadata, "truncated");

            if (rules.Contains("domain", StringComparer.Ordinal))
            {
                checks["domain"] = metadata["domainCheck"];
                if (metadata["domainIssues"] is IEnumerable<Dictionary<string, object?>> domainIssues)
                {
                    foreach (var issue in domainIssues)
                    {
                        var field = (string)issue["field"]!;
                        var count = Convert.ToInt32(issue["count"], CultureInfo.InvariantCulture);
                        var ids = (IReadOnlyList<long>)issue["objectIds"]!;
                        findings.Add(Issue("error", "DOMAIN_VALUE_OUTSIDE_DOMAIN", count,
                            "Values fall outside the assigned domain in field " + field + ".", field, ids, dataset,
                            (bool)issue["objectIdsTruncated"]!));
                    }
                }
            }

            if (rules.Contains("subtype", StringComparer.Ordinal))
            {
                checks["subtype"] = metadata["subtypeCheck"];
                if (metadata["subtypeIssue"] is Dictionary<string, object?> subtypeIssue)
                {
                    var ids = (IReadOnlyList<long>)subtypeIssue["objectIds"]!;
                    findings.Add(Issue("error", "INVALID_SUBTYPE_CODE",
                        Convert.ToInt32(subtypeIssue["count"], CultureInfo.InvariantCulture),
                        "Rows contain subtype codes that are not defined by the dataset.",
                        (string?)subtypeIssue["field"], ids, dataset, (bool)subtypeIssue["objectIdsTruncated"]!));
                }
            }
        }

        foreach (var rule in rules.Where(r => !checks.ContainsKey(r)))
        {
            checks[rule] = new Dictionary<string, object?>
            {
                ["status"] = "not_evaluated",
                ["reason"] = rule switch
                {
                    "domain" => "Domain value evaluation is not implemented in D-084.",
                    "subtype" => "Subtype evaluation is not implemented in D-084.",
                    "topology" => "Topology rules are not accessible through this read-only dataset scan.",
                    "outliers" => "Outlier policy is not defined by the frozen D-084 schema.",
                    _ => "Rule was not evaluated by this D-084 implementation.",
                },
            };
        }

        static int SeverityRank(string s) => s switch { "info" => 0, "warning" => 1, "error" => 2, _ => int.MaxValue };
        var included = findings.Where(x => SeverityRank((string)x["severity"]!) >= SeverityRank(severityFloor)).ToList();
        var report = new Dictionary<string, object?>
        {
            ["dataset"] = dataset,
            ["checks"] = checks,
            ["issues"] = included,
            ["issueCount"] = included.Sum(x => Convert.ToInt32(x["count"], CultureInfo.InvariantCulture)),
            ["totalFeatureCount"] = total,
            ["scannedCount"] = scanned,
            ["countBasis"] = truncated ? "sample" : "total",
            ["truncated"] = truncated,
            ["severityFloor"] = severityFloor,
        };
        return OperationResult<IReadOnlyDictionary<string, object?>>.Ok(report);
    }

    private static OperationResult<IReadOnlyDictionary<string, object?>> ReadDomainSubtype(
        FeatureClass fc, int max, bool evaluateDomains, bool evaluateSubtypes, CancellationToken ct)
    {
        using var definition = fc.GetDefinition();
        var fields = definition.GetFields()
            .Where(f => f.FieldType is not (FieldType.OID or FieldType.Geometry or FieldType.Blob or FieldType.Raster))
            .ToList();
        var oidField = definition.GetObjectIDField();
        var subtypeField = definition.GetSubtypeField();
        var hasSubtypeField = !string.IsNullOrWhiteSpace(subtypeField);
        var subtypeMetadataAvailable = true;
        var subtypeMetadataReason = (string?)null;
        var subtypeCodes = new HashSet<int>();
        var domainConstraints = new Dictionary<string, Dictionary<int, DomainConstraint>>(StringComparer.Ordinal);
        var unsupportedDomainFields = new List<string>();
        const int DefaultSubtypeKey = int.MinValue;

        if (hasSubtypeField && (evaluateSubtypes || evaluateDomains))
        {
            try
            {
                foreach (var subtype in definition.GetSubtypes())
                {
                    ct.ThrowIfCancellationRequested();
                    var subtypeCode = subtype.GetCode();
                    subtypeCodes.Add(subtypeCode);
                    if (!evaluateDomains) continue;
                    foreach (var field in fields)
                    {
                        using var domain = field.GetDomain(subtype);
                        AddDomainConstraint(field.Name, subtypeCode, domain, domainConstraints, unsupportedDomainFields);
                    }
                }
            }
            catch (NotSupportedException ex)
            {
                subtypeMetadataAvailable = false;
                subtypeMetadataReason = ex.Message;
            }
        }

        if (evaluateDomains && (!hasSubtypeField || !subtypeMetadataAvailable || subtypeCodes.Count == 0))
        {
            foreach (var field in fields)
            {
                using var domain = field.GetDomain(null);
                AddDomainConstraint(field.Name, DefaultSubtypeKey, domain, domainConstraints, unsupportedDomainFields);
            }
        }

        var domainCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var domainIds = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        var domainIdsTruncated = new Dictionary<string, bool>(StringComparer.Ordinal);
        var invalidSubtypeCount = 0;
        var invalidSubtypeIds = new List<long>();
        var subtypeIdsTruncated = false;
        var unassessedDomainValueCount = 0;
        var total = fc.GetCount();
        var limit = (int)Math.Min(total, max);
        var scanned = 0;
        var subFields = fields.Where(f =>
                domainConstraints.TryGetValue(f.Name, out var constraints) && constraints.Count > 0)
            .Select(f => f.Name).ToList();
        if (hasSubtypeField) subFields.Add(subtypeField!);
        if (!string.IsNullOrWhiteSpace(oidField)) subFields.Add(oidField);
        subFields = subFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        using (var cursor = fc.Search(new QueryFilter { SubFields = string.Join(",", subFields) }, true))
        {
            while (scanned < limit && cursor.MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                using var row = cursor.Current;
                if (row is null) continue;
                scanned++;
                var oid = ReadOid(row, oidField);
                var rowSubtypeCode = DefaultSubtypeKey;
                var rowSubtypeValid = true;
                if (hasSubtypeField && (evaluateSubtypes || evaluateDomains))
                {
                    try
                    {
                        var rawSubtype = row[subtypeField!];
                        rowSubtypeCode = rawSubtype is null or DBNull
                            ? DefaultSubtypeKey
                            : Convert.ToInt32(rawSubtype, CultureInfo.InvariantCulture);
                        rowSubtypeValid = subtypeMetadataAvailable && subtypeCodes.Contains(rowSubtypeCode);
                    }
                    catch
                    {
                        rowSubtypeValid = false;
                    }
                    if (evaluateSubtypes && subtypeMetadataAvailable && !rowSubtypeValid)
                    {
                        invalidSubtypeCount++;
                        if (invalidSubtypeIds.Count < MaxIssueEvidence) invalidSubtypeIds.Add(oid);
                        else subtypeIdsTruncated = true;
                    }
                }

                if (!evaluateDomains) continue;
                foreach (var field in fields)
                {
                    if (!domainConstraints.TryGetValue(field.Name, out var constraints) || constraints.Count == 0) continue;
                    if (hasSubtypeField && !rowSubtypeValid)
                    {
                        unassessedDomainValueCount++;
                        continue;
                    }
                    var key = hasSubtypeField ? rowSubtypeCode : DefaultSubtypeKey;
                    if (!constraints.TryGetValue(key, out var constraint)) continue;
                    var value = row[field.Name];
                    if (value is null or DBNull) continue;
                    var valid = IsDomainValueValid(constraint, value);
                    if (!valid.HasValue)
                    {
                        unassessedDomainValueCount++;
                        continue;
                    }
                    if (valid.Value) continue;
                    domainCounts[field.Name] = domainCounts.TryGetValue(field.Name, out var current) ? current + 1 : 1;
                    if (!domainIds.TryGetValue(field.Name, out var ids)) domainIds[field.Name] = ids = new List<long>();
                    if (ids.Count < MaxIssueEvidence) ids.Add(oid);
                    else domainIdsTruncated[field.Name] = true;
                }
            }
        }

        var domainFieldReports = domainCounts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new Dictionary<string, object?>
        {
            ["field"] = p.Key,
            ["violationCount"] = p.Value,
            ["objectIds"] = domainIds[p.Key],
            ["objectIdsTruncated"] = domainIdsTruncated.TryGetValue(p.Key, out var wasTruncated) && wasTruncated,
        }).ToList();
        var hasUnassessedDomainValues = unassessedDomainValueCount > 0 || unsupportedDomainFields.Count > 0;
        var result = new Dictionary<string, object?>
        {
            ["totalFeatureCount"] = total,
            ["scannedCount"] = scanned,
            ["countBasis"] = total > scanned ? "sample" : "total",
            ["truncated"] = total > scanned,
            ["domainCheck"] = new Dictionary<string, object?>
            {
                ["status"] = hasUnassessedDomainValues ? "partially_evaluated" : "evaluated",
                ["fieldViolationCount"] = domainFieldReports.Count,
                ["unassessedValueCount"] = unassessedDomainValueCount,
                ["unsupportedFields"] = unsupportedDomainFields.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                ["fields"] = domainFieldReports,
                ["scannedCount"] = scanned,
                ["totalFeatureCount"] = total,
                ["countBasis"] = total > scanned ? "sample" : "total",
            },
            ["subtypeCheck"] = new Dictionary<string, object?>
            {
                ["status"] = hasSubtypeField && !subtypeMetadataAvailable ? "not_evaluated" : "evaluated",
                ["reason"] = subtypeMetadataReason,
                ["applicable"] = hasSubtypeField,
                ["subtypeField"] = hasSubtypeField ? subtypeField : null,
                ["definedSubtypeCount"] = subtypeCodes.Count,
                ["invalidCodeCount"] = invalidSubtypeCount,
                ["invalidObjectIds"] = invalidSubtypeIds,
                ["invalidObjectIdsTruncated"] = subtypeIdsTruncated,
                ["scannedCount"] = scanned,
                ["totalFeatureCount"] = total,
                ["countBasis"] = total > scanned ? "sample" : "total",
            },
            ["domainIssues"] = domainFieldReports.Select(f => new Dictionary<string, object?>
            {
                ["field"] = f["field"], ["count"] = f["violationCount"], ["objectIds"] = f["objectIds"],
                ["objectIdsTruncated"] = f["objectIdsTruncated"],
            }).ToList(),
            ["subtypeIssue"] = invalidSubtypeCount > 0 && hasSubtypeField
                ? new Dictionary<string, object?>
                {
                    ["field"] = subtypeField, ["count"] = invalidSubtypeCount, ["objectIds"] = invalidSubtypeIds,
                    ["objectIdsTruncated"] = subtypeIdsTruncated,
                }
                : null,
        };
        return Ok(result);
    }

    private static void AddDomainConstraint(
        string fieldName, int subtypeCode, Domain? domain,
        IDictionary<string, Dictionary<int, DomainConstraint>> constraints,
        ICollection<string> unsupportedFields)
    {
        if (domain is null) return;
        DomainConstraint? constraint = domain switch
        {
            CodedValueDomain coded => new DomainConstraint(coded.GetName() ?? string.Empty,
                coded.GetCodedValuePairs().Keys.ToHashSet()),
            RangeDomain range => new DomainConstraint(range.GetName() ?? string.Empty,
                range.GetMinValue(), range.GetMaxValue()),
            _ => null,
        };
        if (constraint is null)
        {
            unsupportedFields.Add(fieldName);
            return;
        }
        if (!constraints.TryGetValue(fieldName, out var bySubtype)) constraints[fieldName] = bySubtype = new Dictionary<int, DomainConstraint>();
        bySubtype[subtypeCode] = constraint;
    }

    private static bool? IsDomainValueValid(DomainConstraint constraint, object value)
    {
        if (constraint.CodedValues is not null)
            return constraint.CodedValues.Any(code => DomainValuesEqual(code, value));
        if (constraint.Minimum is null || constraint.Maximum is null) return null;
        try
        {
            if (value is not IComparable comparable || value.GetType() != constraint.Minimum.GetType()
                || value.GetType() != constraint.Maximum.GetType()) return null;
            return comparable.CompareTo(constraint.Minimum) >= 0 && comparable.CompareTo(constraint.Maximum) <= 0;
        }
        catch (ArgumentException) { return null; }
        catch (InvalidCastException) { return null; }
    }

    private static bool DomainValuesEqual(object left, object right)
    {
        if (Equals(left, right)) return true;
        if (!IsNumeric(left) || !IsNumeric(right)) return false;
        try { return Convert.ToDecimal(left, CultureInfo.InvariantCulture) == Convert.ToDecimal(right, CultureInfo.InvariantCulture); }
        catch (OverflowException) { return Convert.ToDouble(left, CultureInfo.InvariantCulture).Equals(Convert.ToDouble(right, CultureInfo.InvariantCulture)); }
    }

    private static bool IsNumeric(object value) => Type.GetTypeCode(value.GetType()) is
        TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or
        TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

    private static OperationResult<IReadOnlyDictionary<string, object?>> ReadGeometryInfo(
        FeatureClass fc, string target, string requestedUnit, int max, CancellationToken ct)
    {
        using var definition = fc.GetDefinition();
        var shapeField = definition.GetShapeField();
        if (string.IsNullOrWhiteSpace(shapeField)) return Fail(ErrorCodes.InvalidArgument, "Target has no geometry field.");
        var oidField = definition.GetObjectIDField();
        var sr = definition.GetSpatialReference();
        var unitName = sr?.Unit?.Name ?? "unknown coordinate units";
        var factor = sr?.Unit?.ConversionFactor ?? double.NaN;
        if (requestedUnit != "layer" && (sr is null || sr.IsGeographic || !double.IsFinite(factor) || factor <= 0))
            return Fail(ErrorCodes.InvalidArgument, "Requested linear/area conversion requires a projected spatial reference with a known unit; no silent conversion was applied.");

        var total = fc.GetCount();
        var limit = (int)Math.Min(total, max);
        var scanned = 0;
        var empty = 0;
        var emptyObjectIds = new List<long>();
        var emptyObjectIdsTruncated = false;
        double? xmin = null, ymin = null, xmax = null, ymax = null;
        double lengthTotal = 0, areaTotal = 0;
        var conversion = ResolveFactors(requestedUnit, factor, unitName);
        var subFields = new[] { shapeField, oidField }.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        using (var cursor = fc.Search(new QueryFilter { SubFields = string.Join(",", subFields) }, true))
        {
            while (scanned < limit && cursor.MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                using var row = cursor.Current;
                scanned++;
                if (row is not Feature feature) continue;
                var geometry = feature.GetShape();
                if (geometry is null || geometry.IsEmpty)
                {
                    empty++;
                    if (emptyObjectIds.Count < MaxIssueEvidence) emptyObjectIds.Add(ReadOid(row, oidField));
                    else emptyObjectIdsTruncated = true;
                    continue;
                }
                var extent = geometry.Extent;
                if (extent is not null)
                {
                    xmin = xmin.HasValue ? Math.Min(xmin.Value, extent.XMin) : extent.XMin;
                    ymin = ymin.HasValue ? Math.Min(ymin.Value, extent.YMin) : extent.YMin;
                    xmax = xmax.HasValue ? Math.Max(xmax.Value, extent.XMax) : extent.XMax;
                    ymax = ymax.HasValue ? Math.Max(ymax.Value, extent.YMax) : extent.YMax;
                }
                var len = GeometryEngine.Instance.Length(geometry);
                var area = GeometryEngine.Instance.Area(geometry);
                lengthTotal += len * conversion.LengthFactor;
                areaTotal += area * conversion.AreaFactor;
            }
        }

        var payload = new Dictionary<string, object?>
        {
            ["layer"] = target,
            ["geometryType"] = definition.GetShapeType().ToString(),
            ["shapeField"] = shapeField,
            ["spatialReference"] = sr?.Name ?? (sr is null ? null : sr.Wkid.ToString(CultureInfo.InvariantCulture)),
            ["coordinateUnit"] = unitName,
            ["geometryUnit"] = requestedUnit,
            ["lengthUnit"] = conversion.LengthUnit,
            ["areaUnit"] = conversion.AreaUnit,
            ["totalFeatureCount"] = total,
            ["scannedCount"] = scanned,
            ["countBasis"] = total > scanned ? "sample" : "total",
            ["truncated"] = total > scanned,
            ["emptyGeometryCount"] = empty,
            ["emptyGeometryObjectIds"] = emptyObjectIds,
            ["emptyGeometryObjectIdsTruncated"] = emptyObjectIdsTruncated,
            ["extent"] = xmin.HasValue ? new Dictionary<string, object?> { ["xmin"] = xmin, ["ymin"] = ymin, ["xmax"] = xmax, ["ymax"] = ymax } : null,
            ["lengthTotal"] = lengthTotal,
            ["areaTotal"] = areaTotal,
        };
        return Ok(payload);
    }

    private static OperationResult<IReadOnlyDictionary<string, object?>> ReadIdentical(
        FeatureClass fc, string target, string mode, IReadOnlyList<string> requestedFields, double tolerance, int max, CancellationToken ct)
    {
        using var definition = fc.GetDefinition();
        var shapeName = definition.GetShapeField();
        var oidField = definition.GetObjectIDField();
        var allFields = definition.GetFields();
        var comparable = allFields.Where(f => f.FieldType is not (FieldType.OID or FieldType.Geometry or FieldType.Blob or FieldType.Raster))
            .Select(f => f.Name).ToList();
        var compareAttributes = mode is "attributes" or "attributes+geometry";
        var compareGeometry = mode is "geometry" or "attributes+geometry";
        if (!compareAttributes && requestedFields.Count > 0)
            return Fail(ErrorCodes.InvalidArgument, "fields can only be supplied when mode compares attributes.");
        if (!compareGeometry && tolerance != 0)
            return Fail(ErrorCodes.InvalidArgument, "tolerance is only valid when mode compares geometry.");
        var resolved = !compareAttributes ? new List<string>() : requestedFields.Count == 0 ? comparable : new List<string>();
        if (compareAttributes && requestedFields.Count > 0)
        {
            foreach (var requested in requestedFields)
            {
                var actual = comparable.FirstOrDefault(n => string.Equals(n, requested, StringComparison.OrdinalIgnoreCase));
                if (actual is null) return Fail(ErrorCodes.InvalidArgument, $"field '{requested}' is not comparable in '{target}'.");
                if (!resolved.Contains(actual, StringComparer.OrdinalIgnoreCase)) resolved.Add(actual);
            }
        }
        if (compareAttributes && resolved.Count == 0) return Fail(ErrorCodes.InvalidArgument, "No comparable attribute fields were found.");
        if (compareGeometry && string.IsNullOrWhiteSpace(shapeName)) return Fail(ErrorCodes.InvalidArgument, "Target has no geometry field.");

        var subFields = new List<string>();
        if (!string.IsNullOrWhiteSpace(oidField)) subFields.Add(oidField);
        if (compareAttributes) subFields.AddRange(resolved);
        if (compareGeometry && !string.IsNullOrWhiteSpace(shapeName)) subFields.Add(shapeName);
        subFields = subFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var total = fc.GetCount();
        var limit = (int)Math.Min(total, max);
        var groups = new List<DuplicateGroup>();
        var buckets = new Dictionary<string, List<DuplicateGroup>>(StringComparer.Ordinal);
        var scanned = 0;
        var sr = definition.GetSpatialReference();
        var toleranceUnit = sr?.Unit?.Name ?? "unknown coordinate units";
        using (var cursor = fc.Search(new QueryFilter { SubFields = string.Join(",", subFields) }, true))
        {
            while (scanned < limit && cursor.MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                using var row = cursor.Current;
                if (row is null) continue;
                scanned++;
                var oid = ReadOid(row, oidField);
                var attrKey = compareAttributes ? BuildAttributeKey(row, resolved) : string.Empty;
                var geometry = compareGeometry && row is Feature feature ? feature.GetShape() : null;
                var key = (compareAttributes ? attrKey : string.Empty) + (compareGeometry ? "|" + GeometryBucketKey(geometry, tolerance) : string.Empty);
                var candidates = CandidateGroups(buckets, compareAttributes ? attrKey : string.Empty, geometry, compareGeometry, tolerance);
                var existing = candidates.FirstOrDefault(g => Equivalent(g.Geometry, geometry, tolerance));
                if (existing is null)
                {
                    existing = new DuplicateGroup { Key = key, Geometry = geometry, ObjectIds = new List<long> { oid } };
                    if (!buckets.TryGetValue(key, out var bucket)) buckets[key] = bucket = new List<DuplicateGroup>();
                    bucket.Add(existing);
                    groups.Add(existing);
                }
                else
                {
                    existing.ObjectIds.Add(oid);
                }
            }
        }

        var duplicates = groups.Where(g => g.ObjectIds.Count > 1)
            .OrderBy(g => g.ObjectIds[0]).ToList();
        var returned = duplicates.Take(MaxReturnedGroups).Select(g => new Dictionary<string, object?>
        {
            ["count"] = g.ObjectIds.Count,
            ["objectIds"] = g.ObjectIds,
            ["keySha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(g.Key))),
        }).ToList();
        var payload = new Dictionary<string, object?>
        {
            ["layer"] = target,
            ["mode"] = mode,
            ["resolvedFields"] = resolved,
            ["geometryTolerance"] = tolerance,
            ["toleranceUnit"] = toleranceUnit,
            ["totalFeatureCount"] = total,
            ["scannedCount"] = scanned,
            ["countBasis"] = total > scanned ? "sample" : "total",
            ["truncated"] = total > scanned,
            ["duplicateGroupCount"] = duplicates.Count,
            ["duplicateFeatureCount"] = duplicates.Sum(g => g.ObjectIds.Count),
            ["groupsReturned"] = returned.Count,
            ["groupsTruncated"] = duplicates.Count > returned.Count,
            ["groups"] = returned,
        };
        return Ok(payload);
    }

    private static OperationResult<IReadOnlyDictionary<string, object?>> ReadNullCounts(FeatureClass fc, int max, CancellationToken ct)
    {
        using var definition = fc.GetDefinition();
        var fields = definition.GetFields().Where(f => f.FieldType is not (FieldType.OID or FieldType.Geometry or FieldType.Blob or FieldType.Raster)).ToList();
        var total = fc.GetCount();
        var limit = (int)Math.Min(total, max);
        var counts = fields.ToDictionary(f => f.Name, _ => 0, StringComparer.Ordinal);
        var objectIdField = definition.GetObjectIDField();
        var objectIds = fields.ToDictionary(f => f.Name, _ => new List<long>(), StringComparer.Ordinal);
        var objectIdsTruncated = fields.ToDictionary(f => f.Name, _ => false, StringComparer.Ordinal);
        var subfields = fields.Count > 0 ? string.Join(",", fields.Select(f => f.Name)) : objectIdField ?? string.Empty;
        var scanned = 0;
        if (subfields.Length > 0)
        {
            using var cursor = fc.Search(new QueryFilter { SubFields = subfields }, true);
            while (scanned < limit && cursor.MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                using var row = cursor.Current;
                if (row is null) continue;
                scanned++;
                var oid = ReadOid(row, objectIdField);
                foreach (var f in fields)
                {
                    if (row[f.Name] is not (null or DBNull)) continue;
                    counts[f.Name]++;
                    if (objectIds[f.Name].Count < MaxIssueEvidence) objectIds[f.Name].Add(oid);
                    else objectIdsTruncated[f.Name] = true;
                }
            }
        }
        var payload = new Dictionary<string, object?>
        {
            ["totalFeatureCount"] = total,
            ["scannedCount"] = scanned,
            ["countBasis"] = total > scanned ? "sample" : "total",
            ["truncated"] = total > scanned,
            ["fields"] = counts.Select(kv => new Dictionary<string, object?>
            {
                ["field"] = kv.Key, ["nullCount"] = kv.Value, ["objectIds"] = objectIds[kv.Key],
                ["objectIdsTruncated"] = objectIdsTruncated[kv.Key],
            }).ToList(),
        };
        return Ok(payload);
    }

    private static OperationResult<IReadOnlyDictionary<string, object?>> WithFeatureClass(
        string target, Func<FeatureClass, OperationResult<IReadOnlyDictionary<string, object?>>> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        bool isGdbPath;
        string workspace;
        string dataset;
        try { isGdbPath = TrySplitGdbPath(target, out workspace, out dataset); }
        catch (Exception ex) { return Fail(ErrorCodes.InvalidArgument, "Dataset path is invalid.", ex.Message); }
        if (isGdbPath)
        {
            if (!Directory.Exists(workspace)) return Fail(ErrorCodes.DatasetNotFound, "File geodatabase does not exist: " + workspace);
            try
            {
                using var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace)));
                string? matchName = null;
                foreach (var definition in gdb.GetDefinitions<FeatureClassDefinition>())
                {
                    using (definition)
                    {
                        if (string.Equals(definition.GetName(), dataset, StringComparison.OrdinalIgnoreCase))
                        {
                            matchName = definition.GetName();
                            break;
                        }
                    }
                }
                if (matchName is null) return Fail(ErrorCodes.DatasetNotFound, "Feature class not found in workspace: " + target);
                using var fc = gdb.OpenDataset<FeatureClass>(matchName);
                return action(fc);
            }
            catch (Exception ex) { return Fail(ErrorCodes.InternalError, "Opening feature class failed.", ex.Message); }
        }

        if (target.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(target);
            if (!File.Exists(full)) return Fail(ErrorCodes.DatasetNotFound, "Shapefile not found: " + full);
            try
            {
                using var datastore = new FileSystemDatastore(new FileSystemConnectionPath(new Uri(Path.GetDirectoryName(full)!), FileSystemDatastoreType.Shapefile));
                using var fc = datastore.OpenDataset<FeatureClass>(Path.GetFileNameWithoutExtension(full));
                return action(fc);
            }
            catch (Exception ex) { return Fail(ErrorCodes.InternalError, "Opening shapefile failed.", ex.Message); }
        }

        var project = Project.Current;
        if (project is null) return Fail(ErrorCodes.MapNotFound, "No current ArcGIS Pro project is available.");
        var uriMatches = new List<BasicFeatureLayer>();
        var nameMatches = new List<BasicFeatureLayer>();
        foreach (var item in project.GetItems<MapProjectItem>())
        {
            ct.ThrowIfCancellationRequested();
            Map? map;
            try { map = item.GetMap(); } catch { continue; }
            if (map is null) continue;
            foreach (var layer in map.GetLayersAsFlattenedList().OfType<BasicFeatureLayer>())
            {
                if (string.Equals(layer.URI, target, StringComparison.OrdinalIgnoreCase)) uriMatches.Add(layer);
                if (string.Equals(layer.Name, target, StringComparison.OrdinalIgnoreCase)) nameMatches.Add(layer);
            }
        }
        var matches = uriMatches.Count > 0 ? uriMatches : nameMatches;
        if (matches.Count == 0) return Fail(ErrorCodes.LayerNotFound, "Layer or dataset not found: " + target);
        if (matches.Count > 1) return Fail(ErrorCodes.AmbiguousLayerName, $"Layer name '{target}' matches {matches.Count} project layers; pass a unique layer URI or dataset path.");
        try
        {
            using var fc = matches[0].GetTable() as FeatureClass;
            return fc is null ? Fail(ErrorCodes.InvalidArgument, "The selected layer is not a feature class.") : action(fc);
        }
        catch (Exception ex) { return Fail(ErrorCodes.LayerDataSourceUnavailable, "Layer data source is unavailable.", ex.Message); }
    }

    private static bool TrySplitGdbPath(string path, out string workspace, out string dataset)
    {
        workspace = string.Empty; dataset = string.Empty;
        var full = Path.GetFullPath(path);
        const string marker = ".gdb";
        var index = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var end = index + marker.Length;
            if (end < full.Length && (full[end] == '\\' || full[end] == '/'))
            {
                var remainder = full[(end + 1)..].TrimEnd('\\', '/');
                if (remainder.Length > 0)
                {
                    workspace = full[..end];
                    dataset = remainder.Replace('/', '\\');
                    return true;
                }
            }
            index = full.IndexOf(marker, index + marker.Length, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static List<DuplicateGroup> CandidateGroups(
        IReadOnlyDictionary<string, List<DuplicateGroup>> buckets, string attributeKey,
        Geometry? geometry, bool compareGeometry, double tolerance)
    {
        if (!compareGeometry || tolerance <= 0)
        {
            var key = attributeKey + (compareGeometry ? "|" + GeometryBucketKey(geometry, tolerance) : string.Empty);
            return buckets.TryGetValue(key, out var exact) ? exact : new List<DuplicateGroup>();
        }

        var keys = GeometryBucketNeighbors(geometry, tolerance).Select(k => attributeKey + "|" + k);
        return keys.Where(buckets.ContainsKey).SelectMany(k => buckets[k]).ToList();
    }

    private static IEnumerable<string> GeometryBucketNeighbors(Geometry? geometry, double tolerance)
    {
        if (geometry is null)
        {
            yield return "NULL";
            yield break;
        }
        var extent = geometry.Extent;
        var cx = (extent.XMin + extent.XMax) / 2d;
        var cy = (extent.YMin + extent.YMax) / 2d;
        var cellX = (long)Math.Floor(cx / tolerance);
        var cellY = (long)Math.Floor(cy / tolerance);
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
            yield return string.Join("|", geometry.GeometryType, cellX + dx, cellY + dy);
    }

    private static string Cell(double value, double size)
        => Math.Floor(value / size).ToString(CultureInfo.InvariantCulture);

    private static string BuildAttributeKey(Row row, IReadOnlyList<string> fields)
    {
        var pieces = new List<string>(fields.Count);
        foreach (var field in fields)
        {
            var value = row[field];
            pieces.Add(field + "=" + StableValue(value));
        }
        return string.Join("\u001f", pieces);
    }

    private static string StableValue(object? value) => value switch
    {
        null or DBNull => "<null>",
        DateTime dt => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        byte[] bytes => Convert.ToHexString(bytes),
        IFormattable f => value.GetType().FullName + ":" + f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.GetType().FullName + ":" + value,
    };

    private static string GeometryBucketKey(Geometry? geometry, double tolerance)
    {
        if (geometry is null) return "NULL";
        var extent = geometry.Extent;
        if (tolerance > 0)
        {
            var cx = (extent.XMin + extent.XMax) / 2d;
            var cy = (extent.YMin + extent.YMax) / 2d;
            return geometry.GeometryType + "|" + Cell(cx, tolerance) + "|" + Cell(cy, tolerance);
        }
        return string.Join("|", geometry.GeometryType,
            R(extent.XMin), R(extent.YMin), R(extent.XMax), R(extent.YMax),
            R(GeometryEngine.Instance.Area(geometry)), R(GeometryEngine.Instance.Length(geometry)));
    }

    private static bool Equivalent(Geometry? left, Geometry? right, double tolerance)
    {
        if (left is null || right is null) return left is null && right is null;
        if (tolerance <= 0) return GeometryEngine.Instance.Equals(left, right);
        var leftBuffer = GeometryEngine.Instance.Buffer(left, tolerance);
        var rightBuffer = GeometryEngine.Instance.Buffer(right, tolerance);
        return GeometryEngine.Instance.Contains(leftBuffer, right) && GeometryEngine.Instance.Contains(rightBuffer, left);
    }

    private static string R(double v) => Math.Round(v, 8, MidpointRounding.ToEven).ToString("R", CultureInfo.InvariantCulture);
    private static long ReadOid(Row row, string? oid) => oid is null ? -1 : Convert.ToInt64(row[oid], CultureInfo.InvariantCulture);

    private static (double LengthFactor, double AreaFactor, string LengthUnit, string AreaUnit) ResolveFactors(
        string requested, double metresPerUnit, string layerUnit)
    {
        if (requested == "layer") return (1, 1, layerUnit, layerUnit + "²");
        var targetMetres = requested switch { "kilometers" => 1000d, "feet" => 0.3048d, "miles" => 1609.344d, _ => 1d };
        var lengthFactor = metresPerUnit / targetMetres;
        var areaFactor = metresPerUnit * metresPerUnit;
        var lengthUnit = requested is "hectares" or "acres" or "square_meters" ? "meters" : requested;
        var areaUnit = requested switch
        {
            "hectares" => "hectares",
            "acres" => "acres",
            "square_meters" => "square_meters",
            _ => "square_" + requested,
        };
        if (requested == "hectares") areaFactor /= 10_000d;
        else if (requested == "acres") areaFactor /= 4046.8564224d;
        else if (requested == "square_meters") { }
        else areaFactor /= targetMetres * targetMetres;
        return (lengthFactor, areaFactor, lengthUnit, areaUnit);
    }

    private static Dictionary<string, object?> Issue(
        string severity, string code, int count, string message, string? field = null,
        IReadOnlyList<long>? objectIds = null, string? dataset = null, bool objectIdsTruncated = false)
    {
        var sampledIds = objectIds ?? Array.Empty<long>();
        var location = new Dictionary<string, object?> { ["dataset"] = dataset };
        if (field is not null) location["field"] = field;
        if (sampledIds.Count > 0) location["objectIds"] = sampledIds;
        return new Dictionary<string, object?>
        {
            ["ruleId"] = code,
            ["severity"] = severity,
            ["code"] = code,
            ["count"] = count,
            ["message"] = message,
            ["field"] = field,
            ["location"] = location,
            ["evidence"] = new Dictionary<string, object?>
            {
                ["sampleObjectIds"] = sampledIds,
                ["sampleObjectIdsTruncated"] = objectIdsTruncated,
                ["issueCount"] = count,
            },
        };
    }

    private static OperationResult<IReadOnlyDictionary<string, object?>> Ok(Dictionary<string, object?> payload)
        => OperationResult<IReadOnlyDictionary<string, object?>>.Ok(payload);
    private static OperationResult<IReadOnlyDictionary<string, object?>> Fail(string code, string message, string? detail = null)
        => detail is null
            ? OperationResult<IReadOnlyDictionary<string, object?>>.Fail(code, message)
            : OperationResult<IReadOnlyDictionary<string, object?>>.Fail(code, message, detail);
    private static OperationResult<IReadOnlyDictionary<string, object?>> Propagate<T>(OperationResult<T> result)
        => OperationResult<IReadOnlyDictionary<string, object?>>.Fail(result.Errors);

    private static string? ReadString(IReadOnlyDictionary<string, object?> d, string key) => d.TryGetValue(key, out var v) ? v?.ToString() : null;
    private static int ReadInt(IReadOnlyDictionary<string, object?> d, string key) => d.TryGetValue(key, out var v) ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : 0;
    private static long ReadLong(IReadOnlyDictionary<string, object?> d, string key) => d.TryGetValue(key, out var v) ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : 0;
    private static bool ReadBool(IReadOnlyDictionary<string, object?> d, string key) => d.TryGetValue(key, out var v) && v is bool b && b;
    private static readonly HashSet<string> Units = new(StringComparer.Ordinal)
    { "layer", "meters", "kilometers", "feet", "miles", "hectares", "acres", "square_meters" };

    private sealed class DuplicateGroup
    {
        public string Key { get; init; } = string.Empty;
        public Geometry? Geometry { get; init; }
        public List<long> ObjectIds { get; init; } = new();
    }

    private sealed class DomainConstraint
    {
        public DomainConstraint(string name, HashSet<object> codedValues)
        {
            Name = name;
            CodedValues = codedValues;
        }

        public DomainConstraint(string name, object minimum, object maximum)
        {
            Name = name;
            Minimum = minimum;
            Maximum = maximum;
        }

        public string Name { get; }
        public HashSet<object>? CodedValues { get; }
        public object? Minimum { get; }
        public object? Maximum { get; }
    }
}
