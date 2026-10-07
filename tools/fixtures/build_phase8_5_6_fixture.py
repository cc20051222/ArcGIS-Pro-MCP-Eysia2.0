"""
D-027 阶段一 —— 构建 Phase 8.5.6 受控写入验收 fixture（设计基线 = outbox/R-DRAFT_phase856-controlled-write-prep.md §2）。

产物三件（均在 TestFixtures\\Phase8_5_6\\，全部为自有 fixture）：
  * WorkBuddyTest.gdb      源数据（验收全程只读）：FC_Points(5) / FC_Lines(3) / FC_Polygons(4) / TBL_Attr(4)
  * WorkBuddyScratch.gdb   GP 写输出专用（验收后整体清理）
  * WorkBuddyControlled.aprx 受控工程：WB_Map(L_Points/L_Lines/L_Polygons/L_Group(L_Sub_A)) + WB_Map_Empty

严格边界：不打开 MyProject1.aprx / Phase4Test.gdb；输出已存在**拒绝覆盖**（三个产物皆然）；
前置条件：ArcGIS Pro 已关闭（构建用 ArcPy，避免锁与许可冲突）。
"""
import hashlib
import json
import os
import time

import arcpy

BASE = r"D:\ArcGIS-Pro-MCP 2.0\TestFixtures\Phase8_5_6"
GDB_TEST = os.path.join(BASE, "WorkBuddyTest.gdb")
GDB_SCRATCH = os.path.join(BASE, "WorkBuddyScratch.gdb")
APRX_OUT = os.path.join(BASE, "WorkBuddyControlled.aprx")
TEMPLATE = r"C:\Program Files\ArcGIS\Pro\Resources\ArcToolBox\Services\routingservices\data\Blank.aprx"

report = {"fixture": "Phase8_5_6", "started": time.strftime("%Y-%m-%dT%H:%M:%S"), "steps": []}


def step(name, fn):
    try:
        r = fn()
        report["steps"].append({"step": name, "ok": True, "detail": str(r)})
        print("OK   %-44s %s" % (name, r))
        return r
    except Exception as e:
        report["steps"].append({"step": name, "ok": False, "error": "%s: %s" % (type(e).__name__, e)})
        print("FAIL %-44s %s: %s" % (name, type(e).__name__, e))
        return None


def hard(name, fn):
    """同 step()，但失败即中止构建（关键步骤不允许带病继续）。"""
    r = step(name, fn)
    if not report["steps"][-1]["ok"]:
        raise SystemExit("ABORT at step: " + name)
    return r


print("ArcGIS Pro:", arcpy.GetInstallInfo().get("Version"))

# ---------- 前置检查 ----------
for p in (APRX_OUT, GDB_TEST, GDB_SCRATCH):
    if os.path.exists(p):
        raise SystemExit("REFUSE: output already exists (no overwrite): " + p)
os.makedirs(BASE, exist_ok=True)

# ---------- 两个 GDB ----------
hard("CreateFileGDB WorkBuddyTest.gdb", lambda: arcpy.management.CreateFileGDB(BASE, "WorkBuddyTest.gdb")[0])
hard("CreateFileGDB WorkBuddyScratch.gdb", lambda: arcpy.management.CreateFileGDB(BASE, "WorkBuddyScratch.gdb")[0])

SR = arcpy.SpatialReference(4326)


def add_common_fields(path):
    arcpy.management.AddField(path, "NAME", "TEXT", field_length=64)
    arcpy.management.AddField(path, "CATEGORY", "TEXT", field_length=32)
    arcpy.management.AddField(path, "VALUE", "LONG")


