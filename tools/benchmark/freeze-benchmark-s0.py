#!/usr/bin/env python3
"""Freeze or verify the D-085 M1 benchmark definition candidate."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import sys


ITEMS = (
    ("overview", "F05 M1 overview", ("Benchmarks/README.md",)),
    (
        "m1Scenarios",
        "M1 six scenarios",
        ("Benchmarks/m1-scenarios-6.jsonl", "Benchmarks/m1-scenarios-6.md"),
    ),
    (
        "m1Templates",
        "M1 six templates",
        ("Benchmarks/m1-templates-6.jsonl", "Benchmarks/m1-templates-6.md"),
    ),
    (
        "comparisonRules",
        "Comparison rules v1",
        ("Benchmarks/comparison-rules-v1.json", "Benchmarks/comparison-rules-v1.md"),
    ),
    (
        "coreHiddenSplit",
        "Core and hidden split v1",
        ("Benchmarks/core-hidden-split-v1.json",),
    ),
    (
        "stressM1Subset",
        "M1 subset of the nominal 40 stress cases",
        ("Benchmarks/stress-40-m1-subset.jsonl",),
    ),
)


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def canonical_aggregate(entries: list[tuple[str, str]]) -> str:
    payload = "".join(f"{path}:{digest}\n" for path, digest in sorted(entries))
    return sha256_bytes(payload.encode("utf-8"))


def ensure_d_drive(root: Path) -> None:
    resolved = root.resolve(strict=True)
    if os.name != "nt" or resolved.drive.upper() != "D:":
        raise RuntimeError(f"Workspace must resolve on D:; found {resolved}")
    if resolved.name.casefold() != "arcgis-pro-mcp 2.0":
        raise RuntimeError(f"Unexpected workspace root: {resolved}")


def build_record(root: Path) -> tuple[dict, str]:
    files: list[dict] = []
    logical_items: list[dict] = []

    for item_id, title, members in ITEMS:
        member_records = []
        for relative in members:
            candidate = (root / Path(relative)).resolve(strict=True)
            if os.path.commonpath((str(root), str(candidate))) != str(root):
                raise RuntimeError(f"Input escaped workspace: {relative}")
            if not candidate.is_file():
                raise RuntimeError(f"Required benchmark input is not a file: {relative}")
            data = candidate.read_bytes()
            entry = {
                "path": relative.replace("\\", "/"),
                "sizeBytes": len(data),
                "sha256": sha256_bytes(data),
            }
            member_records.append(entry)
            files.append(entry)

        item_entries = [(entry["path"], entry["sha256"]) for entry in member_records]
        logical_items.append(
            {
                "itemId": item_id,
                "title": title,
                "sha256": canonical_aggregate(item_entries),
                "members": member_records,
            }
        )

    aggregate = canonical_aggregate([(entry["path"], entry["sha256"]) for entry in files])
    record = {
        "schema": "f05-m1-benchmark-s0-v1",
        "status": "PASS_CANDIDATE",
        "aggregateMethod": "sha256(concat('relpath:sha256\\n' for every A-group member file, sorted by relpath))",
        "logicalItemCount": len(logical_items),
        "memberFileCount": len(files),
        "aggregateSha256": aggregate,
        "logicalItems": logical_items,
        "files": files,
    }

    lines = [
        "F05 M1 benchmark S0 · PASS CANDIDATE",
        "",
        f"Logical A-group items: {len(logical_items)}",
        f"Member files: {len(files)}",
        f"Aggregate SHA-256: {aggregate}",
        "Aggregate method: sha256(concat('relpath:sha256\\n' for every A-group member file, sorted by relpath))",
        "",
    ]
    for item in logical_items:
        lines.append(f"[{item['itemId']}] {item['title']} · {item['sha256']}")
        for member in item["members"]:
            lines.append(f"  {member['path']} · {member['sizeBytes']} bytes · {member['sha256']}")
    lines.extend(
        [
            "",
            "This record freezes definitions for reproducibility only; it is not runtime, visual, or benchmark acceptance evidence.",
            "",
        ]
    )
    return record, "\n".join(lines)


def write_atomic(path: Path, content: bytes) -> None:
    temporary = path.with_name(path.name + ".tmp")
    created = False
    try:
        stream = temporary.open("xb")
        created = True
        with stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        temporary.replace(path)
    finally:
        if created and temporary.exists():
            temporary.unlink()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="recompute and compare without writing")
    mode.add_argument(
        "--refresh-candidate",
        action="store_true",
        help="explicitly replace the PASS CANDIDATE S0 files after definition changes",
    )
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]
    ensure_d_drive(root)
    json_path = root / "Benchmarks" / "benchmark-s0.json"
    text_path = root / "Benchmarks" / "benchmark-s0.txt"
    for output in (json_path, text_path):
        if os.path.commonpath((str(root), str(output.resolve(strict=False)))) != str(root):
            raise RuntimeError(f"Output escaped workspace: {output}")
        if output.resolve(strict=False).drive.upper() != "D:":
            raise RuntimeError(f"Output must remain on D: {output}")

    record, text_record = build_record(root)
    json_bytes = (json.dumps(record, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    text_bytes = text_record.encode("utf-8")
    present = (json_path.exists(), text_path.exists())

    if args.check:
        if not all(present):
            raise RuntimeError("S0 output pair is incomplete; --check does not create files")
        if json_path.read_bytes() != json_bytes or text_path.read_bytes() != text_bytes:
            raise RuntimeError("S0 outputs differ from the independently recomputed candidate")
        print(f"CHECK PASS CANDIDATE: {record['aggregateSha256']} ({record['memberFileCount']} files)")
        return 0

    if args.refresh_candidate:
        write_atomic(json_path, json_bytes)
        write_atomic(text_path, text_bytes)
    elif any(present):
        if not all(present):
            raise RuntimeError("S0 output pair is incomplete; use --refresh-candidate after review")
        if json_path.read_bytes() != json_bytes or text_path.read_bytes() != text_bytes:
            raise RuntimeError("Existing S0 differs; use --refresh-candidate to make an explicit candidate refresh")
    else:
        write_atomic(json_path, json_bytes)
        write_atomic(text_path, text_bytes)

    print(f"FROZEN PASS CANDIDATE: {record['aggregateSha256']} ({record['memberFileCount']} files)")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, RuntimeError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(2)
