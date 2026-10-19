using System.Reflection;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGISProMCP.Core.Progress;
using ArcGISProMCP.Core.Results;

namespace ArcGISProMCP.Compatibility.Services;

public sealed partial class D088QualityService
{
    private const int EvidenceLimit = 500;
    private const int TopologyFeatureLimit = 10000;
    private const long PairBudget = 2000000;
    private static readonly MethodInfo? PublicOgcCheck = typeof(GeometryEngine)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .FirstOrDefault(m => m.Name == "IsSimpleOgc" && m.GetParameters().Length == 3);
    private static readonly string[] TopologyRules = {
        "must_not_overlap", "must_not_have_gaps", "must_not_self_intersect",
        "must_be_single_part", "must_not_self_overlap", "must_not_have_dangles" };

    private static Task<OperationResult<IReadOnlyDictionary<string, object?>>> RunGeometryValidation(
        string target, IReadOnlyList<string> checks, double minimum, int max, CancellationToken ct)
    {
        var progress = ToolProgress.Current;
        return QueuedTask.Run(() => D084AnalysisService.WithFeatureClass(target,
            fc => ValidateGeometryRows(fc, target, checks, minimum, max, progress, ct), ct), TaskCreationOptions.None);
    }

    private static Task<OperationResult<IReadOnlyDictionary<string, object?>>> RunTopologyValidation(
        string target, IReadOnlyList<string> rules, double tolerance, CancellationToken ct)
    {
        if (rules.Count == 0 || rules.Any(r => !TopologyRules.Contains(r, StringComparer.Ordinal)))
            return Task.FromResult(QualityFail("Unsupported topology rule. Supported: " + string.Join(", ", TopologyRules)));
        if (tolerance != 0 && rules.Any(r => r != "must_not_have_dangles"))
            return Task.FromResult(QualityFail("Nonzero clusterTolerance is supported only for read-only dangle connectivity. No clustering, snapping or input edits are performed."));
        var progress = ToolProgress.Current;
        return QueuedTask.Run(() => D084AnalysisService.WithFeatureClass(target,
            fc => ValidateTopologyRows(fc, target, rules.Distinct(StringComparer.Ordinal).ToArray(), tolerance, progress, ct), ct), TaskCreationOptions.None);
    }

    private sealed class Findings
    {
        public long Count;
        public readonly List<Dictionary<string, object?>> Items = new();
        public void Add(string kind, long? oid, string detail, long? other = null)
        {
            Count++;
            if (Items.Count < EvidenceLimit)
                Items.Add(new() { ["kind"] = kind, ["objectId"] = oid, ["otherObjectId"] = other, ["detail"] = detail });
        }
    }

