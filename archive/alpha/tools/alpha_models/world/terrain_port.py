"""Reference port of Alpha terrain base/mountain (voxel units) — mirrors Features/World/AlphaTerrainGenerator.cs.

Compared against EMULATED_ALPHA_CODE samples from terrain_oracle.py by compare_port.py. Float locals of the
shipped code are rounded to float32 where the decompilation shows a float variable; noise is the C# port.
Region landmark rules (column types) are absent, matching the oracle's empty region grid.
"""
import math
import struct


def f32(v):
    return struct.unpack("<f", struct.pack("<f", v))[0]


def noise(x, y):
    ix, iy = int(x), int(y)
    fx, fy = float(ix), float(iy)
    m = 0xFFFFFFFF

    def mix(h):
        h &= m
        return (h ^ (h * 0x2000)) & m

    def node(h):
        v = ((((h * h) & m) * 0xEC4D + 0x131071F) * h + 0xD208DD0D) & 0x7FFFFFFF
        return 1.0 - v * 9.313225746154785e-10

    row0 = (iy * 0x39) & m
    row1 = (int(fy + 1.0) * 0x39) & m
    x1 = int(fx + 1.0)
    g00, g10 = mix(ix + row0), mix(row0 + x1)
    g01, g11 = mix(ix + row1), mix(row1 + x1)
    pi = 3.1415927410125732
    wx = (1.0 - math.cos((x - fx) * pi)) * 0.5
    wy = (1.0 - math.cos((y - fy) * pi)) * 0.5
    top = node(g00) * (1.0 - wx) + node(g10) * wx
    bot = node(g01) * (1.0 - wx) + node(g11) * wx
    return f32(top * (1.0 - wy) + bot * wy)


def ss(f):
    return f * 3.0 * f - f * 2.0 * f * f


