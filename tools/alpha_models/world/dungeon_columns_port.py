"""Reference port of the 0x5CC510 zone-write column loop (0x5CEA6B…0x5D06F2), checked against dungeon_oracle traces.

Covers: the four rotation trials (entrance surface, water check), base height, the 2×3 colour rand()s, the subtype
rand(), and per volume column: rock 14³ (0x5CFB48), above-ground walls with crenellations (0x5CF1AD…0x5CF7E0),
subtype-2 caps (0x5CF0C7), subtype-3 pyramids (0x5CF928/0x5CF989) and the shaft (0x5D06BD). Fills (0x5DF600) are
modelled for their rand() draws and the voxel types they write (colours are not modelled here).
Usage: python dungeon_columns_port.py <entry.pkl> <trace.json> <subtype> <flag> <crt_in>
"""
import json
import pickle
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from tools.alpha_models.world.zone_port import Field, WATER  # noqa: E402
import dungeon_pre_port as pre  # noqa: E402
import dungeon_layout_port as d  # noqa: E402

N = 22


class Snapshot:
    def __init__(self, path, exe=r"D:\Games\Cube world\Cube.exe"):
        snap = pickle.load(open(path, "rb"))
        self.regions = snap["mem"]["regions"]
        self.over = snap["mem"]["over"]
        self.zone = snap["extra"]["zone_ptr"]

    def read(self, va, n):
        for b, e, data in self.regions:
            if b <= va and va + n <= e:
                return data[va - b:va - b + n]
        raise KeyError(hex(va))

    def u32(self, va):
        return struct.unpack("<I", self.read(va, 4))[0]

    def i32(self, va):
        return struct.unpack("<i", self.read(va, 4))[0]

    def fields(self):
        field = self.u32(self.zone + 0xA8)
        zx, zz = self.i32(self.zone + 0x60), self.i32(self.zone + 0x64)
        out = {}
        for lz in range(256):
            for lx in range(256):
                cell = field + (lx + lz * 256) * 0x20
                bottom, s8, ptr, n = struct.unpack("<iiIi", self.read(cell + 0x10, 16))
                f = Field(0, 0, 0)
                f.bottom = bottom
                f.s8 = s8
                raw = self.read(ptr, 4 * n) if n > 0 and ptr else b""
                f.voxels = [list(raw[i * 4:i * 4 + 4]) for i in range(n)]
                out[(zx * 256 + lx, zz * 256 + lz)] = f
        return out, zx, zz


class World:
    def __init__(self, fields):
        self.f = fields

    def col(self, x, y):
        return self.f.get((x, y))

    def top(self, x, y):
        c = self.f[(x, y)]
        return c.bottom + len(c.voxels)

    def type(self, x, y, z):
        c = self.f.get((x, y))
        if c is None:
            return 1
        return c.read(z)[3] & 0x1F

    def write(self, x, y, z, v):
        """0x44E7A0 for voxel types only (soil colours below the old bottom are not modelled)."""
        c = self.f.get((x, y))
        if c is None:
            return
        if not (v[3] != 0 or z < c.bottom + len(c.voxels)):
            return
        old = c.bottom
        if z < 1 and (v[3] & 0x5F) == 0:
            v = WATER
        c.set_at(z - old, v)
        zz = z + 1
        while zz < old:
            c.set_at(zz - c.bottom, [0, 0, 0, 6])
            zz += 1


class CountingCrt(d.Crt):
    def __init__(self, s):
        super().__init__(s)
        self.draws = 0

    def rand(self):
        self.draws += 1
        return super().rand()


class Run:
    def __init__(self, world, rng):
        self.w = world
        self.r = rng
        self.fills = []

    def fill(self, site, x, y, z, size, color, p, typ):
        self.fills.append((site, [x, y, z], list(size), list(color), round(p, 6), typ))
        for i in range(23):
            if i & 7 == 0:
                self.r.rand()
        sx, sy, sz = size
        for ix in range(sx):
            for iy in range(sy):
                for iz in range(sz - 1, -1, -1):
                    v = self.r.rand()
                    flags = (typ | 0x46) if p < v / 32767.0 else (typ | 0x4D)
                    self.w.write(x + ix, y + iy, z + iz, [0, 0, 0, flags & 0xFF])


def vtype(vol, rot, mirror, x, y, z):
    if not (0 <= x < N and 0 <= y < N and 0 <= z < N):
        return 1           # Alpha's out-of-range dummy cell reads 1
    c = pre.at(vol, rot, mirror, x, y, z)
    return vol[c][0]


