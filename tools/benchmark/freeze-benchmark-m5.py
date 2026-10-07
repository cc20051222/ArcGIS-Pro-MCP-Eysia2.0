#!/usr/bin/env python3
"""Freeze or verify the D-122 M5 benchmark definition candidate (G-296 R-2 ruling = option A).

Mechanism reused verbatim from tools/benchmark/freeze-benchmark-s0.py (D-085, G-206 accepted),
freeze-benchmark-m2.py (D-089, G-214), freeze-benchmark-m3.py (D-093, G-216) and
freeze-benchmark-m4.py (D-097, G-220):
aggregate = sha256(concat('relpath:sha256\\n' for every member file, sorted by relpath)).

M5 anchors exactly the eight faces G-296 R-2 names as scheme A: the four generations of the D-117
GP-whitelist-53 closure ledger (`.jsonl` + `.md` for v1..v4). Nothing else joins the anchor, and the
four older contract surfaces are only re-read — every member of M1..M4 is re-hashed and its anchor
recomputed here, so this record itself proves the older faces were byte-unchanged at freeze time.

The four precedent guards are kept: D-drive workspace enforced, workspace root name checked, any path
escaping the workspace refused, and a silent rewrite of an existing record refused unless
--refresh-candidate is given explicitly.

A fifth guard is added because G-296 R-2 lists it as an exclusion that would otherwise constitute an
upgrade: the frozen faces carry status *strings*, and the behavioural column of the underlying ledger
has zero empirically-passing entries — so this script refuses to write any record whose text asserts
that tool behaviour or admission has been demonstrated.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys


ITEMS = (
    ("closureV1", "D-117 Phase A GP-53 closure ledger and coverage ledger, first generation",
     ("Benchmarks/gp-whitelist-closure-53-v1.jsonl", "Benchmarks/gp-whitelist-closure-53-v1.md")),
    ("closureV2", "D-117 fixture segment rectification, second generation of the same two ledgers",
     ("Benchmarks/gp-whitelist-closure-53-v2.jsonl", "Benchmarks/gp-whitelist-closure-53-v2.md")),
    ("closureV3", "D-117 r2 rebuild, third generation with per-parameter carrier pins",
     ("Benchmarks/gp-whitelist-closure-53-v3.jsonl", "Benchmarks/gp-whitelist-closure-53-v3.md")),
    ("closureV4", "D-117 close-out, fourth generation carrying the four-check columns",
     ("Benchmarks/gp-whitelist-closure-53-v4.jsonl", "Benchmarks/gp-whitelist-closure-53-v4.md")),
)

CARRIED_RECORDS = (
    ("M1", "Benchmarks/benchmark-s0.json", "GATE-G206"),
    ("M2", "Benchmarks/benchmark-m2-s0.json", "GATE-G214"),
    ("M3", "Benchmarks/benchmark-m3-s0.json", "GATE-G216"),
    ("M4", "Benchmarks/benchmark-m4-s0.json", "GATE-G220"),
)

# G-296 R-2 exclusion: an M5 record may pin status strings on file, never a claim that behaviour or
# admission was demonstrated. Serialised records containing any of these are refused before writing.
BANNED_CLAIMS = re.compile(
    r"(?i)(verified|validation[- ]?passed|admission[- ]?passed|behaviou?r[- ]?(ok|pass|proved)|"
    r"行为已验证|行为验证通过|准入已通过|准入通过|已验证通过)"
)

SCHEMA_KEYS = ("schema", "status", "batch", "extensionOf", "aggregateMethod", "logicalItemCount",
               "memberFileCount", "aggregateSha256", "carriedAnchors", "logicalItems", "files")


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


def assert_no_behaviour_claim(record: dict) -> None:
    """Refuse a record that would read as behavioural or admission evidence, or that drifts off schema."""
    if tuple(record.keys()) != SCHEMA_KEYS:
        raise RuntimeError(f"M5 record drifted off the carried schema: {list(record.keys())}")
    for member in record["files"]:
        if tuple(member.keys()) != ("path", "sizeBytes", "sha256"):
            raise RuntimeError(f"M5 member entry drifted off schema: {member}")
    hits = sorted({m.group(0) for m in BANNED_CLAIMS.finditer(json.dumps(record, ensure_ascii=False))})
    if hits:
        raise RuntimeError(f"M5 record would assert a claim the anchor may not make: {hits}")


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

    if sorted(e["path"] for e in files) != sorted(m for _, _, ms in ITEMS for m in ms):
        raise RuntimeError("M5 member set differs from the eight faces G-296 R-2 names as scheme A")

    aggregate = canonical_aggregate([(entry["path"], entry["sha256"]) for entry in files])
    record = {
        "schema": "f05-m5-benchmark-s0-v1",
        "status": "PASS_CANDIDATE",
        "batch": "D-122",
        "extensionOf": ("f05-m4-benchmark-s0-v1 (D-097) over f05-m3 (D-093) over f05-m2 (D-089) "
                        "over f05-m1 (D-085); M5 is a new axis, not an extension of the M4 face"),
        "aggregateMethod": "sha256(concat('relpath:sha256\\n' for every M5 member file, sorted by relpath))",
        "logicalItemCount": len(logical_items),
        "memberFileCount": len(files),
        "aggregateSha256": aggregate,
        "carriedAnchors": carried,
        "logicalItems": logical_items,
        "files": files,
    }
    assert_no_behaviour_claim(record)

    lines = [
        "F05 M5 benchmark S0 · PASS CANDIDATE (D-122, the D-117 GP-53 closure definition face, scheme A)",
        "",
        f"Logical M5 items: {len(logical_items)}",
        f"Member files: {len(files)}",
        f"Aggregate SHA-256: {aggregate}",
        "Aggregate method: sha256(concat('relpath:sha256\\n' for every M5 member file, sorted by relpath))",
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
            "This record freezes definitions for reproducibility only. It pins the bytes and status strings of",
            "the four generations of the D-117 closure ledger; it is not runtime, visual or benchmark acceptance",
            "evidence, it is not acceptance of the D-122 candidate, it says nothing about whether any geoprocessing",
            "tool behaves correctly or has been admitted (the close-out ledger records that column as not",
            "established, with zero entries of its own claiming otherwise), and it proves the M1, M2, M3 and M4",
            "faces were byte-unchanged at freeze time.",
            "",
        ]
    )
    text_record = "\n".join(lines)
    BANNED_CLAIMS.search(text_record) and (_ for _ in ()).throw(
        RuntimeError("M5 text record would assert a claim the anchor may not make"))
    return record, text_record


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
    json_path = root / "Benchmarks" / "benchmark-m5-s0.json"
    text_path = root / "Benchmarks" / "benchmark-m5-s0.txt"
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
            raise RuntimeError("M5 S0 output pair is incomplete; --check does not create files")
        if json_path.read_bytes() != json_bytes or text_path.read_bytes() != text_bytes:
            raise RuntimeError("M5 S0 outputs differ from the independently recomputed candidate")
        print(f"CHECK PASS CANDIDATE: {record['aggregateSha256']} ({record['memberFileCount']} files)")
        for item in record["carriedAnchors"]:
            print(f"CARRIED {item['face']} ANCHOR OK: {item['recordedAggregate']} ({item['memberFileCount']} files)")
        return 0

    if args.refresh_candidate:
        write_atomic(json_path, json_bytes)
        write_atomic(text_path, text_bytes)
    elif any(present):
        if not all(present):
            raise RuntimeError("M5 S0 output pair is incomplete; use --refresh-candidate after review")
        if json_path.read_bytes() != json_bytes or text_path.read_bytes() != text_bytes:
            raise RuntimeError("Existing M5 S0 differs; use --refresh-candidate to make an explicit candidate refresh")
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