class Port:
    def __init__(self, seeds, cells):
        """seeds: offset -> value (0x800168..0x800298, 0x800188); cells: (rx, rz) -> dict(anchor_x, anchor_z, base)."""
        self.s = seeds
        self.cells = cells

    def T(self, off):
        return self.s[off]

    def cell(self, rx, rz):
        if rx < 0 or rz < 0 or rx >= 1024 or rz >= 1024:
            return None
        return self.cells.get((rx, rz))

    # 0x59FC90: warped point in region units (1/16384)
    def height_noise(self, x, z):
        nx, nz, nx2, nz2 = x * 0.01, z * 0.01, x * 0.0005, z * 0.0005
        a = noise(self.T(0x800204) + nx, self.T(0x800208) + nz)
        b = noise(self.T(0x8001FC) + nx2, self.T(0x800200) + nz2)
        hx = (a * 0.1 + b) * 500.0 * 6.103515625e-05 + x * 6.103515625e-05
        a = noise(self.T(0x800214) + nx, self.T(0x800218) + nz)
        b = noise(self.T(0x80020C) + nx2, self.T(0x800210) + nz2)
        hz = (a * 0.1 + b) * 500.0 * 6.103515625e-05 + z * 6.103515625e-05
        return hx, hz

    # 0x5CADE0: region of the climate anchor nearest to the domain-warped point
    def nearest_cell(self, x, z):
        n1 = noise(z * 0.0005, 3423.0)
        n2 = noise(x * 0.0005, 23421.0)
        wx = int(f32(f32(float(x)) + f32(f32(n1 * 3.0) * 256.0)))
        wz = int(f32(f32(float(z)) + f32(f32(n2 * 3.0) * 256.0)))
        best, best_d, best_key = None, 0, None
        for rx in range((x - 0x4000) >> 14, ((x + 0x4000) >> 14) + 1):
            for rz in range((z - 0x4000) >> 14, ((z + 0x4000) >> 14) + 1):
                c = self.cell(rx, rz)
                if c is None:
                    continue
                dx = f32(float(c["anchor_x"] - wx))
                dz = f32(float(c["anchor_z"] - wz))
                d = int(f32(f32(dz * dz) + f32(dx * dx)))
                if best is None or d < best_d:
                    best, best_d, best_key = c, d, (rx, rz)
        return best_key

    @staticmethod
    def seg_d2(p, a, b):
        dx3, dx4 = b[0] - p[0], a[0] - p[0]
        dy5, dy2 = a[1] - p[1], b[1] - p[1]
        L = dx4 * dx4 + dy5 * dy5
        if L < 9.999999682655225e-21:
            return f32(dy2 * dy2 + dx3 * dx3)
        t = (dy2 * dy5 + dx3 * dx4) / L
        if t <= 0.0:
            return f32(dy2 * dy2 + dx3 * dx3)
        if t >= 1.0:
            ex, ey = b[0] - a[0], b[1] - a[1]
            return f32(ey * ey + ex * ex)
        dx3 -= dx4 * t
        dy2 -= dy5 * t
        return f32(dy2 * dy2 + dx3 * dx3)

    # 0x5EEA60: squared distance (·0.0002) from the warped point to edges nearest anchor -> 4 neighbour anchors
    def nearest_edge(self, x, z):
        key = self.nearest_cell(x, z)
        if key is None:
            return None
        rx, rz = key
        p = (float(self.cells[key]["anchor_x"]), float(self.cells[key]["anchor_z"]))
        hx, hz = self.height_noise(x, z)
        b = (hx * 16384.0, hz * 16384.0)
        best = -1.0
        for nk in ((rx - 1, rz), (rx + 1, rz), (rx, rz - 1), (rx, rz + 1)):
            c = self.cell(*nk)
            if c is None:
                continue
            d = self.seg_d2(p, (float(c["anchor_x"]), float(c["anchor_z"])), b)
            if best < 0.0 or d < best:
                best = d
        if best < 0.0:
            return None
        return f32(best * 0.0002)

    # 0x5F9340 (no landmark columns, no chunk cell; region cell term 0 as in the oracle)
    def sample_terrain_height(self, x, z):
        a = noise(self.T(0x800170) + x * 0.01, self.T(0x800174) + z * 0.01)
        b = noise(self.T(0x800168) + x * 0.001, self.T(0x80016C) + z * 0.001)
        v = f32(a * 0.1 + b)
        c = noise(x * 0.001, z * 0.001)
        v = f32(abs(v) * f32((c + 1.0) * 0.1 + 0.8))
        near = self.nearest_edge(x, z)
        k = f32(1.0 - near * 0.75)
        if k > 0.0:
            v = f32(k * k * 0.05 + v)
        return v

    def rotate_and_place(self, x, z):
        return self.nearest_edge(x, z)   # height-at-world-xy = 0 without landmark columns

    # 0x5EFFA0
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
        if v < 0.0:
            return 0.0   # ss(0) = 0
        s = f32(self.sample_terrain_height(x, z) * 2.0)
        s = min(s, 1.0)
        v80 = f32(ss(s) * v)
        q = self.rotate_and_place(x, z)
        q = min(q, 1.0)
        v68 = f32(ss(q) * v80)
        r = 0.0
        r = 1.0 - ss(min(r * 2.0, 1.0))
        v = f32(r * r * r * v68)
        return f32(ss(v))   # final smoothstep of the mask (end of 0x5EFFA0)

    # 0x5C5E20
    def base(self, x, z):
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

        # weighted climate base with the same domain warp as 0x5CADE0 (16.16 offsets)
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
                dx = f32(c["anchor_x"] - px)
                dz = f32(c["anchor_z"] - pz)
                d = f32(f32(dz * dz) + f32(dx * dx))
                if not found or d < dmin:
                    found, dmin = True, d
        base = 0.0
        land = 1.0
        if found:
            wsum = wbase = wland = 0.0
            for rx in rng_x:
                for rz in rng_z:
                    c = self.cell(rx, rz)
                    if c is None:
                        continue
                    dx = f32(c["anchor_x"] - px)
                    dz = f32(c["anchor_z"] - pz)
                    t = min(f32((f32(f32(dz * dz) + f32(dx * dx)) - dmin) * 5e-08), 1.0)
                    w = f32((1.0 - t) * (1.0 - t))
                    wsum = f32(wsum + w)
                    wbase = f32(wbase + c["base"] * w)
                    if c["base"] > 0:
                        wland = f32(wland + w)
            base = f32(wbase / wsum)
            land = f32(wland / wsum)
        a = noise(T(0x8001BC) + fx * 0.0002, T(0x8001C0) + fz * 0.0002)
        l154 = f32((a + 1.0) * 100.0 * l148)
        b = noise(T(0x8001B4) + fx * 0.0002, T(0x8001B8) + fz * 0.0002)
        l154 = f32(f32(l154 + f32((b + 1.0) * 100.0 * l14c)) * land + base)
        # height-at-world-xy = 0 (no landmark columns) -> no reduction of l150
        q = self.rotate_and_place(x, z)
        q = max(q, 0.02) * 2.0
        q = min(q, 1.0)
        f = 1.0 - q
        f4 = f32(1.0 - f * f * f * f)
        k = f32(f4 * 0.1 + 0.9)
        l150 = f32((f4 * 0.5 + 0.5) * l150)
        l148 = f32(k * le4)
        l14c = f32(k * l174)
        c1 = noise(T(0x8001CC) + fx * 0.002, T(0x8001D0) + fz * 0.002)
        out = f32((c1 + 1.0) * 50.0 * l14c)
        c2 = noise(T(0x8001C4) + fx * 0.002, T(0x8001C8) + fz * 0.002)
        out = f32(out + f32((c2 + 1.0) * 50.0 * l148))
        c3 = noise(T(0x8001D4) + fx * 0.01, T(0x8001D8) + fz * 0.01)
        out = f32(out + f32((c3 + 1.0) * 20.0 * l150) + l154)
        return out