hard("Create FC_Points", lambda: arcpy.management.CreateFeatureclass(GDB_TEST, "FC_Points", "POINT", spatial_reference=SR)[0])
hard("Create FC_Lines", lambda: arcpy.management.CreateFeatureclass(GDB_TEST, "FC_Lines", "POLYLINE", spatial_reference=SR)[0])
hard("Create FC_Polygons", lambda: arcpy.management.CreateFeatureclass(GDB_TEST, "FC_Polygons", "POLYGON", spatial_reference=SR)[0])
hard("Create TBL_Attr", lambda: arcpy.management.CreateTable(GDB_TEST, "TBL_Attr")[0])
for fc in ("FC_Points", "FC_Lines", "FC_Polygons", "TBL_Attr"):
    hard("AddFields %s" % fc, lambda fc=fc: add_common_fields(os.path.join(GDB_TEST, fc)))


def insert_rows(path, fields, rows):
    with arcpy.da.InsertCursor(path, fields) as cur:
        for row in rows:
            cur.insertRow(row)
    return "%d rows" % len(rows)


# 确定性几何（R-DRAFT §2.2）：点 (0,0)..(4,0) P1..P5，CATEGORY 交替 A/B
pt_rows = [("P%d" % (i + 1), "A" if i % 2 == 0 else "B", (i + 1) * 10,
            arcpy.PointGeometry(arcpy.Point(float(i), 0.0), SR)) for i in range(5)]
hard("Insert FC_Points (5)", lambda: insert_rows(os.path.join(GDB_TEST, "FC_Points"),
                                                 ["NAME", "CATEGORY", "VALUE", "SHAPE@"], pt_rows))

# 线各 2 顶点 N1..N3（底边走向，供 select_by_location 预计算）
line_defs = [("N1", "A", 100, [(0, 0), (1, 0)]),
             ("N2", "B", 200, [(0, 2), (1, 2)]),
             ("N3", "A", 300, [(2, 2), (3, 2)])]
ln_rows = [(n, c, v, arcpy.Polyline(arcpy.Array([arcpy.Point(x, y) for x, y in pts]), SR))
           for n, c, v, pts in line_defs]
hard("Insert FC_Lines (3)", lambda: insert_rows(os.path.join(GDB_TEST, "FC_Lines"),
                                                ["NAME", "CATEGORY", "VALUE", "SHAPE@"], ln_rows))

# 面 4 个单位方块 G1..G4，CATEGORY=A,A,B,B
poly_defs = [("G1", "A", 1, (0, 0)), ("G2", "A", 2, (2, 0)),
             ("G3", "B", 3, (0, 2)), ("G4", "B", 4, (2, 2))]
pg_rows = []
for n, c, v, (x0, y0) in poly_defs:
    ring = arcpy.Array([arcpy.Point(x0, y0), arcpy.Point(x0 + 1, y0),
                        arcpy.Point(x0 + 1, y0 + 1), arcpy.Point(x0, y0 + 1),
                        arcpy.Point(x0, y0)])
    pg_rows.append((n, c, v, arcpy.Polygon(ring, SR)))
hard("Insert FC_Polygons (4)", lambda: insert_rows(os.path.join(GDB_TEST, "FC_Polygons"),
                                                   ["NAME", "CATEGORY", "VALUE", "SHAPE@"], pg_rows))

tbl_rows = [("R1", "A", 1), ("R2", "B", 2), ("R3", "A", 3), ("R4", "B", 4)]
hard("Insert TBL_Attr (4)", lambda: insert_rows(os.path.join(GDB_TEST, "TBL_Attr"),
                                                ["NAME", "CATEGORY", "VALUE"], tbl_rows))


def count(path):
    return int(arcpy.management.GetCount(path)[0])


for fc, expect in (("FC_Points", 5), ("FC_Lines", 3), ("FC_Polygons", 4), ("TBL_Attr", 4)):
    got = hard("Count %s" % fc, lambda fc=fc: count(os.path.join(GDB_TEST, fc)))
    if got != expect:
        raise SystemExit("ABORT: %s count %d != expected %d" % (fc, got, expect))

# ---------- 受控工程 ----------
aprx = hard("Open Blank.aprx template", lambda: arcpy.mp.ArcGISProject(TEMPLATE))
for m in list(aprx.listMaps()):
    hard("deleteItem(template map %s)" % m.name, lambda m=m: aprx.deleteItem(m))


