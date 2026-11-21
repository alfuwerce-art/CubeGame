#!/usr/bin/env python3
"""Build a machine-readable registry from the M0-00B gameplay coverage matrix.

The matrix remains the evidence document; this tool only extracts ids, confidence,
implementation status and the M0 substage so the preparation package can be checked
mechanically instead of by hand.

Usage:
    python tools/m0_transfer/build_transfer_registry.py
    python tools/m0_transfer/build_transfer_registry.py --check
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import Counter, OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MATRIX = ROOT / "docs" / "research" / "alpha2013" / "M0_00B_ALPHA_GAMEPLAY_COVERAGE_MATRIX.md"
OUT = ROOT / "tools" / "m0_transfer" / "registry" / "transfer_registry.json"

ROW = re.compile(r"^\|\s*([A-Z]{1,3}-\d+)\s*\|")
STATUSES = {
    "IMPLEMENTED_CORE",
    "IMPLEMENTED_PILOT",
    "TEST_ONLY",
    "PARTIAL",
    "MISSING",
    "N/A",
}
CONFIDENCE = {"DIRECT", "PARTIAL", "UNKNOWN", "BLOCKED", "DEFERRED_TO_M0"}

SYSTEMS = [
    "world",
    "movement",
    "combat",
    "npc",
    "items",
    "economy_quests",
    "progression_persistence",
    "content_ui",
]


class MatrixError(Exception):
    pass


def clean(cell: str) -> str:
    return cell.replace("`", "").strip()


def domain_for(mechanic_id: str) -> str:
    prefix, number_text = mechanic_id.split("-")
    number = int(number_text)

    if prefix == "W":
        return "world"
    if prefix == "P":
        if 9 <= number <= 15:
            return "progression_persistence"
        return "movement"
    if prefix == "C":
        return "npc" if mechanic_id in {"C-23", "C-24"} else "combat"
    if prefix == "N":
        return "npc"
    if prefix == "I":
        return "economy_quests" if number in {16, 17} else "items"
    if prefix == "AI":
        if 11 <= number <= 15:
            return "combat"
        return "items"
    if prefix == "Q":
        if number in {11, 12, 13, 14, 22}:
            return "progression_persistence"
        if 16 <= number <= 21 or number == 26:
            return "economy_quests"
        return "content_ui"
    raise MatrixError(f"unmapped mechanic prefix: {mechanic_id}")


def parse() -> list[dict]:
    if not MATRIX.exists():
        raise MatrixError(f"matrix not found: {MATRIX}")

    entries: list[dict] = []
    seen: set[str] = set()
    for line_number, line in enumerate(MATRIX.read_text(encoding="utf-8").splitlines(), start=1):
        match = ROW.match(line)
        if not match:
            continue

        mechanic_id = match.group(1)
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) != 9:
            raise MatrixError(f"line {line_number}: expected 9 columns for {mechanic_id}, got {len(cells)}")
        if mechanic_id in seen:
            raise MatrixError(f"line {line_number}: duplicate mechanic id {mechanic_id}")
        seen.add(mechanic_id)

        confidence_cell = clean(cells[2])
        confidence = confidence_cell.split()[0] if confidence_cell else ""
        if confidence not in CONFIDENCE:
            for candidate in ("DIRECT", "PARTIAL", "UNKNOWN", "BLOCKED", "DEFERRED_TO_M0"):
                if candidate in confidence_cell:
                    confidence = candidate
                    break
        if confidence not in CONFIDENCE:
            # Some rows carry a prose note instead of an evidence token (e.g. "no specification").
            # Keep the registry buildable but mark the row as UNKNOWN rather than guessing a level.
            confidence = "UNKNOWN"

        status = clean(cells[4]).split()[0] if clean(cells[4]) else ""
        if status not in STATUSES:
            raise MatrixError(f"line {line_number}: unknown status {cells[4]!r} for {mechanic_id}")

        substage = clean(cells[6]).split()[0] if clean(cells[6]) else ""
        if substage in {"—", "-"}:
            substage = "N/A"
        if not substage.startswith("M0-") and substage != "N/A":
            raise MatrixError(f"line {line_number}: missing M0 substage for {mechanic_id}: {cells[6]!r}")

        entries.append(
            OrderedDict(
                [
                    ("id", mechanic_id),
                    ("domain", domain_for(mechanic_id)),
                    ("confidence", confidence),
                    ("status", status),
                    ("substage", substage),
                    ("line", line_number),
                ]
            )
        )

    if len(entries) != 176:
        raise MatrixError(f"expected 176 matrix rows, parsed {len(entries)}")

    for system in SYSTEMS[:7]:
        if not any(entry["domain"] == system for entry in entries):
            raise MatrixError(f"system {system} has no mechanics")

    return entries


def source_sha256() -> str:
    """SHA-256 of the matrix with LF line endings.

    Hashing the raw bytes made ``--check`` depend on the checkout: with ``core.autocrlf=true``
    (Windows) the working copy has CRLF and the committed registry (LF) was always reported stale.
    """
    return hashlib.sha256(MATRIX.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def build() -> dict:
    entries = parse()
    by_domain = Counter(entry["domain"] for entry in entries)
    by_confidence = Counter(entry["confidence"] for entry in entries)
    by_status = Counter(entry["status"] for entry in entries)
    canonical = json.dumps(entries, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return {
        "schemaVersion": 1,
        "source": "docs/research/alpha2013/M0_00B_ALPHA_GAMEPLAY_COVERAGE_MATRIX.md",
        "sourceSha256": source_sha256(),
        "entriesHash": hashlib.sha256(canonical.encode("utf-8")).hexdigest(),
        "count": len(entries),
        "domains": SYSTEMS,
        "summary": {
            "byDomain": OrderedDict((name, by_domain.get(name, 0)) for name in SYSTEMS),
            "byConfidence": OrderedDict(sorted(by_confidence.items())),
            "byStatus": OrderedDict(sorted(by_status.items())),
        },
        "entries": entries,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    try:
        registry = build()
    except MatrixError as error:
        print(f"TRANSFER_REGISTRY_RESULT=FAIL reason={error}")
        return 1

    rendered = json.dumps(registry, ensure_ascii=False, indent=2) + "\n"
    if args.check:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != rendered:
            print("TRANSFER_REGISTRY_CHECK=FAIL reason=registry is stale; rerun without --check")
            return 1
        print(
            "TRANSFER_REGISTRY_CHECK=PASS rows=176 "
            f"hash={registry['entriesHash'][:16]} "
            + " ".join(f"{name}={registry['summary']['byDomain'][name]}" for name in SYSTEMS)
        )
        return 0

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(rendered, encoding="utf-8", newline="\n")
    print(
        "TRANSFER_REGISTRY_GENERATED=PASS path=" + str(OUT.relative_to(ROOT)) + " rows=176 "
        f"hash={registry['entriesHash'][:16]}"
    )
    print("  domains: " + " ".join(f"{name}={registry['summary']['byDomain'][name]}" for name in SYSTEMS))
    print("  confidence: " + " ".join(f"{k}={v}" for k, v in registry["summary"]["byConfidence"].items()))
    print("  status: " + " ".join(f"{k}={v}" for k, v in registry["summary"]["byStatus"].items()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
