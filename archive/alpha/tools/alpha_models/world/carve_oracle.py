"""Execute Alpha World_carveTerrainFeatureB (0x5CBE00) / A (0x5CB600) on a synthetic zone and compare with zone_port.

The carves cost ~1e8 emulated instructions on a real zone, so zone_oracle.py only records their arguments; this
oracle checks their shapes on small volumes instead. A real cube::Zone is built by its constructor (0x6240F0),
columns are laid with Alpha's own tilemap_writeGlyphColumn (0x44E7A0); colour functions are replaced by the same
deterministic stand-ins on both sides (they are verified separately by compare_colors.py), noise is the verified port.
Cases cover materials the carves fill (air/water/grass/snow/5), ones they skip, covered cave air (0x40), the zone
edge and the abort on an explicitly placed (0x80) voxel.

Usage: python -I carve_oracle.py <Cube.exe>
"""
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.terrain_oracle import Oracle, WORLD  # noqa: E402
from tools.alpha_models.world.zone_port import Field, ZoneFeatures, ZonePort  # noqa: E402

ZX = ZZ = 32800
X0, Z0 = ZX * 256, ZZ * 256


class FakeColors:
    @staticmethod
    def soil_color(x, z, y, h, rare):
        return [100.0 + x % 7, 80.0 + z % 5, 60.0 + y % 3]

    @staticmethod
    def rock_color(x, z, y, h, rare):
        return [120.0, 120.0 + y % 4, 130.5]

    @staticmethod
    def surface_color(x, z, y, t, h, rare):
        return [50, 150 + x % 3, 40, 4 | 0x20]


def initial_column(x, z):
    """Synthetic column: bottom and voxel list (r, g, b, flags) from bottom upwards."""
    h = (x * 3 + z * 5) % 7
    bottom = 30 + h
    vox = [[90, 90, 90, 6], [91, 91, 91, 6]]
    k = (x + 2 * z) % 6
    if k == 0:
        vox += [[0, 0, 0, 0x40], [0, 0, 0, 0x40], [70, 70, 70, 6]]        # covered cave air under a roof
    elif k == 1:
        vox += [[200, 200, 220, 9 | 0x20]]                                  # snow top
    elif k == 2:
        vox += [[30, 40, 200, 2]]                                           # water voxel
    elif k == 3:
        vox += [[60, 160, 50, 4 | 0x20], [0, 0, 0, 0]]                      # grass + stored air
    elif k == 4:
        vox += [[110, 100, 90, 5], [111, 100, 90, 3]]                       # material 5, then 3 (not filled)
    else:
        vox += [[60, 160, 50, 4 | 0x20]]
    return bottom, vox


CASES = [
    # (name, carve, cx, cz, base_h, rx, rz, height, blocker)
    ("B_inner", "B", X0 + 40, Z0 + 40, 35, 4, 5, 6, None),
    ("A_inner", "A", X0 + 100, Z0 + 60, 34, 5, 4, 8, None),
    ("B_edge", "B", X0 + 3, Z0 + 120, 36, 3, 3, 5, None),
    ("A_edge", "A", X0 + 252, Z0 + 200, 33, 4, 3, 6, None),
    ("B_abort", "B", X0 + 160, Z0 + 160, 35, 3, 3, 6, (X0 + 160, Z0 + 160)),
]


def run_case(exe, case):
    name, kind, cx, cz, base_h, rx, rz, height, blocker = case
    o = Oracle(exe, 666666, fast_noise=True)
    args = lambda cpu, n: [cpu.mem.u(cpu.r["esp"] + 4 + 4 * i, 4) for i in range(n)]  # noqa: E731

    def sint(v):
        return v - (1 << 32) if v & 0x80000000 else v

    def surface_stub(cpu):
        out, x, z, y = args(cpu, 4)
        cpu.mem.write(out, bytes(FakeColors.surface_color(sint(x), sint(z), sint(y), 0, 0, 0)))
        cpu.r["eax"] = out

    def float3(fn):
        def s(cpu):
            out, x, z, y = args(cpu, 4)
            cpu.mem.write(out, struct.pack("<3f", *fn(sint(x), sint(z), sint(y), 0, 0)))
            cpu.r["eax"] = out
        return s

    o.cpu.stubs[0x5C56E0] = (surface_stub, 28)
    o.cpu.stubs[0x5C7140] = (float3(FakeColors.rock_color), 20)
    o.cpu.stubs[0x5F9620] = (float3(FakeColors.soil_color), 20)

    zone = o.alloc(200)
    o.call(0x6240F0, [], ecx=zone)
    o.cpu.mem.w(zone + 0x60, 4, ZX)
    o.cpu.mem.w(zone + 0x64, 4, ZZ)
    field = o.cpu.mem.u(zone + 0xA8, 4)

    fields = {}
    xs = range(max(X0, cx - 2 * rx - 1), min(X0 + 256, cx + 2 * rx + 2))
    zs = range(max(Z0, cz - 2 * rz - 1), min(Z0 + 256, cz + 2 * rz + 2))
    port = ZonePort(type("NoLand", (), {"T": None})(), FakeColors, None)
    vbuf = o.alloc(16)
    for x in xs:
        for z in zs:
            bottom, vox = initial_column(x, z)
            if blocker and abs(x - blocker[0]) <= 6 and abs(z - blocker[1]) <= 6:
                # explicitly placed stone column reaching high above the ground: inside any carve volume here
                vox += [[80, 80, 80, 6 | 0x80] for _ in range(30)]
            cell = field + ((x - X0) + (z - Z0) * 256) * 0x20
            o.cpu.mem.w(cell + 0x10, 4, bottom)
            f = Field(0.5, 0.5, 0.0)
            f.bottom = bottom
            for i, v in enumerate(vox):
                o.cpu.mem.write(vbuf, bytes(v))
                o.call(0x44E7A0, [x, z, bottom + i, vbuf, zone])
                port.write(f, x, z, bottom + i, v)
            fields[(x, z)] = f

    def snapshot(x, z):
        cell = field + ((x - X0) + (z - Z0) * 256) * 0x20
        bottom, _, ptr, n = struct.unpack("<iiIi", o.cpu.mem.read(cell + 0x10, 16))
        return bottom, ([list(o.cpu.mem.read(ptr + 4 * i, 4)) for i in range(n)] if n > 0 and ptr else [])

    before = {key: snapshot(*key) for key in fields}
    o.call(0x5CBE00 if kind == "B" else 0x5CB600, [cx, cz, base_h, rx, rz, height, zone])
    features = ZoneFeatures(port, fields, ZX, ZZ, {"type": 0}, (0.0, 0.0), 0)
    features.carve(cx, cz, base_h, rx, rz, height, kind == "A")

    bad = changed = 0
    for (x, z), f in fields.items():
        bottom, alpha = snapshot(x, z)
        changed += (bottom, alpha) != before[(x, z)]
        if bottom != f.bottom or alpha != f.voxels:
            bad += 1
            if bad <= 5:
                print(f"  {name} ({x - X0},{z - Z0}): alpha bottom {bottom} {alpha} | port bottom {f.bottom} {f.voxels}")
    print(f"{name}: columns {len(fields)}, changed by Alpha {changed}, port mismatches {bad}", flush=True)
    return bad


def main():
    cases = [c for c in CASES if len(sys.argv) < 3 or c[0] in sys.argv[2:]]
    total = sum(run_case(sys.argv[1], case) for case in cases)
    print("CARVE_ORACLE", "PASS" if total == 0 else "FAIL", "mismatching columns", total)


if __name__ == "__main__":
    main()
