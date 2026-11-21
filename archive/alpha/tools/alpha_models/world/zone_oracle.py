"""Execute Alpha WorldInfo_generateBiomeContent (0x5E4850) on one zone (256×256 voxels) and dump its voxel columns.

Status: EMULATED_ALPHA_CODE with verified substitutions (noise, base 0x5C5E20, mountain 0x5EFFA0 = ports that match
the executed functions, landmark-aware). Regions of the neighbourhood are generated first in isolation (CubeGame
rule). Execution stops after the river pass (0x5E8490), before any content call (decorations @0x5E84C3, creatures): what is
dumped is Alpha's terrain of the zone — columns, colours, caves, water, carved features, paths.

Zone Field cell (0x20 bytes): +4 temperature, +8 humidity, +0xC rare-cell term, +0x10 bottom Y of the stored
voxel array, +0x14 surface-8, +0x18 uint32* voxels (RGB + material byte), +0x1C voxel count.
Usage: python -I zone_oracle.py <Cube.exe> <master_seed> <zone_x> <zone_z> <out.json> [max_columns]
"""
import json
import struct
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.compare_landmarks import column_lookup  # noqa: E402
from tools.alpha_models.world.compare_port import seeds_for  # noqa: E402
from tools.alpha_models.world.landmark_port import LandmarkPort  # noqa: E402
from tools.alpha_models.world.region_oracle import dump_region  # noqa: E402
from tools.alpha_models.world.terrain_oracle import Oracle, WORLD  # noqa: E402
from tools.alpha_models.universal.x86emu import EmuError  # noqa: E402

# First instruction after the river pass (jge target at 0x5E8483), right before the zone-type checks that lead to
# World_populateRegionDecorations (call @0x5E84C3, only zone types 3/5) and to object/creature placement.
DECORATIONS_CALL = 0x5E8490
COLUMN_LOOP = 0x5E4DE0


class Stop(Exception):
    pass


