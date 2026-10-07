using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>D-084 Session layout mutations; all ArcGIS SDK access stays on the MCT.</summary>
public sealed partial class LayoutService
{
    public Task<OperationResult<object?>> SetElementPropertiesAsync(
        string layoutName, string elementId, IReadOnlyDictionary<string, object?> properties,
        string units, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<object?>>(() =>
        {
            ct.ThrowIfCancellationRequested();
            var (layout, code, message) = ResolveLayoutByName(layoutName);
            if (layout is null) return OperationResult<object?>.Fail(code, message);
            if (properties is null || properties.Count == 0)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "properties must be a non-empty object.");

            var elements = layout.GetElementsAsFlattenedList().ToList();
            var matches = elements.Where(e => string.Equals(e.Name, elementId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
                return OperationResult<object?>.Fail(ErrorCodes.NotFound, $"Layout element '{elementId}' was not found in '{layoutName}'.");
            if (matches.Count > 1)
                return OperationResult<object?>.Fail(ErrorCodes.AmbiguousLayerName, $"Layout element name '{elementId}' matches {matches.Count} elements.");

            var element = matches[0];
            if (properties.TryGetValue("name", out var rawName)
                && elements.Any(e => !ReferenceEquals(e, element)
                    && string.Equals(e.Name, rawName?.ToString(), StringComparison.OrdinalIgnoreCase)))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "The requested element name is already used in this layout.");

            var page = layout.GetPage();
            if (page is null) return OperationResult<object?>.Fail(ErrorCodes.InvalidState, "Layout page units are unavailable.");
            var originallyLocked = element.IsLocked;
            var originals = new Dictionary<string, object?>(StringComparer.Ordinal);
            try
            {
                foreach (var key in properties.Keys)
                {
                    originals[key] = key switch
                    {
                        "x" => element.GetX(),
                        "y" => element.GetY(),
                        "width" => element.GetWidth(),
                        "height" => element.GetHeight(),
                        "rotation" => element.GetRotation(),
                        "visible" => element.IsVisible,
                        "locked" => element.IsLocked,
                        "name" => element.Name,
                        _ => throw new ArgumentException($"Property '{key}' is outside the D-084 allowlist."),
                    };
                }
                originals.TryAdd("locked", originallyLocked);
            }
            catch (Exception ex)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "One or more requested properties cannot be read for this element.", ex.Message);
            }

            var nativePerInch = NativePageUnitsPerInch(page.Units.ToString());
            if (units != "page" && !nativePerInch.HasValue)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "The layout page unit cannot be converted to the requested units.");

            double NativeValue(string key)
            {
                var value = Convert.ToDouble(properties[key], System.Globalization.CultureInfo.InvariantCulture);
                if (units == "page" || key == "rotation") return value;
                var inches = units switch
                {
                    "points" => value / 72d,
                    "inches" => value,
                    "mm" => value / 25.4d,
                    "cm" => value / 2.54d,
                    _ => double.NaN,
                };
                return inches * nativePerInch!.Value;
            }

            var geometryChanged = properties.Keys.Any(k => k is "x" or "y" or "width" or "height" or "rotation");
            try
            {
                if (originallyLocked && geometryChanged) element.SetLocked(false);
                if (properties.TryGetValue("x", out _)) element.SetX(NativeValue("x"));
                if (properties.TryGetValue("y", out _)) element.SetY(NativeValue("y"));
                if (properties.TryGetValue("width", out _)) element.SetWidth(NativeValue("width"));
                if (properties.TryGetValue("height", out _)) element.SetHeight(NativeValue("height"));
                if (properties.TryGetValue("rotation", out _)) element.SetRotation(NativeValue("rotation"));
                if (properties.TryGetValue("visible", out var visible)) element.SetVisible((bool)visible!);
                if (properties.TryGetValue("name", out var name)) element.SetName((string)name!);
                element.SetLocked(properties.TryGetValue("locked", out var locked) ? (bool)locked! : originallyLocked);
            }
            catch (Exception ex)
            {
                var rollback = TryRestore(element, originals);
                return OperationResult<object?>.Fail(ErrorCodes.InternalError,
                    "Updating layout element properties failed; a rollback was attempted.",
                    rollback ? ex.Message : ex.Message + "; rollback was incomplete.");
            }

