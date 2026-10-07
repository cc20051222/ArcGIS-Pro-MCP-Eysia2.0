#!/usr/bin/env python3
"""Freeze or verify the D-089 M2 benchmark definition candidate.

Mechanism reused from tools/benchmark/freeze-benchmark-s0.py (D-085, G-206 accepted):
aggregate = sha256(concat('relpath:sha256\\n' for every member file, sorted by relpath)).

This script is additive by design. It never reads-modifies-writes any M1 member file; it only
re-reads the frozen M1 record to carry its anchor forward, so the M2 anchor itself proves that
the M1 face was unchanged at freeze time.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import sys


ITEMS = (
    ("m2Overview", "F05 M2 overview and freeze policy", ("Benchmarks/m2-overview-v1.md",)),
    (
        "m2Scenarios",
        "M2 twenty-two scenarios",
        ("Benchmarks/m2-scenarios-22.jsonl", "Benchmarks/m2-scenarios-22.md"),
    ),
    (
        "m2Templates",
        "M2 sixteen templates",
        ("Benchmarks/m2-templates-16.jsonl", "Benchmarks/m2-templates-16.md"),
    ),
    (
        "m2ScenarioTemplateMap",
        "M2 scenario-template-criterion mapping",
        ("Benchmarks/m2-scenario-template-map-v1.jsonl", "Benchmarks/m2-scenario-template-map-v1.md"),
    ),
    (
        "comparisonRulesV2",
        "Comparison rules v2 (additive over v1)",
        ("Benchmarks/comparison-rules-v2.json", "Benchmarks/comparison-rules-v2.md"),
    ),
    ("m2CoreHiddenSplit", "M2 core and hidden split", ("Benchmarks/m2-core-hidden-split-v1.json",)),
    (
        "stressM2Subset",
        "M2 subset of the nominal 40 stress cases",
        ("Benchmarks/stress-40-m2-subset.jsonl",),
    ),
    (
        "z09Z11Precalibration",
        "Z09-Z11 pre-calibration criteria",
        ("Benchmarks/z09-z11-precalibration-v1.json", "Benchmarks/z09-z11-precalibration-v1.md"),
    ),
)

M1_RECORD = "Benchmarks/benchmark-s0.json"


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


def member_path(root: Path, relative: str) -> Path:
    candidate = (root / Path(relative)).resolve(strict=True)
    if os.path.commonpath((str(root), str(candidate))) != str(root):
        raise RuntimeError(f"Input escaped workspace: {relative}")
    if not candidate.is_file():
        raise RuntimeError(f"Required benchmark input is not a file: {relative}")
    return candidate


def carried_m1_anchor(root: Path) -> dict:
    """Re-hash the frozen M1 members straight from the M1 record; report, never modify."""
    record_path = member_path(root, M1_RECORD)
    record = json.loads(record_path.read_bytes().decode("utf-8"))
    mismatches = []
    for entry in record["files"]:
        data = member_path(root, entry["path"]).read_bytes()
        if sha256_bytes(data) != entry["sha256"] or len(data) != entry["sizeBytes"]:
            mismatches.append(entry["path"])
    recomputed = canonical_aggregate([(e["path"], e["sha256"]) for e in record["files"]])
    return {
        "recordFile": M1_RECORD,
        "aggregateMethod": record["aggregateMethod"],
        "recordedAggregate": record["aggregateSha256"],
        "recomputedAggregate": recomputed,
        "memberFileCount": record["memberFileCount"],
        "logicalItemCount": record["logicalItemCount"],
        "memberMismatchCount": len(mismatches),
        "memberMismatches": mismatches,
        "statusWhenCarried": "FROZEN_BY_GATE-G206",
    }


def build_record(root: Path) -> tuple[dict, str]:
    m1 = carried_m1_anchor(root)
    if m1["memberMismatchCount"] or m1["recomputedAggregate"] != m1["recordedAggregate"]:
        raise RuntimeError(f"M1 frozen face does not recompute: {m1}")

    files: list[dict] = []
    logical_items: list[dict] = []

    for item_id, title, members in ITEMS:
        member_records = []
        for relative in members:
            data = member_path(root, relative).read_bytes()
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
        "schema": "f05-m2-benchmark-s0-v1",
        "status": "PASS_CANDIDATE",
        "batch": "D-089",
        "extensionOf": "f05-m1-benchmark-s0-v1 (D-085, frozen by GATE-G206)",
        "aggregateMethod": "sha256(concat('relpath:sha256\\n' for every M2 member file, sorted by relpath))",
        "logicalItemCount": len(logical_items),
        "memberFileCount": len(files),
        "aggregateSha256": aggregate,
        "carriedM1Anchor": m1,
        "logicalItems": logical_items,
        "files": files,
    }

    lines = [
        "F05 M2 benchmark S0 · PASS CANDIDATE (D-089, additive over the frozen M1 face)",
        "",
        f"Logical M2 items: {len(logical_items)}",
        f"Member files: {len(files)}",
        f"Aggregate SHA-256: {aggregate}",
        "Aggregate method: sha256(concat('relpath:sha256\\n' for every M2 member file, sorted by relpath))",
        f"Carried M1 anchor: {m1['recordedAggregate']} ({m1['memberFileCount']} files, recomputed equal, mismatches {m1['memberMismatchCount']})",
        "",
    ]
    for item in logical_items:
        lines.append(f"[{item['itemId']}] {item['title']} · {item['sha256']}")
        for member in item["members"]:
            lines.append(f"  {member['path']} · {member['sizeBytes']} bytes · {member['sha256']}")
    lines.extend(
        [
            "",
            "This record freezes definitions for reproducibility only; it is not runtime, visual, or benchmark acceptance evidence,",
            "and it is not acceptance of the D-089 candidate package.",
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
    json_path = root / "Benchmarks" / "benchmark-m2-s0.json"
    text_path = root / "Benchmarks" / "benchmark-m2-s0.txt"
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
            raise RuntimeError("M2 S0 output pair is incomplete; --check does not create files")
        if json_path.read_bytes() != json_bytes or text_path.read_bytes() != text_bytes:
            raise RuntimeError("M2 S0 outputs differ from the independently recomputed candidate")
        print(f"CHECK PASS CANDIDATE: {record['aggregateSha256']} ({record['memberFileCount']} files)")
        print(f"CARRIED M1 ANCHOR OK: {record['carriedM1Anchor']['recordedAggregate']} ({record['carriedM1Anchor']['memberFileCount']} files)")
        return 0

    if args.refresh_candidate:
        write_atomic(json_path, json_bytes)
        write_atomic(text_path, text_bytes)
    elif any(present):
        if not all(present):
            raise RuntimeError("M2 S0 output pair is incomplete; use --refresh-candidate after review")
        if json_path.read_bytes() != json_bytes or text_path.read_bytes() != text_bytes:
            raise RuntimeError("Existing M2 S0 differs; use --refresh-candidate to make an explicit candidate refresh")
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
