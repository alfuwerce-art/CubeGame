#!/usr/bin/env python3
"""Compare two character-save copies (blobs table, v11) and report what changed.

Read-only: opens both SQLite files with mode=ro, never writes. Intended for the
OQ-22/OQ-23 measurement protocol (docs/stages/S00_RUNTIME_PROTOCOL.md): make a
copy of characters.db before and after a controlled movement, exit to the menu,
then compare the copies.

Usage:
  python alpha_save_diff.py <before.db> <after.db> [--expect-pos-change] [--min-blocks N]

Exit codes:
  0  analysis completed; position triple may be unchanged
  3  --expect-pos-change was given but horizontal displacement is below --min-blocks
  2  usage/read error

Important: the meaning of the position triple (current vs spawn position, exact
unit) is OQ-22. The tool prints raw values and 2^24-units/block interpretation
as a hypothesis, not as an established fact.
"""

from __future__ import annotations

import sqlite3
import struct
import sys
import zlib
from dataclasses import dataclass


@dataclass
class Save:
    path: str
    blob: bytes
    header: int
    pos: tuple[int, int, int]
    hp: float
    xp: int
    level: int
    class_id: int
    spec: int


def load(path: str) -> Save:
    db = sqlite3.connect(f"file:{path}?mode=ro", uri=True)
    try:
        row = db.execute("select value from blobs where key='0'").fetchone()
    finally:
        db.close()
    if row is None:
        raise SystemExit(f"error: no key='0' blob in {path}")
    value = row[0]
    try:
        blob = zlib.decompress(value)
    except zlib.error:
        blob = value
    if len(blob) < 0x3E or blob[0:4] != b"\x01\x00\x00\x00":
        raise SystemExit(f"error: {path}: unsupported blob (len={len(blob)}, header={blob[:4].hex()})")
    pos = struct.unpack_from("<3q", blob, 4)
    # header 4 + pos 0x18 + rotation 0x0C = 0x28
    hp = struct.unpack_from("<f", blob, 0x28)[0]
    xp, level = struct.unpack_from("<2i", blob, 0x2C)
    class_id, spec = blob[0x34], blob[0x35]
    return Save(path, blob, 1, pos, hp, xp, level, class_id, spec)


def grouped_ranges(diffs: list[int]) -> list[tuple[int, int]]:
    ranges: list[list[int]] = []
    for i in diffs:
        if ranges and i == ranges[-1][1] + 1:
            ranges[-1][1] = i
        else:
            ranges.append([i, i])
    return [(a, b) for a, b in ranges]


def main() -> int:
    argv = sys.argv[1:]
    expect_change = "--expect-pos-change" in argv
    min_blocks = 0.0
    clean: list[str] = []
    i = 0
    while i < len(argv):
        if argv[i] == "--min-blocks":
            if i + 1 >= len(argv):
                print("error: --min-blocks expects a number")
                return 2
            try:
                min_blocks = float(argv[i + 1])
            except ValueError:
                print("error: --min-blocks expects a number")
                return 2
            i += 2
            continue
        if argv[i] == "--expect-pos-change":
            i += 1
            continue
        clean.append(argv[i])
        i += 1
    args = clean
    if len(args) != 2:
        print("usage: alpha_save_diff.py <before.db> <after.db> [--expect-pos-change] [--min-blocks N]")
        return 2
    before, after = load(args[0]), load(args[1])
    print("=== Alpha character-save diff (read-only) ===")
    print(f"before: {before.path}  blob={len(before.blob)} B")
    print(f"after : {after.path}  blob={len(after.blob)} B")
    if len(before.blob) != len(after.blob):
        print("FAIL: blob sizes differ; a field-level diff is not meaningful")
        return 2

    diffs = [i for i, (a, b) in enumerate(zip(before.blob, after.blob)) if a != b]
    ranges = grouped_ranges(diffs)
    print(f"changed bytes: {len(diffs)} in {len(ranges)} ranges")
    for a, b in ranges[:40]:
        print(f"  0x{a:04X}..0x{b:04X} ({b - a + 1} B): "
              f"{before.blob[a:b + 1].hex()} -> {after.blob[a:b + 1].hex()}")
    if len(ranges) > 40:
        print(f"  ... {len(ranges) - 40} more ranges omitted")

    print("-- position triple (+0x04..0x23, 3 x int64) --")
    print(f"  before raw: {before.pos}")
    print(f"  after  raw: {after.pos}")
    delta = tuple(a - b for a, b in zip(after.pos, before.pos))
    print(f"  delta raw: {delta}")
    print(f"  delta / 2^24 (hypothesis blocks): "
          f"{tuple(round(d / float(1 << 24), 9) for d in delta)}")
    print(f"  before / 2^24: {tuple(round(p / float(1 << 24), 6) for p in before.pos)}")
    print(f"  after  / 2^24: {tuple(round(p / float(1 << 24), 6) for p in after.pos)}")
    pos_changed = before.pos != after.pos
    print(f"  position changed: {pos_changed}")

    # Horizontal displacement under the OQ-22 2^24 hypothesis (x/z axes).
    dx = (after.pos[0] - before.pos[0]) / float(1 << 24)
    dz = (after.pos[2] - before.pos[2]) / float(1 << 24)
    horizontal = (dx * dx + dz * dz) ** 0.5
    print(f"  horizontal displacement (2^24 hypothesis): {horizontal:.6f} blocks")

    print("-- parsed header fields --")
    print(f"  HP: {before.hp:.4f} -> {after.hp:.4f}")
    print(f"  XP: {before.xp} -> {after.xp}")
    print(f"  level: {before.level} -> {after.level}")
    print(f"  class/spec: {before.class_id}/{before.spec} -> {after.class_id}/{after.spec}")

    if expect_change:
        if not pos_changed:
            print("FAIL: --expect-pos-change: position triple did not change")
            return 3
        if horizontal < min_blocks:
            print(f"FAIL: --expect-pos-change: horizontal displacement {horizontal:.6f} "
                  f"blocks < --min-blocks {min_blocks}")
            return 3
    print("DONE: diff written to stdout only; no files modified")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
