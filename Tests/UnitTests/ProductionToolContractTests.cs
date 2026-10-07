using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Tools;
using ArcGISProMCP.Tools;

namespace ArcGISProMCP.UnitTests;

public sealed class ProductionToolContractTests
{
    private static readonly Regex LowerSnakeCase =
        new("^[a-z][a-z0-9]*(?:_[a-z][a-z0-9]*)*$", RegexOptions.CultureInvariant);

    [Fact]
    public void ProductionRegistry_Count_MatchesCurrent239_AndNamesAreUnique()
    {
        var tools = ProductionTools();

        // Phase 9 第一批（D-034）：35 → 39；第二批（D-035）：39 → 42；第三批（D-036）：42 → 45；
        // 第四批（D-038）：45 → 48；Phase 10 第一批（D-042）：48 → 52；
        // Phase 10 第二批（D-043）：52 → 55（set_definition_query / move_layer / set_map_extent）；
        // Phase 10 第三批（D-045）：55 → 60（create_layout / add_layout_text / add_legend / add_north_arrow / add_scale_bar）；
        // Phase 10 第四批（D-046）：60 → 64（get_layer_symbology / set_simple_symbology / get_label_info / set_label_visibility）；
        // Phase 10 第五批（D-047）：64 → 66（export_layout_pdf / export_layout_png；export_map_png 因 MG3' 降级不实现，如实登记）。
        // Phase 11 第一批（D-048）：66 → 69（spatial_join / near / raster_clip；select_by_location 已在现役集不重复实现）。
        // Phase 11 第二批（D-049）：69 → 73（erase / union / raster_resample / raster_statistics；raster_project 因 O-D049-01 放弃）。
        // Phase 11 第三批（D-050）：73 → 75（raster_mosaic / raster_calc；Phase 11 收官批）。
        // Phase 12 第二批（D-053）：75 → 78（delete_dataset / rename_dataset / append_features；**允许的契约面扩大——新增工具**）。
        // Phase 15 功能批（D-060 · B 项，G-160 批准）：78 → 80（cell_statistics / focal_statistics）。
        Assert.Equal(239, tools.Count);   // D-102：208 → 223；D-104：注册 inspect_point_cloud（X15）；D-118：+15 件 M4 Native → 239
        Assert.Equal(
            tools.Count,
            tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ProductionRegistry_Names_MatchTheApprovedSnapshot()
    {
        var actual = ProductionTools().Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var expected = SnapshotByName().Keys.ToHashSet(StringComparer.Ordinal);

        Assert.True(expected.SetEquals(actual), DescribeSetDifference(expected, actual));
    }

    [Fact]
    public void ProductionToolClasses_MatchRegisteredTypes()
    {
        var registeredTypes = ProductionTools().Select(tool => tool.GetType()).ToHashSet();
        var toolTypes = typeof(PingTool).Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace == typeof(PingTool).Namespace &&
                type.IsClass &&
                !type.IsAbstract &&
                typeof(IMCPTool).IsAssignableFrom(type))
            .ToHashSet();

        Assert.Equal(239, toolTypes.Count);   // D-118: 239 registered production tool types
        Assert.True(
            toolTypes.SetEquals(registeredTypes),
            DescribeTypeDifference(toolTypes, registeredTypes));
    }

    [Fact]
    public void ProductionToolMetadata_MatchesSnapshot_AndUsesValidValues()
    {
        var expected = SnapshotByName();
        var validCategories = PublicStringConstants(typeof(ToolCategories));
        var validExecutionTypes = PublicStringConstants(typeof(ExecutionTypes));

        foreach (var tool in ProductionTools())
        {
            Assert.True(expected.TryGetValue(tool.Name, out var contract), $"Unexpected tool: {tool.Name}");

            var metadata = tool.Metadata;
            Assert.NotNull(metadata);
            Assert.Equal(tool.Name, metadata.Name);
            Assert.NotNull(tool.InputSchema);
            Assert.False(string.IsNullOrWhiteSpace(metadata.DisplayName), $"{tool.Name}: empty DisplayName");
            Assert.False(string.IsNullOrWhiteSpace(metadata.Description), $"{tool.Name}: empty Description");
            Assert.False(string.IsNullOrWhiteSpace(metadata.Version), $"{tool.Name}: empty Version");
            Assert.Matches("^[0-9]+\\.[0-9]+\\.[0-9]+$", metadata.Version);
            Assert.Contains(metadata.Category, validCategories);
            Assert.Contains(metadata.ExecutionType, validExecutionTypes);
            Assert.Equal(contract!.Category, metadata.Category);
            Assert.Equal(contract.ExecutionType, metadata.ExecutionType);
            Assert.Equal(contract.RequiresArcGIS, metadata.RequiresArcGIS);
            Assert.True(metadata.SupportsCancellation, $"{tool.Name}: cancellation support is disabled");
        }
    }

    [Fact]
    public void ProductionToolNames_UseLowerSnakeCase()
    {
        foreach (var tool in ProductionTools())
        {
            Assert.Matches(LowerSnakeCase, tool.Name);
        }
    }

    [Fact]
    public void ProductionToolSchemas_HaveObjectPropertiesAndValidRequiredArrays()
    {
        foreach (var tool in ProductionTools())
        {
            using var document = ParseSchema(tool);
            var root = document.RootElement;

            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            Assert.Equal("object", root.GetProperty("type").GetString());
            var properties = root.GetProperty("properties");
            Assert.Equal(JsonValueKind.Object, properties.ValueKind);
            foreach (var property in properties.EnumerateObject())
            {
                Assert.Equal(JsonValueKind.Object, property.Value.ValueKind);
                var propertyType = property.Value.GetProperty("type");
                // D-052 O-D043-01：type 允许**字符串数组**（如 set_map_extent.spatialReference = ["string","integer"]）
                if (propertyType.ValueKind == JsonValueKind.Array)
                {
                    Assert.True(
                        propertyType.GetArrayLength() > 0,
                        $"{tool.Name}: property '{property.Name}' has an empty type array");
                    foreach (var item in propertyType.EnumerateArray())
                    {
                        Assert.Equal(JsonValueKind.String, item.ValueKind);
                        Assert.False(
                            string.IsNullOrWhiteSpace(item.GetString()),
                            $"{tool.Name}: property '{property.Name}' has a blank type in array");
                    }
                }
                else
                {
                    Assert.Equal(JsonValueKind.String, propertyType.ValueKind);
                    Assert.False(
                        string.IsNullOrWhiteSpace(propertyType.GetString()),
                        $"{tool.Name}: property '{property.Name}' has an empty type");
                }

                if (propertyType.ValueKind == JsonValueKind.String && propertyType.GetString() == "array")
                {
                    if (!property.Value.TryGetProperty("items", out var items))
                    {
                        // F03 frozen schemas keep certain arrays untyped (item shape validated at runtime):
                        // - refresh_design_bundle.dataUpdates (D-086)
                        // - validate_geometries.checks, compare_datasets.keyFields, validate_field_constraints.constraints (D-088)
                        // - create_domain.codedValues, update_domain.codedValues, configure_subtypes.subtypes, calculate_geometry_attributes.fields (D-092)
                        // - calculate_service_areas.breaks (D-094)
                        // - raster_pixel_inspect.points/bands, compose_raster_bands.inputs/bandOrder (D-096)
                        // - ps_create_adjustment_layer.targetLayerIds, ps_validate_document.checks (D-102 frozen schemas omit item schemas; operations remain fail-closed)
                        var allowed = new HashSet<string>(StringComparer.Ordinal)
                        {
                            "refresh_design_bundle", "validate_geometries", "compare_datasets", "validate_field_constraints",
                            "create_domain", "update_domain", "configure_subtypes", "calculate_geometry_attributes",
                            "calculate_service_areas", "raster_pixel_inspect", "compose_raster_bands",
                            "ps_export_deliverables", "ps_create_adjustment_layer", "ps_validate_document",
                            // D-091 frozen formats use enum_items instead of an items object.
                            // D-118 frozen faces keep these arrays untyped (item shape validated at runtime by each tool):
                            // - export_time_animation.timeLayers (X06)
                            // - analyze_scenario_sensitivity.scenarios (X32)
                            // - compare_multi_criteria_scenarios.criteria/constraints (X34)
                            // - evaluate_scenario_ensemble.ensemble (X35)
                            // - validate_statistical_assumptions.variables/checks (X36)
                            // - validate_interchange_conformance.deepChecks (X43)
                            "export_time_animation", "analyze_scenario_sensitivity", "compare_multi_criteria_scenarios",
                            "evaluate_scenario_ensemble", "validate_statistical_assumptions", "validate_interchange_conformance",
                        };
                        Assert.True(allowed.Contains(tool.Name),
                            $"{tool.Name}: property '{property.Name}' is an untyped array not in the F03 frozen allowance.");
                        continue;
                    }
                    Assert.Equal(JsonValueKind.Object, items.ValueKind);
                    Assert.Equal(JsonValueKind.String, items.GetProperty("type").ValueKind);
                }
            }

            if (!root.TryGetProperty("required", out var required))
            {
                continue;
            }

            Assert.Equal(JsonValueKind.Array, required.ValueKind);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in required.EnumerateArray())
            {
                Assert.Equal(JsonValueKind.String, item.ValueKind);
                var name = item.GetString();
                Assert.False(string.IsNullOrWhiteSpace(name), $"{tool.Name}: blank required property");
                Assert.True(names.Add(name!), $"{tool.Name}: duplicate required property '{name}'");
            }
        }
    }

