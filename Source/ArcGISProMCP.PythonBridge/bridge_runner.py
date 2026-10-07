#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
ArcGIS-Pro-MCP — Python Bridge 最小原型 (Phase 5.2)

通信协议：newline-delimited JSON (NDJSON)
  stdin : 每行一个 JSON 请求
  stdout: 每行一个 JSON 响应
  stderr: 所有日志 / diagnostics（禁止输出到 stdout，避免污染协议）

仅使用标准库 + ArcPy（不安装第三方包）。

本文件是 Phase 5.2 的最小可行原型，仅用于证明：
  C# -> subprocess(python.exe) -> bridge_runner.py -> stdin/json -> stdout/json -> C#
链路可靠，并能真实 import arcpy 操作真实数据集路径。
"""

import json
import hashlib
import math
import numbers
import os
import re
import struct
import sys
import traceback
import time
import contextlib
import tempfile

try:
    import arcpy
    _ARCPY_OK = True
except Exception as _e:  # pragma: no cover - import 失败仍在启动阶段给出 error
    _ARCPY_OK = False
    _ARCPY_ERR = str(_e)


# ---------------------------------------------------------------- helper
def _runtime_info():
    """返回 Python / ArcPy / ArcGIS API 运行时信息。"""
    py_version = "{0}.{1}.{2}".format(
        sys.version_info[0], sys.version_info[1], sys.version_info[2]
    )
    arcpy_version = None
    arcpy_install_dir = None
    if _ARCPY_OK:
        try:
            info = arcpy.GetInstallInfo()
            arcpy_version = info.get("Version")
            arcpy_install_dir = info.get("InstallDir")
        except Exception:
            pass
    arcgis_version = None
    try:
        import arcgis
        arcgis_version = getattr(arcgis, "__version__", None)
    except Exception:
        arcgis_version = None
    return {
        "pythonVersion": py_version,
        "arcpyImportOk": _ARCPY_OK,
        "arcpyVersion": arcpy_version,
        "arcpyInstallDir": arcpy_install_dir,
        "arcgisVersion": arcgis_version,
    }


def _json_safe(value):
    """递归清理非 JSON 合法值（D-018 A 节）。

    arcpy 对空数据集/未计算统计会返回 NaN / Infinity（如空要素类 extent 全为 NaN）。
    json.dumps 默认把 NaN 写成裸 `NaN` 字面量——**不是合法 JSON**，C# 侧严格解析必然失败，
    会把"数据级问题"升级成"传输不可用"。这里统一把非有限浮点降级为 None
    （项目约定：不可得返回 null，绝不猜测），并关闭 allow_nan 以杜绝协议帧再次被污染。
    """
    if isinstance(value, bool):
        return value
    if isinstance(value, numbers.Integral):
        return int(value)  # 整数保持不变（不得因清理而做数值类型加宽）
    if isinstance(value, numbers.Real):
        f = float(value)
        return f if math.isfinite(f) else None
    if isinstance(value, dict):
        return {k: _json_safe(v) for k, v in value.items()}
    if isinstance(value, (list, tuple)):
        return [_json_safe(v) for v in value]
    return value


def _dumps(obj):
    """协议帧序列化：只允许合法 JSON（非有限浮点已降级，allow_nan 关闭）。"""
    return json.dumps(_json_safe(obj), allow_nan=False)


def _ok(request_id, result):
    """正常响应。stdout 只有这一行 JSON。"""
    sys.stdout.write(_dumps({"id": request_id, "ok": True, "result": result}) + "\n")
    sys.stdout.flush()


def _err(request_id, code, message, detail=None):
    """错误响应。Python 进程不退出，可继续处理下一个请求。"""
    body = {"id": request_id, "ok": False, "error": {"code": code, "message": message}}
    if detail:
        body["error"]["details"] = detail
    sys.stdout.write(_dumps(body) + "\n")
    sys.stdout.flush()


_REAL_STDOUT = sys.stdout


@contextlib.contextmanager
def _isolated_stdout():
    """把 fd 1 与 sys.stdout 临时重定向到临时文件（D-018 A 节：帧污染源隔离）。

    arcpy/原生库可能绕过 sys.stdout 直写 fd 1；隔离后这些输出不进入协议帧，
    由调用方作为 diagnostics（stderr / error.details）处理。
    返回 (read_captured) 可调用对象，用于在恢复后读取被隔离的内容。
    """
    saved_fd = os.dup(1)
    tmp = tempfile.TemporaryFile(mode="w+b")
    captured = {"text": ""}

    def read_captured():
        return captured.get("text", "")

    try:
        sys.stdout.flush()
        os.dup2(tmp.fileno(), 1)
        sys.stdout = os.fdopen(os.dup(1), "w", encoding="utf-8", errors="replace")
        captured["read"] = read_captured
        yield captured
    finally:
        try:
            sys.stdout.flush()
        except Exception:
            pass
        sys.stdout = _REAL_STDOUT
        os.dup2(saved_fd, 1)
        os.close(saved_fd)
        # 必须在关闭临时文件**之前**取回被隔离的内容。
        try:
            tmp.flush()
            tmp.seek(0)
            captured["text"] = tmp.read().decode("utf-8", "replace")
        except Exception:
            pass
        tmp.close()


def _describe_safe(path):
    """arcpy.Describe 的容错封装：返回可序列化的描述字典。"""
    try:
        d = arcpy.Describe(path)
        sr = None
        try:
            s = d.spatialReference
            sr = {"name": s.name, "factoryCode": s.factoryCode, "type": s.type}
        except Exception:
            sr = None
        return {
            "path": path,
            "name": getattr(d, "name", None),
            "dataType": getattr(d, "dataType", None),
            "shapeType": getattr(d, "shapeType", None),
            "workspaceType": getattr(d, "workspaceType", None),
            "spatialReference": sr,
            "children": [c.name for c in d.children] if hasattr(d, "children") else None,
        }
    except Exception as e:
        return {"path": path, "describeError": str(e)}


# ---------------------------------------------------------------- output truncation (Phase 5.5.2)
_MAX_SUMMARY_FIELD_NAMES = 200
_MAX_LIST_FIELDS = 500
_MAX_LIST_WORKSPACE_FC = 500


# ---------------------------------------------------------------- production helpers (Phase 5.5.2)
def _field_summary_dict(f):
    """把单个 arcpy.Field 转为受控 dict。任何无法稳定获得的属性返回 None，绝不猜测。"""
    return {
        "name": getattr(f, "name", None),
        "type": getattr(f, "type", None),
        "aliasName": getattr(f, "aliasName", None),
        "length": getattr(f, "length", None),
        "precision": getattr(f, "precision", None),
        "scale": getattr(f, "scale", None),
        "isNullable": getattr(f, "nullable", None),
        "domain": getattr(f, "domain", None),
        "defaultValue": getattr(f, "defaultValue", None),
        "isOid": None,  # 由调用方按 Describe.OIDFieldName 填充，这里不猜
    }


def _dataset_summary(path):
    """dataset_summary production action。不存在路径按契约返回 exists=false（成功），不抛错误。

    禁止 dump 完整 arcpy.Describe / 大型属性表 / 几何。输出 schema 稳定受控。
    """
    if not arcpy.Exists(path):
        return {
            "path": path,
            "exists": False,
            "dataType": None,
            "shapeType": None,
            "spatialReference": None,
            "featureCount": None,
            "objectIdField": None,
            "shapeFieldName": None,
            "extent": None,
            "fieldsSummary": {"oidCount": 0, "fieldCount": 0, "names": []},
            "truncated": False,
        }

    d = arcpy.Describe(path)
    sr = None
    try:
        s = d.spatialReference
        sr = {"name": getattr(s, "name", None), "factoryCode": getattr(s, "factoryCode", None)}
    except Exception:
        sr = None

    ex = None
    try:
        e = d.extent
        if e is not None:
            ex = {
                "xMin": getattr(e, "XMin", None),
                "yMin": getattr(e, "YMin", None),
                "xMax": getattr(e, "XMax", None),
                "yMax": getattr(e, "YMax", None),
            }
    except Exception:
        ex = None

    # dataType 决定 featureCount/OID 语义
    dt = getattr(d, "dataType", None)
    feature_count = None
    if dt in ("FeatureClass", "Table"):
        try:
            feature_count = int(arcpy.GetCount_management(path).getOutput(0))
        except Exception:
            feature_count = None

    # fields summary（受控：只取名字，截断到 200）
    fnames_all = []
    try:
        fnames_all = [f.name for f in d.fields]
    except Exception:
        fnames_all = []
    truncated = len(fnames_all) > _MAX_SUMMARY_FIELD_NAMES
    fnames = fnames_all[:_MAX_SUMMARY_FIELD_NAMES]
    oid_field = None
    try:
        if dt in ("FeatureClass", "Table"):
            oid_field = getattr(d, "OIDFieldName", None)
    except Exception:
        oid_field = None

    return {
        "path": path,
        "exists": True,
        "dataType": dt,
        "shapeType": getattr(d, "shapeType", None),
        "spatialReference": sr,
        "featureCount": feature_count,
        "objectIdField": oid_field,
        "shapeFieldName": getattr(d, "shapeFieldName", None),
        "extent": ex,
        "fieldsSummary": {
            "oidCount": 1 if oid_field else 0,
            "fieldCount": len(fnames_all),
            "names": fnames,
        },
        "truncated": truncated,
    }


def _list_fields(dataset_path):
    """list_fields production action。Dataset 不存在 → NOT_FOUND。字段属性基于真实 ArcPy，缺→None。"""
    if not arcpy.Exists(dataset_path):
        raise ArcPyNotFoundError(dataset_path)

    d = arcpy.Describe(dataset_path)
    oid_field = None
    try:
        oid_field = getattr(d, "OIDFieldName", None)
    except Exception:
        oid_field = None

    fields_payload = []
    fields_all = []
    try:
        fields_all = [f for f in d.fields]
    except Exception:
        fields_all = []
    truncated = len(fields_all) > _MAX_LIST_FIELDS
    for f in fields_all[:_MAX_LIST_FIELDS]:
        item = _field_summary_dict(f)
        # isOid 由 Describe.OIDFieldName 权威判定，不猜测
        item["isOid"] = (item.get("name") == oid_field) if oid_field else False
        fields_payload.append(item)

    return {
        "dataset_path": dataset_path,
        "exists": True,
        "fields": fields_payload,
        "truncated": truncated,
    }


def _list_workspace_datasets(workspace_path, recursive=False, max_depth=3, max_items=500):
    """list_workspace_datasets (Phase 8.2, D-011): real ListRasters + recursive + caps + escape check."""
    if not arcpy.Exists(workspace_path):
        raise ArcPyNotFoundError(workspace_path)

    root_real = _resolve_real(workspace_path)

    def check_escape(p):
        if not _is_under(root_real, _resolve_real(p)):
            raise EscapeRejectedError(p, _resolve_real(p))

    check_escape(workspace_path)

    d = arcpy.Describe(workspace_path)
    workspace_type = getattr(d, "workspaceFactoryProgID", None) or getattr(d, "workspaceType", None)
    is_folder = ("FileSystem" in (workspace_type or "")) or ("Shapefile" in (workspace_type or ""))

    old_workspace = arcpy.env.workspace
    truncated = False
    truncation_reason = None
    truncated_at = None
    count = 0

    def budget(n=1):
        nonlocal truncated, truncation_reason, truncated_at, count
        count += n
        if count > max_items:
            truncated = True
            truncation_reason = truncation_reason or "maxItemsReached"
            truncated_at = truncated_at or {"depth": None, "count": count}
            return False
        return True

    try:
        arcpy.env.workspace = workspace_path
        fcs_all = list(arcpy.ListFeatureClasses("*", "All") or [])
        fds_all = list(arcpy.ListDatasets(feature_type="FeatureDataset") or [])
        tables_all = list(arcpy.ListTables() or [])
        rasters_all = list(arcpy.ListRasters() or [])
        wss_all = list(arcpy.ListWorkspaces() or []) if is_folder else []
    finally:
        arcpy.env.workspace = old_workspace

    for n in fcs_all + tables_all + rasters_all + wss_all:
        check_escape(n if os.path.isabs(n) else os.path.join(workspace_path, n))

    fc_truncated = len(fcs_all) > _MAX_LIST_WORKSPACE_FC
    fcs = fcs_all[:_MAX_LIST_WORKSPACE_FC]
    if len(fcs_all) > _MAX_LIST_WORKSPACE_FC:
        truncated = True
        truncation_reason = truncation_reason or "maxItemsReached"

    fc_items = []
    for name in fcs:
        if not budget():
            break
        shape = None
        try:
            arcpy.env.workspace = workspace_path
            shape = getattr(arcpy.Describe(os.path.join(workspace_path, name)), "shapeType", None)
        except Exception:
            shape = None
        finally:
            arcpy.env.workspace = old_workspace
        fc_items.append({"name": name, "shapeType": shape, "dataType": "FeatureClass"})

    fd_items = []
    for name in fds_all:
        if not budget():
            break
        fd_items.append({"name": name})

    table_items = []
    for name in tables_all:
        if not budget():
            break
        table_items.append({"name": name, "dataType": "Table"})

    raster_items = []
    for name in rasters_all:
        if not budget():
            break
        raster_items.append({"name": name, "dataType": "RasterDataset"})

    ws_items = []
    for name in wss_all:
        if not budget():
            break
        ws_items.append({"name": name, "dataType": "Workspace"})

    fd_children = []
    folder_children = []
    if recursive and not truncated:
        for fd in fd_items:
            if truncated or not budget():
                break
            fd_path = os.path.join(workspace_path, fd["name"])
            check_escape(fd_path)
            try:
                arcpy.env.workspace = fd_path
                kids = list(arcpy.ListFeatureClasses("*", "All") or [])
            except Exception:
                kids = []
            finally:
                arcpy.env.workspace = old_workspace
            for k in kids:
                if truncated or not budget():
                    break
                fd_children.append({"featureDataset": fd["name"], "name": k, "dataType": "FeatureClass"})

        if is_folder:
            base_depth = workspace_path.rstrip(os.sep).count(os.sep)
            for dirpath, dirnames, filenames in os.walk(workspace_path):
                depth = dirpath.rstrip(os.sep).count(os.sep) - base_depth
                if depth > max_depth:
                    dirnames[:] = []
                    if not truncated:
                        truncated = True
                        truncation_reason = truncation_reason or "maxDepthReached"
                        truncated_at = truncated_at or {"depth": depth, "count": count}
                    continue
                for dn in list(dirnames):
                    check_escape(os.path.join(dirpath, dn))
                for fn in filenames:
                    if not budget():
                        break
                    rel = os.path.relpath(os.path.join(dirpath, fn), workspace_path)
                    folder_children.append({"path": rel, "dataType": "File"})

    raster_enumeration = "done"

    return {
        "workspace_path": workspace_path,
        "resolvedPath": root_real,
        "exists": True,
        "workspaceType": workspace_type,
        "isFolderWorkspace": is_folder,
        "featureDatasets": fd_items,
        "featureClasses": fc_items,
        "tables": table_items,
        "rasterDatasets": raster_items,
        "workspaces": ws_items,
        "featureDatasetChildren": fd_children,
        "folderChildren": folder_children,
        "rasterEnumeration": raster_enumeration,
        "recursive": bool(recursive),
        "maxDepth": max_depth,
        "maxItems": max_items,
        "totalCount": len(fd_items) + len(fc_items) + len(table_items) + len(raster_items) + len(ws_items) + len(fd_children) + len(folder_children),
        "truncated": truncated,
        "truncationReason": truncation_reason,
        "truncatedAt": truncated_at,
    }


# ---------------------------------------------------------------- Phase 8.2 (D-011)
def _resolve_real(path):
    try:
        return os.path.realpath(path)
    except Exception:
        return path


def _is_under(root_real, child_real):
    try:
        return os.path.commonpath([os.path.abspath(root_real)]) == \
            os.path.commonpath([os.path.abspath(root_real), os.path.abspath(child_real)])
    except Exception:
        return False


class EscapeRejectedError(Exception):
    def __init__(self, path, resolved):
        super().__init__("path escapes declared root: {0} (resolved {1})".format(path, resolved))
        self.path = path
        self.resolved = resolved


def _dataset_info(path):
    """dataset_info (Phase 8.2, D-011): light existence + Geo type. Heavy props -> dataset_summary."""
    exists = bool(arcpy.Exists(path))
    info = {"path": path, "resolvedPath": _resolve_real(path), "exists": exists,
            "dataType": None, "name": None, "catalogPath": None, "reason": None}
    if not exists:
        info["reason"] = "NOT_FOUND"
        return info
    d = arcpy.Describe(path)
    info["dataType"] = getattr(d, "dataType", None)
    info["name"] = getattr(d, "baseName", None) or getattr(d, "name", None)
    try:
        info["catalogPath"] = getattr(d, "catalogPath", None)
    except Exception:
        pass
    return info


class RasterQualificationError(Exception):
    """Internal fail-closed signal for a present raster qualification sidecar."""


_TIFF_TYPE_INFO = {
    1: ("B", 1), 2: ("c", 1), 3: ("H", 2), 4: ("I", 4),
    5: ("II", 8), 6: ("b", 1), 7: ("B", 1), 8: ("h", 2),
    9: ("i", 4), 10: ("ii", 8), 11: ("f", 4), 12: ("d", 8),
}


def _sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        while True:
            chunk = stream.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest().upper()


def _read_tiff_ifd(path, max_bytes=None):
    """Read one classic TIFF IFD, including inline values, without third-party modules."""
    size = os.path.getsize(path)
    if max_bytes is not None and size > max_bytes:
        raise RasterQualificationError("qualified GeoTIFF exceeds the supported size limit")
    with open(path, "rb") as stream:
        header = stream.read(8)
        if len(header) != 8 or header[:2] not in (b"II", b"MM"):
            raise RasterQualificationError("input is not a supported classic TIFF")
        endian = "<" if header[:2] == b"II" else ">"
        magic, ifd_offset = struct.unpack(endian + "HI", header[2:8])
        if magic != 42:
            raise RasterQualificationError("BigTIFF or invalid TIFF header is not supported by this qualification")
        if ifd_offset < 8 or ifd_offset + 2 > size:
            raise RasterQualificationError("TIFF IFD offset is outside the file")
        stream.seek(ifd_offset)
        entry_count_raw = stream.read(2)
        if len(entry_count_raw) != 2:
            raise RasterQualificationError("TIFF IFD is truncated")
        entry_count = struct.unpack(endian + "H", entry_count_raw)[0]
        if entry_count > 4096 or ifd_offset + 2 + entry_count * 12 + 4 > size:
            raise RasterQualificationError("TIFF IFD entry count or bounds are invalid")
        tags = {}
        for _ in range(entry_count):
            entry = stream.read(12)
            if len(entry) != 12:
                raise RasterQualificationError("TIFF IFD entry is truncated")
            tag, field_type, count = struct.unpack(endian + "HHI", entry[:8])
            type_info = _TIFF_TYPE_INFO.get(field_type)
            if type_info is None or count == 0:
                continue
            fmt, unit_size = type_info
            byte_count = count * unit_size
            if byte_count <= 4:
                raw = entry[8:8 + byte_count]
            else:
                value_offset = struct.unpack(endian + "I", entry[8:12])[0]
                if value_offset + byte_count > size:
                    raise RasterQualificationError("TIFF tag value is outside the file")
                stream.seek(value_offset)
                raw = stream.read(byte_count)
                if len(raw) != byte_count:
                    raise RasterQualificationError("TIFF tag value is truncated")
                stream.seek(ifd_offset + 2 + (_ * 12) + 12)
            if field_type == 2:
                value = raw.split(b"\x00", 1)[0].decode("ascii", "strict")
            elif field_type in (5, 10):
                pair_fmt = "II" if field_type == 5 else "ii"
                pairs = struct.iter_unpack(endian + pair_fmt, raw)
                value = [float(numerator) / denominator if denominator else None
                         for numerator, denominator in pairs]
            else:
                values = list(struct.unpack(endian + (fmt * count), raw))
                value = values[0] if count == 1 else values
            tags[tag] = value
        return tags, endian


def _value_list(value):
    if isinstance(value, list):
        return value
    if isinstance(value, tuple):
        return list(value)
    return [] if value is None else [value]


def _geotiff_properties(tags):
    scale = _value_list(tags.get(33550))
    tiepoint = _value_list(tags.get(33922))
    pixel_size_x = abs(float(scale[0])) if len(scale) >= 2 else None
    pixel_size_y = abs(float(scale[1])) if len(scale) >= 2 else None
    projected_code = None
    keys = _value_list(tags.get(34735))
    if len(keys) >= 4:
        declared_key_count = int(keys[3])
        available_key_count = max(0, (len(keys) - 4) // 4)
        # Some GeoTIFF writers leave the directory count larger than the values
        # actually stored. Read only complete entries that are present.
        for i in range(min(declared_key_count, available_key_count)):
            entry = keys[4 + i * 4:8 + i * 4]
            key_id, location, count, value_offset = [int(v) for v in entry]
            if key_id == 3072 and location == 0 and count == 1:
                projected_code = value_offset
                break
    return {
        "pixelSizeX": pixel_size_x,
        "pixelSizeY": pixel_size_y,
        "projectedCrsCode": projected_code,
        "width": tags.get(256),
        "height": tags.get(257),
        "tiepoint": tiepoint,
    }


def _qualification_sidecars(dataset_path):
    ext = os.path.splitext(dataset_path)[1].lower()
    if ext not in (".tif", ".tiff"):
        return []
    folder = os.path.dirname(os.path.abspath(dataset_path))
    if not os.path.isdir(folder):
        return []
    try:
        names = [name for name in os.listdir(folder)
                 if name.lower().endswith(".json") and "qualification" in name.lower()]
    except Exception as exc:
        raise RasterQualificationError("cannot inspect the input directory for a qualification sidecar: " + str(exc))
    return [os.path.join(folder, name) for name in sorted(names, key=lambda x: x.lower())]


def _read_qualified_pixels(path, tags, endian):
    bits = _value_list(tags.get(258))
    samples = int(tags.get(277) or 1)
    sample_format = _value_list(tags.get(339, 1))
    if len(bits) == 1:
        bits = bits * samples
    if len(sample_format) == 1:
        sample_format = sample_format * samples
    if samples < 1 or any(int(v) != 16 for v in bits) or len(bits) != samples:
        raise RasterQualificationError("qualified raster must use 16-bit samples in every band")
    if any(int(v) != 2 for v in sample_format) or len(sample_format) != samples:
        raise RasterQualificationError("qualified raster must use signed integer samples in every band")
    if int(tags.get(259, 1)) != 1 or int(tags.get(284, 1)) != 1:
        raise RasterQualificationError("qualified raster must be uncompressed and pixel-interleaved")
    width = int(tags.get(256) or 0)
    height = int(tags.get(257) or 0)
    strip_offsets = _value_list(tags.get(273))
    strip_counts = _value_list(tags.get(279))
    if width < 1 or height < 1 or not strip_offsets or len(strip_offsets) != len(strip_counts):
        raise RasterQualificationError("qualified raster dimensions or strips are invalid")
    bands = [[] for _ in range(samples)]
    sample_struct = endian + ("h" * samples)
    with open(path, "rb") as stream:
        for offset, byte_count in zip(strip_offsets, strip_counts):
            offset = int(offset); byte_count = int(byte_count)
            if offset < 0 or byte_count < 0 or offset + byte_count > os.path.getsize(path):
                raise RasterQualificationError("TIFF strip bounds are invalid")
            stream.seek(offset)
            raw = stream.read(byte_count)
            if len(raw) != byte_count or byte_count % (2 * samples) != 0:
                raise RasterQualificationError("TIFF strip size is invalid for the declared samples")
            for pixel in struct.iter_unpack(sample_struct, raw):
                for band_index, value in enumerate(pixel):
                    bands[band_index].append(value)
    if any(len(band) != width * height for band in bands):
        raise RasterQualificationError("TIFF pixel count does not match width and height")
    return bands


def _qualify_raster_if_applicable(dataset_path):
    """Evaluate every same-directory qualification sidecar against the current input bytes.

    No approval is cached: both the input and the sidecar are hashed before and after
    the read, and the successful result carries those hashes for the internal caller.
    """
    sidecars = _qualification_sidecars(dataset_path)
    if not sidecars:
        return {"applies": False, "qualified": True}
    if len(sidecars) != 1:
        raise RasterQualificationError("multiple qualification sidecars make the input policy ambiguous")
    sidecar_path = sidecars[0]
    if not os.path.isfile(dataset_path):
        raise RasterQualificationError("qualification sidecar is present but the input file is missing")

    input_hash_before = _sha256_file(dataset_path)
    sidecar_hash_before = _sha256_file(sidecar_path)
    try:
        with open(sidecar_path, "r", encoding="utf-8") as stream:
            qualification = json.load(stream)
    except Exception as exc:
        raise RasterQualificationError("qualification sidecar is unreadable: " + str(exc))
    if not isinstance(qualification, dict) or qualification.get("predicateId") != "TS08-INPUT-QUAL-001":
        raise RasterQualificationError("qualification sidecar predicate is missing or unsupported")

    try:
        clause = qualification["clauseWalk"]
        required_bands = int(clause["minimumSpectrallyIndependentBands"]["required"])
        roles = clause["bandIdentityAndSpectralRole"]
        grid = clause["sharedGrid"]
        no_data_rule = clause["noDataRule"]["valueOrMask"]
        distinctness = clause["spectralIndependence"]["pairwiseDistinctness"]
        counterexample = clause["rejectRepeatedSingleBandLineage"]["counterExample"]
        crs_text = grid["CRS"]
        expected_width = int(grid["width"])
        expected_height = int(grid["height"])
        expected_cell_size = float(grid["cellSize"])
        origin = [float(v) for v in grid["origin"]]
    except Exception as exc:
        raise RasterQualificationError("qualification sidecar is missing required predicate fields: " + str(exc))
    if required_bands < 2 or not isinstance(roles, list) or len(roles) < required_bands:
        raise RasterQualificationError("qualification sidecar has an invalid independent-band requirement")
    try:
        match = re.search(r"(-?\d+(?:\.\d+)?)\s*(?:%|percent)", distinctness, re.IGNORECASE)
        minimum_distinct_fraction = float(match.group(1)) / 100.0 if match else None
        nd_match = re.search(r"(?:tag\s*=\s*)(-?\d+(?:\.\d+)?)", no_data_rule, re.IGNORECASE)
        expected_nodata = int(float(nd_match.group(1))) if nd_match else None
        epsg_match = re.search(r"EPSG:(\d+)", crs_text, re.IGNORECASE)
        expected_epsg = int(epsg_match.group(1)) if epsg_match else None
    except Exception as exc:
        raise RasterQualificationError("qualification sidecar predicate values are invalid: " + str(exc))
    if minimum_distinct_fraction is None or expected_nodata is None or expected_epsg is None or len(origin) < 2:
        raise RasterQualificationError("qualification sidecar omits a required threshold, NoData value, CRS, or origin")

    counterexample_name = os.path.basename(counterexample.split()[0])
    if os.path.basename(dataset_path).lower() == counterexample_name.lower():
        raise RasterQualificationError("repeated-single-band counterexample is explicitly marked for refusal")

    tags, endian = _read_tiff_ifd(dataset_path, max_bytes=16 * 1024 * 1024)
    props = _geotiff_properties(tags)
    samples = int(tags.get(277) or 1)
    if (props["width"] != expected_width or props["height"] != expected_height
            or samples != len(roles) or samples < required_bands):
        raise RasterQualificationError("raster dimensions or band count do not match the qualification predicate")
    scale = _value_list(tags.get(33550))
    tiepoint = props["tiepoint"]
    if len(scale) < 2 or len(tiepoint) < 6:
        raise RasterQualificationError("GeoTIFF scale or tiepoint is unavailable")
    if (abs(abs(float(scale[0])) - expected_cell_size) > 1e-9
            or abs(abs(float(scale[1])) - expected_cell_size) > 1e-9):
        raise RasterQualificationError("raster cell size does not match the qualification predicate")
    if props["projectedCrsCode"] != expected_epsg:
        raise RasterQualificationError("raster projected CRS does not match the qualification predicate")
    raster_i, raster_j = float(tiepoint[0]), float(tiepoint[1])
    model_x, model_y = float(tiepoint[3]), float(tiepoint[4])
    origin_x = model_x - raster_i * float(scale[0])
    origin_y = model_y + raster_j * float(scale[1])
    if abs(origin_x - origin[0]) > 1e-8 or abs(origin_y - origin[1]) > 1e-8:
        raise RasterQualificationError("raster origin does not match the qualification predicate")

    nodata_tag = tags.get(42113)
    try:
        actual_nodata = int(float(str(nodata_tag).strip().strip("\x00")))
    except Exception:
        actual_nodata = None
    if actual_nodata != expected_nodata:
        raise RasterQualificationError("raster NoData tag does not match the qualification predicate")

    bands = _read_qualified_pixels(dataset_path, tags, endian)
    valid = [i for i in range(expected_width * expected_height)
             if all(band[i] != expected_nodata for band in bands)]
    if not valid:
        raise RasterQualificationError("raster has no valid pixels for band-independence qualification")
    differing_pairs = 0
    total_pairs = samples * (samples - 1) // 2
    for first in range(samples):
        for second in range(first + 1, samples):
            different = sum(1 for i in valid if bands[first][i] != bands[second][i])
            fraction = float(different) / len(valid)
            if fraction + 1e-12 < minimum_distinct_fraction:
                raise RasterQualificationError(
                    "bands {0} and {1} differ in only {2:.2%} of valid pixels".format(first, second, fraction))
            differing_pairs += 1

    input_hash_after = _sha256_file(dataset_path)
    sidecar_hash_after = _sha256_file(sidecar_path)
    if input_hash_after != input_hash_before or sidecar_hash_after != sidecar_hash_before:
        raise RasterQualificationError("input or qualification sidecar changed while the verdict was being computed")
    return {
        "applies": True,
        "qualified": True,
        "predicateId": qualification["predicateId"],
        "inputSha256": input_hash_after,
        "sidecarSha256": sidecar_hash_after,
        "bandCount": samples,
        "validPixelCount": len(valid),
        "independentBandPairCount": differing_pairs,
        "checkedBandPairCount": total_pairs,
    }


def _raster_info(dataset_path):
    """raster_info (Phase 8.2, D-011): full raster contract.
    Statistics: getStatistics("") entries with count>0 only (spike: plain attribute reads
    COMPUTE on the fly for non-persisted stats -> contract forbids). Field name is
    standardDeviation. GetRasterProperties/CalculateStatistics forbidden (design S3)."""
    qualification = _qualify_raster_if_applicable(dataset_path)
    exists = bool(arcpy.Exists(dataset_path))
    out = {"dataset_path": dataset_path, "resolvedPath": _resolve_real(dataset_path), "exists": exists,
           "isRaster": False, "reason": None, "dataType": None, "name": None, "format": None,
           "width": None, "height": None, "bandCount": None, "pixelType": None,
           "pixelSizeX": None, "pixelSizeY": None, "spatialReference": None, "extent": None,
           "noDataValue": None, "statistics": "unknown", "catalogPath": None}
    if not exists:
        out["reason"] = "NOT_FOUND"
        return out
    d = arcpy.Describe(dataset_path)
    dt = getattr(d, "dataType", None)
    out["dataType"] = dt
    if dt != "RasterDataset":
        out["reason"] = "NOT_A_RASTER_DATASET"
        return out
    out["isRaster"] = True
    r = arcpy.Raster(dataset_path)
    out["name"] = getattr(r, "name", None)
    try:
        out["width"] = int(r.width); out["height"] = int(r.height); out["bandCount"] = int(r.bandCount)
    except Exception:
        pass
    out["pixelType"] = getattr(r, "pixelType", None)
    nd = getattr(r, "noDataValue", None)
    out["noDataValue"] = None if nd is None else str(nd)
    sr = getattr(r, "spatialReference", None)
    if sr is None or int(getattr(sr, "factoryCode", 0) or 0) == 0:
        try:
            sr = getattr(d, "spatialReference", None)
        except Exception:
            pass
    if sr is not None:
        out["spatialReference"] = {"name": getattr(sr, "name", None), "factoryCode": getattr(sr, "factoryCode", None)}
    e = getattr(r, "extent", None)
    if e is not None:
        out["extent"] = {"xMin": getattr(e, "XMin", None), "yMin": getattr(e, "YMin", None),
                         "xMax": getattr(e, "XMax", None), "yMax": getattr(e, "YMax", None)}
    try:
        out["catalogPath"] = getattr(r, "catalogPath", None)
    except Exception:
        pass
    try:
        out["pixelSizeX"] = getattr(d, "meanCellWidth", None)
        out["pixelSizeY"] = getattr(d, "meanCellHeight", None)
        out["format"] = getattr(d, "extension", None)
    except Exception:
        pass
    if os.path.splitext(dataset_path)[1].lower() in (".tif", ".tiff"):
        try:
            tags, _ = _read_tiff_ifd(dataset_path)
            geotiff = _geotiff_properties(tags)
            if out["pixelSizeX"] is None:
                out["pixelSizeX"] = geotiff["pixelSizeX"]
            if out["pixelSizeY"] is None:
                out["pixelSizeY"] = geotiff["pixelSizeY"]
            if geotiff["projectedCrsCode"] is not None and (
                    out["spatialReference"] is None
                    or int(out["spatialReference"].get("factoryCode", 0) or 0) == 0):
                try:
                    projected_sr = arcpy.SpatialReference(geotiff["projectedCrsCode"])
                    sr_name = getattr(projected_sr, "name", None)
                except Exception:
                    sr_name = None
                out["spatialReference"] = {
                    "name": sr_name or "EPSG:{0}".format(geotiff["projectedCrsCode"]),
                    "factoryCode": geotiff["projectedCrsCode"],
                }
        except Exception:
            # Fallback is best-effort for metadata reporting; qualification remains fail-closed.
            pass
    try:
        st = r.getStatistics("")
        if isinstance(st, list) and st:
            per_band = []
            any_persisted = False
            for band in st:
                if not isinstance(band, dict):
                    continue
                persisted = float(band.get("count", 0) or 0) > 0
                any_persisted = any_persisted or persisted
                per_band.append({"minimum": band.get("min"), "maximum": band.get("max"),
                                 "mean": band.get("mean"),
                                 "standardDeviation": band.get("standardDeviation"),
                                 "persisted": persisted})
            out["statistics"] = per_band if any_persisted else "unknown"
        else:
            out["statistics"] = "unknown"
    except Exception:
        out["statistics"] = "unknown"
    return out



class ArcPyNotFoundError(Exception):
    """内部信号：路径/workspace 不存在，由 _handle 映射为 NOT_FOUND 错误码。"""

    def __init__(self, path):
        super().__init__("dataset not found: {0}".format(path))
        self.path = path


# ---------------------------------------------------------------- action registry
def _handle_ping(request_id, args):
    return _ok(request_id, "pong")


def _handle_runtime_info(request_id, args):
    return _ok(request_id, _runtime_info())


def _handle_arcpy_exists(request_id, args):
    path = args.get("path") if isinstance(args, dict) else None
    if not path:
        return _err(request_id, "INVALID_ARGUMENT", "path is required")
    return _ok(request_id, bool(arcpy.Exists(path)) if _ARCPY_OK else False)


def _handle_describe(request_id, args):
    path = args.get("path") if isinstance(args, dict) else None
    if not path:
        return _err(request_id, "INVALID_ARGUMENT", "path is required")
    return _ok(request_id, _describe_safe(path))


def _missing_dataset_summary(path):
    return {
        "path": path, "exists": False, "dataType": None, "shapeType": None,
        "spatialReference": None, "featureCount": None, "objectIdField": None,
        "shapeFieldName": None, "extent": None,
        "fieldsSummary": {"oidCount": 0, "fieldCount": 0, "names": []},
        "truncated": False,
    }


def _handle_dataset_summary(request_id, args):
    path = args.get("path") if isinstance(args, dict) else None
    if not path:
        return _err(request_id, "INVALID_ARGUMENT", "path is required")
    try:
        return _ok(request_id, _dataset_summary(path))
    except ArcPyNotFoundError:
        # 契约：不存在路径→正常成功返回 exists=false（本函数内部处理），到此处已是规避。
        return _ok(request_id, _missing_dataset_summary(path))


def _handle_list_fields(request_id, args):
    dataset_path = args.get("dataset_path") if isinstance(args, dict) else None
    if not dataset_path:
        return _err(request_id, "INVALID_ARGUMENT", "dataset_path is required")
    try:
        return _ok(request_id, _list_fields(dataset_path))
    except ArcPyNotFoundError:
        return _err(request_id, "NOT_FOUND", "dataset not found: " + dataset_path)


def _handle_list_workspace_datasets(request_id, args):
    a = args or {}
    workspace_path = a.get("workspace_path")
    if not workspace_path:
        return _err(request_id, "INVALID_ARGUMENT", "workspace_path is required")
    recursive = bool(a.get("recursive", False))
    try:
        max_depth = max(1, min(5, int(a.get("max_depth", 3) or 3)))
    except Exception:
        max_depth = 3
    try:
        max_items = max(1, min(2000, int(a.get("max_items", 500) or 500)))
    except Exception:
        max_items = 500
    try:
        return _ok(request_id, _list_workspace_datasets(workspace_path, recursive, max_depth, max_items))
    except ArcPyNotFoundError:
        return _err(request_id, "NOT_FOUND", "workspace not found: " + workspace_path)
    except EscapeRejectedError as e:
        return _err(request_id, "PATH_ESCAPE_REJECTED", str(e))


def _handle_dataset_info(request_id, args):
    path = (args or {}).get("path")
    if not path:
        return _err(request_id, "INVALID_ARGUMENT", "path is required")
    try:
        return _ok(request_id, _dataset_info(path))
    except Exception as e:
        return _err(request_id, "ARCPY_ERROR", str(e)[:300])


def _handle_raster_info(request_id, args):
    dataset_path = (args or {}).get("dataset_path")
    if not dataset_path:
        return _err(request_id, "INVALID_ARGUMENT", "dataset_path is required")
    try:
        return _ok(request_id, _raster_info(dataset_path))
    except RasterQualificationError as e:
        return _err(request_id, "INVALID_ARGUMENT", str(e)[:300])
    except Exception as e:
        return _err(request_id, "ARCPY_ERROR", str(e)[:300])


def _handle_raster_qualification(request_id, args):
    dataset_path = (args or {}).get("dataset_path")
    if not dataset_path:
        return _err(request_id, "INVALID_ARGUMENT", "dataset_path is required")
    try:
        result = _qualify_raster_if_applicable(dataset_path)
        return _ok(request_id, result)
    except RasterQualificationError as e:
        return _err(request_id, "INVALID_ARGUMENT", str(e)[:300])
    except Exception as e:
        return _err(request_id, "INVALID_ARGUMENT", "qualification could not be evaluated: " + str(e)[:250])


def _handle_raise_test_exception(request_id, args):
    raise RuntimeError("intentional test exception for error isolation")


def _handle_sleep_test(request_id, args):
    seconds = float((args or {}).get("seconds", 5) or 5)
    time.sleep(seconds)  # 由 C# 生命周期测试验证 post-dispatch timeout/cancellation
    return _ok(request_id, "slept")


PRODUCTION_ACTIONS = {
    "ping": _handle_ping,
    "runtime_info": _handle_runtime_info,
    "arcpy_exists": _handle_arcpy_exists,
    "describe": _handle_describe,
    "dataset_summary": _handle_dataset_summary,
    "list_fields": _handle_list_fields,
    "list_workspace_datasets": _handle_list_workspace_datasets,
    "dataset_info": _handle_dataset_info,
    "raster_info": _handle_raster_info,
    "raster_qualification": _handle_raster_qualification,
}

TEST_ACTIONS = {
    "raise_test_exception": _handle_raise_test_exception,
    "sleep_test": _handle_sleep_test,
}

_ALLOW_TEST_ACTIONS = os.environ.get(
    "ARCGIS_PRO_MCP_ALLOW_TEST_ACTIONS", "0"
) == "1"


# ---------------------------------------------------------------- handlers
def _handle(request_id, action, args):
    """分派 allowlisted action；test action 必须由启动环境显式开启。"""
    if not isinstance(action, str):
        return _err(request_id, "UNKNOWN_ACTION", "unknown action: {0}".format(action))

    handler = PRODUCTION_ACTIONS.get(action)
    if handler is not None:
        return handler(request_id, args)

    test_handler = TEST_ACTIONS.get(action)
    if test_handler is not None:
        if not _ALLOW_TEST_ACTIONS:
            # 不向生产调用方暴露 test hook 的存在；复用既有 UNKNOWN_ACTION 契约。
            return _err(request_id, "UNKNOWN_ACTION", "unknown action: {0}".format(action))
        return test_handler(request_id, args)

    return _err(request_id, "UNKNOWN_ACTION", "unknown action: {0}".format(action))


# ---------------------------------------------------------------- main loop
def _handle_isolated(request_id, action, args):
    """在隔离 stdout 的沙箱内分派；只有合法 JSON 帧回写到真 stdout，噪声走 stderr。"""
    try:
        with _isolated_stdout() as cap:
            _handle(request_id, action, args)
        payload = cap["read"]()
    except Exception:  # noqa: BLE001 - 隔离机制失效时退化为直接处理，不得阻断协议
        _handle(request_id, action, args)
        return

    frame = None
    noise_lines = []
    for raw in payload.splitlines():
        item = raw.strip()
        if not item:
            continue
        try:
            obj = json.loads(item)
        except Exception:
            noise_lines.append(item)
            continue
        if frame is None:
            frame = obj
        else:
            noise_lines.append(item)

    if frame is None:
        # 处理器未产出任何可解析帧：以合法帧上报（数据集/执行级），绝不静默。
        _err(
            request_id,
            "PYTHON_EXECUTION_ERROR",
            "handler produced no parsable response frame",
            detail="\n".join(noise_lines) or None,
        )
    else:
        # 被隔离的 arcpy 原文并入 error.details，便于定位（ok 帧不改结构）。
        if noise_lines and isinstance(frame.get("error"), dict):
            prev = frame["error"].get("details")
            frame["error"]["details"] = ((prev + "\n") if prev else "") + "\n".join(noise_lines)
        sys.stdout.write(_dumps(frame) + "\n")
        sys.stdout.flush()

    if noise_lines:
        sys.stderr.write("isolated stdout noise: " + " | ".join(noise_lines) + "\n")
        sys.stderr.flush()


def main():
    # 启动阶段诊断走 stderr，不污染 stdout 协议。
    sys.stderr.write("bridge_runner.py started (arcpy_ok={0})\n".format(_ARCPY_OK))
    sys.stderr.flush()

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue

        request_id = None
        try:
            req = json.loads(line)
            if not isinstance(req, dict):
                raise ValueError("request must be a JSON object")
            request_id = req.get("id")
            action = req.get("action")
            args = req.get("args") or {}
            _handle_isolated(request_id, action, args)
        except json.JSONDecodeError as e:
            # malformed JSON：返回错误但进程不退出
            _err(request_id, "INVALID_REQUEST", "malformed JSON: {0}".format(e))
        except Exception as e:  # noqa: BLE001 - 任何异常都转 JSON 错误并继续
            detail = traceback.format_exc()
            _err(request_id, "PYTHON_EXECUTION_ERROR", str(e), detail=detail)
            # 错误写入 stderr，便于 C# 侧诊断
            sys.stderr.write(detail + "\n")
            sys.stderr.flush()


if __name__ == "__main__":
    main()