def _safe(obj, attr, default=None):
    try:
        return getattr(obj, attr)
    except Exception:
        return default


def drop_basemaps(m):
    removed = []
    for lyr in list(m.listLayers()):
        is_base = bool(_safe(lyr, "isBasemapLayer", False))
        lname = _safe(lyr, "name", None)
        if is_base or (lname in ("Topographic", "World Topographic Map", "World Imagery")):
            m.removeLayer(lyr)
            removed.append(lname or "<unnamed-basemap>")
    return removed or "none"


wb_map = hard("createMap WB_Map (MAP)", lambda: aprx.createMap("WB_Map", "MAP"))
hard("createMap WB_Map_Empty (MAP)", lambda: aprx.createMap("WB_Map_Empty", "MAP"))
hard("drop default basemap on ALL maps",
     lambda: {_safe(m, "name", "?"): drop_basemaps(m) for m in aprx.listMaps()})


def add(fc, newname):
    lyr = wb_map.addDataFromPath(fc)
    lyr.name = newname
    return "%s -> %s" % (fc.rsplit("\\", 1)[-1], lyr.name)


hard("add L_Points", lambda: add(os.path.join(GDB_TEST, "FC_Points"), "L_Points"))
hard("add L_Lines", lambda: add(os.path.join(GDB_TEST, "FC_Lines"), "L_Lines"))
hard("add L_Polygons", lambda: add(os.path.join(GDB_TEST, "FC_Polygons"), "L_Polygons"))
grp = hard("createGroupLayer L_Group", lambda: wb_map.createGroupLayer("L_Group"))


def add_sub(fc, newname, group):
    """同 Phase8_1 模式：先建层、再入组、最后移除 addLayerToGroup 保留的顶层原副本。"""
    lyr = wb_map.addDataFromPath(fc)
    lyr.name = newname
    wb_map.addLayerToGroup(group, lyr)
    stray = "none"
    try:
        wb_map.removeLayer(lyr)
        stray = "removed top-level original"
    except Exception as e:
        stray = "remove top-level FAILED: %s" % e
    return "group %s += %s (%s)" % (group.name, newname, stray)


hard("group += L_Sub_A", lambda: add_sub(os.path.join(GDB_TEST, "FC_Points"), "L_Sub_A", grp))

if os.path.exists(APRX_OUT):
    raise SystemExit("REFUSE: output appeared during build: " + APRX_OUT)
hard("saveACopy", lambda: (aprx.saveACopy(APRX_OUT), APRX_OUT)[1])

# ---------- 保存后回读逐项断言 ----------
EXPECTED_MAPS = ["WB_Map", "WB_Map_Empty"]
EXPECTED_LAYERS = ["L_Points", "L_Lines", "L_Polygons", "L_Group"]
EXPECTED_GROUP_CHILDREN = ["L_Sub_A"]
EXPECTED_EMPTY = []


