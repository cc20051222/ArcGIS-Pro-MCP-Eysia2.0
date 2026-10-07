# -*- coding: utf-8 -*-
"""D-011 fixture builder: TestFixtures/Phase8_2 (exercises G-25 authorization).
ArcPy headless, per D-004 precedent. CalculateStatistics ONLY for stat1band.tif (allowed by dispatch).
Run: arcgispro-py3 python build_phase8_2_fixture.py
"""
import os, shutil, subprocess, json, hashlib, io, traceback

import arcpy

BASE = r"D:\ArcGIS-Pro-MCP 2.0\TestFixtures\Phase8_2"
R = os.path.join(BASE, "rasters")
GDB = os.path.join(BASE, "gdb", "Phase8_2Test.gdb")
F = os.path.join(BASE, "folders")

def log(m): print(m, flush=True)

def step(name, fn):
    try:
        r = fn()
        log("OK   %s %s" % (name, r if r is not None else ""))
        return r
    except Exception:
        log("FAIL %s\n%s" % (name, traceback.format_exc()[-500:]))
        raise

def sha(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()

# 0) clean slate for our own fixture dirs (idempotent rebuild)
if os.path.isdir(BASE):
    log("REMOVE existing Phase8_2 fixture dir (self-owned)")
    # remove junction first to avoid traversal into target
    jl = os.path.join(F, "jail", "jlink")
    if os.path.isdir(jl) and not os.path.islink(jl):
        subprocess.run(["cmd", "/c", "rmdir", jl], check=False)
    shutil.rmtree(BASE, ignore_errors=True)
os.makedirs(R, exist_ok=True)
os.makedirs(os.path.dirname(GDB), exist_ok=True)

# 1) rasters
src = r"C:\Program Files\ArcGIS\Pro\Resources\pedata\geoid\WGS84.img"
stat1 = os.path.join(R, "stat1band.tif")
step("stat1band.tif (copy + calc stats)", lambda: arcpy.management.CopyRaster(src, stat1))
step("  CalculateStatistics(stat1band)", lambda: arcpy.management.CalculateStatistics(stat1, 1, 1, [1], "SKIP_EXISTING", "NONE"))

nostats = os.path.join(R, "nostats.tif")
step("nostats.tif (copy, NO stats)", lambda: arcpy.management.CopyRaster(src, nostats))
# ensure no .aux/.statistics beside it
for junk in arcpy.ListFiles(os.path.dirname(nostats)) if False else []:
    pass
for f in os.listdir(R):
    if f.startswith("nostats") and (f.endswith(".aux") or f.endswith(".statistics")):
        os.remove(os.path.join(R, f)); log("  removed %s" % f)

multi = os.path.join(R, "multi.tif")
step("multi.tif (CompositeBands x3)", lambda: arcpy.management.CompositeBands(
    [nostats + "/Band_1", nostats + "/Band_1", nostats + "/Band_1"], multi))

f32 = os.path.join(R, "float32.img")
step("float32.img (F32 copy)", lambda: arcpy.management.CopyRaster(nostats, f32, pixel_type="32_BIT_FLOAT"))

# corrupt.tif: valid tiny TIFF header then truncated body
corrupt = os.path.join(R, "corrupt.tif")
with open(corrupt, "wb") as f:
    f.write(b"II*\x00" + b"\x08\x00\x00\x00" + os.urandom(200))
log("OK   corrupt.tif (truncated fake)")

# 2) gdb
step("CreateFileGDB", lambda: arcpy.management.CreateFileGDB(os.path.dirname(GDB), os.path.basename(GDB)))
ws = GDB
step("FC_Points", lambda: arcpy.management.CreateFeatureclass(ws, "FC_Points", "POINT", spatial_reference=arcpy.SpatialReference(4326)))
step("FC_Lines", lambda: arcpy.management.CreateFeatureclass(ws, "FC_Lines", "POLYLINE", spatial_reference=arcpy.SpatialReference(4326)))
step("TB_Main", lambda: arcpy.management.CreateTable(ws, "TB_Main"))
step("FD_Group", lambda: arcpy.management.CreateFeatureDataset(ws, "FD_Group", arcpy.SpatialReference(4326)))
step("FC_InFD", lambda: arcpy.management.CreateFeatureclass(os.path.join(ws, "FD_Group"), "FC_InFD", "POINT", spatial_reference=arcpy.SpatialReference(4326)))
step("RasterInGdb", lambda: arcpy.management.CopyRaster(nostats, os.path.join(ws, "RG_Nostats")))

# 3) folders: hierarchy + jail/outside + junction
step("folders/ws (folder workspace, copy a tif)", lambda: (shutil.copy(nostats, os.path.join(F, "ws", "in_ws.tif")) if os.path.isdir(F) else None) or os.makedirs(os.path.join(F, "ws"), exist_ok=True) or shutil.copy(nostats, os.path.join(F, "ws", "in_ws.tif")))
for d in ["hierarchy/l1/l2/l3", "jail", "outside"]:
    os.makedirs(os.path.join(F, d.replace("/", os.sep)), exist_ok=True)
shutil.copy(nostats, os.path.join(F, "hierarchy", "l1", "l2", "deep.tif"))
shutil.copy(nostats, os.path.join(F, "jail", "inside.tif"))
shutil.copy(nostats, os.path.join(F, "outside", "escaped.tif"))
# junction: jail/jlink -> ../outside (within workspace, per dispatch)
jl = os.path.join(F, "jail", "jlink")
step("junction jail/jlink -> outside", lambda: subprocess.run(["cmd", "/c", "mklink", "/J", jl, os.path.join(F, "outside")], check=True, capture_output=True))

# 4) inventory + hashes
inv = {"fixtures": []}
for root, dirs, files in os.walk(BASE):
    # do not traverse into junction target twice
    for fn in files:
        p = os.path.join(root, fn)
        rel = os.path.relpath(p, BASE)
        try:
            inv["fixtures"].append({"path": rel, "size": os.path.getsize(p), "sha256": sha(p)})
        except OSError as e:
            inv["fixtures"].append({"path": rel, "error": str(e)})
inv["generated"] = "build_phase8_2_fixture.py"
io.open(os.path.join(BASE, "fixture-inventory.json"), "w", encoding="utf-8").write(
    json.dumps(inv, ensure_ascii=False, indent=1))
log("INVENTORY written: %d entries" % len(inv["fixtures"]))
log("ALL DONE")
