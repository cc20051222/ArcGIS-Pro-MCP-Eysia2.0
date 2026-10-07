# 合成语料 revision-3 × GP 白名单 53 件覆盖差集盘点 · D-117 Phase A

- 载体候选＝语料中 `kind∈{input, updated-instance}` 共 **32** 件（按件名清单见机读件 `carrierArtefacts`）。
- 类型兼容口径＝按件 `format` 的格式族映射（栅格／点云／矢量·表／压缩包），**属格式判断，非 schema 资格判定，亦非准入证据**。
- 全域结论＝**executableInputAvailable=False（53/53）**；出处见 `gp-whitelist-closure-53-v1.md` 第四节。

| 件名 | 需 GIS 载体的输入类型 | 类型兼容载体数 | 兼容件（前 8） | 可执行输入可用 |
|---|---|---|---|---|
| `analysis.Buffer` | `featurelayer`、`multivalue` | 27 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 27） | 否 |
| `analysis.Clip` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `analysis.Erase` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `analysis.Union` | `multivalue` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `analysis.Intersect` | `multivalue` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `analysis.Identity` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `analysis.SpatialJoin` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `analysis.Near` | `featurelayer`、`multivalue` | 27 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 27） | 否 |
| `analysis.Statistics` | `multivalue`、`tableview` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `analysis.Frequency` | `multivalue`、`tableview` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `analysis.TabulateArea` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `analysis.TabulateIntersection` | `featurelayer`、`multivalue` | 27 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 27） | 否 |
| `management.Dissolve` | `featurelayer`、`multivalue` | 27 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 27） | 否 |
| `management.Merge` | `multivalue` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `management.Project` | `dataset`、`multivalue` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `management.RepairGeometry` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `management.CalculateField` | `tableview` | 14 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X4-03`、`D101-X4-04`、`D101-X5-02`、`D101-X5-05`、`D101-X6-01`（共 14） | 否 |
| `management.CopyFeatures` | `dataset` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `management.MultipartToSinglepart` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `conversion.ExportFeatures` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `conversion.TableToTable` | `tableview` | 14 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X4-03`、`D101-X4-04`、`D101-X5-02`、`D101-X5-05`、`D101-X6-01`（共 14） | 否 |
| `conversion.RasterToPolygon` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `conversion.RasterToPoint` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `conversion.PolygonToRaster` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `conversion.PointToRaster` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `conversion.PolylineToRaster` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `conversion.FeatureToRaster` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `conversion.ASCIIToRaster` | 不需 GIS 载体 | 0 | — | 否 |
| `conversion.RasterToASCII` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `conversion.FeaturesToJSON` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `conversion.JSONToFeatures` | 不需 GIS 载体 | 0 | — | 否 |
| `sa.Slope` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.Aspect` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.Hillshade` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.Contour` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.Curvature` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.Fill` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.FlowDirection` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.FlowAccumulation` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.Watershed` | `dataset`、`rasterlayer` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `sa.SnapPourPoint` | `dataset`、`rasterlayer` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `sa.ZonalStatistics` | `dataset`、`rasterlayer` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `sa.ZonalStatisticsAsTable` | `dataset`、`rasterlayer` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `sa.Reclassify` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.ExtractByMask` | `dataset`、`rasterlayer` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `sa.ExtractValuesToPoints` | `featurelayer`、`rasterlayer` | 27 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 27） | 否 |
| `sa.Resample` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.Con` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.CellStatistics` | `multivalue` | 24 | `D101-TS08-01`、`D101-TS08-04`、`D101-X1-01`、`D101-X1-04`、`D101-X2-01`、`D101-X2-02`、`D101-X2-05`、`D101-X2-08`（共 24） | 否 |
| `sa.FocalStatistics` | `rasterlayer` | 10 | `D101-TS08-01`、`D101-TS08-04`、`D101-X2-01`、`D101-X2-05`、`D101-X2-08`、`D101-X4-01`、`D101-X4-06`、`D101-X4-09`（共 10） | 否 |
| `sa.RasterCalculator` | 不需 GIS 载体 | 0 | — | 否 |
| `management.AddJoin` | `featurelayer`、`tableview` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
| `management.RemoveJoin` | `featurelayer` | 17 | `D101-X1-01`、`D101-X1-04`、`D101-X2-02`、`D101-X3-01`、`D101-X3-03`、`D101-X3-06`、`D101-X4-03`、`D101-X4-04`（共 17） | 否 |
