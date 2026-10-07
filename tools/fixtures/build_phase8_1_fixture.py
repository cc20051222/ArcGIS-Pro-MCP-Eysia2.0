"""
D-004 §4 —— 建立本轮自有 Phase 8.1 测试工程与 GDB（行使 G-04 授权）。

v2：精确匹配设计 §5 期望清单
  * 地图 5 个：MD_Active(2D) ×2（重名）、MD_Inactive、MD_Scene(3D)、MD_Empty
  * MD_Active 顶层图层 6 个：L_Points / L_Lines / L_Polygons / L_Group(L_Sub_A,L_Sub_B) / L_Lines(重名) / L_Hidden(不可见)
  * 布局 1 个：LY_Discovery
  * 移除模板/新建地图带入的默认底图，避免污染计数

严格边界：只写 TestFixtures\\Phase8_1\\；不打开 MyProject1.aprx / Phase4Test.gdb；
.aprx 目标已存在时**拒绝覆盖**（由调用方负责先移除旧的自有产物）。
"""
import json
import os

import arcpy

BASE = r"D:\ArcGIS-Pro-MCP 2.0\TestFixtures\Phase8_1"
GDB = os.path.join(BASE, "Phase8_1Test.gdb")
APRX_OUT = os.path.join(BASE, "Phase8_1_MapDiscovery.aprx")
TEMPLATE = r"C:\Program Files\ArcGIS\Pro\Resources\ArcToolBox\Services\routingservices\data\Blank.aprx"

report = {"steps": []}


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


print("ArcGIS Pro:", arcpy.GetInstallInfo().get("Version"))
os.makedirs(BASE, exist_ok=True)
if os.path.exists(APRX_OUT):
    raise SystemExit("REFUSE: output aprx already exists (no overwrite): " + APRX_OUT)

if not os.path.exists(GDB):
    step("CreateFileGDB", lambda: arcpy.management.CreateFileGDB(BASE, "Phase8_1Test.gdb")[0])
else:
    print("SKIP CreateFileGDB (reuse):", GDB)

SR = arcpy.SpatialReference(4326)


def mk(name, geom):
    out = os.path.join(GDB, name)
    if arcpy.Exists(out):
        print("SKIP FC (reuse):", name)
        return out
    arcpy.management.CreateFeatureclass(GDB, name, geom, spatial_reference=SR)
    arcpy.management.AddField(out, "NAME", "TEXT", field_length=64)
    with arcpy.da.InsertCursor(out, ["SHAPE@", "NAME"]) as cur:
        if geom == "POINT":
            cur.insertRow([arcpy.Point(0.0, 0.0), name])
        elif geom == "POLYLINE":
            cur.insertRow([arcpy.Polyline(arcpy.Array([arcpy.Point(0, 0), arcpy.Point(1, 1)]), SR), name])
        else:
            cur.insertRow([arcpy.Polygon(arcpy.Array([arcpy.Point(0, 0), arcpy.Point(0, 1),
                                                     arcpy.Point(1, 1), arcpy.Point(1, 0)]), SR), name])
    return out


fc_pt = mk("FC_Points", "POINT")
fc_ln = mk("FC_Lines", "POLYLINE")
fc_pg = mk("FC_Polygons", "POLYGON")

aprx = step("Open Blank.aprx template", lambda: arcpy.mp.ArcGISProject(TEMPLATE))
if aprx is None:
    raise SystemExit("cannot open template")

for m in list(aprx.listMaps()):
    step("deleteItem(template map %s)" % m.name, lambda m=m: aprx.deleteItem(m))


def _safe(obj, attr, default=None):
    try:
        return getattr(obj, attr)
    except Exception:
        return default


def drop_basemaps(m):
    """移除 createMap 带入的默认底图图层，避免污染图层计数（空地图须真正为空）。"""
    removed = []
    for lyr in list(m.listLayers()):
        is_base = bool(_safe(lyr, "isBasemapLayer", False))
        lname = _safe(lyr, "name", None)
        if is_base or (lname in ("Topographic", "World Topographic Map", "World Imagery")):
            m.removeLayer(lyr)
            removed.append(lname or "<unnamed-basemap>")
    return removed or "none"


