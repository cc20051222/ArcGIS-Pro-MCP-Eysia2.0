"""
D-027 阶段三 —— 清理 Phase 8.5.6 自有 fixture（清理四判据全绿才执行，任一不满足即拒绝）。

只删除自有三产物 + 本轮两份 JSON 报告 + 同目录 scratch shapefile（OUT_*）。
不触碰 TestFixtures\\ 其他目录、保护资产（MyProject1.aprx / Phase4Test.gdb / retained fixture）。

清理四判据（HANDOVER §7）：
  1. MCP 端口 6520 无监听；2. 无 ArcGISPro 进程；3. Phase8_5_6 下锁文件为 0；4. 目标 mtime 冻结（>300s）。
"""
import os
import re
import shutil
import subprocess
import sys
import time

BASE = r"D:\ArcGIS-Pro-MCP 2.0\TestFixtures\Phase8_5_6"
TARGETS = ["WorkBuddyControlled.aprx", "WorkBuddyTest.gdb", "WorkBuddyScratch.gdb",
           "fixture-inventory.json", "fixture-build-report.json"]

failures = []

# 判据 1：6520 无监听
net = subprocess.run(["netstat", "-ano"], capture_output=True, text=True).stdout
if re.search(r"127\.0\.0\.1:6520\s+.*LISTENING", net):
    failures.append("port 6520 still listening")

# 判据 2：无 ArcGISPro 进程
tl = subprocess.run(["tasklist", "/FI", "IMAGENAME eq ArcGISPro.exe"], capture_output=True, text=True).stdout
if "ArcGISPro.exe" in tl:
    failures.append("ArcGISPro.exe still running")

# 判据 3：锁文件为 0
locks = []
for root, _d, files in os.walk(BASE):
    locks += [os.path.join(root, f) for f in files if f.endswith(".lock")]
if locks:
    failures.append("lock files present: %s" % locks)

# 判据 4：目标 mtime 冻结（>300s 未变动）
now = time.time()
for t in TARGETS:
    p = os.path.join(BASE, t)
    if os.path.isdir(p):
        mt = max((os.path.getmtime(os.path.join(r, f)) for r, _d, fs in os.walk(p) for f in fs), default=0)
    elif os.path.isfile(p):
        mt = os.path.getmtime(p)
    else:
        continue
    if now - mt < 300:
        failures.append("%s modified %.0fs ago (<300s)" % (t, now - mt))

if failures:
    print("REFUSE cleanup — 前置判据不满足：")
    for f_ in failures:
        print("  -", f_)
    sys.exit(2)

removed, missing = [], []
for t in TARGETS:
    p = os.path.join(BASE, t)
    if os.path.isdir(p):
        shutil.rmtree(p)
        removed.append(t)
    elif os.path.isfile(p):
        os.remove(p)
        removed.append(t)
    else:
        missing.append(t)

# 同目录 scratch shapefile（仅 OUT_* 前缀）
for fn in os.listdir(BASE):
    if fn.startswith("OUT_"):
        p = os.path.join(BASE, fn)
        if os.path.isdir(p):
            shutil.rmtree(p)
        else:
            os.remove(p)
        removed.append(fn)

print("removed:", removed or "none")
print("missing (already absent):", missing or "none")
left = os.listdir(BASE)
print("remaining in %s: %s" % (BASE, left or "<empty>"))
if not left:
    os.rmdir(BASE)
    print("removed empty dir:", BASE)