            var readback = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var key in properties.Keys)
                readback[key] = key switch
                {
                    "x" => element.GetX(), "y" => element.GetY(), "width" => element.GetWidth(),
                    "height" => element.GetHeight(), "rotation" => element.GetRotation(),
                    "visible" => element.IsVisible, "locked" => element.IsLocked, "name" => element.Name,
                    _ => null,
                };
            return OperationResult<object?>.Ok(new Dictionary<string, object?>
            {
                ["layout"] = layout.Name,
                ["elementId"] = elementId,
                ["elementName"] = element.Name,
                ["pageUnits"] = page.Units.ToString(),
                ["inputUnits"] = units,
                ["readbackUnits"] = "page",
                ["properties"] = readback,
                ["updated"] = true,
            });
        }, TaskCreationOptions.None);

    public Task<OperationResult<object?>> ConfigureMapSeriesAsync(
        string mapName, string indexField, string? sortField, string extentSource,
        string? nameField, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<object?>>(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(mapName) || string.IsNullOrWhiteSpace(indexField)
                || extentSource is not ("bookmarks" or "layer" or "fixed"))
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "map, indexField and a supported extentSource are required.");

            var resolved = MapResolver.Resolve(mapName);
            if (MapResolver.FailIfNotOk<object?>(resolved, mapName) is { } mapFailure)
                return OperationResult<object?>.Fail(mapFailure.Errors);
            var map = resolved.Map!;
            var project = Project.Current;
            if (project is null) return OperationResult<object?>.Fail(ErrorCodes.MapNotFound, "No current ArcGIS Pro project is available.");
            var layoutFrames = new List<(Layout Layout, MapFrame Frame)>();
            foreach (var item in project.GetItems<LayoutProjectItem>())
            {
                ct.ThrowIfCancellationRequested();
                Layout? layout;
                try { layout = item.GetLayout(); } catch { continue; }
                if (layout is null) continue;
                foreach (var frame in layout.GetElementsAsFlattenedList().OfType<MapFrame>())
                    if (string.Equals(frame.Map?.Name, map.Name, StringComparison.OrdinalIgnoreCase))
                        layoutFrames.Add((layout, frame));
            }
            if (layoutFrames.Count == 0)
                return OperationResult<object?>.Fail(ErrorCodes.NotFound, $"No layout map frame references map '{mapName}'.");
            if (layoutFrames.Count > 1)
                return OperationResult<object?>.Fail(ErrorCodes.AmbiguousLayerName,
                    $"Map '{mapName}' appears in {layoutFrames.Count} layout frames; the contract has no layout/frame selector.");

            var (targetLayout, targetFrame) = layoutFrames[0];
            FeatureLayer? selectedLayer = null;
            var actualIndexField = string.Empty;
            var actualSortField = string.Empty;
            var actualNameField = string.Empty;
            MapSeries? series;
            try
            {
                series = extentSource == "bookmarks"
                    ? MapSeries.CreateBookmarkMapSeries(targetLayout, targetFrame)
                    : CreateSpatialSeries(targetLayout, targetFrame, map, indexField, sortField, nameField,
                        extentSource == "fixed", out selectedLayer, out actualIndexField, out actualSortField, out actualNameField);
            }
            catch (InvalidOperationException ex)
            {
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                    "Map-series inputs are missing or ambiguous; no layout state was changed.", ex.Message);
            }

            if (series is null)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "A map series could not be created from the selected extent source.");
            targetLayout.SetMapSeries(series);
            var result = new Dictionary<string, object?>
            {
                ["map"] = map.Name,
                ["layout"] = targetLayout.Name,
                ["mapFrame"] = targetFrame.Name,
                ["extentSource"] = extentSource,
                ["extentPolicy"] = extentSource switch
                {
                    "layer" => "best_fit",
                    "fixed" => "center_and_maintain_scale",
                    _ => "bookmark_extent",
                },
                ["enabled"] = series.Enabled,
                ["pageCount"] = SafePageCount(series),
                ["updated"] = true,
            };
            if (extentSource == "layer")
            {
                result["indexLayer"] = selectedLayer!.Name;
                result["indexField"] = actualIndexField;
                result["sortField"] = actualSortField;
                result["nameField"] = actualNameField;
                result["fieldParametersApplied"] = true;
                if (extentSource == "fixed")
                    result["disclosure"] = "Each index feature is centered while the map frame retains its current scale.";
            }
            else
            {
                result["fieldParametersApplied"] = false;
                result["ignoredFieldParameters"] = new Dictionary<string, object?>
                {
                    ["indexField"] = indexField, ["sortField"] = sortField, ["nameField"] = nameField,
                };
                result["disclosure"] = "Bookmark page order and names derive from the selected map bookmarks; indexField, sortField and nameField are not used by the bookmark-series API.";
            }
            return OperationResult<object?>.Ok(result);
        }, TaskCreationOptions.None);

    private static MapSeries CreateSpatialSeries(
        Layout layout, MapFrame frame, Map map, string indexField, string? sortField, string? nameField,
        bool fixedScale,
        out FeatureLayer? selectedLayer, out string actualIndexField, out string actualSortField, out string actualNameField)
    {
        selectedLayer = null;
        actualIndexField = actualSortField = actualNameField = string.Empty;
        var requested = new[] { indexField, sortField, nameField }.Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var candidates = new List<(FeatureLayer Layer, Dictionary<string, string> Fields)>();
        foreach (var layer in map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
        {
            using var table = layer.GetTable();
            if (table is null) continue;
            using var definition = table.GetDefinition();
            var fields = definition.GetFields().ToDictionary(f => f.Name, f => f.Name, StringComparer.OrdinalIgnoreCase);
            if (requested.All(fields.ContainsKey)) candidates.Add((layer, fields));
        }
        if (candidates.Count == 0)
            throw new InvalidOperationException("No feature layer in the map contains every requested map-series field.");
        if (candidates.Count > 1)
            throw new InvalidOperationException($"{candidates.Count} feature layers contain the requested fields; the contract has no index-layer selector.");
        selectedLayer = candidates[0].Layer;
        var resolvedFields = candidates[0].Fields;
        actualIndexField = resolvedFields[indexField];
        actualSortField = string.IsNullOrWhiteSpace(sortField) ? actualIndexField : resolvedFields[sortField!];
        actualNameField = string.IsNullOrWhiteSpace(nameField) ? actualIndexField : resolvedFields[nameField!];
        var series = MapSeries.CreateSpatialMapSeries(layout, frame, selectedLayer, actualNameField);
        var definitionCim = series.GetDefinition() as CIMSpatialMapSeries
            ?? throw new InvalidOperationException("ArcGIS did not return a spatial map-series definition.");
        definitionCim.ExtentOptions = fixedScale ? ExtentFitType.ExtentCenter : ExtentFitType.BestFit;
        definitionCim.NumberField = actualIndexField;
        definitionCim.NameField = actualNameField;
        definitionCim.SortField = actualSortField;
        definitionCim.SortAscending = true;
        series.SetDefinition(definitionCim);
        return series;
    }

    private static bool TryRestore(Element element, IReadOnlyDictionary<string, object?> originals)
    {
        var complete = true;
        try { element.SetLocked(false); } catch { complete = false; }
        foreach (var pair in originals)
        {
            try
            {
                switch (pair.Key)
                {
                    case "x": element.SetX((double)pair.Value!); break;
                    case "y": element.SetY((double)pair.Value!); break;
                    case "width": element.SetWidth((double)pair.Value!); break;
                    case "height": element.SetHeight((double)pair.Value!); break;
                    case "rotation": element.SetRotation((double)pair.Value!); break;
                    case "visible": element.SetVisible((bool)pair.Value!); break;
                    case "name": element.SetName((string)pair.Value!); break;
                }
            }
            catch { complete = false; }
        }
        if (originals.TryGetValue("locked", out var locked))
        {
            try { element.SetLocked((bool)locked!); } catch { complete = false; }
        }
        return complete;
    }

    private static int? SafePageCount(MapSeries series)
    {
        try { return series.PageCount; } catch { return null; }
    }

    private static double? NativePageUnitsPerInch(string units) => units switch
    {
        "esriInches" => 1d,
        "esriPoints" => 72d,
        "esriMillimeters" => 25.4d,
        "esriCentimeters" => 2.54d,
        "esriFeet" => 1d / 12d,
        "esriMeters" => 0.0254d,
        "esriKilometers" => 0.0000254d,
        _ => null,
    };
}