def main():
    exe, master, zx, zz, out = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
    max_columns = int(sys.argv[6]) if len(sys.argv) > 6 else None
    rx0, rz0 = zx >> 6, zz >> 6
    o = Oracle(exe, master, fast_noise=True)
    o.cpu.max_steps = 4_000_000_000
    o.cpu.mem.w(WORLD + 0xA4, 4, 1)
    o.cpu.mem.w(WORLD + 0xB4, 1, 1)
    o.cpu.stubs[0x4498D0] = ((lambda cpu: cpu.r.__setitem__("eax", 0)), 8)
    cells = {(x, z): o.cell(x, z) for x in range(rx0 - 5, rx0 + 6) for z in range(rz0 - 5, rz0 + 6)}
    seeds = seeds_for(master)
    free = LandmarkPort(seeds, cells, lambda x, z: None)

    def stub(fn):
        def s(cpu):
            x, z = cpu.mem.u(cpu.r["esp"] + 4, 4), cpu.mem.u(cpu.r["esp"] + 8, 4)
            x = x - (1 << 32) if x & 0x80000000 else x
            z = z - (1 << 32) if z & 0x80000000 else z
            cpu.fpu.append(fn(x, z))
        return s

    o.cpu.stubs[0x5C5E20] = (stub(free.base), 12)
    o.cpu.stubs[0x5EFFA0] = (stub(free.mountain), 12)
    regions = []
    for rx in range(rx0 - 1, rx0 + 2):
        for rz in range(rz0 - 1, rz0 + 2):
            o.call(0x5DA280, [rx, rz])
            regions.append(dump_region(o, rx, rz))
    land = LandmarkPort(seeds, cells, column_lookup(regions))
    del o.cpu.stubs[0x5C5E20]
    del o.cpu.stubs[0x5EFFA0]

    # Verify the climate ports (incl. the Forest bonus of 0x5C4800) against the real functions before using them.
    import random
    rng = random.Random(zx * 31 + zz)
    worst_t = worst_h = 0.0
    forest = [c for r in regions for c in r["columns"] if c["type"] == 3]
    for i in range(40):
        if forest and i % 2 == 0:
            c = forest[i % len(forest)]
            x, z = int(c["x"] + rng.uniform(-c["f16"], c["f16"])), int(c["z"] + rng.uniform(-c["f16"], c["f16"]))
        else:
            x, z = (zx << 8) + rng.randrange(256), (zz << 8) + rng.randrange(256)
        worst_t = max(worst_t, abs(o.call_float(0x5C4800, [x, z]) - land.temperature(x, z)))
        worst_h = max(worst_h, abs(o.call_float(0x5C4DD0, [x, z]) - land.humidity(x, z)))
    print("climate port check: worst |dT|", worst_t, "|dH|", worst_h, "forest columns", len(forest), flush=True)

    o.cpu.stubs[0x5C5E20] = (stub(land.base), 12)
    o.cpu.stubs[0x5EFFA0] = (stub(land.mountain), 12)
    o.cpu.stubs[0x5C4800] = (stub(land.temperature), 8)
    o.cpu.stubs[0x5C4DD0] = (stub(land.humidity), 8)
    o.cpu.stubs[0x5EF040] = (stub(land.region_cell), 8)
    o.cpu.stubs[0x5F9340] = (stub(land.sample_terrain_height), 12)
    o.cpu.stubs[0x5989D0] = (stub(land.height_at_xy), 8)
    o.cpu.stubs[0x5F9F70] = (stub(land.rotate_and_place), 12)

    # Colour functions: ports byte-identical to execution (compare_colors.py, 300 samples).
    from tools.alpha_models.world.color_port import ColorPort
    colors = ColorPort(seeds, land)

    def sint(v):
        return v - (1 << 32) if v & 0x80000000 else v

    def args(cpu, n):
        return [cpu.mem.u(cpu.r["esp"] + 4 + 4 * i, 4) for i in range(n)]

    def surface_stub(cpu):
        out, x, z, y, t, h = args(cpu, 6)
        x, z, y = sint(x), sint(z), sint(y)
        t = struct.unpack("<f", struct.pack("<I", t))[0]
        h = struct.unpack("<f", struct.pack("<I", h))[0]
        cpu.mem.write(out, bytes(colors.surface_color(x, z, y, t, h, land.region_cell(x, z))))
        cpu.r["eax"] = out

    def float3_stub(fn):
        def s(cpu):
            out, x, z, y = args(cpu, 4)
            x, z, y = sint(x), sint(z), sint(y)
            rgb = fn(x, z, y, land.humidity(x, z), land.region_cell(x, z))
            cpu.mem.write(out, struct.pack("<3f", *rgb))
            cpu.r["eax"] = out
        return s

    o.cpu.stubs[0x5C56E0] = (surface_stub, 28)

    # carveB 0x5CBE00 / carveA 0x5CB600 (thiscall, 7 args, callee pops 28): ~1e8 emulated instructions each, so here
    # they only record their arguments (they draw no rand()); their shapes are verified by carve_oracle.py.
    carves = []

    def carve_stub(kind):
        def s(cpu):
            a = [sint(v) for v in args(cpu, 6)]
            carves.append({"kind": kind, "cx": a[0], "cz": a[1], "base_h": a[2], "rx": a[3], "rz": a[4], "height": a[5]})
        return s

    o.cpu.stubs[0x5CBE00] = (carve_stub("B"), 28)
    o.cpu.stubs[0x5CB600] = (carve_stub("A"), 28)
    o.cpu.stubs[0x5C7140] = (float3_stub(colors.rock_color), 20)
    o.cpu.stubs[0x5F9620] = (float3_stub(colors.soil_color), 20)

    count = {"columns": 0}
    started = time.time()

    def on_column(cpu):
        count["columns"] += 1
        if count["columns"] % 4096 == 0:
            print("columns", count["columns"], "steps", cpu.steps, "s", round(time.time() - started), flush=True)
        if max_columns and count["columns"] > max_columns:
            raise Stop()

    def on_decorations(cpu):
        raise Stop()

    zone_ptr = {}

    def on_zone_ctor(cpu):
        zone_ptr.setdefault('zone', cpu.r['ecx'])

    o.cpu.hooks[0x6240F0] = on_zone_ctor   # cube::Zone::ctor_0 (thiscall: ecx = new zone)
    o.cpu.hooks[COLUMN_LOOP] = on_column
    o.cpu.hooks[DECORATIONS_CALL] = on_decorations
    try:
        o.call(0x5E4850, [zx, zz])
        print("generateBiomeContent returned without reaching decorations")
    except Stop:
        pass
    except EmuError as e:
        print("stop:", e, "eip", hex(o.cpu.eip), "calls", [hex(c) for c in o.cpu.trace_calls[-6:]])
        raise SystemExit(1)

    # world_getRegionCell64 (0x434D10) -> zone pointer; Field array at zone+0xA8
    zone = zone_ptr['zone']
    field = o.cpu.mem.u(zone + 0xA8, 4)
    columns = []
    for lz in range(256):
        for lx in range(256):
            cell = field + (lx + lz * 256) * 0x20
            temp, humid, rare = struct.unpack("<3f", o.cpu.mem.read(cell + 4, 12))
            bottom, s8, ptr, n = struct.unpack("<iiIi", o.cpu.mem.read(cell + 0x10, 16))
            vox = list(struct.unpack(f"<{n}I", o.cpu.mem.read(ptr, 4 * n))) if n > 0 and ptr else []
            columns.append({"lx": lx, "lz": lz, "t": temp, "h": humid, "rare": rare, "bottom": bottom,
                            "s8": s8, "vox": vox})
    Path(out).write_text(json.dumps({"status": "EMULATED_ALPHA_CODE", "master_seed": master, "zone": [zx, zz],
                                     "columns_processed": count["columns"], "regions": regions,
                                     "carves_skipped": carves,
                                     "cells": list(cells.values()), "columns": columns}), encoding="utf-8")
    print("dumped", len(columns), "columns; processed", count["columns"], "in", round(time.time() - started), "s")


if __name__ == "__main__":
    main()