    [Fact]
    public void ProductionToolSchemas_RequiredPropertiesExistInProperties()
    {
        foreach (var tool in ProductionTools())
        {
            using var document = ParseSchema(tool);
            var root = document.RootElement;
            var properties = root.GetProperty("properties");

            if (!root.TryGetProperty("required", out var required))
            {
                continue;
            }

            foreach (var item in required.EnumerateArray())
            {
                var name = item.GetString();
                Assert.True(properties.TryGetProperty(name!, out _), $"{tool.Name}: required '{name}' is not declared");
            }
        }
    }

    [Fact]
    public void ProductionToolSchemas_SerializeAndParseAsJson()
    {
        foreach (var tool in ProductionTools())
        {
            var json = JsonSerializer.Serialize(tool.InputSchema);
            Assert.False(string.IsNullOrWhiteSpace(json), $"{tool.Name}: schema serialized to empty JSON");
            using var document = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        }
    }

    [Fact]
    public void PythonTools_HaveTheExpectedSevenToolContract()
    {
        // Phase 8.3 (D-013)：按 Category=Python 过滤（select_by_attribute/location 虽走 Python
        // 执行但 Category=Selection，属选择契约，不在此断言范围）。
        var expected = ProductionToolContractSnapshot.Tools
            .Where(contract => contract.Category == ToolCategories.Python)
            .ToDictionary(contract => contract.Name, StringComparer.Ordinal);
        var actual = ProductionTools()
            .Where(tool => tool.Metadata.Category == ToolCategories.Python)
            .ToList();

        Assert.Equal(7, actual.Count);
        Assert.True(
            expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(actual.Select(tool => tool.Name)),
            DescribeSetDifference(expected.Keys, actual.Select(tool => tool.Name)));

        foreach (var tool in actual)
        {
            Assert.Equal(ToolCategories.Python, tool.Metadata.Category);
            Assert.Equal(ExecutionTypes.Python, tool.Metadata.ExecutionType);
            Assert.False(tool.Metadata.RequiresArcGIS);
        }

        var expectedSchemas = new Dictionary<string, (string[] Required, IReadOnlyDictionary<string, string> Types)>(
            StringComparer.Ordinal)
        {
            ["python_bridge_ping"] = (Array.Empty<string>(), new Dictionary<string, string>(StringComparer.Ordinal)),
            ["python_runtime_info"] = (Array.Empty<string>(), new Dictionary<string, string>(StringComparer.Ordinal)),
            ["dataset_summary"] = (["dataset_path"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dataset_path"] = "string"
            }),
            ["list_fields"] = (["dataset_path"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dataset_path"] = "string"
            }),
            ["list_workspace_datasets"] = (["workspace_path"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["workspace_path"] = "string",
                ["recursive"] = "boolean",
                ["max_depth"] = "integer",
                ["max_items"] = "integer"
            }),
            ["get_dataset_info"] = (["path"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["path"] = "string"
            }),
            ["get_raster_info"] = (["datasetPath"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["datasetPath"] = "string"
            })
        };

        foreach (var tool in actual)
        {
            var schema = expectedSchemas[tool.Name];
            AssertSchemaShape(tool, schema.Required, schema.Types);
        }
    }

