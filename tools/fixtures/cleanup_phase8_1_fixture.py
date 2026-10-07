"""D-004 §4 收尾：清理自有 fixture 中由模板/默认设置带入的底图图层，并回读核对。

只操作**本轮自有**的 TestFixtures\\Phase8_1\\Phase8_1_MapDiscovery.aprx（就地保存）。
注意：某些图层实例（如底图）不支持 .name / .visible，需防御式读取。
"""
import json
import os

import arcpy

APRX = r"D:\ArcGIS-Pro-MCP 2.0\TestFixtures\Phase8_1\Phase8_1_MapDiscovery.aprx"
assert os.path.isfile(APRX), APRX


def safe(obj, attr, default=None):
    try:
        return getattr(obj, attr)
    except Exception:
        return default


aprx = arcpy.mp.ArcGISProject(APRX)
removed = []
for m in aprx.listMaps():
    for lyr in list(m.listLayers()):
        is_base = bool(safe(lyr, "isBasemapLayer", False))
        lname = safe(lyr, "name", None)
        if is_base or (lname in ("Topographic", "World Topographic Map", "World Imagery")):
            m.removeLayer(lyr)
            removed.append("%s/%s" % (safe(m, "name", "?"), lname if lname else "<unnamed-basemap>"))

aprx.save()
print("removed basemap layers:", removed if removed else "none")

# 回读核对
a = arcpy.mp.ArcGISProject(APRX)
out = {"inventory": []}
for m in a.listMaps():
    layers = []
    vis = {}
    for l in m.listLayers():
        nm = safe(l, "name", "<unnamed>")
        layers.append(nm)
        vis[nm] = safe(l, "visible", None)
    out["inventory"].append({"name": safe(m, "name", "?"), "mapType": safe(m, "mapType", "?"),
                             "layers": layers, "visible": vis})
out["layouts"] = [safe(x, "name", "?") for x in a.listLayouts()]
print(json.dumps(out, ensure_ascii=False, indent=2))

inv = os.path.join(os.path.dirname(APRX), "fixture-inventory.json")
with open(inv, "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=2)
print("inventory ->", inv)