def verify():
    a = arcpy.mp.ArcGISProject(APRX_OUT)
    maps = []
    for m in a.listMaps():
        groups = {}
        for l in m.listLayers():
            if _safe(l, "isGroupLayer", False):
                groups[_safe(l, "name", "?")] = [_safe(c, "name", "?") for c in l.listLayers()]
        child_names = {c for kids in groups.values() for c in kids}
        # listLayers() 是扁平列表（子层紧跟组层），需剔除子层得到真正的顶层序列
        top_level = [_safe(l, "name", "?") for l in m.listLayers() if _safe(l, "name", "?") not in child_names]
        maps.append({"name": m.name, "mapType": m.mapType,
                     "layers": top_level,
                     "layers_flat": [l.name for l in m.listLayers()],
                     "group_children": groups,
                     "visible": {l.name: l.visible for l in m.listLayers()}})
    maps = sorted(maps, key=lambda x: EXPECTED_MAPS.index(x["name"]) if x["name"] in EXPECTED_MAPS else 99)
    assert [m["name"] for m in maps] == EXPECTED_MAPS, "maps mismatch: %s" % [m["name"] for m in maps]
    wb = [m for m in maps if m["name"] == "WB_Map"][0]
    # 判据按集合 + 唯一性（listLayers() 返回顺序不代表 TOC 顺序）
    assert sorted(wb["layers"]) == sorted(EXPECTED_LAYERS), "WB_Map top-level layers mismatch: %s" % wb["layers"]
    assert len(set(wb["layers"])) == len(wb["layers"]), "duplicate top-level layer names: %s" % wb["layers"]
    assert wb["group_children"].get("L_Group") == EXPECTED_GROUP_CHILDREN, \
        "L_Group children mismatch: %s" % wb["group_children"]
    empty = [m for m in maps if m["name"] == "WB_Map_Empty"][0]
    assert empty["layers"] == EXPECTED_EMPTY, "WB_Map_Empty not empty: %s" % empty["layers"]
    counts = {"FC_Points": count(os.path.join(GDB_TEST, "FC_Points")),
              "FC_Lines": count(os.path.join(GDB_TEST, "FC_Lines")),
              "FC_Polygons": count(os.path.join(GDB_TEST, "FC_Polygons")),
              "TBL_Attr": count(os.path.join(GDB_TEST, "TBL_Attr"))}
    assert counts == {"FC_Points": 5, "FC_Lines": 3, "FC_Polygons": 4, "TBL_Attr": 4}, "counts mismatch: %s" % counts
    return {"maps": maps, "feature_counts": counts}


v = hard("verify saved project (exact)", verify)
report.update(v)

# ---------- fixture-inventory.json（验收判据以清单内容为准） ----------


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def gdb_files_hash(gdb):
    out = {}
    for root, _dirs, files in os.walk(gdb):
        for fn in files:
            if fn.endswith(".lock"):
                continue
            p = os.path.join(root, fn)
            out[os.path.relpath(p, BASE)] = sha256(p)
    return out


def dataset_info(name, is_table):
    p = os.path.join(GDB_TEST, name)
    fields = [{"name": f.name, "type": f.type, "length": f.length}
              for f in arcpy.ListFields(p) if f.type not in ("OID", "Geometry") and f.name not in ("Shape",)]
    return {"name": name, "type": "TABLE" if is_table else "FEATURE_CLASS",
            "rowCount": count(p), "fields": fields}


inventory = {
    "fixture": "Phase8_5_6",
    "built": time.strftime("%Y-%m-%dT%H:%M:%S"),
    "spatial_reference": "EPSG:4326",
    "project": {
        "aprx": "WorkBuddyControlled.aprx",
        "aprx_sha256_evidence_only": sha256(APRX_OUT),
        "maps": v["maps"],
    },
    "gdb_test": {
        "name": "WorkBuddyTest.gdb",
        "read_only_during_acceptance": True,
        "datasets": [dataset_info("FC_Points", False), dataset_info("FC_Lines", False),
                     dataset_info("FC_Polygons", False), dataset_info("TBL_Attr", True)],
        "file_sha256": gdb_files_hash(GDB_TEST),
    },
    "gdb_scratch": {"name": "WorkBuddyScratch.gdb", "purpose": "GP outputs only, cleaned after acceptance"},
}

inv_path = os.path.join(BASE, "fixture-inventory.json")
with open(inv_path, "w", encoding="utf-8") as f:
    json.dump(inventory, f, ensure_ascii=False, indent=2)
print("\ninventory ->", inv_path)

report["finished"] = time.strftime("%Y-%m-%dT%H:%M:%S")
out = os.path.join(BASE, "fixture-build-report.json")
with open(out, "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)
print("report ->", out)
print("\nBUILD OK: WB_Map layers =", [l["layers"] for l in v["maps"] if l["name"] == "WB_Map"][0])