md_active = step("createMap MD_Active (MAP)", lambda: aprx.createMap("MD_Active", "MAP"))
step("createMap MD_Inactive (MAP)", lambda: aprx.createMap("MD_Inactive", "MAP"))
md_scene = step("createMap MD_Scene (SCENE)", lambda: aprx.createMap("MD_Scene", "SCENE"))
step("createMap MD_Empty (MAP)", lambda: aprx.createMap("MD_Empty", "MAP"))


def dup_map():
    m = aprx.createMap("MD_Active", "MAP")
    before = m.name
    m.name = "MD_Active"
    return "created '%s' -> forced '%s'" % (before, m.name)


step("createMap duplicate MD_Active", dup_map)

if md_active is not None:
    step("drop default basemap on ALL maps", lambda: {_safe(m, "name", "?"): drop_basemaps(m)
                                                      for m in aprx.listMaps()})

    def add(fc, newname):
        lyr = md_active.addDataFromPath(fc)
        lyr.name = newname
        return "%s -> %s" % (fc.rsplit("\\", 1)[-1], lyr.name)

    step("add L_Points", lambda: add(fc_pt, "L_Points"))
    step("add L_Lines (#1)", lambda: add(fc_ln, "L_Lines"))
    step("add L_Polygons", lambda: add(fc_pg, "L_Polygons"))

    grp = step("createGroupLayer L_Group", lambda: md_active.createGroupLayer("L_Group"))

    def add_sub(fc, newname, group):
        """先建层、再入组、最后移除顶层原副本 —— arcpy 的 addLayerToGroup 会保留顶层原层。"""
        lyr = md_active.addDataFromPath(fc)
        lyr.name = newname
        md_active.addLayerToGroup(group, lyr)
        stray = "none"
        try:
            md_active.removeLayer(lyr)
            stray = "removed top-level original"
        except Exception as e:
            stray = "remove top-level FAILED: %s" % e
        return "group %s += %s (%s)" % (group.name, newname, stray)

    if grp is not None:
        step("group += L_Sub_A", lambda: add_sub(fc_pt, "L_Sub_A", grp))
        step("group += L_Sub_B", lambda: add_sub(fc_pg, "L_Sub_B", grp))

    def add_hidden(fc):
        lyr = md_active.addDataFromPath(fc)
        lyr.name = "L_Hidden"
        lyr.visible = False
        return "L_Hidden (visible=False)"

    step("add L_Lines (#2 重名)", lambda: add(fc_ln, "L_Lines"))
    step("add L_Hidden (visible=False)", lambda: add_hidden(fc_pg))

    step("createLayout LY_Discovery", lambda: aprx.createLayout(8.5, 11, "INCH", "LY_Discovery"))

if os.path.exists(APRX_OUT):
    raise SystemExit("REFUSE: output appeared during build: " + APRX_OUT)
step("saveACopy", lambda: (aprx.saveACopy(APRX_OUT), APRX_OUT)[1])

# 回读核对（按每个地图分别统计，避免重名地图被合并）
if os.path.isfile(APRX_OUT):
    def verify():
        a = arcpy.mp.ArcGISProject(APRX_OUT)
        maps = []
        for m in a.listMaps():
            groups = {}
            for l in m.listLayers():
                if _safe(l, "isGroupLayer", False):
                    groups[_safe(l, "name", "?")] = [_safe(c, "name", "?") for c in l.listLayers()]
            maps.append({"name": m.name, "mapType": m.mapType,
                         "layers": [l.name for l in m.listLayers()],
                         "group_children": groups,
                         "visible": {l.name: l.visible for l in m.listLayers()}})
        return {"maps": maps, "layouts": [x.name for x in a.listLayouts()]}

    v = step("verify saved project", verify)
    if v:
        report.update(v)
        print()
        for m in v["maps"]:
            print("MAP %-14s %-6s layers=%s visible=%s" % (m["name"], m["mapType"], m["layers"], m["visible"]))
        print("LAYOUTS:", v["layouts"])

out = os.path.join(BASE, "fixture-build-report.json")
with open(out, "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)
print("\nreport ->", out)
