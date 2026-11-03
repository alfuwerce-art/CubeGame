#!/usr/bin/env python3
"""Compare two character-save copies (blobs table, v11) and report what changed.

Read-only: opens both SQLite files with mode=ro, never writes. Intended for the
OQ-22/OQ-23 measurement protocol (docs/stages/S00_RUNTIME_PROTOCOL.md).

The axis order of the three int64 position fields is NOT proven. The tool always
prints raw deltas for all three components and candidate distances in the XY, XZ
and YZ planes. A horizontal plane must be selected explicitly with --plane for a
distance/speed measurement; without it no speed is emitted.

Usage:
  python alpha_save_diff.py <before.db> <after.db> [--plane xy|xz|yz]
                            [--duration SECONDS] [--expect-pos-change]
                            [--min-blocks N] [--self-test]

Exit codes:
  0  analysis completed (or self-test passed)
  1  self-test failed
  2  usage/read error
  3  --expect-pos-change was given but the selected plane displacement is below
     --min-blocks (default 1.0) or --plane is missing

Important: component order (x/y/z vs x/z/y) remains OQ-22 until a runtime test
proves it. "2^24 raw units = 1 block" is also a hypothesis, not an established
constant. The tool labels the selected plane as CALLER_SELECTED, never as proven.
"""

from __future__ import annotations

import sqlite3
import struct
import sys
import zlib
from dataclasses import dataclass

UNIT_PER_BLOCK = 1 << 24
PLANE_AXES = {"xy": (0, 1), "xz": (0, 2), "yz": (1, 2)}
AXIS_LABELS = ("component0", "component1", "component2")


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


def delta_blocks(after: tuple[int, int, int],
                 before: tuple[int, int, int]) -> tuple[float, float, float]:
    return tuple((a - b) / float(UNIT_PER_BLOCK) for a, b in zip(after, before))


def plane_distance(delta: tuple[float, float, float], plane: str) -> float:
    i, j = PLANE_AXES[plane]
    return (delta[i] * delta[i] + delta[j] * delta[j]) ** 0.5


def self_test() -> int:
    cases = [
        ("xy=5, xz=3, yz=4", (3.0, 4.0, 0.0), {"xy": 5.0, "xz": 3.0, "yz": 4.0}),
        ("xy=3, xz=4, yz=5", (0.0, 3.0, 4.0), {"xy": 3.0, "xz": 4.0, "yz": 5.0}),
        ("xy=6, xz=10, yz=8", (6.0, 0.0, 8.0), {"xy": 6.0, "xz": 10.0, "yz": 8.0}),
        ("zero", (0.0, 0.0, 0.0), {"xy": 0.0, "xz": 0.0, "yz": 0.0}),
    ]
    failures: list[str] = []
    for name, delta, expected in cases:
        for plane, want in expected.items():
            got = plane_distance(delta, plane)
            if abs(got - want) > 1e-9:
                failures.append(f"{name}: {plane}={got:.9f}, expected {want:.9f}")
    # Raw conversion: one block = 2^24 hypothesis.
    if abs(delta_blocks((UNIT_PER_BLOCK, 2 * UNIT_PER_BLOCK, 0), (0, 0, 0))[0] - 1.0) > 1e-12:
        failures.append("delta_blocks conversion failed")
    print("=== alpha_save_diff self-test ===")
    if failures:
        for item in failures:
            print(" -", item)
        print("FAIL")
        return 1
    print("PASS: XY/XZ/YZ distances and 2^24 conversion")
    return 0