    private static OperationResult<IReadOnlyDictionary<string, object?>> ValidateGeometryRows(
        FeatureClass fc, string target, IReadOnlyList<string> checks, double minimum, int max,
        ToolProgress.ProgressScope? progress, CancellationToken ct)
    {
        using var definition = fc.GetDefinition();
        var sr = definition.GetSpatialReference();
        var total = fc.GetCount();
        var limit = Math.Min(total, max);
        var findings = new Findings();
        var unassessed = new Findings();
        var counts = checks.Distinct().ToDictionary(c => c, _ => 0L, StringComparer.Ordinal);
        var statuses = checks.Distinct().ToDictionary(c => c, _ => "EVALUATED", StringComparer.Ordinal);
        if (counts.ContainsKey("short_segment") && minimum == 0) statuses["short_segment"] = "DISABLED_THRESHOLD_ZERO";
        if (definition.GetShapeType() is GeometryType.Point or GeometryType.Multipoint)
        {
            if (statuses.ContainsKey("short_segment")) statuses["short_segment"] = "NOT_APPLICABLE";
            if (definition.GetShapeType() == GeometryType.Point && statuses.ContainsKey("duplicate_vertex"))
                statuses["duplicate_vertex"] = "NOT_APPLICABLE";
        }
        var scanned = 0;
        var simpleAvailable = true;
        var stage = "quality:geometry:" + Guid.NewGuid().ToString("N")[..8];
        progress?.Report(stage, "RUNNING", "只读几何质检；检查原始几何，不自动修复", 0, limit, "ArcGIS.SDK.GeometryQuality");
        using var cursor = fc.Search(new QueryFilter { SubFields = definition.GetObjectIDField() + "," + definition.GetShapeField() }, true);
        while (scanned < limit && cursor.MoveNext())
        {
            ct.ThrowIfCancellationRequested();
            using var row = cursor.Current;
            var oid = row.GetObjectID();
            var geometry = ((Feature)row).GetShape();
            scanned++;
            if (geometry is null || geometry.IsEmpty)
            {
                if (counts.ContainsKey("null_geometry")) { counts["null_geometry"]++; findings.Add("null_geometry", oid, "Null or empty shape."); }
                // An empty geometry cannot certify the other requested checks.
                else findings.Add("unassessable_empty_geometry", oid, "Requested non-null checks cannot inspect an empty shape.");
            }
            else
            {
                if (counts.ContainsKey("self_intersection"))
                {
                    long work = 0;
                    var intersection = SelfIntersection(geometry, ref work, ct, out var reason);
                    if (intersection is null) { simpleAvailable = false; statuses["self_intersection"] = "INCOMPLETE"; unassessed.Add("unassessed_self_intersection", oid, reason); }
                    else if (intersection.Value)
                    {
                        counts["self_intersection"]++;
                        findings.Add("self_intersection", oid, reason);
                    }
                }
                if (geometry is Multipart multipart)
                {
                    var partIndex = 0;
                    foreach (var part in multipart.Parts)
                    {
                        var segments = part.ToArray();
                        if (counts.ContainsKey("short_segment") && minimum > 0)
                            foreach (var segment in segments)
                                if (segment.Length < minimum)
                                { counts["short_segment"]++; findings.Add("short_segment", oid, $"Part {partIndex}; length={segment.Length:R} < {minimum:R} in CRS units."); }
                        if (counts.ContainsKey("duplicate_vertex") && segments.Length > 0)
                        {
                            var points = segments.Select(s => s.StartPoint).Append(segments[^1].EndPoint).ToList();
                            // Required closure vertex is not an accidental duplicate, for a ring or a closed line.
                            if (SameXY(points[0], points[^1])) points.RemoveAt(points.Count - 1);
                            CountDuplicateVertices(points, oid, partIndex, counts, findings);
                        }
                        partIndex++;
                    }
                }
                else if (geometry is Multipoint multipoint && counts.ContainsKey("duplicate_vertex"))
                {
                    CountDuplicateVertices(multipoint.Points, oid, 0, counts, findings);
                    if (statuses.ContainsKey("short_segment") && minimum > 0) statuses["short_segment"] = "NOT_APPLICABLE";
                }
                else if (geometry is MapPoint)
                {
                    foreach (var check in new[] { "short_segment", "duplicate_vertex" })
                        if (statuses.ContainsKey(check) && statuses[check] != "DISABLED_THRESHOLD_ZERO") statuses[check] = "NOT_APPLICABLE";
                }
                else if (geometry is not MapPoint and not Multipoint)
                {
                    foreach (var check in new[] { "short_segment", "duplicate_vertex" })
                        if (statuses.ContainsKey(check)) statuses[check] = "NOT_SUPPORTED_FOR_GEOMETRY_TYPE";
                }
            }
            if (scanned == limit || scanned % 25 == 0)
                progress?.Report(stage, "RUNNING", $"已检查 {scanned}/{limit} 个原始要素", scanned, limit, "ArcGIS.SDK.GeometryQuality");
        }
        var complete = scanned == total && simpleAvailable && statuses.Values.All(s => s is "EVALUATED" or "DISABLED_THRESHOLD_ZERO" or "NOT_APPLICABLE");
        var allChecksApplied = statuses.Values.All(s => s is "EVALUATED" or "NOT_APPLICABLE");
        return QualityOk(new()
        {
            ["dataset"] = target, ["scope"] = "Entire underlying feature class; map selection and definition queries are not used.",
            ["totalFeatureCount"] = total, ["scannedCount"] = scanned, ["countBasis"] = scanned == total ? "total" : "sample",
            ["truncated"] = scanned < total, ["coverageComplete"] = complete,
            ["allRequestedChecksApplied"] = allChecksApplied,
            ["spatialReference"] = sr?.Name, ["coordinateUnit"] = sr?.Unit?.Name,
            ["xyTolerance"] = sr?.XYTolerance, ["xyResolution"] = sr?.XYResolution,
            ["minimumSegmentLength"] = minimum, ["checks"] = statuses.Select(p => new Dictionary<string, object?> {
                ["check"] = p.Key, ["status"] = p.Value, ["issueCount"] = counts[p.Key] }).ToArray(),
            ["issueCount"] = findings.Count, ["issues"] = findings.Items, ["issuesTruncated"] = findings.Count > findings.Items.Count,
            ["unassessedCount"] = unassessed.Count, ["unassessed"] = unassessed.Items, ["unassessedTruncated"] = unassessed.Count > unassessed.Items.Count,
            ["qualityVerdict"] = total == 0 ? "NO_FEATURES" : counts.Values.Any(v => v > 0) ? "FAIL" : !complete ? "INCOMPLETE" : findings.Count > 0 ? "FAIL" : !allChecksApplied ? "PASS_WITH_LIMITATIONS" : "PASS",
            ["shortSegmentCheckApplied"] = statuses.TryGetValue("short_segment", out var shortStatus) && shortStatus == "EVALUATED",
            ["method"] = "Polyline segment interior crossings/overlaps (endpoint self-touch allowed); polygon forced OGC self-intersection reason, other first reasons incomplete; true segment lengths and exact XY duplicates per part, required closure excluded.",
            ["inputModified"] = false, ["industryCertification"] = "NOT CLAIMED"
        });
    }

