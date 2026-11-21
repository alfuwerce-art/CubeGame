"""Compare color_port.ColorPort with the executed colour functions (0x5C56E0 surface, 0x5C7140 rock, 0x5F9620 soil)."""
import random
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.color_port import ColorPort  # noqa: E402
from tools.alpha_models.world.compare_landmarks import column_lookup  # noqa: E402
from tools.alpha_models.world.compare_port import seeds_for  # noqa: E402
from tools.alpha_models.world.landmark_port import LandmarkPort  # noqa: E402
from tools.alpha_models.world.region_oracle import dump_region  # noqa: E402
from tools.alpha_models.world.terrain_oracle import Oracle, WORLD  # noqa: E402


def f2u(v):
    return struct.unpack("<I", struct.pack("<f", v))[0]


def main():
    exe, master, rx0, rz0, n = sys.argv[1], 666666, 512, 512, int(sys.argv[2]) if len(sys.argv) > 2 else 300
    o = Oracle(exe, master, fast_noise=True)
    o.cpu.mem.w(WORLD + 0xA4, 4, 1)
    o.cpu.mem.w(WORLD + 0xB4, 1, 1)
    o.cpu.stubs[0x4498D0] = ((lambda cpu: cpu.r.__setitem__("eax", 0)), 8)
    cells = {(x, z): o.cell(x, z) for x in range(rx0 - 5, rx0 + 6) for z in range(rz0 - 5, rz0 + 6)}
    seeds = seeds_for(master)
    free = LandmarkPort(seeds, cells, lambda x, z: None)

    def stub(fn):
        def s(cpu):
            cpu.fpu.append(fn(cpu.mem.u(cpu.r["esp"] + 4, 4), cpu.mem.u(cpu.r["esp"] + 8, 4)))
        return s

    o.cpu.stubs[0x5C5E20] = (stub(free.base), 12)
    o.cpu.stubs[0x5EFFA0] = (stub(free.mountain), 12)
    regions = []
    for rx in range(rx0 - 1, rx0 + 2):
        for rz in range(rz0 - 1, rz0 + 2):
            o.call(0x5DA280, [rx, rz])
            regions.append(dump_region(o, rx, rz))
    del o.cpu.stubs[0x5C5E20]
    del o.cpu.stubs[0x5EFFA0]
    land = LandmarkPort(seeds, cells, column_lookup(regions))
    colors = ColorPort(seeds, land)
    rng = random.Random(7)
    lake = [c for r in regions for c in r["columns"] if c["type"] == 4]
    out = o.alloc(64)
    bad = {"surface": 0, "rock": 0, "soil": 0}
    worst = {"surface": 0, "rock": 0.0, "soil": 0.0}
    for i in range(n):
        if lake and i % 3 == 0:
            c = lake[i % len(lake)]
            x, z = int(c["x"] + rng.uniform(-c["f16"], c["f16"])), int(c["z"] + rng.uniform(-c["f16"], c["f16"]))
        else:
            x, z = rng.randrange((rx0 - 1) << 14, (rx0 + 2) << 14), rng.randrange((rz0 - 1) << 14, (rz0 + 2) << 14)
        y = rng.randrange(-60, 220)
        t, h, rare = land.temperature(x, z), land.humidity(x, z), land.region_cell(x, z)
        if i % 5 == 1:          # force the cold/wet and dry branches regularly
            t, h = rng.uniform(0.0, 0.2), rng.uniform(0.75, 1.0)
        elif i % 5 == 2:
            h = rng.uniform(0.0, 0.35)
        o.cpu.mem.write(out, bytes(16))
        o.call(0x5C56E0, [out, x, z, y & 0xFFFFFFFF, f2u(t), f2u(h), 0])
        alpha = list(o.cpu.mem.read(out, 4))
        mine = colors.surface_color(x, z, y, t, h, rare)
        d = max(abs(alpha[k] - mine[k]) for k in range(3))
        worst["surface"] = max(worst["surface"], d)
        if d > 1 or alpha[3] != mine[3]:
            bad["surface"] += 1
            if bad["surface"] <= 6:
                print("surface", x, z, y, round(t, 3), round(h, 3), round(rare, 3), "alpha", alpha, "port", mine)
        real_h = land.humidity(x, z)
        o.call(0x5C7140, [out, x, z, y & 0xFFFFFFFF, 0])
        a = struct.unpack("<3f", o.cpu.mem.read(out, 12))
        m = colors.rock_color(x, z, y, real_h, rare)
        worst["rock"] = max(worst["rock"], max(abs(a[k] - m[k]) for k in range(3)))
        o.call(0x5F9620, [out, x, z, y & 0xFFFFFFFF, 0])
        a = struct.unpack("<3f", o.cpu.mem.read(out, 12))
        m = colors.soil_color(x, z, y, real_h, rare)
        worst["soil"] = max(worst["soil"], max(abs(a[k] - m[k]) for k in range(3)))
    print("samples", n, "surface mismatches (>1 or material)", bad["surface"], "worst", worst)


if __name__ == "__main__":
    main()
