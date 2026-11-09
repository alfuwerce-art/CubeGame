"""Landmark-aware terrain port (extends terrain_port.Port) — mirrors the C# AlphaTerrainGenerator.

columns(x, z) -> column dict of the 2048-voxel column containing voxel (x, z) (region generated) or None.
Column dict keys: x, z (voxels), radius, base, type (see region_port.py).
"""
import math

from tools.alpha_models.world.terrain_port import Port, f32, noise, ss


class LandmarkPort(Port):
    def __init__(self, seeds, cells, column_lookup):
        super().__init__(seeds, cells)
        self.column_at = column_lookup

    # vec3_distanceSquared (0x5F8E10): squared distance / radius²; except Rock/Tree/Zone14 the point is displaced
    # by noise(x*0.0025+8432984, z*0.0025+90493)*256 and noise(x*0.0025+3423, z*0.0025+112)*256 (scale fitted to
    # executed values; Peak 0xD has its own variant and is not produced by generateRegion).
    @staticmethod
    def dist(col, x, z):
        r = col["radius"]
        if r < 0.001:
            return 0.0
        if col["type"] in (0xB, 0xC, 0xE):
            dx = f32(float(x - col["x"]))
            dz = f32(float(z - col["z"]))
        else:
            n1 = noise(x * 0.0025 + 8432984.0, z * 0.0025 + 90493.0)
            n2 = noise(x * 0.0025 + 3423.0, z * 0.0025 + 112.0)
            dx = f32(f32(n1 * 256.0) + float(x) - col["x"])
            dz = f32(f32(n2 * 256.0) + float(z) - col["z"])
        return f32(f32(f32(dz * dz) + f32(dx * dx)) / f32(r * r))

    @staticmethod
    def falloff(d):
        f = f32(1.0 - d)
        return f32(f * f) if f > 0.0 else 0.0

    # 0x5EF040: weight share of "rare" (flagged) climate cells around the warped point
    def region_cell(self, x, z):
        rng = [(rx, rz) for rx in range((x - 0x4000) >> 14, ((x + 0x4000) >> 14) + 1)
               for rz in range((z - 0x4000) >> 14, ((z + 0x4000) >> 14) + 1)]
        cells = [self.cell(*k) for k in rng]
        if any(c is None for c in cells) or not any(c["flag"] == 1 for c in cells):
            return 0.0
        n1 = noise(z * 0.0005, 3423.0)
        n2 = noise(x * 0.0005, 23421.0)
        px = int(f32(f32(float(x)) + f32(f32(n1 * 3.0) * 256.0)))
        pz = int(f32(f32(float(z)) + f32(f32(n2 * 3.0) * 256.0)))
        ds = [int(f32(f32(float(c["anchor_z"] - pz)) ** 2 + f32(float(c["anchor_x"] - px)) ** 2)) for c in cells]
        best = min(ds)
        total = flagged = 0.0
        for c, d in zip(cells, ds):
            w = f32(1.0 - min(1.0, f32(float(d - best) * 5e-07)))
            total = f32(total + w)
            if c["flag"] == 1:
                flagged = f32(flagged + w)
        return f32(flagged / total) if total > 0.0 else 0.0

    def height_at_xy(self, x, z):
        col = self.column_at(x, z)
        if col is not None and col["type"] == 1:
            return self.falloff(self.dist(col, x, z))
        return 0.0

    def sample_terrain_height(self, x, z):
        v = super().sample_terrain_height(x, z)
        col = self.column_at(x, z)
        if col is not None:
            t = col["type"]
            if t in (1, 2, 4, 13):
                v = f32(self.falloff(self.dist(col, x, z)) + v)
            if t in (6, 7):
                v = f32(self.falloff(self.dist(col, x, z)) * 0.5 + v)
        return f32(self.region_cell(x, z) + v)

    def rotate_and_place(self, x, z):
        r = self.height_at_xy(x, z)
        hx, hz = self.height_noise(x, z)
        near = self.nearest_edge(x, z)
        if r > 0.0:
            f = min(f32(r * 3.0), 1.0)
            f = f32(1.0 - f * f)
            w = f32(1.0 - f * f)
            c = f32(math.cos(hx * 360.0) * w + 1.0)
            if c < near:
                near = c
            c = f32(math.cos(hz * 360.0) * w + 1.0)
            if c < near:
                near = c
        if r > 0.65:
            f = f32((0.7 - r) / 0.05)
            near = f32((f if f > 0.0 else 0.0) * near)
        col = self.column_at(x, z)
        if col is not None and col["type"] in (2, 4):
            near = f32(self.falloff(self.dist(col, x, z)) * 2.0 + near)
        return near

    def mountain(self, x, z):
        nx, nz = x * 0.01, z * 0.01
        n0 = noise(nx, nz)
        k = f32(1.0 - n0 * n0)
        ox, oz = self.T(0x8001DC), self.T(0x8001E0)
        n1 = f32(noise(ox + nx + 843.0, oz + nz + 984.0) * 0.1)
        n2 = noise(x * 0.0025 + ox, z * 0.0025 + oz)
        v = f32(1.0 - abs(n1 + n2) * f32((1.0 - k * k) * 1.3 + 2.0))
        n3 = noise(x * 0.005 + 94.0, z * 0.005 + 874.0)
        v = f32(v * f32(n3 * 0.4 + 0.6))
        col = self.column_at(x, z)
        if col is not None and col["type"] in (6, 7):
            d = self.dist(col, x, z)
            if v < 1.0:
                rim = f32(1.0 - v) if f32(v + 0.5) > 1.0 else 0.5
            else:
                rim = 0.0
            if d > 0.36:
                if d < 1.0:
                    f = f32((f32(math.sqrt(d)) - 0.6) / 0.39999998)
                    f = f32(1.0 - f * f)
                    v = f32(f * f * rim + v)
            else:
                f = min(f32((1.0 - d / 0.36) * 1.5), 1.0)
                v = f32((rim + v) * f32(1.0 - f * f))
        if v >= 0.0:
            s = min(f32(self.sample_terrain_height(x, z) * 2.0), 1.0)
            v = f32(ss(s) * v)
            q = min(self.rotate_and_place(x, z), 1.0)
            v = f32(ss(q) * v)
            r = min(f32(self.height_at_xy(x, z) * 2.0), 1.0)
            r = f32(1.0 - ss(r))
            v = f32(r * r * r * v)
        else:
            v = 0.0
        if col is not None:
            t = col["type"]
            if t == 2:
                return f32(ss(v) + self.falloff(self.dist(col, x, z)))
            if t == 10 and col["radius"] > 0.0:
                plain = dict(col, type=0xB)          # the type-10 cut measures the plain (unwarped) distance
                d = self.dist(plain, x, z)
                if d < 1.0:
                    f = f32(1.0 - d)
                    v = f32(f32(1.0 - f * f) * v)
            if t in (4, 5):
                d = self.dist(col, x, z)
                if d > 0.25:
                    if d < 1.0:
                        f = f32((f32(math.sqrt(d)) - 0.5) * 2.0)
                        f = f32(1.0 - f * f)
                        v = f32(f32(1.0 - f * f) * v)
                else:
                    v = 0.0
        return f32(ss(v))

    def base(self, x, z):
        # identical to Port.base up to the noise sums, plus the landmark blocks (0x5C5E20)
        T = self.T
        fx, fz = float(x), float(z)

        def sq_half(off_a, off_b, scale):
            v = noise(T(off_a) + fx * scale, T(off_b) + fz * scale)
            h = f32((v + 1.0) * 0.5)
            return f32(h * h)

        l14c = sq_half(0x80018C, 0x800190, 0.0001)
        l148 = sq_half(0x800194, 0x800198, 0.0001)
        l160 = sq_half(0x80019C, 0x8001A0, 0.001)
        l158 = sq_half(0x8001A4, 0x8001A8, 0.001)
        l140 = sq_half(0x8001AC, 0x8001B0, 0.002)
        s = min(f32(self.sample_terrain_height(x, z) * 4.0), 1.0)
        l150 = f32(ss(s))
        l150 = f32(l150 * l150)
        le4 = f32(l150 * l160)
        l174 = f32(l150 * l158)
        l150 = f32(l150 * l140)
        n1 = noise(fz * 0.0005, 3423.0)
        n2 = noise(fx * 0.0005, 23421.0)
        offx = int(f32(f32(f32(n1 * 3.0) * 256.0) * 65536.0)) / 65536.0
        offz = int(f32(f32(f32(n2 * 3.0) * 256.0) * 65536.0)) / 65536.0
        px, pz = x + offx, z + offz
        rng_x = range((x - 0x4000) >> 14, ((x + 0x4000) >> 14) + 1)
        rng_z = range((z - 0x4000) >> 14, ((z + 0x4000) >> 14) + 1)
        found, dmin = False, 0.0
        for rx in rng_x:
            for rz in rng_z:
                c = self.cell(rx, rz)
                if c is None:
                    continue
                d = f32(f32(f32(c["anchor_z"] - pz) ** 2) + f32(f32(c["anchor_x"] - px) ** 2))
                if not found or d < dmin:
                    found, dmin = True, d
        weighted = 0.0
        land = 1.0
        if found:
            wsum = wbase = wland = 0.0
            for rx in rng_x:
                for rz in rng_z:
                    c = self.cell(rx, rz)
                    if c is None:
                        continue
                    d = f32(f32(f32(c["anchor_z"] - pz) ** 2) + f32(f32(c["anchor_x"] - px) ** 2))
                    t = min(f32((d - dmin) * 5e-08), 1.0)
                    w = f32((1.0 - t) * (1.0 - t))
                    wsum = f32(wsum + w)
                    wbase = f32(wbase + c["base"] * w)
                    if c["base"] > 0:
                        wland = f32(wland + w)
            weighted = f32(wbase / wsum)
            land = f32(wland / wsum)
        a = noise(T(0x8001BC) + fx * 0.0002, T(0x8001C0) + fz * 0.0002)
        l154 = f32((a + 1.0) * 100.0 * l148)
        b = noise(T(0x8001B4) + fx * 0.0002, T(0x8001B8) + fz * 0.0002)
        l154 = f32(f32(l154 + f32((b + 1.0) * 100.0 * l14c)) * land + weighted)
        r = self.height_at_xy(x, z)
        if r > 0.5:
            f = min(f32((r - 0.5) * 2.0), 1.0)
            l150 = f32(f32(1.0 - ss(f)) * l150)
        q = self.rotate_and_place(x, z)
        q = min(max(q, 0.02) * 2.0, 1.0)
        f = 1.0 - q
        f4 = f32(1.0 - f * f * f * f)
        k = f32(f4 * 0.1 + 0.9)
        l150 = f32((f4 * 0.5 + 0.5) * l150)
        l148 = f32(k * le4)
        l14c = f32(k * l174)
        col = self.column_at(x, z)
        if col is not None and col["type"] == 1:
            f = f32(1.0 - f32(self.falloff(self.dist(col, x, z)) * 0.5))
            l148 = f32(f * l148)
            l14c = f32(f * l14c)
            l150 = f32(f * l150)
        c1 = noise(T(0x8001CC) + fx * 0.002, T(0x8001D0) + fz * 0.002)
        out = f32((c1 + 1.0) * 50.0 * l14c)
        c2 = noise(T(0x8001C4) + fx * 0.002, T(0x8001C8) + fz * 0.002)
        out = f32(out + f32((c2 + 1.0) * 50.0 * l148))
        c3 = noise(T(0x8001D4) + fx * 0.01, T(0x8001D8) + fz * 0.01)
        out = f32(out + f32((c3 + 1.0) * 20.0 * l150) + l154)
        if col is not None and col["type"] != 0:
            near = self.nearest_cell_of(int(col["x"]), int(col["z"]))
            if near is not None and near["base"] < 0 and col["type"] != 0xB:
                a = noise(x * 0.0025 + 8432984.0, z * 0.0025 + 90493.0)
                dx = f32(f32(a * 100.0) + float(x) - col["x"])
                b = noise(x * 0.0025 + 3423.0, z * 0.0025 + 112.0)
                dz = f32(f32(f32(b * 100.0) + float(z)) - col["z"])
                rr = f32(col["radius"] + 256.0)
                f = min(f32(f32(dz * dz + dx * dx) / f32(rr * rr)), 1.0)
                if f32(1.0 - f) > 0.0:
                    g = min(f32((1.0 - f) * 1.1), 1.0)
                    out = f32(out - f32(ss(g) * float(near["base"])))
            t = col["type"]
            if t == 4:
                d = self.dist(col, x, z)
                if d > 0.25:
                    if d < 1.0:
                        f = f32((f32(math.sqrt(d)) - 0.5) * 2.0)
                        f = f32(1.0 - f * f)
                        out = f32(f32(f32(f32(col["base"] - 25.0) - out) * f) * f + out)
                else:
                    f = f32(1.0 - d * 4.0)
                    f = f32(f * f)
                    out = f32(f32(1.0 - f) * f32(col["base"] - 25.0) + f32(col["base"] - 50.0) * f)
            if t in (6, 7):
                d = self.dist(col, x, z)
                if d > 0.25:
                    if d < 1.0:
                        f = f32((f32(math.sqrt(d)) - 0.5) * 2.0)
                        f = f32(1.0 - f * f)
                        out = f32(f * f * 10.0 + out)
                else:
                    f = f32(1.0 - d * 4.0)
                    f = f32(f * f)
                    out = f32(f32(1.0 - f) * f32(out + 10.0) + f32(out - 30.0) * f)
                if out < weighted:
                    out = weighted
        return out

    def nearest_cell_of(self, x, z):
        """findNearestEntityInGrid (0x477E10) — the climate cell nearest to the warped point."""
        n1 = noise(z * 0.0005, 3423.0)
        n2 = noise(x * 0.0005, 23421.0)
        px = int(f32(f32(float(x)) + f32(f32(n1 * 3.0) * 256.0)))
        pz = int(f32(f32(float(z)) + f32(f32(n2 * 3.0) * 256.0)))
        best, best_d = None, 0
        for rx in range((x - 0x4000) >> 14, ((x + 0x4000) >> 14) + 1):
            for rz in range((z - 0x4000) >> 14, ((z + 0x4000) >> 14) + 1):
                c = self.cell(rx, rz)
                if c is None:
                    continue
                d = int(f32(f32(float(c["anchor_z"] - pz)) ** 2 + f32(float(c["anchor_x"] - px)) ** 2))
                if best is None or d < best_d:
                    best, best_d = c, d
        return best