def columns(world, vol, rot0, mirror, subtype, x0, y0, pal_a, wall, scale, rng):
    run = Run(world, rng)
    W = world
    open_set = (0, 4, 1)
    best = None
    rot = rot0
    for _ in range(4):
        rot = (rot + 1) & 3
        ex = ey = ez = 0
        for z in range(N - 1, -1, -1):
            for x in range(N):
                for y in range(N):
                    if vtype(vol, rot, mirror, x, y, z) == 4:
                        ex, ey, ez = x, y, z
        wx, wy = 10 * ex + x0 + 5, 10 * ey + y0 + 5
        if W.col(wx, wy) is None:
            raise RuntimeError("no column above the entrance (Alpha aborts the dungeon)")
        s = W.top(wx, wy)
        if W.type(wx, wy, s) == 0:
            while True:
                s -= 1
                if W.type(wx, wy, s) != 0:
                    break
        s += 1
        water = False
        for dx in range(10):
            for dy in range(10):
                for dz in range(10):
                    if W.type(x0 + 10 * ex + dx, y0 + 10 * ey + dy, s - 5 + dz) in (2, 3):
                        water = True
        cand = s + (100 if water else 0) - 10 * ez
        if best is None or cand < best[0]:
            best = (cand, rot, ex, ey, ez, water)
    base, brot, _, _, bez, bwater = best
    if subtype in (4, 5):
        base += 50
    if bwater:
        base -= 100
    if 10 * bez + base < 0:
        base = -10 * bez
    rot = max(brot, 0)
    c1 = [0, 0, 0]
    c2 = [0, 0, 0]
    c1[0] = rng.rand() % 180 + 50
    c1[1] = rng.rand() % 180 + 50
    c1[2] = rng.rand() % 180 + 50
    c2[0] = rng.rand() % 180 + 50
    c2[1] = rng.rand() % 180 + 50
    c2[2] = rng.rand() % 180 + 50
    if subtype in (0, 3):
        rng.rand()

    def t(x, y, z):
        return vtype(vol, rot, mirror, x, y, z)

    for cx in range(N):
        for cy in range(N):
            WX, WY = x0 + 10 * cx, y0 + 10 * cy
            roof = False
            top35 = 0
            entr = False
            min_surf = W.top(WX, WY)
            for dx in range(10):
                for dy in range(10):
                    min_surf = min(min_surf, W.top(WX + dx, WY + dy))
            for cz in range(N - 1, -1, -1):
                ty = t(cx, cy, cz)
                if ty == 0:
                    continue
                if not roof:
                    top35 = base + 10 * (cz + 1) - (10 if ty == 4 else 0)
                if ty == 4:
                    entr = True
                    roof = True
                    continue
                if not roof and 10 * cz + 5 + base > min_surf:
                    if subtype == 2:
                        run.fill(0x5CF0CC, WX, WY, 10 * cz + 11 + base, (10, 10, 3), pal_a, 0.0, 0)
                    elif subtype in (0, 1, 3):
                        top = base + 10 * (cz + 1)
                        if t(cx - 1, cy, cz) in open_set and t(cx - 1, cy, cz + 1) in open_set:
                            run.fill(0x5CF1B2, WX - 3, WY - 3, top, (2, 16, 3), pal_a, 0.0, 0)
                            for k in range(8):
                                yy = WY - 2 + 2 * k
                                if W.type(WX - 3, yy, top + 3) in (0, 2):
                                    W.write(WX - 3, yy, top + 3, [0, 0, 0, 0x40])
                                run.fill(0x5CF286, WX - 3, yy - 1, top + 3, (1, 1, 1), pal_a, 0.0, 0)
                        if t(cx + 1, cy, cz) in open_set and t(cx + 1, cy, cz + 1) in open_set:
                            X2 = x0 + 10 * (cx + 1)
                            run.fill(0x5CF37E, X2 + 1, WY - 3, top, (2, 16, 3), pal_a, 0.0, 0)
                            for k in range(8):
                                yy = WY - 1 + 2 * k
                                if W.type(X2 + 2, yy, top + 3) in (0, 2):
                                    W.write(X2 + 2, yy, top + 3, [0, 0, 0, 0x40])
                                run.fill(0x5CF455, X2 + 2, yy - 1, top + 3, (1, 1, 1), pal_a, 0.0, 0)
                        if t(cx, cy - 1, cz) in open_set and t(cx, cy - 1, cz + 1) in open_set:
                            run.fill(0x5CF541, WX - 3, WY - 3, top, (16, 2, 3), pal_a, 0.0, 0)
                            for k in range(8):
                                xx = WX - 2 + 2 * k
                                if W.type(xx, WY - 3, top + 3) in (0, 2):
                                    W.write(xx, WY - 3, top + 3, [0, 0, 0, 0x40])
                                run.fill(0x5CF616, xx - 1, WY - 3, top + 3, (1, 1, 1), pal_a, 0.0, 0)
                        if t(cx, cy + 1, cz) in open_set and t(cx, cy + 1, cz + 1) in open_set:
                            Y2 = y0 + 10 * (cy + 1)
                            run.fill(0x5CF70D, WX - 3, Y2 + 1, top, (16, 2, 3), pal_a, 0.0, 0)
                            for k in range(8):
                                xx = WX - 1 + 2 * k
                                if W.type(xx, Y2 + 2, top + 3) in (0, 2):
                                    W.write(xx, Y2 + 2, top + 3, [0, 0, 0, 0x40])
                                run.fill(0x5CF7E5, xx - 1, Y2 + 2, top + 3, (1, 1, 1), pal_a, 0.0, 0)
                        if subtype == 3 and all(t(cx + dx, cy + dy, cz) in open_set
                                                for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1))):
                            # 0x5CF813: the four neighbours at the same level ([ebp-0x2B58] holds cx-1 here)
                            run.fill(0x5CF92D, WX - 2, WY - 2, base + 10 * (cz + 1), (14, 14, 4), wall, 0.0, 0)
                            b, i = 14, 0
                            while b > 0:
                                run.fill(0x5CF98E, WX - 2 + i, WY - 2 + i, base + 10 * cz + 14 + 2 * i,
                                         (b, b, 2), wall, 0.0, 0)
                                b -= 2
                                i += 1
                run.fill(0x5CFB4D, WX - 2, WY - 2, base + 10 * cz - 2, (14, 14, 14), pal_a, scale, 0)
                roof = True
            if roof or subtype in (4, 5):
                if W.col(WX, WY) is None:
                    continue
                min_b = W.col(WX, WY).bottom
                for dx in range(-2, 12):
                    for dy in range(-2, 12):
                        c = W.col(WX + dx, WY + dy)
                        if c is not None:
                            min_b = min(min_b, c.bottom)
                if subtype in (0, 1, 2, 3):
                    run.fill(0x5D06C2, WX - 2, WY - 2, min_b, (14, 14, top35 - min_b), pal_a, scale,
                             0x80 if entr else 0)
                elif subtype == 4:
                    # 0x5CFC2A…0x5CFFB5: stepped tower towards the volume centre
                    half = N // 2 - 1
                    dxa, dya = abs(cx - half), abs(cy - half)
                    steps = N // 2 - max(dxa, dya) - 1
                    if cx == half and cy == half:
                        steps -= 1
                    h = base + 10 * (bez - 5 + steps)
                    if dxa <= 1 and dya <= 1:
                        h += 2
                    run.fill(0x5CFD77, WX - 2, WY - 2, min_b, (14, 14, h - min_b - 4), pal_a, scale, 0)
                    if h - min_b > 0:
                        run.fill(0x5CFDDF, WX - 1, WY - 1, h - 4, (12, 12, 3), pal_a, 0.0, 0)
                        run.fill(0x5CFE2D, WX, WY, h - 1, (10, 10, 3), pal_a, 0.0, 0)
                        # Surface-coloured cap from noise(x·0.02, y·0.02) > 0 — no rand(); voxel colours/types of
                        # 0x5C56E0 are not modelled in this checker.
    return run, dict(base=base, rot=rot, c1=c1, c2=c2)


