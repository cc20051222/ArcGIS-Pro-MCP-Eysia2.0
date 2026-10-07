#!/usr/bin/env python3
"""Freeze or verify the D-093 M3 benchmark definition candidate.

Mechanism reused from tools/benchmark/freeze-benchmark-s0.py (D-085, G-206 accepted) and
tools/benchmark/freeze-benchmark-m2.py (D-089, G-214 accepted):
aggregate = sha256(concat('relpath:sha256\\n' for every member file, sorted by relpath)).

Additive by design. The two frozen faces are only re-read: their member files are re-hashed and their
anchors recomputed, so the M3 anchor itself proves M1 and M2 were untouched at freeze time.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import sys


ITEMS = (
    ("m3Overview", "F05 M3 overview and freeze policy", ("Benchmarks/m3-overview-v1.md",)),
    (
        "m3Scenarios",
        "M3 five base scenarios closing the base thirty",
        ("Benchmarks/m3-scenarios-5.jsonl", "Benchmarks/m3-scenarios-5.md"),
    ),
    (
        "m3ScenarioTemplateMap",
        "M3 scenario-template-criterion mapping",
        ("Benchmarks/m3-scenario-template-map-v1.jsonl", "Benchmarks/m3-scenario-template-map-v1.md"),
    ),
    (
        "m3CapabilityUnlock",
        "M3 unlock surface over the M2 qualified-input references",
        ("Benchmarks/m3-capability-unlock-v1.jsonl",),
    ),
    (
        "comparisonRulesV3",
        "Comparison rules v3 (additive over v2 and v1)",
        ("Benchmarks/comparison-rules-v3.json", "Benchmarks/comparison-rules-v3.md"),
    ),
    ("m3CoreHiddenSplit", "M3 core and hidden split", ("Benchmarks/m3-core-hidden-split-v1.json",)),
    (
        "stressM3Subset",
        "M3 subset of the nominal 40 stress cases",
        ("Benchmarks/stress-40-m3-subset.jsonl",),
    ),
    (
        "z12Precalibration",
        "Z12 continuous design acceptance criteria",
        ("Benchmarks/z12-precalibration-v1.json", "Benchmarks/z12-precalibration-v1.md"),
    ),
)

CARRIED_RECORDS = (
    ("M1", "Benchmarks/benchmark-s0.json", "GATE-G206"),
    ("M2", "Benchmarks/benchmark-m2-s0.json", "GATE-G214"),
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


def member_path(root: Path, relative: str) -> Path:
    candidate = (root / Path(relative)).resolve(strict=True)
    if os.path.commonpath((str(root), str(candidate))) != str(root):
        raise RuntimeError(f"Input escaped workspace: {relative}")
    if not candidate.is_file():
        raise RuntimeError(f"Required benchmark input is not a file: {relative}")
    return candidate


def carried_anchor(root: Path, face: str, record_relative: str, ruling: str) -> dict:
    """Re-hash a frozen face straight from its own record; report, never modify."""
    record = json.loads(member_path(root, record_relative).read_bytes().decode("utf-8"))
    mismatches = []
    for entry in record["files"]:
        data = member_path(root, entry["path"]).read_bytes()
        if sha256_bytes(data) != entry["sha256"] or len(data) != entry["sizeBytes"]:
            mismatches.append(entry["path"])
    recomputed = canonical_aggregate([(e["path"], e["sha256"]) for e in record["files"]])
    return {
        "face": face,
        "recordFile": record_relative,
        "frozenBy": ruling,
        "aggregateMethod": record["aggregateMethod"],
        "recordedAggregate": record["aggregateSha256"],
        "recomputedAggregate": recomputed,
        "memberFileCount": record["memberFileCount"],
        "logicalItemCount": record["logicalItemCount"],
        "memberMismatchCount": len(mismatches),
        "memberMismatches": mismatches,
    }


def build_record(root: Path) -> tuple[dict, str]:
    carried = [carried_anchor(root, face, rec, ruling) for face, rec, ruling in CARRIED_RECORDS]
    for item in carried:
        if item["memberMismatchCount"] or item["recomputedAggregate"] != item["recordedAggregate"]:
            raise RuntimeError(f"{item['face']} frozen face does not recompute: {item}")

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
        "schema": "f05-m3-benchmark-s0-v1",
        "status": "PASS_CANDIDATE",
        "batch": "D-093",
        "extensionOf": "f05-m2-benchmark-s0-v1 (D-089) over f05-m1-benchmark-s0-v1 (D-085)",
        "aggregateMethod": "sha256(concat('relpath:sha256\\n' for every M3 member file, sorted by relpath))",
        "logicalItemCount": len(logical_items),
        "memberFileCount": len(files),
        "aggregateSha256": aggregate,
        "carriedAnchors": carried,
        "logicalItems": logical_items,
        "files": files,
    }

    lines = [
        "F05 M3 benchmark S0 · PASS CANDIDATE (D-093, additive over the frozen M1 and M2 faces)",
        "",
        f"Logical M3 items: {len(logical_items)}",
        f"Member files: {len(files)}",
        f"Aggregate SHA-256: {aggregate}",
        "Aggregate method: sha256(concat('relpath:sha256\\n' for every M3 member file, sorted by relpath))",
    ]
    for item in carried:
        lines.append(
            f"Carried {item['face']} anchor: {item['recordedAggregate']} "
            f"({item['memberFileCount']} files, recomputed equal, mismatches {item['memberMismatchCount']}, {item['frozenBy']})"
        )
    lines.append("")
    for item in logical_items:
        lines.append(f"[{item['itemId']}] {item['title']} · {item['sha256']}")
        for member in item["members"]:
            lines.append(f"  {member['path']} · {member['sizeBytes']} bytes · {member['sha256']}")
    lines.extend(
        [
            "",
            "This record freezes definitions for reproducibility only; it is not runtime, visual, or benchmark acceptance",
            "evidence, and it is not acceptance of the D-093 candidate package.",
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
    json_path = root / "Benchmarks" / "benchmark-m3-s0.json"
    text_path = root / "Benchmarks" / "benchmark-m3-s0.txt"
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
            raise RuntimeError("M3 S0 output pair is incomplete; --check does not create files")
        if json_path.read_bytes() != json_bytes or text_path.read_bytes() != text_bytes:
            raise RuntimeError("M3 S0 outputs differ from the independently recomputed candidate")
        print(f"CHECK PASS CANDIDATE: {record['aggregateSha256']} ({record['memberFileCount']} files)")
        for item in record["carriedAnchors"]:
            print(f"CARRIED {item['face']} ANCHOR OK: {item['recordedAggregate']} ({item['memberFileCount']} files)")
        return 0

    if args.refresh_candidate:
        write_atomic(json_path, json_bytes)
        write_atomic(text_path, text_bytes)
    elif any(present):
        if not all(present):
            raise RuntimeError("M3 S0 output pair is incomplete; use --refresh-candidate after review")
        if json_path.read_bytes() != json_bytes or text_path.read_bytes() != text_bytes:
            raise RuntimeError("Existing M3 S0 differs; use --refresh-candidate to make an explicit candidate refresh")
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
