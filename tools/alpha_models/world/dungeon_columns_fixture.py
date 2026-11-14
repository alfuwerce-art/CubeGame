"""Writes the C# test fixture of the dungeon column loop from the verified Python port (dungeon_columns_port.py).
Usage: python dungeon_columns_fixture.py <entry.pkl> <out.json> subtype:flag:crt [...]"""
import hashlib
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import dungeon_columns_port as cp  # noqa: E402
import dungeon_pre_port as pre  # noqa: E402

entry, out = sys.argv[1], sys.argv[2]
snap = cp.Snapshot(entry)
field = snap.u32(snap.zone + 0xA8)
zh = hashlib.sha256()
for lz in range(256):
    for lx in range(256):
        cell = field + (lx + lz * 256) * 0x20
        bottom, s8, ptr, cnt = struct.unpack("<iiIi", snap.read(cell + 0x10, 16))
        zh.update(struct.pack("<ii", bottom, cnt))
        if cnt > 0 and ptr:
            raw = snap.read(ptr, 4 * cnt)
            zh.update(bytes(raw[i * 4 + 3] for i in range(cnt)))   # flags/material byte only (colours: P1 rounding)
cases = []
for spec in sys.argv[3:]:
    subtype, flag, crt = (int(v) for v in spec.split(":"))
    fields, zx, zz = snap.fields()
    x0, y0 = zx * 256 + 32, zz * 256 + 32
    vol, rot, mirror, pal, state = pre.run(subtype, flag, crt)
    rng = cp.CountingCrt(state)
    run, info = cp.columns(cp.World(fields), vol, rot, mirror, subtype, x0, y0,
                           [pal[0x9c], pal[0x9d], pal[0x9e]], [pal[0], pal[1], pal[2]],
                           struct.unpack("<f", bytes(pal[0x70:0x74]))[0], rng)
    text = ";".join(f"{f[0]:x},{f[1][0]},{f[1][1]},{f[1][2]},{f[2][0]},{f[2][1]},{f[2][2]},{f[5]}" for f in run.fills)
    cases.append({"subtype": subtype, "flag": flag, "crtIn": crt, "fills": len(run.fills),
                  "fillsSha256": hashlib.sha256(text.encode()).hexdigest(), "crtOut": rng.state,
                  "base": info["base"], "rotation": info["rot"], "color1": info["c1"], "color2": info["c2"]})
    print(spec, len(run.fills))
json.dump({"status": "EMULATED_ALPHA_CODE",
           "source": "dungeon_columns_port.py, every fill and the CRT state equal to dungeon_oracle.py traces",
           "masterSeed": 666666, "zone": [zx, zz], "zoneMaterialsSha256": zh.hexdigest(), "cases": cases},
          open(out, "w", encoding="utf-8"))