    private static void CountDuplicateVertices(IEnumerable<MapPoint> points, long oid, int part,
        Dictionary<string, long> counts, Findings findings)
    {
        var seen = new HashSet<(double, double)>();
        foreach (var point in points)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
            { findings.Add("non_finite_coordinate", oid, $"Part {part}: non-finite XY coordinate."); continue; }
            if (!seen.Add((point.X, point.Y)))
            { counts["duplicate_vertex"]++; findings.Add("duplicate_vertex", oid, $"Part {part}: repeated XY vertex ({point.X:R},{point.Y:R})."); }
        }
    }

    private sealed record ShapeRow(long Oid, Geometry Shape);
    private sealed record LinePiece(long Oid, int Part, int Index, int PartSegments, Segment Segment, Polyline Line);

    private static OperationResult<IReadOnlyDictionary<string, object?>> ValidateTopologyRows(
        FeatureClass fc, string target, IReadOnlyList<string> rules, double tolerance,
        ToolProgress.ProgressScope? progress, CancellationToken ct)
    {
        using var definition = fc.GetDefinition();
        var total = fc.GetCount();
        if (total > TopologyFeatureLimit)
            return QualityFail($"Read-only topology full-check limit is {TopologyFeatureLimit} features; this dataset has {total}. No sample is certified. Use explicitly bounded datasets.");
        var shapeType = definition.GetShapeType();
        if (shapeType is not (GeometryType.Polygon or GeometryType.Polyline))
            return QualityFail("These topology rules require Polygon or Polyline; points/multipatches are not silently certified.");
        if (shapeType != GeometryType.Polygon && rules.Contains("must_not_have_gaps"))
            return QualityFail("must_not_have_gaps requires Polygon; exterior coverage needs an explicit business boundary and is not inferred.");
        if (shapeType != GeometryType.Polyline && rules.Any(r => r is "must_not_self_overlap" or "must_not_have_dangles"))
            return QualityFail("Dangle and self-overlap rules require Polyline.");
        var sr = definition.GetSpatialReference();
        if (tolerance > 0 && (sr is null || sr.IsGeographic))
            return QualityFail("Nonzero dangle distance requires a projected CRS with known linear units.");
        var rows = new List<ShapeRow>();
        var findings = new Findings();
        var stage = "quality:topology:" + Guid.NewGuid().ToString("N")[..8];
        progress?.Report(stage + ":read", "RUNNING", "只读拓扑检查：读取全部原始要素", 0, total, "ArcGIS.SDK.TopologyQuality");
        var scanned = 0;
        using (var cursor = fc.Search(new QueryFilter { SubFields = definition.GetObjectIDField() + "," + definition.GetShapeField() }, true))
            while (cursor.MoveNext())
            {
                ct.ThrowIfCancellationRequested();
                using var row = cursor.Current;
                var geometry = ((Feature)row).GetShape();
                scanned++;
                if (geometry is null || geometry.IsEmpty) findings.Add("null_geometry", row.GetObjectID(), "Null or empty input cannot be topologically certified.");
                else rows.Add(new(row.GetObjectID(), geometry));
                if (scanned % 25 == 0 || scanned == total)
                    progress?.Report(stage + ":read", "RUNNING", $"已读取 {scanned}/{total} 个原始要素", scanned, total, "ArcGIS.SDK.TopologyQuality");
            }
        var reports = new List<Dictionary<string, object?>>();
        var allComplete = true;
        long comparisons = 0;
        var simpleFlags = new Dictionary<long, bool?>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            simpleFlags[row.Oid] = OgcSimple(row.Shape, out _);
        }
        foreach (var rule in rules)
        {
            ct.ThrowIfCancellationRequested();
            var before = findings.Count;
            var complete = true;
            var detail = "";
            progress?.Report(stage + ":rules", "RUNNING", "正在检查规则 " + rule, reports.Count, rules.Count, "ArcGIS.SDK.TopologyQuality");
            switch (rule)
            {
                case "must_not_self_intersect":
                    detail = "Polyline segment interior crossing/overlap; endpoint-only self-touch permitted. Polygon forced OGC self-intersection reason; other first reasons remain incomplete. No simplification.";
                    foreach (var row in rows)
                    {
                        ct.ThrowIfCancellationRequested();
                        var intersection = SelfIntersection(row.Shape, ref comparisons, ct, out var reason);
                        if (intersection is null) { complete = false; detail += " Unassessed feature " + row.Oid + ": " + reason; }
                        else if (intersection.Value) findings.Add(rule, row.Oid, reason);
                    }
                    break;
                case "must_be_single_part":
                    detail = "Counts polyline paths or polygon exterior shells; polygon holes are not separate features/parts for this rule.";
                    foreach (var row in rows)
                    {
                        var parts = row.Shape is Polygon p ? p.ExteriorRingCount : ((Multipart)row.Shape).PartCount;
                        if (parts > 1) findings.Add(rule, row.Oid, "Part count=" + parts);
                    }
                    break;
                case "must_not_overlap":
                    detail = "Same-dimension interior intersection (DE-9IM 2******** for polygons, 1******** for lines); containment/duplicates count, boundary-only touching does not.";
                    if (shapeType == GeometryType.Polygon && simpleFlags.Values.Any(v => v != true)) { complete = false; detail += " Invalid/unassessable input: pairwise check not certified."; break; }
                    var ordered = rows.OrderBy(r => r.Shape.Extent.XMin).ToArray();
                    for (var i = 0; i < ordered.Length && complete; i++)
                        for (var j = i + 1; j < ordered.Length; j++)
                        {
                            ct.ThrowIfCancellationRequested();
                            var a = ordered[i]; var b = ordered[j];
                            if (b.Shape.Extent.XMin > a.Shape.Extent.XMax) break;
                            if (!BoxesIntersect(a.Shape.Extent, b.Shape.Extent)) continue;
                            if (++comparisons > PairBudget) { complete = false; break; }
                            if (GeometryEngine.Instance.Relate(a.Shape, b.Shape, shapeType == GeometryType.Polygon ? "2********" : "1********"))
                                findings.Add(rule, a.Oid, "Same-dimensional interior intersection.", b.Oid);
                        }
                    break;
                case "must_not_have_gaps":
                    detail = "Interior holes in the polygon union; outside/exterior area is excluded. This does not prove coverage against an unspecified study boundary.";
                    if (simpleFlags.Values.Any(v => v != true)) { complete = false; detail += " Invalid/unassessable input: union not certified."; break; }
                    if (rows.Count > 0)
                    {
                        var union = GeometryEngine.Instance.Union(rows.Select(r => r.Shape)) as Polygon;
                        if (union is null) { complete = false; break; }
                        for (var part = 0; part < union.PartCount; part++)
                            if (!union.IsExteriorRing(part)) findings.Add(rule, null, "Enclosed interior gap, union ring index=" + part);
                    }
                    break;
                case "must_not_self_overlap":
                    detail = "Same-dimensional overlap among actual curve/line segments of each feature; ordinary adjacent endpoint sharing is allowed.";
                    foreach (var row in rows)
                    {
                        var pieces = Pieces(row).ToArray();
                        foreach (var piece in pieces.Where(p => p.Segment is not LineSegment))
                        {
                            var simple = OgcSimple(piece.Line, out _);
                            if (simple != true) { complete = false; detail += " Non-simple/unsupported single curve cannot exclude internal retracing."; break; }
                        }
                        if (!complete) break;
                        for (var i = 0; i < pieces.Length && complete; i++)
                            for (var j = i + 1; j < pieces.Length; j++)
                            {
                                ct.ThrowIfCancellationRequested();
                                if (!BoxesIntersect(pieces[i].Line.Extent, pieces[j].Line.Extent)) continue;
                                if (++comparisons > PairBudget) { complete = false; break; }
                                if (GeometryEngine.Instance.Relate(pieces[i].Line, pieces[j].Line, "1********"))
                                    findings.Add(rule, row.Oid, $"Overlapping segments {i}/{j}.");
                            }
                        if (!complete) break;
                    }
                    break;
                case "must_not_have_dangles":
                    detail = "Each open path endpoint must touch another segment (same feature allowed except its incident segment). Explicit distance or dataset XYTolerance; no snapping or input mutation.";
                    var allPieces = rows.SelectMany(Pieces).ToArray();
                    var endpointTolerance = tolerance > 0 ? tolerance : sr?.XYTolerance ?? 0;
                    foreach (var group in allPieces.GroupBy(p => (p.Oid, p.Part)))
                    {
                        var path = group.OrderBy(p => p.Index).ToArray();
                        if (SameXY(path[0].Segment.StartPoint, path[^1].Segment.EndPoint)) continue;
                        foreach (var end in new[] { (Point: path[0].Segment.StartPoint, Incident: path[0]), (Point: path[^1].Segment.EndPoint, Incident: path[^1]) })
                        {
                            var connected = false;
                            foreach (var piece in allPieces)
                            {
                                ct.ThrowIfCancellationRequested();
                                if (piece == end.Incident) continue;
                                if (++comparisons > PairBudget) { complete = false; break; }
                                if (GeometryEngine.Instance.Distance(end.Point, piece.Line) <= endpointTolerance) { connected = true; break; }
                            }
                            if (!complete) break;
                            if (!connected) findings.Add(rule, group.Key.Oid, $"Unconnected path {group.Key.Part} endpoint ({end.Point.X:R},{end.Point.Y:R}), tolerance={endpointTolerance:R}.");
                        }
                        if (!complete) break;
                    }
                    break;
            }
            allComplete &= complete;
            reports.Add(new() { ["rule"] = rule, ["status"] = complete ? "EVALUATED" : "INCOMPLETE",
                ["violationCount"] = findings.Count - before, ["method"] = detail });
        }
        var resultComplete = allComplete && scanned == total;
        progress?.Report(stage + ":rules", "RUNNING", "所选拓扑规则已返回；成果判定和外部验收另行进行", rules.Count, rules.Count, "ArcGIS.SDK.TopologyQuality");
        return QualityOk(new()
        {
            ["dataset"] = target, ["scope"] = "Entire underlying feature class; selected explicit rules only; no geodatabase topology object is created or altered.",
            ["totalFeatureCount"] = total, ["scannedCount"] = scanned, ["countBasis"] = "total", ["truncated"] = false,
            ["coverageComplete"] = resultComplete, ["geometryType"] = shapeType.ToString(), ["spatialReference"] = sr?.Name,
            ["coordinateUnit"] = sr?.Unit?.Name, ["clusterTolerance"] = tolerance, ["sdkXYTolerance"] = sr?.XYTolerance,
            ["rules"] = reports, ["supportedRules"] = TopologyRules, ["pairComparisons"] = comparisons, ["pairBudget"] = PairBudget,
            ["issueCount"] = findings.Count, ["issues"] = findings.Items, ["issuesTruncated"] = findings.Count > findings.Items.Count,
            ["qualityVerdict"] = total == 0 ? "NO_FEATURES" : !resultComplete ? "INCOMPLETE" : findings.Count > 0 ? "FAIL" : "PASS",
            ["inputModified"] = false, ["industryCertification"] = "NOT CLAIMED"
        });
    }

    private static IEnumerable<LinePiece> Pieces(ShapeRow row)
    {
        if (row.Shape is not Multipart multipart) yield break;
        var partIndex = 0;
        foreach (var part in multipart.Parts)
        {
            var segments = part.ToArray();
            for (var i = 0; i < segments.Length; i++)
                yield return new(row.Oid, partIndex, i, segments.Length, segments[i], PolylineBuilderEx.CreatePolyline(new[] { segments[i] }, row.Shape.SpatialReference));
            partIndex++;
        }
    }

    private static bool BoxesIntersect(Envelope a, Envelope b)
        => a.XMin <= b.XMax && b.XMin <= a.XMax && a.YMin <= b.YMax && b.YMin <= a.YMax;
    private static bool SameXY(MapPoint a, MapPoint b) => a.X == b.X && a.Y == b.Y;

    private static bool? SelfIntersection(Geometry geometry, ref long work, CancellationToken ct, out string reason)
    {
        if (geometry is Polyline)
        {
            var pieces = Pieces(new ShapeRow(0, geometry)).ToArray();
            foreach (var piece in pieces.Where(p => p.Segment is not LineSegment))
                if (OgcSimple(piece.Line, out _) != true)
                { reason = "Single curve non-simple or unavailable: internal crossing/retracing not safely classified."; return null; }
            for (var i = 0; i < pieces.Length; i++)
                for (var j = i + 1; j < pieces.Length; j++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (++work > PairBudget) { reason = "Segment comparison budget exhausted."; return null; }
                    if (!BoxesIntersect(pieces[i].Line.Extent, pieces[j].Line.Extent)) continue;
                    if (GeometryEngine.Instance.Relate(pieces[i].Line, pieces[j].Line, "0********") ||
                        GeometryEngine.Instance.Relate(pieces[i].Line, pieces[j].Line, "1********"))
                    { reason = $"Segment interiors cross/overlap: {i}/{j}. Endpoint-only self-touch is permitted."; return true; }
                }
            var ogc = OgcSimple(geometry, out var ogcReason);
            if (ogc == true) { reason = "No segment-interior crossing or overlap."; return false; }
            if (pieces.Any(p => p.Segment is not LineSegment))
            { reason = "Non-simple curve node tangents not classified: " + ogcReason; return null; }
            // A crossing may land exactly on an existing vertex. Segment-pair interiors
            // alone miss this. Compare paired incident rays at visits and segment interiors.
            var visits = LineVisits((Polyline)geometry).Where(v => v.Rays.Length == 2).ToArray();
            for (var i = 0; i < visits.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                for (var j = i + 1; j < visits.Length; j++)
                {
                    if (++work > PairBudget) { reason = "Node comparison budget exhausted."; return null; }
                    if (SameXY(visits[i].Point, visits[j].Point) && Alternates(visits[i].Rays, visits[j].Rays))
                    { reason = "Crossing at a repeated vertex (alternating incident rays)."; return true; }
                }
                foreach (var piece in pieces)
                {
                    if (++work > PairBudget) { reason = "Node/segment comparison budget exhausted."; return null; }
                    var node = visits[i].Point;
                    if (SameXY(node, piece.Segment.StartPoint) || SameXY(node, piece.Segment.EndPoint)) continue;
                    if (!BoxesIntersect(EnvelopeBuilderEx.CreateEnvelope(node.X, node.Y, node.X, node.Y, geometry.SpatialReference), piece.Line.Extent)) continue;
                    if (GeometryEngine.Instance.Relate(node, piece.Line, "0********") &&
                        Alternates(visits[i].Rays, new[] { Ray(node, piece.Segment.StartPoint), Ray(node, piece.Segment.EndPoint) }))
                    { reason = "Crossing at a vertex on another segment interior."; return true; }
                }
            }
            reason = "No interior crossing/overlap or alternating straight-segment node rays; endpoint/same-side self-touch allowed."; return false;
        }
        if (geometry is MapPoint or Multipoint) { reason = "Not applicable to points."; return false; }
        var simple = OgcSimple(geometry, out reason);
        if (simple == true) return false;
        if (simple == false && reason.Contains("SelfIntersections", StringComparison.OrdinalIgnoreCase)) return true;
        reason = "SDK first non-simple reason cannot certify self-intersection separately: " + reason;
        return null;
    }

    private sealed record NodeVisit(MapPoint Point, double[] Rays);
    private static double Ray(MapPoint from, MapPoint to) => Math.Atan2(to.Y - from.Y, to.X - from.X);
    private static IEnumerable<NodeVisit> LineVisits(Polyline line)
    {
        foreach (var part in line.Parts)
        {
            var segments = part.ToArray();
            if (segments.Length == 0) continue;
            var points = segments.Select(s => s.StartPoint).Append(segments[^1].EndPoint).ToList();
            // Collapse consecutive zero-length visits only for direction analysis;
            // original rows/vertices stay untouched and duplicate checks still see them.
            points = points.Where((p, i) => i == 0 || !SameXY(p, points[i - 1])).ToList();
            var closed = SameXY(points[0], points[^1]);
            if (closed) points.RemoveAt(points.Count - 1);
            for (var i = 0; i < points.Count; i++)
            {
                var rays = new List<double>();
                if (i > 0 || closed)
                {
                    var prior = points[(i - 1 + points.Count) % points.Count];
                    if (!SameXY(points[i], prior)) rays.Add(Ray(points[i], prior));
                }
                if (i + 1 < points.Count || closed)
                {
                    var next = points[(i + 1) % points.Count];
                    if (!SameXY(points[i], next)) rays.Add(Ray(points[i], next));
                }
                yield return new(points[i], rays.ToArray());
            }
        }
    }
    private static bool Alternates(double[] a, double[] b)
    {
        // Coincident rays are touching/overlap, not an isolated X crossing.
        var ordered = a.Select(x => (Angle: x, Group: 0)).Concat(b.Select(x => (Angle: x, Group: 1))).OrderBy(x => x.Angle).ToArray();
        for (var i = 0; i < 4; i++)
        {
            var next = ordered[(i + 1) % 4];
            if (Math.Abs(Math.Sin(ordered[i].Angle - next.Angle)) < 1e-12 && Math.Cos(ordered[i].Angle - next.Angle) > 0) return false;
            if (ordered[i].Group == next.Group) return false;
        }
        return true;
    }

    private static bool? OgcSimple(Geometry geometry, out string reason)
    {
        // Public SDK version adapter: Pro 3.0 references lack IsSimpleOgc; Pro >=3.2 exposes it.
        // Reflection only calls this public API on the MCT; never accesses private host state.
        var method = PublicOgcCheck;
        if (method is null) { reason = "Public IsSimpleOgc is unavailable in this host version."; return null; }
        object?[] args = { geometry, null, true };
        var value = method.Invoke(GeometryEngine.Instance, args);
        reason = args[1]?.ToString() ?? "Unknown SDK reason";
        return value as bool?;
    }

    private static OperationResult<IReadOnlyDictionary<string, object?>> QualityFail(string message)
        => OperationResult<IReadOnlyDictionary<string, object?>>.Fail(ErrorCodes.InvalidArgument, message);
    private static OperationResult<IReadOnlyDictionary<string, object?>> QualityOk(Dictionary<string, object?> data)
        => OperationResult<IReadOnlyDictionary<string, object?>>.Ok(data);
}
