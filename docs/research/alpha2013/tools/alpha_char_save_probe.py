#!/usr/bin/env python3
"""Read-only probe of a Cube World Alpha 2013 character save copy (blob v11).

Usage:  python alpha_char_save_probe.py <path-to-characters.db-COPY>

Prints the documented v11 fields: header, position, rotation, HP, XP, level,
class, specialization. Works only with COPIES: open read-only, never writes.

Field order per S00-R02 (`DATA_AND_SAVE_SCHEMAS.md` §L3). The position triple
at 0x04..0x23 is three int64 fixed-point values copied from Creature
+0x10/+0x18/+0x20 by GameWorld_serialize_state (0044d790). Its exact unit and
meaning (current vs spawn position) remain OQ-22, so the probe prints the raw
values and several interpretations without asserting one:

  * 2^24 units/block — hypothesis consistent with the 0x1000000 (=1 block)
    constant used for entity positions and with the explored map region 512;
  * raw/65536 — world/grid units (256 per block); this is NOT blocks, the
    result must be divided by 256, which is the same as raw/2^24;
  * raw/2^32 — 32.32 reading for comparison only.
"""
from __future__ import annotations

import sqlite3
import struct
import sys
import zlib


def parse(blob: bytes) -> dict:
    out: dict = {}
    off = 0

    def take(n: int) -> bytes:
        nonlocal off
        b = blob[off:off + n]
        off += n
        return b

    out["header"] = struct.unpack("<i", take(4))[0]
    raw_pos = take(0x18)
    out["pos_raw"] = struct.unpack("<3q", raw_pos)
    out["pos_raw_hex"] = raw_pos.hex()
    out["pos_raw_2pow24_blocks"] = [round(v / float(1 << 24), 6) for v in out["pos_raw"]]
    out["pos_raw_grid_units"] = [round(v / 65536.0, 3) for v in out["pos_raw"]]
    out["pos_raw_32_32"] = [round(v / 4294967296.0, 4) for v in out["pos_raw"]]
    out["rot_raw"] = struct.unpack("<q", take(0x0C)[:8])[0]
    out["hp"] = round(struct.unpack("<f", take(4))[0], 2)
    out["xp"] = struct.unpack("<i", take(4))[0]
    out["level"] = struct.unpack("<i", take(4))[0]
    out["class"] = take(1)[0]
    out["spec"] = take(1)[0]
    out["unknown_a"], out["unknown_b"] = struct.unpack("<ii", take(8))
    out["parsed_bytes"] = off
    out["blob_size"] = len(blob)
    return out


def main() -> int:
    if len(sys.argv) < 2:
        print("usage: alpha_char_save_probe.py <characters.db copy>")
        return 2
    path = sys.argv[1]
    db = sqlite3.connect(f"file:{path}?mode=ro", uri=True)
    cur = db.cursor()
    num = cur.execute("select value from blobs where key='num'").fetchone()
    print("num:", struct.unpack("<I", num[0])[0] if num else None)
    for key, val in cur.execute("select key, value from blobs where key != 'num' order by key"):
        try:
            data, note = zlib.decompress(val), "zlib"
        except zlib.error:
            data, note = val, "raw"
        info = parse(data)
        print(f"--- key={key} ({note}, {len(data)} bytes)")
        for k, v in info.items():
            print(f"  {k}: {v}")
    db.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
