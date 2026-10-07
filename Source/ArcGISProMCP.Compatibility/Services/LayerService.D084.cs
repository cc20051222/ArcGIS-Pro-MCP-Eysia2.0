using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using System.Drawing.Text;

namespace ArcGISProMCP.Compatibility.Services;

/// <summary>D-084 Session label changes. Expressions are normalized to one Arcade field reference.</summary>
public sealed partial class LayerService
{
    public Task<OperationResult<object?>> SetLabelPropertiesAsync(
        string? mapName, string layerName, string? expression, string? fontFamily, double? fontSize,
        string? placement, bool? visible, CancellationToken ct = default)
        => QueuedTask.Run<OperationResult<object?>>(() =>
        {
            ct.ThrowIfCancellationRequested();
            var resolvedMap = MapResolver.Resolve(mapName);
            if (MapResolver.FailIfNotOk<object?>(resolvedMap, mapName) is { } mapFail)
                return OperationResult<object?>.Fail(mapFail.Errors);
            var layerResult = LayerResolver.Resolve(resolvedMap.Map!, layerName, flatten: true);
            if (layerResult.Status == LayerResolveStatus.Ambiguous)
                return OperationResult<object?>.Fail(ErrorCodes.AmbiguousLayerName,
                    AmbiguousLayerMessage(layerName, layerResult));
            if (layerResult.Status != LayerResolveStatus.Ok || layerResult.Layer is null)
                return OperationResult<object?>.Fail(ErrorCodes.LayerNotFound, $"Layer '{layerName}' not found.");
            if (layerResult.Layer is not FeatureLayer featureLayer)
                return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"Layer '{layerName}' does not support feature labels.");

            var classes = featureLayer.LabelClasses;
            var labelClassCount = classes?.Count ?? 0;
            if (labelClassCount == 0)
            {
                if (expression is not null || fontFamily is not null || fontSize.HasValue || placement is not null || visible == true)
                    return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                        $"Layer '{layerName}' has no label class; this tool does not create classes.");
                featureLayer.SetLabelVisibility(false);
                return OperationResult<object?>.Ok(new Dictionary<string, object?>
                {
                    ["layer"] = featureLayer.Name,
                    ["map"] = resolvedMap.Map!.Name,
                    ["labelClassCount"] = 0,
                    ["enabled"] = featureLayer.IsLabelVisible,
                    ["updatedProperties"] = new[] { "visible" },
                });
            }

