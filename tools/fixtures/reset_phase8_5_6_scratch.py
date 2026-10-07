# -*- coding: utf-8 -*-
"""重置 Phase8_5_6 的 scratch GDB（GP 写输出容器）。

用途：受控写入验收（D-027）需要"fresh 输出"，而 scratch 中可能残留上一轮 GP 产物。
      本脚本**只**删除并重建 `WorkBuddyScratch.gdb`，不触碰 WorkBuddyTest.gdb / WorkBuddyControlled.aprx。

硬前置（四判据风格）：
  1. ArcGIS Pro 未运行（tasklist 无 ArcGISPro.exe）；
  2. base 目录下无 `*.sr.lock`（无活跃数据锁）。
任一不满足 → 拒绝执行（退出码 2），绝不强删。

用法：
  "<Pro>\bin\Python\envs\arcgispro-py3\python.exe" -u tools/fixtures/reset_phase8_5_6_scratch.py \
      --base "D:\\ArcGIS-Pro-MCP 2.0\\TestFixtures\\Phase8_5_6" \
      --log  "<evidence dir>\\scratch-reset-log.json"
"""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time

BASE_DEFAULT = r"D:\ArcGIS-Pro-MCP 2.0\TestFixtures\Phase8_5_6"
SCRATCH_NAME = "WorkBuddyScratch.gdb"
TEST_NAME = "WorkBuddyTest.gdb"
APRX_NAME = "WorkBuddyControlled.aprx"


def pro_running():
    try:
        out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq ArcGISPro.exe"],
                             capture_output=True, text=True, timeout=60).stdout or ""
    except Exception as e:
        return None, "tasklist failed: %r" % (e,)
    if "ArcGISPro.exe" in out:
        return True, "tasklist: " + out.strip().replace("\n", " | ")[:300]
    return False, "tasklist: no ArcGISPro.exe"


def locks_present(base):
    found = []
    for root, dirs, files in os.walk(base):
        for fn in files:
            if fn.endswith(".sr.lock"):
                found.append(os.path.join(root, fn))
    return found


def hash_tree(root, skip_ext=(".sr.lock",)):
    out = {}
    for r, d, files in os.walk(root):
        for fn in sorted(files):
            if fn.endswith(skip_ext):
                continue
            p = os.path.join(r, fn)
            h = hashlib.sha256()
            with open(p, "rb") as f:
                for chunk in iter(lambda: f.read(1 << 20), b""):
                    h.update(chunk)
            out[os.path.relpath(p, root).replace("\\", "/")] = h.hexdigest()
    return out


def dataset_count(path):
    import arcpy
    d = path.replace("\\", "/")
    arcpy.env.workspace = d
    items = sorted([x for x in (arcpy.ListFeatureClasses() or []) + (arcpy.ListTables() or [])])
    return items


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default=BASE_DEFAULT)
    ap.add_argument("--log", default=None)
    args = ap.parse_args()

    base = args.base
    scratch = os.path.join(base, SCRATCH_NAME)
    test = os.path.join(base, TEST_NAME)
    aprx = os.path.join(base, APRX_NAME)

    log = {"script": "reset_phase8_5_6_scratch.py", "started": time.strftime("%Y-%m-%dT%H:%M:%S"),
           "base": base, "steps": []}

    def step(name, ok, detail=""):
        log["steps"].append({"step": name, "ok": bool(ok), "detail": str(detail)})
        print(("OK   " if ok else "FAIL ") + name.ljust(46) + " " + str(detail)[:220], flush=True)

    # ---- 前置判据 ----
    running, rdetail = pro_running()
    step("precheck ArcGISPro not running", running is False, rdetail)
    if running is None:
        print("REFUSE: cannot determine Pro state", flush=True)
        return 2
    if running is True:
        print("REFUSE: ArcGIS Pro is running — 拒绝在 Pro 运行时删目录（红线）", flush=True)
        return 2

    locks = locks_present(base)
    step("precheck no .sr.lock under base", len(locks) == 0, "%d lock(s): %s" % (len(locks), locks[:5]))
    if locks:
        print("REFUSE: data locks present (Pro may still be shutting down)", flush=True)
        return 2

    # ---- 保全：Test.gdb 与 aprx 前后内容哈希 ----
    test_before = hash_tree(test)
    step("hash WorkBuddyTest.gdb BEFORE", True, "%d files" % len(test_before))
    log["test_gdb_hash_before"] = test_before
    log["aprx_sha256"] = hashlib.sha256(open(aprx, "rb").read()).hexdigest() if os.path.exists(aprx) else None
    step("hash WorkBuddyControlled.aprx", True, log["aprx_sha256"])
    log["scratch_existed_before"] = os.path.isdir(scratch)

    import arcpy  # noqa: E402

    # ---- 删除 scratch ----
    if os.path.isdir(scratch):
        try:
            shutil.rmtree(scratch)
            step("delete WorkBuddyScratch.gdb", not os.path.exists(scratch), scratch)
        except Exception as e:
            step("delete WorkBuddyScratch.gdb", False, repr(e))
            return 3
    else:
        step("delete WorkBuddyScratch.gdb", True, "not present (nothing to delete)")

    # ---- 重建空 scratch ----
    try:
        arcpy.management.CreateFileGDB(base, SCRATCH_NAME)
        step("create empty WorkBuddyScratch.gdb", os.path.isdir(scratch), scratch)
    except Exception as e:
        step("create empty WorkBuddyScratch.gdb", False, repr(e))
        return 4

    items = dataset_count(scratch)
    step("verify scratch empty (0 datasets)", len(items) == 0, "%d: %s" % (len(items), items[:10]))
    log["scratch_datasets_after"] = items

    # ---- 清理同目录 scratch shapefile 残留（OUT_*.shp 及其边车）----
    removed_shp = []
    for fn in sorted(os.listdir(base)):
        low = fn.lower()
        if low.startswith("out_") and low.endswith((".shp", ".shx", ".dbf", ".prj", ".cpg", ".sbn", ".sbx")):
            try:
                os.remove(os.path.join(base, fn))
                removed_shp.append(fn)
            except Exception as e:
                step("delete " + fn, False, repr(e))
    step("delete scratch shapefiles (OUT_*)", True, "%d: %s" % (len(removed_shp), removed_shp))
    log["removed_shp_residue"] = removed_shp

    # ---- 后置：Test.gdb 未被改动 ----
    test_after = hash_tree(test)
    same = (test_before == test_after)
    diff = sorted(set(test_before) ^ set(test_after)) + \
        sorted(k for k in set(test_before) & set(test_after) if test_before[k] != test_after[k])
    step("verify WorkBuddyTest.gdb UNCHANGED", same,
         "identical" if same else ("diff=%d %s" % (len(diff), diff[:5])))
    log["test_gdb_hash_after"] = test_after

    log["finished"] = time.strftime("%Y-%m-%dT%H:%M:%S")
    log["result"] = "OK" if (same and len(items) == 0) else "CHECK"
    if args.log:
        with open(args.log, "w", encoding="utf-8") as f:
            json.dump(log, f, ensure_ascii=False, indent=2)
        print("log -> %s" % args.log, flush=True)
    print("RESET %s" % log["result"], flush=True)
    return 0 if log["result"] == "OK" else 5


if __name__ == "__main__":
    sys.exit(main())
