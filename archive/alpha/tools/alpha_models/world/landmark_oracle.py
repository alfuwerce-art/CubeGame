"""Terrain with region landmarks: executed Alpha base (0x5C5E20) and mountain mask (0x5EFFA0) near landmark columns.

1. Regions (rx0±1, rz0±1) are generated with generateRegion (0x5DA280) in one synthetic world; while generating,
   base/mountain are the landmark-free ports (regions see no earlier landmarks = CubeGame's order-free rule).
2. The substitutions are removed (noise stays the verified port) and the REAL functions are executed at
   points inside landmark disks, so their landmark branches run on Alpha's own column records.
Output (EMULATED_ALPHA_CODE): regions (columns) + samples {x, z, base, mountain, type}.
Usage: python -I landmark_oracle.py <Cube.exe> <master_seed> <rx0> <rz0> <samples_per_column> <out.json>
"""
import json
import math
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.compare_port import seeds_for  # noqa: E402
from tools.alpha_models.world.region_oracle import dump_region  # noqa: E402
from tools.alpha_models.world.terrain_oracle import Oracle, WORLD  # noqa: E402
from tools.alpha_models.world.landmark_port import LandmarkPort  # noqa: E402


def main():
    exe, master, rx0, rz0, per_col, out = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5]), sys.argv[6]
    o = Oracle(exe, master, fast_noise=True)
    o.cpu.mem.w(WORLD + 0xA4, 4, 1)
    o.cpu.mem.w(WORLD + 0xB4, 1, 1)
    o.cpu.stubs[0x4498D0] = ((lambda cpu: cpu.r.__setitem__("eax", 0)), 8)
    for x in range(rx0 - 5, rx0 + 6):
        for z in range(rz0 - 5, rz0 + 6):
            o.cell(x, z)
    cells = {(x, z): o.cell(x, z) for x in range(rx0 - 5, rx0 + 6) for z in range(rz0 - 5, rz0 + 6)}
    port = LandmarkPort(seeds_for(master), cells, lambda x, z: None)

    def stub(fn):
        def s(cpu):
            x, z = cpu.mem.u(cpu.r["esp"] + 4, 4), cpu.mem.u(cpu.r["esp"] + 8, 4)
            cpu.fpu.append(fn(x, z))
        return s

    o.cpu.stubs[0x5C5E20] = (stub(port.base), 12)
    o.cpu.stubs[0x5EFFA0] = (stub(port.mountain), 12)
    regions = []
    for rx in range(rx0 - 1, rx0 + 2):
        for rz in range(rz0 - 1, rz0 + 2):
            o.call(0x5DA280, [rx, rz])
            regions.append(dump_region(o, rx, rz))
            print("region", rx, rz, flush=True)
    del o.cpu.stubs[0x5C5E20]
    del o.cpu.stubs[0x5EFFA0]

    rng = random.Random(master + rx0 * 7 + rz0)
    samples = []
    for r in regions:
        for c in r["columns"]:
            if c["type"] == 0 or c["f16"] <= 0:
                continue
            for _ in range(per_col):
                a = rng.random() * 2 * math.pi
                d = math.sqrt(rng.random()) * c["f16"] * 1.1
                x = int(c["x"] + math.cos(a) * d)
                z = int(c["z"] + math.sin(a) * d)
                samples.append({"x": x, "z": z, "type": c["type"],
                                "base": o.call_float(0x5C5E20, [x, z, 0]),
                                "mountain": o.call_float(0x5EFFA0, [x, z, 0])})
        print("sampled", r["rx"], r["rz"], len(samples), flush=True)
    Path(out).write_text(json.dumps({"status": "EMULATED_ALPHA_CODE", "master_seed": master, "regions": regions,
                                     "cells": list(cells.values()), "samples": samples}), encoding="utf-8")


if __name__ == "__main__":
    main()
