"""Reference port of the landmark-column part of Alpha generateRegion (0x5DA280) — mirrors the C# port.

Columns: 8×8 per region (each 0x800 voxels = 8 zones), record fields used by terrain/content:
x, z (voxels, 16.16 in Alpha), radius (+0x10), base (+0x14), type (+0x18), sub (+0x1C), id (+0x20),
level (+0x24), level class (+0x28). Compared with region_oracle.py (EMULATED_ALPHA_CODE) by compare_regions.py.
Not ported here: the 16-byte zone grid, village roads, stored-region loading (content stages M0-06/M0-07).
"""
import math
import struct

from tools.alpha_models.world.terrain_oracle import Crt
from tools.alpha_models.world.terrain_port import f32, noise


def level_of(rx, rz):
    if rx == 0x200 and rz == 0x200:
        return 1
    dz = float(0x200 - rz)
    dx = float(0x200 - rx)
    d = math.sqrt(f32(f32(dz * dz) + f32(dx * dx)))
    return 2 - int(f32(f32(d) * -0.75))


def fistp(v):
    """x87 round-to-nearest-even (float_to_uint64_round_guarded with default rounding)."""
    return int(round(v))


class RegionPort:
    def __init__(self, port, world_seed, db_open=True):
        self.port = port            # terrain_port.Port (base without landmarks) + cells
        self.seed = world_seed
        self.db_open = db_open

    def nearest_cell(self, x, z):
        """findNearestEntityInGrid (0x477E10): climate cell nearest to the warped point (int distance)."""
        n1 = noise(z * 0.0005, 3423.0)
        n2 = noise(x * 0.0005, 23421.0)
        px = int(f32(f32(float(x)) + f32(f32(n1 * 3.0) * 256.0)))
        pz = int(f32(f32(float(z)) + f32(f32(n2 * 3.0) * 256.0)))
        best, best_d = None, 0
        for rx in range((x - 0x4000) >> 14, ((x + 0x4000) >> 14) + 1):
            for rz in range((z - 0x4000) >> 14, ((z + 0x4000) >> 14) + 1):
                c = self.port.cell(rx, rz)
                if c is None:
                    continue
                dx = f32(float(c["anchor_x"] - px))
                dz = f32(float(c["anchor_z"] - pz))
                d = int(f32(f32(dz * dz) + f32(dx * dx)))
                if best is None or d < best_d:
                    best, best_d = (rx, rz), d
        return best

    def climate_avg(self, x, z, field):
        """0x5C4800 / 0x5C4DD0 without landmark columns: weighted mean of climate field 3 / field 4."""
        n1 = noise(z * 0.0005, 3423.0)
        n2 = noise(x * 0.0005, 23421.0)
        px = int(f32(f32(float(x)) + f32(f32(n1 * 3.0) * 256.0)))
        pz = int(f32(f32(float(z)) + f32(f32(n2 * 3.0) * 256.0)))
        keys = [(rx, rz) for rx in range((x - 0x4000) >> 14, ((x + 0x4000) >> 14) + 1)
                for rz in range((z - 0x4000) >> 14, ((z + 0x4000) >> 14) + 1)]
        cells = [self.port.cell(*k) for k in keys]
        ds = [int(f32(f32(float(c["anchor_z"] - pz)) ** 2 + f32(float(c["anchor_x"] - px)) ** 2)) for c in cells]
        best = min(ds)
        tw = tv = 0.0
        for c, d in zip(cells, ds):
            w = f32(1.0 - min(1.0, f32(float(d - best) * 5e-07)))
            tw = f32(tw + w)
            tv = f32(tv + f32(c[field] * w))
        return f32(tv / tw)

    @staticmethod
    def level_range(cell):
        """assign_two_dwords (0x5EE4B0): creature level range of a region from its climate cell."""
        lo, hi = 1, 10
        if cell["f4"] < 0.2:
            lo, hi = 10, 20
        if cell["f3"] < 0.2 and cell["f4"] > 0.8:
            lo, hi = 15, 25
        if cell["f3"] > 0.8 and cell["f4"] > 0.8:
            lo, hi = 10, 20
        if cell["flag"] == 1:
            lo, hi = 20, 30
        return lo, hi

    def generate(self, rx, rz):
        crt = Crt()
        crt.srand((self.seed + rz * 0x400 + rx) & 0xFFFFFFFF)
        level = level_of(rx, rz)
        variant = crt.rand() % 5 if level > 4 else 0
        orientation = crt.rand() & 3
        next_id = crt.rand() % 10000
        cell = self.port.cell(rx, rz)
        if cell["f4"] > 0.81:
            orientation = (crt.rand() & 1) + 4
        level_min, level_max = self.level_range(cell)
        acx = (cell["anchor_x"] >> 11) & 7
        acz = (cell["anchor_z"] >> 11) & 7
        cols = [dict(type=0, sub=0, id=0, level=1, cls=0, radius=0.0, base=0.0, x=0.0, z=0.0) for _ in range(64)]
        entries = []
        for cx in range(8):
            for cz in range(8):
                col = cols[cx * 8 + cz]
                x0 = rx * 0x4000 + cx * 0x800
                z0 = rz * 0x4000 + cz * 0x800
                if self.db_open and cx == acx and cz == acz:
                    col["type"] = 1
                    col["sub"] = orientation
                    col["id"] = cell["id"]
                    r = f32(float(crt.rand() % 200 + 0x200))
                    col["radius"] = r
                    x, z = float(cell["anchor_x"]), float(cell["anchor_z"])
                    # keep the disk (r + 256) inside the column
                    if x - r - 256.0 < x0:
                        x = float(int(f32(f32(x0 + r + 256.0))))
                    if z - r - 256.0 < z0:
                        z = float(int(f32(f32(z0 + r + 256.0))))
                    if x + r + 256.0 > x0 + 0x800:
                        x = float(int(f32(f32(x0 + 0x800 - r - 256.0))))
                    if z + r + 256.0 > z0 + 0x800:
                        z = float(int(f32(f32(z0 + 0x800 - r - 256.0))))
                    col["x"], col["z"] = x, z
                    b = self.port.base(int(x), int(z))
                    col["base"] = max(b, 0.0)
                    continue
                if self.nearest_cell(x0 + 0x400, z0 + 0x400) != (rx, rz):
                    continue
                r = f32(float((crt.rand() & 0xFF) + 0x200))
                col["radius"] = r
                col["level"] = level_min                       # +0x24 = region level range minimum (0x5EE4B0)
                span = f32(2048.0 - f32(r + 256.0) * 2.0)
                oz = int(f32(f32(f32(crt.rand() * span) / 32767.0) * 65536.0))
                z = z0 + (int(f32(r * 65536.0)) + oz) / 65536.0 + 256.0
                ox = int(f32(f32(f32(crt.rand() * span) / 32767.0) * 65536.0))
                x = x0 + (int(f32(f32(r + 256.0) * 65536.0)) + ox) / 65536.0
                col["x"], col["z"] = x, z
                col["base"] = self.port.base(int(x), int(z))
                if self.nearest_cell(int(x), int(z)) == (rx, rz):
                    dist = f32(math.sqrt(f32(float(acz - cz) ** 2 + float(acx - cx) ** 2)))
                    key = f32(f32(f32(crt.rand() * 4.0) / 32767.0) + dist) - 2.0
                    entries.append((f32(key), cx, cz))
        entries.sort(key=lambda e: e[0])
        j = 0
        order = list(entries)
        while j < 64 and order:
            if j & 1:
                j += 1
                continue
            key, cx, cz = order.pop(0)
            col = cols[cx * 8 + cz]
            if col["base"] < 0:
                col["base"] = 0.0                            # column +0x14 clamped for processed columns
            col["level"] = int(f32((1.0 / (1.0 - f32(j * 0.015625)) - 1.0) * 20.0 + 1.0))   # _ftol2: truncation
            if (j >> 1) & 1:
                col["type"] = 0xE
                col["radius"] = 150.0
                zx = int(col["x"]) & ~0xFF
                zz = int(col["z"]) & ~0xFF
                col["x"], col["z"] = float(zx + 0x80), float(zz + 0x80)
                zone_sub = crt.rand() & 3                     # zone record byte +1
                zcx = (rx * 0x40 + ((zx >> 8) & 0x3F)) * 0x100 + 0x80
                zcz = (rz * 0x40 + ((zz >> 8) & 0x3F)) * 0x100 + 0x80
                if self.climate_avg(zcx, zcz, "f4") > 0.8:                      # humidity 0x5C4DD0
                    zone_sub = (1 if self.climate_avg(zcx, zcz, "f3") <= 0.8 else 0) + 4   # temperature 0x5C4800
                col["zone_id"] = crt.rand() % 10000000 + 1     # zone record +4
                col["id"] = next_id                            # column +0x20
                next_id += 1 + crt.rand() % 0x32
                col["sub"] = zone_sub                          # column +0x1C = zone subtype
                lvl = col["level"]
                if lvl < 5:
                    cls = 0
                elif lvl < 10:
                    cls = crt.rand() & 1
                elif lvl < 15:
                    cls = crt.rand() % 3
                elif lvl > 18:
                    cls = (crt.rand() & 3) + 1
                else:
                    cls = crt.rand() % 3 + 1
                col["zone_cls"] = cls                         # class goes to the zone record (+0xC), not the column
                j += 1
                continue
            pick = crt.rand() & 7
            base_neg = cell["base"] < 0
            if pick == 0:
                col["type"] = 2
            elif pick == 1:
                col["type"] = 3
                col["sub"] = crt.rand() % 3
            elif pick == 2:
                col["type"] = 0xF if base_neg else 4
            elif pick == 3:
                col["type"] = 5
                if cell["f4"] <= 0.8:
                    col["sub"] = 0
                elif cell["f3"] <= 0.8:
                    col["sub"] = 2 if cell["f3"] < 0.2 else 0
                else:
                    col["sub"] = 3
                r = f32(float((crt.rand() & 0xFF) + 0x100))
                col["radius"] = r
                span = f32(2048.0 - f32(r + 256.0) * 2.0)
                z0 = rz * 0x4000 + cz * 0x800
                x0 = rx * 0x4000 + cx * 0x800
                oz = int(f32(f32(f32(crt.rand() * span) / 32767.0) * 65536.0))
                col["z"] = z0 + (int(f32(r * 65536.0)) + oz) / 65536.0 + 256.0
                ox = int(f32(f32(f32(crt.rand() * span) / 32767.0) * 65536.0))
                col["x"] = x0 + (int(f32(f32(r + 256.0) * 65536.0)) + ox) / 65536.0
            elif pick == 4:
                col["type"] = 0xF if base_neg else 6
            elif pick == 5:
                col["type"] = 15 if base_neg else 7
            else:
                col["type"] = 0xB if pick == 6 else 0xC
                col["radius"] = 128.0
                col["x"] = float((int(col["x"]) & ~0xFF) + 0x80)
                col["z"] = float((int(col["z"]) & ~0xFF) + 0x80)
            lvl = col["level"]
            if lvl < 5:
                cls = 0
            elif lvl < 10:
                cls = crt.rand() & 1
            elif lvl < 15:
                cls = crt.rand() % 3
            elif lvl > 18:
                cls = (crt.rand() & 3) + 1
            else:
                cls = crt.rand() % 3 + 1
            col["cls"] = cls
            col["id"] = next_id
            next_id += 1 + crt.rand() % 0x32
            j += 1
        # five random remaining candidates become type 10
        for _ in range(5):
            if not order:
                break
            k = crt.rand() % len(order)
            _, cx, cz = order.pop(k)
            col = cols[cx * 8 + cz]
            col["type"] = 10
            col["id"] = crt.rand() % 10000000 + 1
        return {"rx": rx, "rz": rz, "level": level, "variant": variant, "orientation": orientation, "columns": cols}
