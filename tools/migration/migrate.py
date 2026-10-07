#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
D-001 迁移准备 Gate：白名单逐文件复制 + SHA256 前后核验 + migration-manifest.json

硬约束（来自派工单 D-001 第 3 节）：
  * 旧仓库只读，绝不写入/删除/修改旧仓库任何文件。
  * 排除 bin/obj/.git/.runtime/.codex/.codex-artifacts/.cursor/.workbuddy/
    TestDate/tmp/__pycache__ 以及 GDB/APRX/锁/历史产物。
  * 不跟随 reparse point（junction/symlink），遇到即跳过并登记。
  * 目标文件已存在时绝不覆盖，登记为 CONFLICT 并计入失败条件。
  * Release 仅复制固定 r5 ZIP + .sha256 + 发布 manifest。
"""
import hashlib
import json
import os
import shutil
import sys
from datetime import datetime, timezone

SRC = r"D:\ArcGIS-Pro-MCP"
DST = r"D:\ArcGIS-Pro-MCP 2.0"

# ---- 白名单：顶层条目 -------------------------------------------------------
INCLUDE_TOP = [
    "Source",
    "Tests",
    "scripts",
    "Config",
    "Docs",
    "ArcGIS-Pro-MCP.sln",
    "Directory.Build.props",
    "README.md",
    ".gitignore",
]

# ---- Release 仅复制固定产物 -------------------------------------------------
RELEASE_ONLY = [
    r"Release\ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip",
    r"Release\ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64.zip.sha256",
    r"Release\ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64\bundle-manifest.json",
    r"Release\ArcGIS-Pro-MCP-OneClick-1.0.2-r5-Windows-x64\payload"
    r"\ArcGISProMCP.Compatibility.release-manifest.json",
]

# ---- 排除目录名（任意层级） -------------------------------------------------
EXCLUDE_DIR_NAMES = {
    "bin", "obj", ".git", ".runtime", ".codex", ".codex-artifacts",
    ".cursor", ".workbuddy", "TestDate", "tmp", "__pycache__", ".vs",
}

# ---- 排除文件后缀 -----------------------------------------------------------
EXCLUDE_EXT = {".lock", ".pyc", ".pyo"}

# ---- 排除文件名片段（历史产物/副本） ---------------------------------------
EXCLUDE_NAME_PARTS = [" - 副本", " - Copy"]


def sha256(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def is_reparse(path: str) -> bool:
    try:
        st = os.lstat(path)
    except OSError:
        return False
    # Windows: FILE_ATTRIBUTE_REPARSE_POINT = 0x400
    return bool(getattr(st, "st_file_attributes", 0) & 0x400)


def excluded(name: str, is_dir: bool) -> bool:
    if is_dir and name in EXCLUDE_DIR_NAMES:
        return True
    if not is_dir:
        ext = os.path.splitext(name)[1].lower()
        if ext in EXCLUDE_EXT:
            return True
        for part in EXCLUDE_NAME_PARTS:
            if part in name:
                return True
    return False


def collect_files(root: str) -> list:
    """按白名单收集相对路径。返回 (rel, abs) 列表。"""
    out = []
    for top in INCLUDE_TOP:
        base = os.path.join(root, top)
        if os.path.isfile(base):
            out.append((top, base))
            continue
        if not os.path.isdir(base):
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            # 剪枝排除目录（原地修改 dirnames）
            dirnames[:] = [
                d for d in dirnames
                if d not in EXCLUDE_DIR_NAMES and not is_reparse(os.path.join(dirpath, d))
            ]
            for fn in filenames:
                if excluded(fn, is_dir=False):
                    continue
                abs_p = os.path.join(dirpath, fn)
                rel = os.path.relpath(abs_p, root)
                out.append((rel, abs_p))
    # Release 固定产物
    for rel in RELEASE_ONLY:
        abs_p = os.path.join(root, rel)
        if os.path.isfile(abs_p):
            out.append((rel, abs_p))
    return out


def main() -> int:
    if not os.path.isdir(SRC):
        print("FATAL: source not found: %s" % SRC)
        return 2
    os.makedirs(DST, exist_ok=True)

    entries = collect_files(SRC)
    entries.sort(key=lambda x: x[0].lower())

    manifest = {
        "schema": "arcgis-pro-mcp-migration-manifest-v1",
        "dispatch_id": "D-001",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "source_root": SRC,
        "destination_root": DST,
        "source_git_commits": 0,
        "source_git_tracked_files": 0,
        "excluded_dir_names": sorted(EXCLUDE_DIR_NAMES),
        "excluded_extensions": sorted(EXCLUDE_EXT),
        "summary": {},
        "files": [],
    }

    counts = {"copied": 0, "conflict": 0, "reparse_skipped": 0, "hash_mismatch": 0, "error": 0}
    skipped_reparse = []
    conflicts = []

    for rel, src_abs in entries:
        dst_abs = os.path.join(DST, rel)
        if is_reparse(src_abs):
            counts["reparse_skipped"] += 1
            skipped_reparse.append(rel)
            continue

        rec = {
            "path": rel.replace("\\", "/"),
            "source": src_abs,
            "size": os.path.getsize(src_abs),
        }
        try:
            rec["sha256_source"] = sha256(src_abs)
        except OSError as e:
            counts["error"] += 1
            rec["status"] = "ERROR"
            rec["error"] = str(e)
            manifest["files"].append(rec)
            continue

        if os.path.exists(dst_abs):
            counts["conflict"] += 1
            rec["status"] = "CONFLICT_NOT_OVERWRITTEN"
            rec["sha256_destination"] = sha256(dst_abs)
            conflicts.append(rel)
            manifest["files"].append(rec)
            continue

        os.makedirs(os.path.dirname(dst_abs), exist_ok=True)
        shutil.copy2(src_abs, dst_abs)
        rec["sha256_destination"] = sha256(dst_abs)

        if rec["sha256_source"] == rec["sha256_destination"]:
            rec["status"] = "OK"
            counts["copied"] += 1
        else:
            rec["status"] = "HASH_MISMATCH"
            counts["hash_mismatch"] += 1

        manifest["files"].append(rec)

    manifest["summary"] = {
        "total_collected": len(entries),
        "copied": counts["copied"],
        "conflict_not_overwritten": counts["conflict"],
        "reparse_skipped": counts["reparse_skipped"],
        "hash_mismatch": counts["hash_mismatch"],
        "error": counts["error"],
        "post_check_new_tree_bad_dirs": sorted(
            d for d in EXCLUDE_DIR_NAMES
            if any(
                os.path.isdir(os.path.join(dp, d))
                for dp, dn, fn in os.walk(DST)
            )
        ),
    }
    manifest["skipped_reparse_points"] = skipped_reparse
    manifest["conflicts"] = conflicts

    out_path = os.path.join(DST, "migration-manifest.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print("manifest: %s" % out_path)
    print(json.dumps(manifest["summary"], ensure_ascii=False, indent=2))
    if conflicts:
        print("CONFLICTS:")
        for c in conflicts:
            print("  " + c)
    if skipped_reparse:
        print("REPARSE SKIPPED:")
        for c in skipped_reparse:
            print("  " + c)

    ok = counts["conflict"] == 0 and counts["hash_mismatch"] == 0 and counts["error"] == 0
    return 0 if ok else 3


if __name__ == "__main__":
    sys.exit(main())