    [Fact]
    public void GeoprocessingTools_HaveTheExpectedSevenToolContract()
    {
        // D-014 方案 A：select_by_attribute/location 改 GP 执行（Category=Selection），不在 Analysis 断言范围。
        // Phase 11 第一批（D-048）：4 → 7（spatial_join / near / raster_clip）。
        // Phase 11 第二批（D-049）：7 → 11（erase / union / raster_resample / raster_statistics）。
        // Phase 11 第三批（D-050）：11 → 13（raster_mosaic / raster_calc）。
        // Phase 15 功能批（D-060 · B 项）：13 → 15（cell_statistics / focal_statistics）。
        // D-094 M2 批三 P5：15 → 23（八件分析工具；polygon_neighbors 为 Native，其余 7 件 GP）。
        // D-096 M2 批四 P6：23 → 28（栅格族五件 Analysis/GP；raster_pixel_inspect 为 Quality 不在此范围）。
        var expected = ProductionToolContractSnapshot.Tools
            .Where(contract => contract.Category == ToolCategories.Analysis)
            .ToDictionary(contract => contract.Name, StringComparer.Ordinal);
        var actual = ProductionTools()
            .Where(tool => tool.Metadata.Category == ToolCategories.Analysis)
            .ToList();

        Assert.Equal(28, actual.Count);
        Assert.True(
            expected.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(actual.Select(tool => tool.Name)),
            DescribeSetDifference(expected.Keys, actual.Select(tool => tool.Name)));

        foreach (var tool in actual)
        {
            Assert.Equal(ToolCategories.Analysis, tool.Metadata.Category);
            // D-094：polygon_neighbors 为 Native（冻结 schema executionType=Native），其余 Analysis 件为 Geoprocessing。
            Assert.True(
                tool.Metadata.ExecutionType == ExecutionTypes.Geoprocessing
                || (tool.Name == "polygon_neighbors" && tool.Metadata.ExecutionType == ExecutionTypes.Native),
                $"{tool.Name}: unexpected ExecutionType {tool.Metadata.ExecutionType}");
            Assert.True(tool.Metadata.RequiresArcGIS);
        }

        var expectedSchemas = new Dictionary<string, (string[] Required, IReadOnlyDictionary<string, string> Types)>(
            StringComparer.Ordinal)
        {
            ["buffer"] = (["input", "output", "distance"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["output"] = "string",
                ["distance"] = "number",
                ["distanceUnit"] = "string",
                ["dissolve"] = "boolean",
                ["overwrite"] = "boolean"
            }),
            ["clip"] = (["input", "clipFeatures", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["clipFeatures"] = "string",
                ["output"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["intersect"] = (["inputs", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inputs"] = "string",
                ["output"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["dissolve"] = (["input", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["output"] = "string",
                ["dissolveField"] = "string",
                ["overwrite"] = "boolean"
            }),
            // Phase 11 第一批（D-048，66 -> 69）：空间分析首批
            ["spatial_join"] = (["targetFeatures", "joinFeatures", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetFeatures"] = "string",
                ["joinFeatures"] = "string",
                ["output"] = "string",
                ["joinOperation"] = "string",
                ["joinType"] = "string",
                ["matchOption"] = "string",
                ["searchRadius"] = "number",
                ["searchRadiusUnit"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["near"] = (["input", "nearFeatures", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["nearFeatures"] = "string",
                ["output"] = "string",
                ["searchRadius"] = "number",
                ["searchRadiusUnit"] = "string",
                ["location"] = "boolean",
                ["angle"] = "boolean",
                ["method"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["raster_clip"] = (["inputRaster", "maskFeatures", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inputRaster"] = "string",
                ["maskFeatures"] = "string",
                ["output"] = "string",
                ["extractionArea"] = "string",
                ["overwrite"] = "boolean"
            }),
            // Phase 11 第二批（D-049，69 -> 73）
            ["erase"] = (["input", "eraseFeatures", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["eraseFeatures"] = "string",
                ["output"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["union"] = (["inputs", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inputs"] = "string",
                ["output"] = "string",
                ["joinAttributes"] = "string",
                ["gaps"] = "boolean",
                ["overwrite"] = "boolean"
            }),
            ["raster_resample"] = (["inputRaster", "output", "cellSize"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inputRaster"] = "string",
                ["output"] = "string",
                ["cellSize"] = "string",
                ["resamplingType"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["raster_statistics"] = (["inputRaster"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inputRaster"] = "string",
                ["propertyType"] = "string",
                ["bandIndex"] = "string"
            }),
            // Phase 11 第三批（D-050，73 -> 75）
            ["raster_mosaic"] = (["inputs", "output", "numberOfBands"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inputs"] = "string",
                ["output"] = "string",
                ["numberOfBands"] = "string",
                ["cellSize"] = "string",
                ["pixelType"] = "string",
                ["mosaicMethod"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["raster_calc"] = (["rasters", "expression", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["rasters"] = "string",
                ["expression"] = "string",
                ["output"] = "string",
                ["overwrite"] = "boolean"
            }),
            // Phase 15 功能批（D-060 · B 项，G-160 批准，78 -> 80）
            ["cell_statistics"] = (["inputRasters", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inputRasters"] = "string",
                ["output"] = "string",
                ["statisticsType"] = "string",
                ["ignoreNoData"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["focal_statistics"] = (["inputRaster", "output"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inputRaster"] = "string",
                ["output"] = "string",
                ["neighborhood"] = "string",
                ["statisticsType"] = "string",
                ["ignoreNoData"] = "string",
                ["overwrite"] = "boolean"
            }),
            // D-094 M2 批三 P5 八件分析工具
            ["simplify_features"] = (["input", "outputPath", "algorithm", "tolerance"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["outputPath"] = "string",
                ["algorithm"] = "string",
                ["tolerance"] = "number",
                ["preserveTopology"] = "boolean",
                ["overwrite"] = "boolean"
            }),
            ["smooth_features"] = (["input", "outputPath", "algorithm", "tolerance"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["outputPath"] = "string",
                ["algorithm"] = "string",
                ["tolerance"] = "number",
                ["preserveEndpoints"] = "boolean",
                ["overwrite"] = "boolean"
            }),
            ["polygon_neighbors"] = (["input", "outputPath"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["outputPath"] = "string",
                ["includeEdgeLength"] = "boolean",
                ["includePointTouches"] = "boolean",
                ["overwrite"] = "boolean"
            }),
            ["generate_tessellation"] = (["extent", "outputPath", "shapeType", "size"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["extent"] = "object",
                ["outputPath"] = "string",
                ["shapeType"] = "string",
                ["size"] = "number",
                ["overwrite"] = "boolean"
            }),
            ["calculate_service_areas"] = (["network", "facilities", "outputPath", "breaks", "impedance"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["network"] = "string",
                ["facilities"] = "string",
                ["outputPath"] = "string",
                ["breaks"] = "array",
                ["impedance"] = "string",
                ["travelDirection"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["solve_routes"] = (["network", "stops", "outputPath", "impedance"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["network"] = "string",
                ["stops"] = "string",
                ["outputPath"] = "string",
                ["impedance"] = "string",
                ["findBestOrder"] = "boolean",
                ["preserveFirstLast"] = "boolean",
                ["overwrite"] = "boolean"
            }),
            ["spatial_autocorrelation"] = (["input", "outputPath", "field"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["outputPath"] = "string",
                ["field"] = "string",
                ["conceptualization"] = "string",
                ["standardization"] = "string",
                ["local"] = "boolean",
                ["overwrite"] = "boolean"
            }),
            ["hotspot_analysis"] = (["input", "outputPath", "field"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["outputPath"] = "string",
                ["field"] = "string",
                ["conceptualization"] = "string",
                ["falseDiscoveryRate"] = "boolean",
                ["local"] = "boolean",
                ["overwrite"] = "boolean"
            }),
            // D-096 M2 批四 P6 栅格族五件 Analysis/GP（raster_pixel_inspect 为 Quality，不在此范围）
            ["build_raster_pyramids"] = (["input"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["resampling"] = "string",
                ["levels"] = "integer",
                ["skipFirst"] = "boolean"
            }),
            ["compose_raster_bands"] = (["inputs", "outputPath"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["inputs"] = "array",
                ["outputPath"] = "string",
                ["bandOrder"] = "array",
                ["outputPixelType"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["raster_change_detection"] = (["before", "afterRaster", "outputPath", "method"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["before"] = "string",
                ["afterRaster"] = "string",
                ["outputPath"] = "string",
                ["method"] = "string",
                ["threshold"] = "number",
                ["overwrite"] = "boolean"
            }),
            ["raster_reproject"] = (["input", "outputPath", "targetCrs"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["input"] = "string",
                ["outputPath"] = "string",
                ["targetCrs"] = "string",
                ["resampling"] = "string",
                ["cellSize"] = "number",
                ["transformation"] = "string",
                ["overwrite"] = "boolean"
            }),
            ["zonal_histogram"] = (["zones", "values", "outputPath"], new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zones"] = "string",
                ["zoneField"] = "string",
                ["values"] = "string",
                ["outputPath"] = "string",
                ["binWidth"] = "number",
                ["overwrite"] = "boolean"
            })
        };

        foreach (var tool in actual)
        {
            var schema = expectedSchemas[tool.Name];
            AssertSchemaShape(tool, schema.Required, schema.Types);
        }
    }

    [Fact]
    public void ForbiddenTools_AreNotRegistered()
    {
        var registry = ProductionCompositionAccess.BuildRegistry();

        foreach (var forbiddenName in ProductionToolContractSnapshot.ForbiddenToolNames)
        {
            Assert.False(registry.Contains(forbiddenName), $"Forbidden tool is registered: {forbiddenName}");
            Assert.Null(registry.Get(forbiddenName));
        }
    }

    private static IReadOnlyList<IMCPTool> ProductionTools()
        => ProductionCompositionAccess.BuildRegistry().List();

    private static Dictionary<string, ProductionToolContract> SnapshotByName()
        => ProductionToolContractSnapshot.Tools.ToDictionary(contract => contract.Name, StringComparer.Ordinal);

    private static JsonDocument ParseSchema(IMCPTool tool)
        => JsonDocument.Parse(JsonSerializer.Serialize(tool.InputSchema));

    private static void AssertSchemaShape(
        IMCPTool tool,
        IEnumerable<string> expectedRequired,
        IReadOnlyDictionary<string, string> expectedPropertyTypes)
    {
        using var document = ParseSchema(tool);
        var root = document.RootElement;
        var actualPropertyTypes = root
            .GetProperty("properties")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetProperty("type").GetString()!, StringComparer.Ordinal);
        var actualRequired = root.TryGetProperty("required", out var required)
            ? required.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var expectedRequiredSet = expectedRequired.ToHashSet(StringComparer.Ordinal);

        Assert.True(
            expectedPropertyTypes.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(actualPropertyTypes.Keys),
            $"{tool.Name}: property names differ");
        Assert.True(
            expectedRequiredSet.SetEquals(actualRequired),
            $"{tool.Name}: required names differ");

        foreach (var (name, type) in expectedPropertyTypes)
        {
            Assert.Equal(type, actualPropertyTypes[name]);
        }
    }

    private static HashSet<string> PublicStringConstants(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    private static string DescribeSetDifference(
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var actualSet = actual.ToHashSet(StringComparer.Ordinal);
        var missing = expectedSet.Except(actualSet, StringComparer.Ordinal).OrderBy(name => name);
        var unexpected = actualSet.Except(expectedSet, StringComparer.Ordinal).OrderBy(name => name);
        return $"Missing: [{string.Join(", ", missing)}]; Unexpected: [{string.Join(", ", unexpected)}]";
    }

    private static string DescribeTypeDifference(
        IEnumerable<Type> expected,
        IEnumerable<Type> actual)
    {
        var expectedSet = expected.ToHashSet();
        var actualSet = actual.ToHashSet();
        var missing = expectedSet.Except(actualSet).Select(type => type.FullName).OrderBy(name => name);
        var unexpected = actualSet.Except(expectedSet).Select(type => type.FullName).OrderBy(name => name);
        return $"Missing: [{string.Join(", ", missing)}]; Unexpected: [{string.Join(", ", unexpected)}]";
    }
}

/// <summary>
/// Test-only access to the real Composition assembly. The production Composition remains internal
/// and is invoked through its public BuildRegistry method; no second test registry is created.
/// </summary>
internal static class ProductionCompositionAccess
{
    private static readonly Lazy<MCPToolRegistry> Registry =
        new(LoadProductionRegistry, LazyThreadSafetyMode.ExecutionAndPublication);

    public static MCPToolRegistry BuildRegistry() => Registry.Value;

    public static T CreateProductionService<T>(string typeName)
    {
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(FindCompatibilityAssemblyPath());
        var serviceType = assembly.GetType(typeName, throwOnError: true)!;
        return (T)Activator.CreateInstance(serviceType)!;
    }

    private static MCPToolRegistry LoadProductionRegistry()
    {
        var assemblyPath = FindCompatibilityAssemblyPath();
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        var compositionType = assembly.GetType("ArcGISProMCP.Compatibility.Composition", throwOnError: true)!;
        var method = compositionType.GetMethod(
            "BuildRegistry",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        if (method is null || method.ReturnType != typeof(MCPToolRegistry))
        {
            throw new InvalidOperationException(
                "Production Composition.BuildRegistry() was not found with the expected return type.");
        }

        try
        {
            return (MCPToolRegistry)method.Invoke(null, null)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new InvalidOperationException(
                "Production Composition.BuildRegistry() failed.",
                ex.InnerException);
        }
    }

    private static string FindCompatibilityAssemblyPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var binRoot = Path.Combine(repositoryRoot, "Source", "ArcGISProMCP.Compatibility", "bin");
        if (!Directory.Exists(binRoot))
        {
            throw new InvalidOperationException($"Compatibility build output was not found: {binRoot}");
        }

        var path = Directory
            .EnumerateFiles(binRoot, "ArcGISProMCP.Compatibility.dll", SearchOption.AllDirectories)
            .Where(candidate => !candidate.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return path is null
            ? throw new InvalidOperationException(
                $"Production Compatibility assembly was not found below: {binRoot}")
            : Path.GetFullPath(path);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ArcGIS-Pro-MCP.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Repository root could not be located from test base directory: {AppContext.BaseDirectory}");
    }
}
