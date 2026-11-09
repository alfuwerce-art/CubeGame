"""Trace arguments/results of Alpha terrain helpers (research for the C# port; values = EMULATED_ALPHA_CODE).

For one voxel (x, z): records every call of vec3_dot_or_distance (p, a, b as double pairs) inside
World_findNearestObject, and the direct results of the helpers called with (x, z) on the world `this`.
"""
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.terrain_oracle import Oracle  # noqa: E402

exe = sys.argv[1]
points = [(512 * 0x4000 + 0x2000 + 3000, 512 * 0x4000 + 0x1234), (511 * 0x4000 + 77, 513 * 0x4000 + 9000)]
o = Oracle(exe, 666666)
for rx in range(509, 516):
    for rz in range(509, 516):
        o.cell(rx, rz)

seg = []


def on_seg(cpu):
    sp = cpu.r["esp"]
    ptrs = [cpu.mem.u(sp + 4 + 4 * i, 4) for i in range(3)]
    vals = [struct.unpack("<2d", cpu.mem.read(p, 16)) for p in ptrs]
    seg.append(vals)


o.cpu.hooks[0x5EF5A0] = on_seg
out = []
for x, z in points:
    seg.clear()
    rec = {"x": x, "z": z}
    rec["nearest"] = o.call_float(0x5EEA60, [x, z])
    rec["segments"] = [list(map(list, s)) for s in seg]
    seg.clear()
    rec["sampleTerrainHeight"] = o.call_float(0x5F9340, [x, z])
    rec["sampleHeightAtWorldXY"] = o.call_float(0x5989D0, [x, z])
    rec["rotateAndPlace"] = o.call_float(0x5F9F70, [x, z])
    rec["regionCellPtr"] = o.call_float(0x5EF040, [x, z])
    buf = o.alloc(16)
    o.call(0x59FC90, [buf, x, z])
    rec["heightNoise"] = list(struct.unpack("<2d", o.cpu.mem.read(buf, 16)))
    out.append(rec)
print(json.dumps(out, indent=1))
cells = {(c["rx"], c["rz"]): c for c in (o.cell(rx, rz) for rx in range(509, 516) for rz in range(509, 516))}
for k in [(511, 512), (512, 512), (513, 512), (512, 511), (512, 513)]:
    c = cells[k]
    print(k, c["anchor_x"], c["anchor_z"], c["anchor_x"] / 16384.0, c["anchor_z"] / 16384.0, c["base"])