def run_draws(run):
    return run.r.draws


def main():
    entry, trace_path = sys.argv[1], sys.argv[2]
    subtype, flag, crt = int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5])
    snap = Snapshot(entry)
    fields, zx, zz = snap.fields()
    x0, y0 = zx * 256 + 32, zz * 256 + 32
    vol, rot, mirror, pal, rng_state = pre.run(subtype, flag, crt)
    rng = CountingCrt(rng_state)
    pal_a = [pal[0x9c], pal[0x9d], pal[0x9e]]
    wall = [pal[0], pal[1], pal[2]]
    scale = struct.unpack("<f", bytes(pal[0x70:0x74]))[0]
    run, info = columns(World(fields), vol, rot, mirror, subtype, x0, y0, pal_a, wall, scale, rng)
    trace = json.loads(Path(trace_path).read_text())
    alpha = [c for c in trace["calls"] if c[0] == 0x5DF600][:len(run.fills)]
    bad = 0
    for i, (m, a) in enumerate(zip(run.fills, alpha)):
        ma = (m[0], m[1], m[2], m[3], m[4], m[5])
        aa = (a[1], a[3], a[4], a[5], round(a[6], 6), a[7])
        if ma != aa:
            print("first difference at fill", i, "\n port ", ma, "\n alpha", aa)
            bad = 1
            break
    # CRT after the column loop: Alpha's state after the same number of draws
    states = [st for _, st in trace["rand"]]
    drawn = states.index(rng_state) + 1 + run_draws(run)
    alpha_state = trace["rand"][drawn - 1][1] if drawn - 1 < len(trace["rand"]) else None
    print(len(run.fills), "fills ported;", "identical" if not bad else "DIFFERENT", info,
          "| CRT after loop", "equal" if alpha_state == rng.state else f"DIFF port {rng.state} alpha {alpha_state}")


if __name__ == "__main__":
    main()
