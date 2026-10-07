#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Fixed, test-only Phase 5.8.1 fixture preparation.

The companion .NET runner creates the owned root and marker first, then starts
this script from an externally activated ArcGIS Pro Python environment.  The
script accepts only the fixed modes below; it is not exposed through MCP and
never uses ArcGISProject("CURRENT") or writes to the historical source.
"""

import gc
import hashlib
import json
import os
import re
import sys
import traceback


SOURCE = r"D:\ArcGIS-Pro-MCP\TestDate\Phase4Test.gdb\TestPolygons"
TEMPLATE = r"C:\Program Files\ArcGIS\Pro\Resources\ArcToolBox\Services\routingservices\data\Blank.aprx"
MARKER_NAME = ".arcgis-pro-mcp-test-owned"
REQUIRED_SOURCE_FIELDS = (
    "OBJECTID",
    "Shape",
    "Shape_Length",
    "Shape_Area",
    "Name",
    "Type",
)
RUN_ID_PATTERN = re.compile(r"^P57_[0-9A-F]{8}$")

# ArcPy is deliberately imported only after the owned marker has been checked.
arcpy = None
current_stage = "startup"


class FixtureValidationError(RuntimeError):
    """Raised when a read-only or owned fixture contract is not satisfied."""


def canonical(path):
    return os.path.normcase(os.path.abspath(os.path.normpath(path)))


def is_within(root, path):
    try:
        return os.path.commonpath([canonical(root), canonical(path)]) == canonical(root)
    except ValueError:
        return False


def marker_path(root):
    return os.path.join(root, MARKER_NAME)


def validate_owned_root(root, run_id):
    if not RUN_ID_PATTERN.fullmatch(run_id):
        raise FixtureValidationError("The runner supplied an invalid run ID.")
    if not os.path.isdir(root):
        raise FixtureValidationError("The owned root does not exist.")

    parent = os.path.dirname(canonical(root))
    if os.path.basename(parent).lower() != "arcgispromcp":
        raise FixtureValidationError(
            "The owned root is not directly under the TestWorkspace parent."
        )

    path = marker_path(root)
    if not os.path.isfile(path):
        raise FixtureValidationError("The ownership marker is missing.")
    with open(path, "r", encoding="utf-8") as stream:
        contents = stream.read()

    values = {}
    for part in contents.strip().split(";"):
        if "=" in part:
            key, value = part.split("=", 1)
            values[key] = value

    if values.get("owner") != "ArcGIS-Pro-MCP":
        raise FixtureValidationError("The ownership marker owner is invalid.")
    if values.get("run") != run_id:
        raise FixtureValidationError("The ownership marker run ID does not match.")
    if not values.get("createdUtc"):
        raise FixtureValidationError("The ownership marker has no creation time.")

    return {
        "markerPresentBeforeArcPyWrites": True,
        "markerPath": path,
        "markerContents": contents.strip(),
        "runId": run_id,
        "rootPath": os.path.abspath(root),
        "createdUtc": values["createdUtc"],
    }


def require_arcpy():
    if arcpy is None:
        raise FixtureValidationError("ArcPy was not initialized after marker validation.")
    return arcpy


def field_record(field, oid_name):
    field_type = getattr(field, "type", None)
    name = getattr(field, "name", None)
    return {
        "name": name,
        "type": field_type,
        "length": getattr(field, "length", None),
        "isOid": bool(name == oid_name or str(field_type).upper() == "OID"),
    }


def describe(path):
    api = require_arcpy()
    exists = bool(api.Exists(path))
    if not exists:
        return {
            "path": path,
            "exists": False,
            "name": None,
            "dataType": None,
            "shapeType": None,
            "workspaceType": None,
            "workspaceFactoryProgID": None,
            "spatialReference": None,
            "featureCount": None,
            "objectIdField": None,
            "fields": [],
        }

    descriptor = api.Describe(path)
    spatial_reference = getattr(descriptor, "spatialReference", None)
    oid_name = getattr(descriptor, "OIDFieldName", None)
    fields = [
        field_record(field, oid_name)
        for field in (getattr(descriptor, "fields", None) or [])
    ]
    data_type = getattr(descriptor, "dataType", None)
    count = None
    if data_type in ("FeatureClass", "Table"):
        count = int(api.management.GetCount(path).getOutput(0))

    return {
        "path": path,
        "exists": True,
        "name": getattr(descriptor, "name", None),
        "dataType": data_type,
        "shapeType": getattr(descriptor, "shapeType", None),
        "workspaceType": getattr(descriptor, "workspaceType", None),
        "workspaceFactoryProgID": getattr(descriptor, "workspaceFactoryProgID", None),
        "spatialReference": {
            "name": getattr(spatial_reference, "name", None),
            "factoryCode": getattr(spatial_reference, "factoryCode", None),
        }
        if spatial_reference is not None
        else None,
        "featureCount": count,
        "objectIdField": oid_name,
        "fields": fields,
    }


def type_values(path):
    api = require_arcpy()
    with api.da.SearchCursor(path, ["OBJECTID", "Name", "Type"]) as cursor:
        return [
            {"OBJECTID": row[0], "Name": row[1], "Type": row[2]}
            for row in cursor
        ]


def validate_source(path, label="source"):
    info = describe(path)
    failures = []
    if not info["exists"]:
        failures.append("exists=false")
    if info["dataType"] != "FeatureClass":
        failures.append("dataType is not FeatureClass")
    if info["shapeType"] != "Polygon":
        failures.append("shapeType is not Polygon")
    spatial_reference = info["spatialReference"] or {}
    if spatial_reference.get("factoryCode") != 3857:
        failures.append("spatial reference is not WKID 3857")
    if info["featureCount"] != 4:
        failures.append("featureCount is not 4")

    field_names = {field["name"] for field in info["fields"]}
    missing = [name for name in REQUIRED_SOURCE_FIELDS if name not in field_names]
    if missing:
        failures.append("missing fields: " + ",".join(missing))
    if info["objectIdField"] != "OBJECTID":
        failures.append("OIDFieldName is not OBJECTID")
    if not any(
        field["name"] == "OBJECTID" and field["isOid"]
        for field in info["fields"]
    ):
        failures.append("OBJECTID is not an OID field")

    values = type_values(path) if not failures else []
    if failures:
        raise FixtureValidationError(
            "{} health contract failed: {}".format(label, "; ".join(failures))
        )

    return {
        "status": "PASS",
        "label": label,
        "path": path,
        "describe": info,
        "typeValues": values,
        "contract": {
            "dataType": "FeatureClass",
            "shapeType": "Polygon",
            "spatialReferenceWkid": 3857,
            "featureCount": 4,
            "requiredFields": list(REQUIRED_SOURCE_FIELDS),
            "objectIdField": "OBJECTID",
        },
    }


def validate_clip_mask(path):
    info = describe(path)
    failures = []
    if not info["exists"]:
        failures.append("exists=false")
    if info["dataType"] != "FeatureClass":
        failures.append("dataType is not FeatureClass")
    if info["shapeType"] != "Polygon":
        failures.append("shapeType is not Polygon")
    spatial_reference = info["spatialReference"] or {}
    if spatial_reference.get("factoryCode") != 3857:
        failures.append("spatial reference is not WKID 3857")
    if not isinstance(info["featureCount"], int) or info["featureCount"] <= 0:
        failures.append("featureCount is not greater than zero")
    if failures:
        raise FixtureValidationError(
            "ClipMask health contract failed: {}".format("; ".join(failures))
        )
    return {
        "status": "PASS",
        "label": "clipMask",
        "path": path,
        "describe": info,
        "contract": {
            "dataType": "FeatureClass",
            "shapeType": "Polygon",
            "compatibleSpatialReferenceWkid": 3857,
            "minimumFeatureCount": 1,
        },
    }


def create_gdb(root, run_id, purpose="Phase58"):
    api = require_arcpy()
    name = "Phase58_{}".format(run_id)
    expected = os.path.join(root, name + ".gdb")
    result = api.management.CreateFileGDB(root, name, "CURRENT")
    actual = str(result.getOutput(0))
    if canonical(actual) != canonical(expected):
        raise FixtureValidationError(
            "{} returned an unexpected FileGDB path: {}".format(purpose, actual)
        )
    if not is_within(root, actual):
        raise FixtureValidationError("The created FileGDB is outside the owned root.")
    if not bool(api.Exists(actual)):
        raise FixtureValidationError("The created FileGDB does not exist.")
    return actual


def file_gdb_health(path):
    info = describe(path)
    failures = []
    if not info["exists"]:
        failures.append("exists=false")
    if info["dataType"] != "Workspace":
        failures.append("dataType is not Workspace")
    if not canonical(path).lower().endswith(".gdb"):
        failures.append("path does not end in .gdb")
    if failures:
        raise FixtureValidationError(
            "FileGDB health contract failed: {}".format("; ".join(failures))
        )
    return {
        "status": "PASS",
        "path": path,
        "describe": info,
        "workspaceEvidence": {
            "dataType": info["dataType"],
            "workspaceType": info["workspaceType"],
            "workspaceFactoryProgID": info["workspaceFactoryProgID"],
            "extension": ".gdb",
        },
    }


def template_health(path):
    if not os.path.isfile(path):
        raise FixtureValidationError("Vendor Blank.aprx does not exist.")
    with open(path, "rb") as stream:
        digest = hashlib.sha256(stream.read()).hexdigest().upper()
    return {
        "status": "PASS",
        "path": path,
        "exists": True,
        "readable": True,
        "size": os.path.getsize(path),
        "sha256": digest,
    }


def layer_data_source(layer):
    try:
        return getattr(layer, "dataSource", None)
    except Exception as exception:
        return {"error": str(exception)}


def selection_baseline(layer):
    method = getattr(layer, "getSelectionSet", None)
    if not callable(method):
        return {
            "supported": False,
            "status": "NOT_EXPOSED",
            "count": None,
        }
    try:
        selected = method()
        count = len(selected) if selected is not None else 0
        return {
            "supported": True,
            "status": "PASS" if count == 0 else "FAIL",
            "count": count,
        }
    except Exception as exception:
        return {
            "supported": False,
            "status": "NOT_VERIFIED",
            "count": None,
            "error": str(exception),
        }


def visibility_baseline(layer):
    try:
        value = getattr(layer, "visible")
        return {
            "supported": isinstance(value, bool),
            "status": "PASS" if isinstance(value, bool) else "NOT_EXPOSED",
            "visible": value if isinstance(value, bool) else None,
        }
    except Exception as exception:
        return {
            "supported": False,
            "status": "NOT_VERIFIED",
            "visible": None,
            "error": str(exception),
        }


def project_dirty_state(project):
    try:
        value = getattr(project, "isDirty")
        if isinstance(value, bool):
            return {
                "supported": True,
                "status": "PASS" if not value else "FAIL",
                "isDirty": value,
            }
        return {"supported": False, "status": "NOT_EXPOSED", "isDirty": None}
    except Exception as exception:
        return {
            "supported": False,
            "status": "NOT_VERIFIED",
            "isDirty": None,
            "error": str(exception),
        }


def layer_record(layer):
    return {
        "name": getattr(layer, "name", None),
        "longName": getattr(layer, "longName", None),
        "isFeatureLayer": bool(getattr(layer, "isFeatureLayer", False)),
        "dataSource": layer_data_source(layer),
        "visible": visibility_baseline(layer),
        "selection": selection_baseline(layer),
    }


def map_record(map_obj):
    return {
        "name": getattr(map_obj, "name", None),
        "layers": [layer_record(layer) for layer in map_obj.listLayers()],
    }


def controlled_project(root, owned_source, clip_mask):
    api = require_arcpy()
    template = template_health(TEMPLATE)
    project_path = os.path.join(root, "Phase58Controlled.aprx")
    if os.path.exists(project_path):
        raise FixtureValidationError("The controlled project path already exists.")
    if not is_within(root, project_path):
        raise FixtureValidationError("The controlled project is outside the owned root.")

    template_project = api.mp.ArcGISProject(TEMPLATE)
    template_project.saveACopy(project_path)
    del template_project
    gc.collect()
    if not os.path.isfile(project_path):
        raise FixtureValidationError("The controlled project copy was not created.")

    project = api.mp.ArcGISProject(project_path)
    maps_before = project.listMaps()
    matching_maps = [item for item in maps_before if item.name == "Phase58_TestMap"]
    if len(matching_maps) > 1:
        del project
        raise FixtureValidationError("The controlled project has duplicate target maps.")
    map_obj = matching_maps[0] if matching_maps else project.createMap("Phase58_TestMap", "MAP")

    source_layer = map_obj.addDataFromPath(owned_source)
    source_layer.name = "P58_TestPolygons"
    mask_layer = map_obj.addDataFromPath(clip_mask)
    mask_layer.name = "P58_ClipMask"
    project.save()
    saved_dirty = project_dirty_state(project)
    del source_layer, mask_layer, map_obj, matching_maps, maps_before, project
    gc.collect()

    reopened = api.mp.ArcGISProject(project_path)
    reopened_maps = reopened.listMaps()
    target_maps = [item for item in reopened_maps if item.name == "Phase58_TestMap"]
    if len(target_maps) != 1:
        del reopened
        raise FixtureValidationError("The controlled map could not be reopened by name.")
    target_map = target_maps[0]
    all_map_records = [map_record(item) for item in reopened_maps]
    target_layers = target_map.listLayers()

    expected_sources = {
        "P58_TestPolygons": canonical(owned_source),
        "P58_ClipMask": canonical(clip_mask),
    }
    datasource_checks = []
    matched_layers = {}
    for layer in target_layers:
        name = getattr(layer, "name", None)
        if name not in expected_sources:
            continue
        data_source = layer_data_source(layer)
        normalized = canonical(data_source) if isinstance(data_source, str) else None
        check = {
            "layerName": name,
            "dataSource": data_source,
            "expectedDataSource": expected_sources[name],
            "matchesOwnedPath": normalized == expected_sources[name],
            "isFeatureLayer": bool(getattr(layer, "isFeatureLayer", False)),
        }
        datasource_checks.append(check)
        matched_layers.setdefault(name, []).append(layer)

    failures = []
    for name in expected_sources:
        if len(matched_layers.get(name, [])) != 1:
            failures.append("missing or duplicate layer {}".format(name))
        elif not datasource_checks[
            next(
                index
                for index, item in enumerate(datasource_checks)
                if item["layerName"] == name
            )
        ]["matchesOwnedPath"]:
            failures.append("layer {} does not point to its owned dataset".format(name))
    if failures:
        del reopened
        raise FixtureValidationError("Controlled layer gate failed: " + "; ".join(failures))

    reopened_dirty = project_dirty_state(reopened)
    selection = {
        name: selection_baseline(matched_layers[name][0])
        for name in expected_sources
    }
    visibility = {
        name: visibility_baseline(matched_layers[name][0])
        for name in expected_sources
    }
    if any(
        item["supported"] and item["status"] != "PASS"
        for item in selection.values()
    ):
        del reopened
        raise FixtureValidationError("Controlled project selection baseline is not zero.")
    if any(not item["supported"] for item in visibility.values()):
        del reopened
        raise FixtureValidationError("Controlled project visibility baseline is unavailable.")

    del target_layers, target_maps, target_map, matched_layers, reopened_maps, reopened
    gc.collect()
    return {
        "status": "PASS",
        "path": project_path,
        "name": os.path.splitext(os.path.basename(project_path))[0],
        "createdFromVendorTemplate": template,
        "mapName": "Phase58_TestMap",
        "maps": all_map_records,
        "layers": datasource_checks,
        "layerDatasourceChecks": datasource_checks,
        "projectReopenSucceeded": True,
        "projectDirtyBaselineAfterSave": saved_dirty,
        "projectDirtyBaselineAfterReopen": reopened_dirty,
        "selectionBaseline": selection,
        "visibilityBaseline": visibility,
        "releasedBeforeReturn": True,
        "explicitPathOnly": True,
        "usedCurrentProject": False,
    }


def list_owned_feature_classes(gdb):
    api = require_arcpy()
    old_workspace = api.env.workspace
    try:
        api.env.workspace = gdb
        return sorted(list(api.ListFeatureClasses() or []))
    finally:
        api.env.workspace = old_workspace


def create_retained(root, run_id, ownership):
    global current_stage
    api = require_arcpy()

    current_stage = "source_health_before_writes"
    source_health = validate_source(SOURCE, "sharedSourceBeforeWrites")
    current_stage = "vendor_template_read_only_check"
    vendor_template = template_health(TEMPLATE)

    current_stage = "create_owned_filegdb"
    gdb = create_gdb(root, run_id)
    gdb_health = file_gdb_health(gdb)

    owned_source = os.path.join(gdb, "P58_TestPolygons")
    clip_mask = os.path.join(gdb, "P58_ClipMask")
    if not is_within(root, owned_source) or not is_within(root, clip_mask):
        raise FixtureValidationError("A derived dataset path escaped the owned root.")

    current_stage = "copy_owned_source"
    api.management.CopyFeatures(SOURCE, owned_source)
    current_stage = "owned_source_health"
    copy_health = validate_source(owned_source, "ownedSource")

    current_stage = "shared_source_isolation_recheck"
    source_after_copy = validate_source(SOURCE, "sharedSourceAfterCopy")

    current_stage = "create_preparation_clipmask"
    # Preparation-only direct ArcPy.  This is deliberately not a production
    # MCP buffer call and is not business-tool acceptance evidence.
    api.analysis.Buffer(
        in_features=owned_source,
        out_feature_class=clip_mask,
        buffer_distance_or_field="1000 Meters",
        line_side="FULL",
        line_end_type="ROUND",
        dissolve_option="ALL",
    )
    current_stage = "clipmask_health"
    clip_mask_health = validate_clip_mask(clip_mask)

    current_stage = "owned_gdb_inventory"
    inventory = {
        "featureClasses": list_owned_feature_classes(gdb),
        "expectedFeatureClasses": ["P58_ClipMask", "P58_TestPolygons"],
    }
    if set(inventory["featureClasses"]) != set(inventory["expectedFeatureClasses"]):
        raise FixtureValidationError(
            "Owned GDB inventory contains unexpected feature classes: {}".format(
                inventory["featureClasses"]
            )
        )
    inventory["status"] = "PASS"

    current_stage = "controlled_project_preparation"
    project_result = controlled_project(root, owned_source, clip_mask)

    current_stage = "final_shared_source_isolation_recheck"
    final_source_health = validate_source(SOURCE, "sharedSourceAfterPreparation")

    return {
        "status": "READY",
        "runId": run_id,
        "rootPath": os.path.abspath(root),
        "ownership": ownership,
        "pythonExecutable": sys.executable,
        "arcpyVersion": getattr(api, "__version__", None),
        "source": SOURCE,
        "sourceHealth": source_health,
        "sourceHealthAfterCopy": source_after_copy,
        "sourceHealthAfterPreparation": final_source_health,
        "vendorTemplate": vendor_template,
        "ownedGdb": gdb,
        "ownedGdbHealth": gdb_health,
        "ownedSource": owned_source,
        "copyHealth": copy_health,
        "clipMask": clip_mask,
        "clipMaskHealth": clip_mask_health,
        "ownedGdbInventory": inventory,
        "controlledProject": project_result,
        "preparationClassification": (
            "PREPARATION; direct trusted ArcPy, not production GP acceptance"
        ),
        "productionToolVerification": "NOT PERFORMED",
        "retention": "INTENTIONALLY_RETAINED_OWNED_FIXTURE",
    }


def cleanup_probe(root, run_id, ownership):
    global current_stage
    api = require_arcpy()
    current_stage = "cleanup_probe_source_health"
    source_health = validate_source(SOURCE, "sharedSourceBeforeCleanupProbe")
    current_stage = "cleanup_probe_create_gdb"
    gdb = create_gdb(root, run_id, "cleanup-probe")
    current_stage = "cleanup_probe_copy_source"
    owned_source = os.path.join(gdb, "P58_CleanupSource")
    api.management.CopyFeatures(SOURCE, owned_source)
    current_stage = "cleanup_probe_describe"
    owned_health = validate_source(owned_source, "cleanupProbeOwnedSource")
    source_after_copy = validate_source(SOURCE, "sharedSourceAfterCleanupProbeCopy")
    gc.collect()
    return {
        "status": "PASS",
        "mode": "cleanup-probe",
        "runId": run_id,
        "rootPath": os.path.abspath(root),
        "ownership": ownership,
        "pythonExecutable": sys.executable,
        "arcpyVersion": getattr(api, "__version__", None),
        "sourceHealth": source_health,
        "ownedGdb": gdb,
        "ownedSource": owned_source,
        "ownedSourceHealth": owned_health,
        "sourceHealthAfterCopy": source_after_copy,
        "releasedBeforeProcessExit": True,
        "cleanupExpectation": "C# runner cleans the current-run root after Python exits",
    }


def project_probe(root, run_id, ownership):
    global current_stage
    api = require_arcpy()
    current_stage = "project_probe_vendor_template"
    template = template_health(TEMPLATE)
    project_path = os.path.join(root, "Phase58CleanupProbe.aprx")
    if not is_within(root, project_path):
        raise FixtureValidationError("The disposable project is outside the owned root.")
    current_stage = "project_probe_copy"
    template_project = api.mp.ArcGISProject(TEMPLATE)
    template_project.saveACopy(project_path)
    del template_project
    gc.collect()
    if not os.path.isfile(project_path):
        raise FixtureValidationError("The disposable project copy was not created.")
    current_stage = "project_probe_open_reopen"
    project = api.mp.ArcGISProject(project_path)
    first_maps = [getattr(item, "name", None) for item in project.listMaps()]
    first_dirty = project_dirty_state(project)
    del project
    gc.collect()
    reopened = api.mp.ArcGISProject(project_path)
    second_maps = [getattr(item, "name", None) for item in reopened.listMaps()]
    reopened_dirty = project_dirty_state(reopened)
    del reopened
    gc.collect()
    return {
        "status": "PASS",
        "mode": "project-probe",
        "runId": run_id,
        "rootPath": os.path.abspath(root),
        "ownership": ownership,
        "projectPath": project_path,
        "vendorTemplate": template,
        "firstOpenMaps": first_maps,
        "reopenMaps": second_maps,
        "projectReopenSucceeded": True,
        "projectDirtyBaselineFirstOpen": first_dirty,
        "projectDirtyBaselineAfterReopen": reopened_dirty,
        "releasedBeforeProcessExit": True,
        "explicitPathOnly": True,
        "usedCurrentProject": False,
        "cleanupExpectation": "C# runner cleans the current-run root after Python exits",
    }


def arc_py_messages():
    if arcpy is None:
        return None
    try:
        return arcpy.GetMessages(2)
    except Exception:
        return None


def main():
    global arcpy
    global current_stage
    if len(sys.argv) != 4:
        raise ValueError("fixed mode, owned root, and run ID are required")
    mode, root, run_id = sys.argv[1:]

    # This check intentionally happens before importing ArcPy or performing any
    # ArcGIS write.  The .NET runner owns creation of the marker.
    ownership = validate_owned_root(root, run_id)
    current_stage = "arcpy_import_after_marker_validation"
    import arcpy as imported_arcpy

    arcpy = imported_arcpy
    if mode == "create-retained":
        result = create_retained(root, run_id, ownership)
    elif mode == "cleanup-probe":
        result = cleanup_probe(root, run_id, ownership)
    elif mode == "project-probe":
        result = project_probe(root, run_id, ownership)
    else:
        raise ValueError("unsupported fixed fixture mode")

    print(json.dumps(result, ensure_ascii=False, default=str))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as exception:
        failure = {
            "status": "FAIL",
            "stage": current_stage,
            "exceptionType": type(exception).__name__,
            "message": str(exception),
            "arcPyMessages": arc_py_messages(),
        }
        print(json.dumps({"fixtureFailure": failure}, ensure_ascii=False), flush=True)
        traceback.print_exc(file=sys.stderr)
        sys.exit(2)