            var labelClass = classes![0];
            string? normalizedExpression = null;
            if (expression is not null)
            {
                var trimmed = expression.Trim();
                var requestedField = trimmed.StartsWith("$feature.", StringComparison.OrdinalIgnoreCase)
                    ? trimmed[9..]
                    : trimmed[1..^1];
                using var table = featureLayer.GetTable();
                if (table is null)
                    return OperationResult<object?>.Fail(ErrorCodes.LayerDataSourceUnavailable, "Layer attribute schema is unavailable.");
                using var definition = table.GetDefinition();
                var field = definition.GetFields().FirstOrDefault(f =>
                    string.Equals(f.Name, requestedField, StringComparison.OrdinalIgnoreCase));
                if (field is null)
                    return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"Label field '{requestedField}' does not exist.");
                normalizedExpression = "$feature." + field.Name;
            }

            string? installedFontName = null;
            if (fontFamily is not null)
            {
                using var fonts = new InstalledFontCollection();
                installedFontName = fonts.Families.FirstOrDefault(f =>
                    string.Equals(f.Name, fontFamily.Trim(), StringComparison.OrdinalIgnoreCase))?.Name;
                if (installedFontName is null)
                    return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, $"Font family '{fontFamily}' is not installed.");
            }

            CIMTextSymbol? textSymbol = null;
            if (installedFontName is not null || fontSize.HasValue)
            {
                try { textSymbol = labelClass.GetTextSymbol()?.Clone() as CIMTextSymbol; }
                catch (Exception ex)
                {
                    return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "The current label text symbol cannot be read for editing.", ex.Message);
                }
                if (textSymbol is null)
                    return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "The current label text symbol is unavailable.");
                if (installedFontName is not null) textSymbol.FontFamilyName = installedFontName;
                if (fontSize.HasValue) textSymbol.Height = fontSize.Value;
            }

            CIMStandardLabelPlacementProperties? placementProperties = null;
            if (placement is not null)
            {
                using var table = featureLayer.GetTable();
                if (table is null) return OperationResult<object?>.Fail(ErrorCodes.LayerDataSourceUnavailable, "Layer geometry schema is unavailable.");
                using var definition = table.GetDefinition();
                if (definition is not FeatureClassDefinition featureDefinition
                    || featureDefinition.GetShapeType().ToString() != "Point")
                    return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument,
                        "Directional D-084 placements are supported for point layers only; no label state was changed.");
                try { placementProperties = labelClass.GetStandardLabelPlacementProperties()?.Clone() as CIMStandardLabelPlacementProperties; }
                catch (Exception ex)
                {
                    return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "The current standard label placement cannot be read.", ex.Message);
                }
                if (placementProperties is null)
                    return OperationResult<object?>.Fail(ErrorCodes.InvalidArgument, "The current standard label placement is unavailable.");
                if (placement == "center")
                {
                    placementProperties.PointPlacementMethod = StandardPointPlacementMethod.OnTopPoint;
                    placementProperties.PointPlacementAngles = Array.Empty<double>();
                }
                else
                {
                    placementProperties.PointPlacementMethod = StandardPointPlacementMethod.SpecifiedAngles;
                    placementProperties.PointPlacementAngles = new[]
                    {
                        placement switch
                        {
                            "above" => 90d,
                            "above_right" => 45d,
                            "above_left" => 135d,
                            "below" => 270d,
                            "below_right" => 315d,
                            "below_left" => 225d,
                            _ => throw new ArgumentOutOfRangeException(nameof(placement)),
                        }
                    };
                }
            }

            var oldExpression = labelClass.Expression;
            var oldEngine = labelClass.ExpressionEngine;
            var oldText = labelClass.GetTextSymbol()?.Clone() as CIMTextSymbol;
            var oldPlacement = labelClass.GetStandardLabelPlacementProperties()?.Clone() as CIMStandardLabelPlacementProperties;
            var oldVisibility = featureLayer.IsLabelVisible;
            try
            {
                if (normalizedExpression is not null)
                {
                    labelClass.SetExpression(normalizedExpression);
                    labelClass.SetExpressionEngine(LabelExpressionEngine.Arcade);
                }
                if (textSymbol is not null) labelClass.SetTextSymbol(textSymbol);
                if (placementProperties is not null) labelClass.SetStandardLabelPlacementProperties(placementProperties);
                if (visible.HasValue) featureLayer.SetLabelVisibility(visible.Value);
            }
            catch (Exception ex)
            {
                var rollback = TryRestoreLabelState(labelClass, featureLayer, oldExpression, oldEngine, oldText, oldPlacement, oldVisibility);
                return OperationResult<object?>.Fail(ErrorCodes.InternalError,
                    "Updating label properties failed; a rollback was attempted.",
                    rollback ? ex.Message : ex.Message + "; rollback was incomplete.");
            }

            var updated = new List<string>();
            if (normalizedExpression is not null) updated.Add("expression");
            if (installedFontName is not null) updated.Add("fontFamily");
            if (fontSize.HasValue) updated.Add("fontSize");
            if (placement is not null) updated.Add("placement");
            if (visible.HasValue) updated.Add("visible");
            var readback = labelClass.GetTextSymbol();
            return OperationResult<object?>.Ok(new Dictionary<string, object?>
            {
                ["map"] = resolvedMap.Map!.Name,
                ["layer"] = featureLayer.Name,
                ["labelClass"] = labelClass.Name,
                ["labelClassCount"] = labelClassCount,
                ["expression"] = labelClass.Expression,
                ["expressionEngine"] = labelClass.ExpressionEngine.ToString(),
                ["fontFamily"] = readback?.FontFamilyName,
                ["fontSize"] = readback?.Height,
                ["placement"] = placement,
                ["enabled"] = featureLayer.IsLabelVisible,
                ["updatedProperties"] = updated,
                ["updatedLabelClasses"] = 1,
                ["disclosure"] = "Only the first existing label class was updated; expressions are constrained and normalized to Arcade field syntax.",
            });
        }, TaskCreationOptions.None);

    private static bool TryRestoreLabelState(
        LabelClass labelClass, FeatureLayer layer, string? expression, LabelExpressionEngine engine,
        CIMTextSymbol? textSymbol, CIMStandardLabelPlacementProperties? placement, bool visible)
    {
        var complete = true;
        try { if (expression is not null) labelClass.SetExpression(expression); } catch { complete = false; }
        try { labelClass.SetExpressionEngine(engine); } catch { complete = false; }
        try { if (textSymbol is not null) labelClass.SetTextSymbol(textSymbol); } catch { complete = false; }
        try { if (placement is not null) labelClass.SetStandardLabelPlacementProperties(placement); } catch { complete = false; }
        try { layer.SetLabelVisibility(visible); } catch { complete = false; }
        return complete;
    }
}