def main() -> int:
    argv = sys.argv[1:]
    if "--self-test" in argv:
        return self_test()

    plane: str | None = None
    duration: float | None = None
    expect_change = "--expect-pos-change" in argv
    min_blocks = 1.0
    clean: list[str] = []
    i = 0
    while i < len(argv):
        if argv[i] in ("--plane", "--duration", "--min-blocks"):
            if i + 1 >= len(argv):
                print(f"error: {argv[i]} expects a value")
                return 2
            value = argv[i + 1]
            try:
                if argv[i] == "--plane":
                    if value not in PLANE_AXES:
                        print("error: --plane must be xy, xz or yz")
                        return 2
                    plane = value
                elif argv[i] == "--duration":
                    duration = float(value)
                    if duration <= 0:
                        raise ValueError
                else:
                    min_blocks = float(value)
            except ValueError:
                print(f"error: invalid value for {argv[i]}: {value}")
                return 2
            i += 2
            continue
        if argv[i] == "--expect-pos-change":
            i += 1
            continue
        clean.append(argv[i])
        i += 1

    if len(clean) != 2:
        print("usage: alpha_save_diff.py <before.db> <after.db> "
              "[--plane xy|xz|yz] [--duration SECONDS] "
              "[--expect-pos-change] [--min-blocks N] [--self-test]")
        return 2
    before, after = load(clean[0]), load(clean[1])
    print("=== Alpha character-save diff (read-only) ===")
    print(f"before: {before.path}  blob={len(before.blob)} B")
    print(f"after : {after.path}  blob={len(after.blob)} B")
    if len(before.blob) != len(after.blob):
        print("FAIL: blob sizes differ; a field-level diff is not meaningful")
        return 2

    diffs = [i for i, (a, b) in enumerate(zip(before.blob, after.blob)) if a != b]
    ranges = grouped_ranges(diffs)
    print(f"changed bytes: {len(diffs)} in {len(ranges)} ranges")
    for a, b in ranges[:20]:
        print(f"  0x{a:04X}..0x{b:04X} ({b - a + 1} B): "
              f"{before.blob[a:b + 1].hex()} -> {after.blob[a:b + 1].hex()}")
    if len(ranges) > 20:
        print(f"  ... {len(ranges) - 20} more ranges omitted")

    print("-- position triple (+0x04..0x23, 3 x int64) --")
    print(f"  before raw: {before.pos}")
    print(f"  after  raw: {after.pos}")
    raw_delta = tuple(a - b for a, b in zip(after.pos, before.pos))
    print(f"  delta raw: {raw_delta}")
    for idx, value in enumerate(raw_delta):
        print(f"  {AXIS_LABELS[idx]} delta raw: {value}")
    delta = delta_blocks(after.pos, before.pos)
    print("  delta / 2^24 (hypothesis blocks): "
          f"{tuple(round(v, 9) for v in delta)}")
    print(f"  before / 2^24: {tuple(round(p / float(UNIT_PER_BLOCK), 6) for p in before.pos)}")
    print(f"  after  / 2^24: {tuple(round(p / float(UNIT_PER_BLOCK), 6) for p in after.pos)}")

    print("-- candidate planes (axis order UNKNOWN) --")
    distances = {name: plane_distance(delta, name) for name in PLANE_AXES}
    for name in ("xy", "xz", "yz"):
        print(f"  {name}: {distances[name]:.6f} blocks")
    if plane is not None:
        print(f"  selected plane: {plane.upper()} (CALLER_SELECTED; axes not proven)")
        if duration is not None:
            print(f"  measured speed on {plane.upper()}: {distances[plane] / duration:.6f} blocks/s "
                  f"(duration={duration:.3f} s; axes CALLER_SELECTED)")
        else:
            print("  speed not computed: --duration not supplied")
    else:
        print("  selected plane: NONE; no horizontal distance or speed is confirmed")
        if duration is not None:
            print("  speed not computed: --plane not supplied")

    print("-- parsed header fields --")
    print(f"  HP: {before.hp:.4f} -> {after.hp:.4f}")
    print(f"  XP: {before.xp} -> {after.xp}")
    print(f"  level: {before.level} -> {after.level}")
    print(f"  class/spec: {before.class_id}/{before.spec} -> {after.class_id}/{after.spec}")

    if expect_change:
        if plane is None:
            print("FAIL: --expect-pos-change requires --plane (axis order is not proven)")
            return 3
        if distances[plane] < min_blocks:
            print(f"FAIL: --expect-pos-change: {plane.upper()} displacement "
                  f"{distances[plane]:.6f} blocks < --min-blocks {min_blocks}")
            return 3
    print("DONE: diff written to stdout only; no files modified")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
