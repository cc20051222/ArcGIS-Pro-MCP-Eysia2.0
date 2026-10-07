using System.Globalization;
using System.Text.Json;
using ArcGISProMCP.Core.Models;
using ArcGISProMCP.Core.Results;
using ArcGISProMCP.Core.Services;
using ArcGISProMCP.Core.Tools;

namespace ArcGISProMCP.Tools;

/// <summary>Frozen D-102 PS schemas and fail-closed candidates. Governance metadata is omitted from public InputSchema.</summary>
internal static class D102PsSchemas
{
    private const string SchemaCompareDocumentVersions = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_compare_document_versions.schema.json"",
 ""title"": ""ps_compare_document_versions"",
 ""type"": ""object"",
 ""properties"": {
  ""leftRef"": {
   ""type"": ""string"",
   ""description"": ""左版本引用（snapshotId 或路径）。""
  },
  ""rightRef"": {
   ""type"": ""string"",
   ""description"": ""右版本引用。""
  },
  ""mode"": {
   ""type"": ""string"",
   ""default"": ""structure"",
   ""description"": ""比较模式。"",
   ""enum"": [
    ""structure"",
    ""pixels"",
    ""both""
   ]
  },
  ""pixelTolerance"": {
   ""type"": ""number"",
   ""default"": 2.0,
   ""description"": ""像素容差（0–255）。""
  }
 },
 ""required"": [
  ""leftRef"",
  ""rightRef""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P12"",
  ""catalogSection"": ""§2 P12"",
  ""overlapWithExisting"": false,
  ""rw"": ""R"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaGetDocumentInfo = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_get_document_info.schema.json"",
 ""title"": ""ps_get_document_info"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  }
 },
 ""required"": [
  ""documentId""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P10"",
  ""catalogSection"": ""§2 P10"",
  ""overlapWithExisting"": false,
  ""rw"": ""R"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaGetLayerInfo = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_get_layer_info.schema.json"",
 ""title"": ""ps_get_layer_info"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""layerId"": {
   ""type"": ""string"",
   ""description"": ""图层 ID。""
  }
 },
 ""required"": [
  ""documentId"",
  ""layerId""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P10"",
  ""catalogSection"": ""§2 P10"",
  ""overlapWithExisting"": false,
  ""rw"": ""R"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": ""与现役 get_layer_info 同族但不同宿主 ⇒ 描述须硬划界（dedup-86.md 第 75 行命名划界提示）。""
 }
}
";
    private const string SchemaListDocuments = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_list_documents.schema.json"",
 ""title"": ""ps_list_documents"",
 ""type"": ""object"",
 ""properties"": {
  ""maxItems100"": {
   ""type"": ""integer"",
   ""default"": 100,
   ""minimum"": 1,
   ""maximum"": 500,
   ""description"": ""返回条目上限（缺省 100，上限 500）。超限 ⇒ 截断并如实披露 truncated/returnedCount/totalMatched。 文档清单返回上限。""
  },
  ""includeClosed"": {
   ""type"": ""boolean"",
   ""default"": false,
   ""description"": ""是否包含最近关闭的文档（来自快照缓存）。""
  }
 },
 ""required"": [],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P10"",
  ""catalogSection"": ""§2 P10"",
  ""overlapWithExisting"": false,
  ""rw"": ""R"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": ""共用前置：P-05 未闭合 ⇒ 真实行为 NOT VERIFIED（D-080 evidence-index §1.5）。""
 }
}
";
    private const string SchemaListLayers = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_list_layers.schema.json"",
 ""title"": ""ps_list_layers"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""includeHidden"": {
   ""type"": ""boolean"",
   ""default"": true,
   ""description"": ""是否包含隐藏图层。""
  },
  ""maxItems500"": {
   ""type"": ""integer"",
   ""default"": 500,
   ""minimum"": 1,
   ""maximum"": 5000,
   ""description"": ""返回条目上限（缺省 500，上限 5000）。超限 ⇒ 截断并如实披露 truncated/returnedCount/totalMatched。 图层树返回上限。""
  }
 },
 ""required"": [
  ""documentId""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P10"",
  ""catalogSection"": ""§2 P10"",
  ""overlapWithExisting"": false,
  ""rw"": ""R"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaValidateDocument = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_validate_document.schema.json"",
 ""title"": ""ps_validate_document"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""checks"": {
   ""type"": ""array"",
   ""default"": [
    ""overflow"",
    ""collision"",
    ""clipping"",
    ""missing_font"",
    ""icc""
   ],
   ""description"": ""启用检查目录。""
  },
  ""reportPath"": {
   ""type"": ""string"",
   ""description"": ""可选：结构化报告落盘绝对路径。一旦提供，该工具按 W（写）检查：路径守卫 + OUTPUT_EXISTS 闸门 + 产物进 ArtifactManifest。未提供则只返回内存结果。""
  }
 },
 ""required"": [
  ""documentId""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P11"",
  ""catalogSection"": ""§2 P11"",
  ""overlapWithExisting"": false,
  ""rw"": ""R"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaCreateAdjustmentLayer = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_create_adjustment_layer.schema.json"",
 ""title"": ""ps_create_adjustment_layer"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""kind"": {
   ""type"": ""string"",
   ""description"": ""调整类型。"",
   ""enum"": [
    ""levels"",
    ""curves"",
    ""brightness_contrast"",
    ""hue_saturation"",
    ""color_balance"",
    ""gradient_map"",
    ""solid_color""
   ]
  },
  ""params"": {
   ""type"": ""object"",
   ""default"": {},
   ""description"": ""调整参数（按 kind 的固定 schema 校验）。""
  },
  ""targetLayerIds"": {
   ""type"": ""array"",
   ""default"": [],
   ""description"": ""受影响图层（空＝仅新建不裁剪）。""
  },
  ""specRevision"": {
   ""type"": ""string"",
   ""description"": ""DesignSpec 不可变 revision 标识。写类 PS 工具必填；旧 revision ⇒ INVALID_STATE（旧 revision 不覆盖新版，FINAL 路线图 §2 DesignPatch 行）。""
  },
  ""apsReference"": {
   ""type"": ""object"",
   ""description"": ""ApprovalRecord 引用（approvalId + planDigest + expiry + scopeDigest）。PS 写类工具必填；服务端校验 digest 与 scope，不匹配 ⇒ PERMISSION_DENIED。客户端 confirm=true 不构成批准（FINAL 路线图 §2 ApprovalRecord 行）。""
  }
 },
 ""required"": [
  ""documentId"",
  ""kind"",
  ""apsReference""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P11"",
  ""catalogSection"": ""§2 P11"",
  ""overlapWithExisting"": false,
  ""rw"": ""S"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaManageArtboards = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_manage_artboards.schema.json"",
 ""title"": ""ps_manage_artboards"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""action"": {
   ""type"": ""string"",
   ""description"": ""动作。"",
   ""enum"": [
    ""list"",
    ""create"",
    ""resize"",
    ""reorder"",
    ""delete""
   ]
  },
  ""artboardId"": {
   ""type"": ""string"",
   ""description"": ""目标画板 ID（create 时省略）。""
  },
  ""size"": {
   ""type"": ""object"",
   ""description"": ""尺寸（create/resize 必填）。""
  },
  ""index"": {
   ""type"": ""integer"",
   ""description"": ""顺序位置（reorder 必填）。""
  },
  ""specRevision"": {
   ""type"": ""string"",
   ""description"": ""DesignSpec 不可变 revision 标识。写类 PS 工具必填；旧 revision ⇒ INVALID_STATE（旧 revision 不覆盖新版，FINAL 路线图 §2 DesignPatch 行）。""
  },
  ""apsReference"": {
   ""type"": ""object"",
   ""description"": ""ApprovalRecord 引用（approvalId + planDigest + expiry + scopeDigest）。PS 写类工具必填；服务端校验 digest 与 scope，不匹配 ⇒ PERMISSION_DENIED。客户端 confirm=true 不构成批准（FINAL 路线图 §2 ApprovalRecord 行）。""
  }
 },
 ""required"": [
  ""documentId"",
  ""action"",
  ""apsReference""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P12"",
  ""catalogSection"": ""§2 P12"",
  ""overlapWithExisting"": false,
  ""rw"": ""S"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaPlaceDesignAsset = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_place_design_asset.schema.json"",
 ""title"": ""ps_place_design_asset"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""assetPath"": {
   ""type"": ""string"",
   ""description"": ""素材路径（D 盘工作根内）。""
  },
  ""targetFrame"": {
   ""type"": ""object"",
   ""description"": ""目标矩形（{x,y,width,height}）。""
  },
  ""fitMode"": {
   ""type"": ""string"",
   ""default"": ""contain"",
   ""description"": ""适配模式。"",
   ""enum"": [
    ""contain"",
    ""cover"",
    ""stretch"",
    ""none""
   ]
  },
  ""specRevision"": {
   ""type"": ""string"",
   ""description"": ""DesignSpec 不可变 revision 标识。写类 PS 工具必填；旧 revision ⇒ INVALID_STATE（旧 revision 不覆盖新版，FINAL 路线图 §2 DesignPatch 行）。""
  },
  ""apsReference"": {
   ""type"": ""object"",
   ""description"": ""ApprovalRecord 引用（approvalId + planDigest + expiry + scopeDigest）。PS 写类工具必填；服务端校验 digest 与 scope，不匹配 ⇒ PERMISSION_DENIED。客户端 confirm=true 不构成批准（FINAL 路线图 §2 ApprovalRecord 行）。""
  }
 },
 ""required"": [
  ""documentId"",
  ""assetPath"",
  ""apsReference""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P12"",
  ""catalogSection"": ""§2 P12"",
  ""overlapWithExisting"": false,
  ""rw"": ""S"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaRestoreDocumentSnapshot = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_restore_document_snapshot.schema.json"",
 ""title"": ""ps_restore_document_snapshot"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""snapshotId"": {
   ""type"": ""string"",
   ""description"": ""快照 ID。""
  },
  ""createSafetySnapshot"": {
   ""type"": ""boolean"",
   ""default"": true,
   ""description"": ""恢复前是否先建安全快照（缺省 true）。""
  },
  ""apsReference"": {
   ""type"": ""object"",
   ""description"": ""ApprovalRecord 引用（approvalId + planDigest + expiry + scopeDigest）。PS 写类工具必填；服务端校验 digest 与 scope，不匹配 ⇒ PERMISSION_DENIED。客户端 confirm=true 不构成批准（FINAL 路线图 §2 ApprovalRecord 行）。""
  }
 },
 ""required"": [
  ""documentId"",
  ""snapshotId"",
  ""apsReference""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P11"",
  ""catalogSection"": ""§2 P11"",
  ""overlapWithExisting"": false,
  ""rw"": ""S"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaSetLayerMask = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_set_layer_mask.schema.json"",
 ""title"": ""ps_set_layer_mask"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""layerId"": {
   ""type"": ""string"",
   ""description"": ""图层 ID。""
  },
  ""maskSource"": {
   ""type"": ""object"",
   ""description"": ""蒙版来源（{kind: layer|channel|path|file, ref}）。""
  },
  ""linkToLayer"": {
   ""type"": ""boolean"",
   ""default"": true,
   ""description"": ""是否与图层链接。""
  },
  ""specRevision"": {
   ""type"": ""string"",
   ""description"": ""DesignSpec 不可变 revision 标识。写类 PS 工具必填；旧 revision ⇒ INVALID_STATE（旧 revision 不覆盖新版，FINAL 路线图 §2 DesignPatch 行）。""
  },
  ""apsReference"": {
   ""type"": ""object"",
   ""description"": ""ApprovalRecord 引用（approvalId + planDigest + expiry + scopeDigest）。PS 写类工具必填；服务端校验 digest 与 scope，不匹配 ⇒ PERMISSION_DENIED。客户端 confirm=true 不构成批准（FINAL 路线图 §2 ApprovalRecord 行）。""
  }
 },
 ""required"": [
  ""documentId"",
  ""layerId"",
  ""maskSource"",
  ""apsReference""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P12"",
  ""catalogSection"": ""§2 P12"",
  ""overlapWithExisting"": false,
  ""rw"": ""S"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaSetLayerProperties = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_set_layer_properties.schema.json"",
 ""title"": ""ps_set_layer_properties"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""layerId"": {
   ""type"": ""string"",
   ""description"": ""图层 ID。""
  },
  ""properties"": {
   ""type"": ""object"",
   ""description"": ""允许属性白名单（name/visible/opacity/blendMode/locked）。""
  },
  ""specRevision"": {
   ""type"": ""string"",
   ""description"": ""DesignSpec 不可变 revision 标识。写类 PS 工具必填；旧 revision ⇒ INVALID_STATE（旧 revision 不覆盖新版，FINAL 路线图 §2 DesignPatch 行）。""
  },
  ""apsReference"": {
   ""type"": ""object"",
   ""description"": ""ApprovalRecord 引用（approvalId + planDigest + expiry + scopeDigest）。PS 写类工具必填；服务端校验 digest 与 scope，不匹配 ⇒ PERMISSION_DENIED。客户端 confirm=true 不构成批准（FINAL 路线图 §2 ApprovalRecord 行）。""
  }
 },
 ""required"": [
  ""documentId"",
  ""layerId"",
  ""properties"",
  ""apsReference""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P11"",
  ""catalogSection"": ""§2 P11"",
  ""overlapWithExisting"": false,
  ""rw"": ""S"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaSetTextProperties = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_set_text_properties.schema.json"",
 ""title"": ""ps_set_text_properties"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""layerId"": {
   ""type"": ""string"",
   ""description"": ""文字图层 ID。""
  },
  ""properties"": {
   ""type"": ""object"",
   ""description"": ""允许属性白名单（fontFamily/fontSize/leading/tracking/color/alignment）。""
  },
  ""text"": {
   ""type"": ""string"",
   ""description"": ""替换文字内容（须给 factRef 或显式来源，禁止凭空数字）。""
  },
  ""specRevision"": {
   ""type"": ""string"",
   ""description"": ""DesignSpec 不可变 revision 标识。写类 PS 工具必填；旧 revision ⇒ INVALID_STATE（旧 revision 不覆盖新版，FINAL 路线图 §2 DesignPatch 行）。""
  },
  ""apsReference"": {
   ""type"": ""object"",
   ""description"": ""ApprovalRecord 引用（approvalId + planDigest + expiry + scopeDigest）。PS 写类工具必填；服务端校验 digest 与 scope，不匹配 ⇒ PERMISSION_DENIED。客户端 confirm=true 不构成批准（FINAL 路线图 §2 ApprovalRecord 行）。""
  }
 },
 ""required"": [
  ""documentId"",
  ""layerId"",
  ""properties"",
  ""apsReference""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P12"",
  ""catalogSection"": ""§2 P12"",
  ""overlapWithExisting"": false,
  ""rw"": ""S"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaCreateDocumentSnapshot = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_create_document_snapshot.schema.json"",
 ""title"": ""ps_create_document_snapshot"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""outputPath"": {
   ""type"": ""string"",
   ""description"": ""输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。""
  },
  ""includeHistory"": {
   ""type"": ""boolean"",
   ""default"": false,
   ""description"": ""是否含历史记录。""
  },
  ""overwrite"": {
   ""type"": ""boolean"",
   ""default"": false,
   ""description"": ""允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。"",
   ""note"": ""现役复用点 OverwritePolicy.SchemaProperty（Source/Shared/ArcGISProMCP.Core/Results/OverwritePolicy.cs:40）；其 description 目前把默认值写在**文本**里而非 schema 的 default 键 ⇒ O-D080-04 待归并项。""
  }
 },
 ""required"": [
  ""documentId"",
  ""outputPath""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P11"",
  ""catalogSection"": ""§2 P11"",
  ""overlapWithExisting"": false,
  ""rw"": ""W"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": """"
 }
}
";
    private const string SchemaPreviewDocument = @"
{
 ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
 ""$id"": ""arcgis-pro-mcp/candidate/ps_preview_document.schema.json"",
 ""title"": ""ps_preview_document"",
 ""type"": ""object"",
 ""properties"": {
  ""documentId"": {
   ""type"": ""string"",
   ""description"": ""Photoshop 文档身份（拥有关系由 RenderPlan.documentOwnership 决定）。不在拥有列表内 ⇒ PERMISSION_DENIED（不抢用户 activeDocument）。""
  },
  ""maxEdge"": {
   ""type"": ""integer"",
   ""default"": 1600,
   ""description"": ""预览长边像素上限（缺省 1600）。""
  },
  ""outputPath"": {
   ""type"": ""string"",
   ""description"": ""输出绝对路径（D 盘工作根内）。经路径守卫；落在受保护根 ⇒ PATH_ESCAPE_REJECTED。""
  },
  ""overwrite"": {
   ""type"": ""boolean"",
   ""default"": false,
   ""description"": ""允许覆盖已存在的输出。默认 false：输出已存在且未显式 overwrite=true ⇒ OUTPUT_EXISTS，不执行任何写。"",
   ""note"": ""现役复用点 OverwritePolicy.SchemaProperty（Source/Shared/ArcGISProMCP.Core/Results/OverwritePolicy.cs:40）；其 description 目前把默认值写在**文本**里而非 schema 的 default 键 ⇒ O-D080-04 待归并项。""
  }
 },
 ""required"": [
  ""documentId"",
  ""outputPath""
 ],
 ""additionalProperties"": false,
 ""x-d082"": {
  ""status"": ""PENDING F03-a adjudication"",
  ""batch"": ""P10"",
  ""catalogSection"": ""§2 P10"",
  ""overlapWithExisting"": false,
  ""rw"": ""W"",
  ""requiresArcGIS"": false,
  ""executionType"": ""Bridge(PS/UXP)"",
  ""category"": ""Photoshop"",
  ""pythonBridge"": false,
  ""psDependent"": true,
  ""hold"": null,
  ""note"": ""预览也落盘 ⇒ 按 W 检查（能力目录 §4 公共规则）。""
 }
}
";

    private sealed record SchemaEntry(JsonDocument Document, IReadOnlyDictionary<string, object?> PublicSchema);

    private static readonly IReadOnlyDictionary<string, SchemaEntry> Entries =
        new Dictionary<string, SchemaEntry>(StringComparer.Ordinal)
        {
            ["ps_compare_document_versions"] = Create(SchemaCompareDocumentVersions),
            ["ps_get_document_info"] = Create(SchemaGetDocumentInfo),
            ["ps_get_layer_info"] = Create(SchemaGetLayerInfo),
            ["ps_list_documents"] = Create(SchemaListDocuments),
            ["ps_list_layers"] = Create(SchemaListLayers),
            ["ps_validate_document"] = Create(SchemaValidateDocument),
            ["ps_create_adjustment_layer"] = Create(SchemaCreateAdjustmentLayer),
            ["ps_manage_artboards"] = Create(SchemaManageArtboards),
            ["ps_place_design_asset"] = Create(SchemaPlaceDesignAsset),
            ["ps_restore_document_snapshot"] = Create(SchemaRestoreDocumentSnapshot),
            ["ps_set_layer_mask"] = Create(SchemaSetLayerMask),
            ["ps_set_layer_properties"] = Create(SchemaSetLayerProperties),
            ["ps_set_text_properties"] = Create(SchemaSetTextProperties),
            ["ps_create_document_snapshot"] = Create(SchemaCreateDocumentSnapshot),
            ["ps_preview_document"] = Create(SchemaPreviewDocument),
        };

    public static IReadOnlyDictionary<string, object?> Get(string name) => Entries[name].PublicSchema;
    public static JsonElement GetRoot(string name) => Entries[name].Document.RootElement;

    private static SchemaEntry Create(string json)
    {
        var document = JsonDocument.Parse(json);
        var publicSchema = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!StringComparer.Ordinal.Equals(property.Name, "x-d082"))
                publicSchema.Add(property.Name, ConvertPublicValue(property.Value));
        }

        return new SchemaEntry(document, publicSchema);
    }

    private static object? ConvertPublicValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(
                property => property.Name,
                property => ConvertPublicValue(property.Value),
                StringComparer.Ordinal),
            JsonValueKind.Array => ConvertPublicArray(value),
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => throw new InvalidOperationException("Unexpected schema token: " + value.ValueKind),
        };

    private static object ConvertPublicArray(JsonElement value)
    {
        var items = value.EnumerateArray().ToArray();
        if (items.All(item => item.ValueKind == JsonValueKind.String))
            return items.Select(item => item.GetString() ?? string.Empty).ToArray();

        return items.Select(ConvertPublicValue).ToArray();
    }
}

internal static class D102PsSchemaValidator
{
    public static OperationError? Validate(
        IMCPTool tool,
        IReadOnlyDictionary<string, object?>? arguments)
    {
        JsonElement argumentElement;
        try
        {
            argumentElement = JsonSerializer.SerializeToElement(
                arguments ?? new Dictionary<string, object?>(StringComparer.Ordinal));
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return Invalid("Arguments are not JSON-compatible.", tool.Name);
        }

        return ValidateNode(D102PsSchemas.GetRoot(tool.Name), argumentElement, tool.Name);
    }

    private static OperationError? ValidateNode(JsonElement schema, JsonElement value, string path)
    {
        if (schema.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String
            && !MatchesType(type.GetString()!, value))
        {
            return Invalid($"Argument '{path}' must be {type.GetString()}.", path);
        }

        if (schema.TryGetProperty("enum", out var enumValues)
            && enumValues.ValueKind == JsonValueKind.Array
            && !enumValues.EnumerateArray().Any(candidate => JsonEquals(candidate, value)))
        {
            return Invalid($"Argument '{path}' has an unsupported value.", path);
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetDouble(out var number))
                return Invalid($"Argument '{path}' is outside the supported numeric range.", path);

            if (schema.TryGetProperty("minimum", out var minimum)
                && minimum.ValueKind == JsonValueKind.Number
                && number < minimum.GetDouble())
                return Invalid($"Argument '{path}' is below its minimum.", path);

            if (schema.TryGetProperty("maximum", out var maximum)
                && maximum.ValueKind == JsonValueKind.Number
                && number > maximum.GetDouble())
                return Invalid($"Argument '{path}' exceeds its maximum.", path);
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            var properties = schema.TryGetProperty("properties", out var schemaProperties)
                && schemaProperties.ValueKind == JsonValueKind.Object
                    ? schemaProperties
                    : default;

            if (schema.TryGetProperty("required", out var required)
                && required.ValueKind == JsonValueKind.Array)
            {
                foreach (var requiredName in required.EnumerateArray())
                {
                    var name = requiredName.GetString();
                    if (name is not null && !value.TryGetProperty(name, out _))
                        return Invalid($"Missing required argument '{name}'.", path + "." + name);
                }
            }

            if (schema.TryGetProperty("additionalProperties", out var additionalProperties)
                && additionalProperties.ValueKind == JsonValueKind.False)
            {
                foreach (var property in value.EnumerateObject())
                {
                    if (properties.ValueKind != JsonValueKind.Object
                        || !properties.TryGetProperty(property.Name, out _))
                        return Invalid($"Unknown argument '{property.Name}'.", path + "." + property.Name);
                }
            }

            if (properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in value.EnumerateObject())
                {
                    if (properties.TryGetProperty(property.Name, out var propertySchema))
                    {
                        var error = ValidateNode(propertySchema, property.Value, path + "." + property.Name);
                        if (error is not null)
                            return error;
                    }
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array
            && schema.TryGetProperty("items", out var itemSchema))
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                var error = ValidateNode(itemSchema, item, path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]");
                if (error is not null)
                    return error;
                index++;
            }
        }

        return null;
    }

    private static bool MatchesType(string type, JsonElement value)
        => type switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number
                && value.TryGetDouble(out var integer)
                && Math.Truncate(integer) == integer,
            "number" => value.ValueKind == JsonValueKind.Number,
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            _ => true,
        };

    private static bool JsonEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.String)
            return StringComparer.Ordinal.Equals(left.GetString(), right.GetString());

        return StringComparer.Ordinal.Equals(left.GetRawText(), right.GetRawText());
    }

    private static OperationError Invalid(string message, string field)
        => new(ErrorCodes.InvalidArgument, message, field);
}

/// <summary>D-102 candidate tools. All business operations remain NOT_VERIFIED and are never sent to Photoshop/UXP.</summary>
public abstract class D102PsToolBase : D091PsToolBase
{
    public override IReadOnlyDictionary<string, object?> InputSchema => D102PsSchemas.Get(Name);

    public override Task<OperationResult<object?>> ExecuteAsync(ToolExecutionContext context)
    {
        if (context.ReadOnly is { IsReadOnly: true }
            && ToolWriteClassification.RefusedInReadOnly(Name))
        {
            return Task.FromResult(OperationResult<object?>.Fail(ToolWriteClassification.ReadOnlyRefusal(Name)));
        }

        if (D102PsSchemaValidator.Validate(this, context.Arguments) is { } schemaError)
            return Task.FromResult(OperationResult<object?>.Fail(schemaError));

        var details = JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = "NOT_VERIFIED",
            ["operation"] = Name,
            ["psRequestSent"] = false,
            ["sideEffects"] = false,
            ["reason"] = "D-102 has no verified PS business-command route; this operation was not attempted.",
        });
        return Task.FromResult(OperationResult<object?>.Fail(
            ErrorCodes.NotImplemented,
            Name + " is registered, but PS/UXP execution is NOT_VERIFIED and was not attempted.",
            details));
    }
}
public sealed class PsCompareDocumentVersionsTool : D102PsToolBase
{
    public override string Name => "ps_compare_document_versions";
    public override string Description => "比较两个 Photoshop 文档版本；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsGetDocumentInfoTool : D102PsToolBase
{
    public override string Name => "ps_get_document_info";
    public override string Description => "读取 Photoshop 文档信息；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsGetLayerInfoTool : D102PsToolBase
{
    public override string Name => "ps_get_layer_info";
    public override string Description => "读取 Photoshop 图层信息；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsListDocumentsTool : D102PsToolBase
{
    public override string Name => "ps_list_documents";
    public override string Description => "列出 Photoshop 文档缓存；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsListLayersTool : D102PsToolBase
{
    public override string Name => "ps_list_layers";
    public override string Description => "列出 Photoshop 图层；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsValidateDocumentTool : D102PsToolBase
{
    public override string Name => "ps_validate_document";
    public override string Description => "校验 Photoshop 文档；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsCreateAdjustmentLayerTool : D102PsToolBase
{
    public override string Name => "ps_create_adjustment_layer";
    public override string Description => "创建 Photoshop 调整图层；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsManageArtboardsTool : D102PsToolBase
{
    public override string Name => "ps_manage_artboards";
    public override string Description => "管理 Photoshop 画板；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsPlaceDesignAssetTool : D102PsToolBase
{
    public override string Name => "ps_place_design_asset";
    public override string Description => "放置设计素材；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsRestoreDocumentSnapshotTool : D102PsToolBase
{
    public override string Name => "ps_restore_document_snapshot";
    public override string Description => "恢复 Photoshop 文档快照；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsSetLayerMaskTool : D102PsToolBase
{
    public override string Name => "ps_set_layer_mask";
    public override string Description => "设置 Photoshop 图层蒙版；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsSetLayerPropertiesTool : D102PsToolBase
{
    public override string Name => "ps_set_layer_properties";
    public override string Description => "设置 Photoshop 图层属性；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsSetTextPropertiesTool : D102PsToolBase
{
    public override string Name => "ps_set_text_properties";
    public override string Description => "设置 Photoshop 文字属性；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsCreateDocumentSnapshotTool : D102PsToolBase
{
    public override string Name => "ps_create_document_snapshot";
    public override string Description => "创建 Photoshop 文档快照；当前 UXP 业务路由未验证，执行 fail-closed。";
}

public sealed class PsPreviewDocumentTool : D102PsToolBase
{
    public override string Name => "ps_preview_document";
    public override string Description => "生成 Photoshop 文档预览；当前 UXP 业务路由未验证，执行 fail-closed。";
}
