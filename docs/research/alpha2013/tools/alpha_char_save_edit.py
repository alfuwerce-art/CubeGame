#!/usr/bin/env python3
"""Create an edited COPY of a Cube World Alpha character save (position triple only).

Safety rules (enforced):
  * the input database is opened read-only;
  * the output must be a different path and must NOT be inside the original
    installation (`D:\\Games\\Cube world`) — the original stays untouched;
  * existing output is not overwritten unless --overwrite is given.

This tool exists for the OQ-22 coordinate-system experiment in
docs/stages/S00_RUNTIME_PROTOCOL.md: put a known position into a *copy* of
characters.db, load that copy, and observe where the character appears. It is
not a game trainer and does not touch a running process.

Usage:
  python alpha_char_save_edit.py <input.db> <output.db> --pos-raw X Y Z
  python alpha_char_save_edit.py <input.db> <output.db> --pos-blocks X Y Z
  python alpha_char_save_edit.py <input.db> <output.db> --component-delta 0 64
  python alpha_char_save_edit.py <input.db> <output.db> --show

--pos-blocks converts to the raw triple using the 2^24-units/block hypothesis
(OQ-22; not an established constant). Use --pos-raw to avoid that assumption.
--component-delta <0|1|2> <delta-blocks> edits one raw component; component
order is still OQ-22, so the tool never calls it x/y/z.
"""

from __future__ import annotations

import shutil
import sqlite3
import struct
import sys
import zlib
from pathlib import Path

ORIGINAL_DIR = Path(r"D:\Games\Cube world").resolve()


def read_blob(path: Path) -> bytes:
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
    if len(blob) < 0x1C or blob[0:4] != b"\x01\x00\x00\x00":
        raise SystemExit(f"error: {path}: unsupported blob (len={len(blob)})")
    return blob


def main() -> int:
    argv = sys.argv[1:]
    overwrite = "--overwrite" in argv
    argv = [a for a in argv if a != "--overwrite"]
    if len(argv) < 2:
        print(__doc__)
        return 2
    src, dst = Path(argv[0]), Path(argv[1])
    mode = argv[2] if len(argv) > 2 else "--show"
    values = argv[3:]

    if not src.is_file():
        raise SystemExit(f"error: input not found: {src}")
    src_r, dst_r = src.resolve(), dst.resolve()
    if src_r == dst_r:
        raise SystemExit("error: input and output must differ (copy only)")
    for p in (src_r, dst_r):
        if ORIGINAL_DIR == p or ORIGINAL_DIR in p.parents:
            raise SystemExit("error: refusing any path inside the original installation")
    if dst.exists() and not overwrite:
        raise SystemExit(f"error: output exists: {dst} (use --overwrite)")

    before = read_blob(src)
    old_pos = struct.unpack_from("<3q", before, 4)
    print(f"input : {src}  pos_raw={old_pos}")
    print(f"        /2^24 (hypothesis blocks)={tuple(round(v / float(1 << 24), 6) for v in old_pos)}")

    if mode == "--show":
        print("show only: output not written")
        return 0
    if mode == "--component-delta":
        if len(values) != 2:
            raise SystemExit("error: expected --component-delta <0|1|2> <delta-blocks>")
        try:
            component = int(values[0])
            delta_blocks = float(values[1])
        except ValueError:
            raise SystemExit("error: --component-delta expects integer component and numeric delta")
        if component not in (0, 1, 2):
            raise SystemExit("error: component must be 0, 1 or 2")
        raw = list(old_pos)
        raw[component] += int(round(delta_blocks * float(1 << 24)))
        raw = tuple(raw)
        print(f"note: --component-delta uses the OQ-22 2^24 hypothesis and component index {component}; raw={raw}")
    elif mode in ("--pos-raw", "--pos-blocks") and len(values) == 3:
        nums = [float(v) for v in values]
        if mode == "--pos-blocks":
            raw = tuple(int(round(v * float(1 << 24))) for v in nums)
            print(f"note: --pos-blocks uses the OQ-22 2^24 hypothesis; raw={raw}")
        else:
            raw = tuple(int(v) for v in nums)
    else:
        raise SystemExit("error: expected --pos-raw|--pos-blocks X Y Z, "
                         "--component-delta N D, or --show")
    if any(v < -(1 << 63) or v >= (1 << 63) for v in raw):
        raise SystemExit("error: position out of int64 range")

    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    db = sqlite3.connect(dst)
    try:
        row = db.execute("select value from blobs where key='0'").fetchone()
        value = row[0]
        try:
            blob = bytearray(zlib.decompress(value))
        except zlib.error:
            blob = bytearray(value)
        struct.pack_into("<3q", blob, 4, *raw)
        payload = zlib.compress(bytes(blob), 9)
        db.execute("update blobs set value=? where key='0'", (payload,))
        db.commit()
    finally:
        db.close()
    after = read_blob(dst)
    after_pos = struct.unpack_from("<3q", after, 4)
    print(f"output: {dst}  pos_raw={after_pos}")
    print(f"        components={{0: {after_pos[0]}, 1: {after_pos[1]}, 2: {after_pos[2]}}}")
    print("OK: edited COPY written; original game files untouched")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
