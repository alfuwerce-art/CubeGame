"""Reference port of the voxel-column part of Alpha WorldInfo_generateBiomeContent (0x5E4850).

Field cell per voxel column: temperature, humidity, rare term, `bottom` (Y of voxels[0]) and `voxels` (list of
[r, g, b, flags]); flags & 0x1F = material (0 air, 1 default solid, 2 water, 3 lake bed, 4 grass, 6 stone,
9 snow/sand, 10 shore, 12 rare), 0x20 surface, 0x40 covered cave air, 0x80 explicitly written.
Read rule (array_atChecked4 + defaults): y < bottom -> (200,200,200,1); in array -> stored (air below y=1 reads as
water when not covered); above -> water (255,255,255,0x82) for y <= 0 else air.
Mirrors the C# AlphaZoneGenerator; compared with zone_oracle.py by compare_zone.py.
"""
import math

from tools.alpha_models.world.terrain_port import f32, noise, ss

WATER = [255, 255, 255, 0x82]
DEFAULT = [200, 200, 200, 1]
AIR = [0, 0, 0, 0]            # zero fill of a grown voxel array
AIR_CONST = [255, 255, 255, 0]  # DAT_0076DDF8: air written by the road/river clearing and read above a column


def to_byte(v):
    return int(v) & 0xFF


class Field:
    __slots__ = ("t", "h", "rare", "bottom", "s8", "voxels")

    def __init__(self, t, h, rare):
        self.t, self.h, self.rare = t, h, rare
        self.bottom = 0
        self.s8 = 0
        self.voxels = []

    def read(self, y):
        if y < self.bottom:
            return DEFAULT
        i = y - self.bottom
        if i < len(self.voxels):
            v = self.voxels[i]
            if (v[3] & 0x1F) == 0 and y < 1 and (v[3] & 0x40) == 0:
                return WATER
            return v
        return WATER if y <= 0 else AIR_CONST

    def set_at(self, index, v):
        """sparseArray_setAt."""
        if index < 0:
            self.voxels[0:0] = [list(AIR) for _ in range(-index)]
            self.bottom += index
            self.voxels[0] = list(v)
            return
        while len(self.voxels) <= index:
            self.voxels.append(list(AIR))
        cell = self.voxels[index]
        if cell[3] & 0x80:
            if (v[3] & 0x1F) == 0:
                return
            self.voxels[index] = [v[0], v[1], v[2], v[3] | 0x80]
            return
        self.voxels[index] = list(v)


class ZonePort:
    def __init__(self, land, colors, rand):
        self.land = land            # LandmarkPort with landmark columns
        self.colors = colors        # ColorPort
        self.rand = rand            # callable -> next CRT rand() of the zone stream
        self.T = land.T
        self.spawns = []

    def write(self, f, x, z, y, v):
        """tilemap_writeGlyphColumn (0x44E7A0)."""
        if not (v[3] != 0 or y < f.bottom + len(f.voxels)):
            return
        old_bottom = f.bottom
        if y < 1 and (v[3] & 0x5F) == 0:
            v = WATER
        f.set_at(y - old_bottom, v)
        yy = y + 1
        while yy < old_bottom:
            s = self.colors.soil_color(x, z, yy, f.h, f.rare)
            f.set_at(yy - f.bottom, [to_byte(s[0]), to_byte(s[1]), to_byte(s[2]), 6])
            yy += 1

    def generate(self, zx, zz, limit=None):
        """Columns in Alpha order (x outer, z inner); `limit` = number of columns (oracle probes)."""
        land = self.land
        x0, z0 = zx * 256, zz * 256
        fields = {}
        base = {}

        def base_at(x, z):
            if (x, z) not in base:
                base[(x, z)] = land.base(x, z)
            return base[(x, z)]

        zone_col = land.column_at(x0, z0)
        ztype = zone_col["type"] if zone_col else 0
        done = 0
        for x in range(x0, x0 + 256):
            for z in range(z0, z0 + 256):
                if limit is not None and done >= limit:
                    return fields
                f = Field(land.temperature(x, z), land.humidity(x, z), land.region_cell(x, z))
                fields[(x, z)] = f
                for key in ((x, z), (x + 1, z), (x, z + 1)):
                    base_at(*key)
                self.column(f, x, z, base, zone_col, ztype)
                done += 1
        return fields

    def column(self, f, x, z, base, col, ctype):
        land, colors = self.land, self.colors
        b = base[(x, z)]
        steep = abs(f32(b - base[(x + 1, z)])) > 0.3 or abs(f32(b - base[(x, z + 1)])) > 0.3
        m = land.mountain(x, z)
        dx, dz = float(x), float(z)
        a = noise(x * 0.04 + 432.0, dz * 0.04 + 432.0)
        bb = noise(x * 0.08 + 432.0, dz * 0.08 + 432.0)
        r1 = f32(1.0 - abs(f32(a + f32(bb * 0.05))))
        r1 = f32(1.0 - r1 * r1 * r1)
        t1 = f32(r1 * r1)
        c = noise(x * 0.08 + 4234.0, dz * 0.08 + 234.0) * 0.05
        d = noise(x * 0.05 + 423432.0, dz * 0.05 + 54352.0)
        r2 = f32(1.0 - abs(c + d))
        r2 = f32(1.0 - r2 * r2 * r2)
        t2 = f32(r2 * r2 + t1)
        md = m
        if ctype in (6, 13):
            ff = land.falloff(land.dist(col, x, z)) * 2.0
            ff = min(ff, 1.0)
            ff = f32(1.0 - ff * ff)
            k = f32(1.0 - ff * ff)
            s = f32(noise(x * 0.01 + 985.0, dz * 0.01 + 98584.0) * 1.3 + 1.0)
            s = 0.0 if s < 0.0 else (1.0 if s > 1.0 else s)
            md = f32(f32(f32(ss(s) * k) * 0.4) + m)
        mt = f32(md * t2)
        k = f32(mt * 1.25)
        k = 0.0 if k < 0.0 else (1.0 if k > 1.0 else k)
        rockw = f32(ss(k))
        n = noise(x * 0.01 + 34432.0, dz * 0.01 + 8992.0)
        height = f32(f32(f32(f32(n + 1.5) * 60.0) * m) + f32(mt * 8.0) + b)
        surf = int(height)
        sc = colors.surface_color(x, z, surf, f.t, f.h, f.rare)
        mat = sc[3]
        if rockw > 0.5:
            mat = 6
        soil = colors.soil_color(x, z, surf, f.h, f.rare)
        inv = f32(1.0 - rockw)
        top_rgb = [to_byte(f32(f32(sc[i] * inv) + f32(soil[i] * rockw))) for i in range(3)]
        f.bottom = surf
        md = f32(md * 8.0)                   # local_12f8 *= 8, then clamped to 1
        if md > 1.0:
            md = 1.0
        if not steep:
            rock = colors.rock_color(x, z, surf, f.h, f.rare)
            kk = f32(1.0 - md)
            half = f32(md * 0.5)
            v0 = [to_byte(f32(f32(f32(top_rgb[i] + soil[i]) * half) + f32(rock[i] * kk))) for i in range(3)] + [6]
        else:
            v0 = top_rgb + [mat]
        f.voxels = [v0, top_rgb + [mat]]
        f.s8 = surf - 8
        # caves
        roof = f32(f32(f32(noise(x * 0.01 + 435.0, dz * 0.01 + 847.0) + 1.0) * 20.0) + b)
        if roof < height:
            ca = noise(x * 0.005 + self.T(0x800178), dz * 0.005 + self.T(0x80017C))
            cb = noise(self.T(0x800178) + x * 0.01, self.T(0x80017C) + dz * 0.01)
            fv = f32(1.0 - abs(f32(ca + cb)) * 4.0)
            if fv < 0.0:
                return
            g = f32(1.0 - fv * fv)
            s1 = f32(1.0 - g * g)
            g2 = f32(1.0 - fv * fv)
            s2 = f32(1.0 - g2 * g2 * g2 * g2)
            c3 = noise(x * 0.03 + 7635.0, dz * 0.03 + 123847.0)
            tt = f32(f32(f32(c3 * 10.0) + f32(f32(height - roof) - 20.0)) / 10.0)
            tt = 0.0 if tt < 0.0 else (1.0 if tt > 1.0 else tt)
            q = f32(1.0 - tt * tt)
            rocky = f32(1.0 - q * q)
            u = f32(1.0 - tt)
            u = f32(u * u * u * u * u * u * u)
            top = f32(f32(f32(f32(f32(m * 20.0) + 4.0) * s1) + roof) * f32(1.0 - u) + f32(height + 2.0) * u)
            floor_raw = f32(f32(f32(t2 * m) * 6.0) + roof)
            bottom = f32(f32(f32(1.0 - s2) * height) + f32(s2 * floor_raw) + 2.0)
            if floor_raw < f32(height - 2.0):
                inv2 = f32(1.0 - rocky)
                y = surf
                for i in range(2):
                    v = f.read(y)
                    s = colors.soil_color(x, z, int(f32(float(i) + height)), f.h, f.rare)
                    self.write(f, x, z, y, [to_byte(f32(f32(v[k] * rocky) + f32(s[k] * inv2))) for k in range(3)] + [v[3]])
                    y += 1
            if top <= bottom:
                return
            flag = 0x40 if top < height else 0
            y = int(bottom)
            while float(y) < top:
                self.write(f, x, z, y, [0, 0, 0, 2] if y < 1 else [0, 0, 0, flag])
                y += 1
            fy = int(f32(bottom - 1.0))
            sc2 = colors.surface_color(x, z, fy, f.t, f.h, f.rare)
            mat2 = sc2[3]
            if rockw > 0.5 or rocky > 0.5:
                mat2 = 6
            k74 = f32(1.0 - rocky)
            p = colors.soil_color(x, z, fy, f.h, f.rare)
            r = colors.rock_color(x, z, fy, f.h, f.rare)
            k98 = f32(rocky * 0.4)
            mix = [to_byte(f32(f32(f32(p[i] + r[i]) * k98) + f32(sc2[i] * k74))) for i in range(3)]
            s3 = colors.soil_color(x, z, fy, f.h, f.rare)
            final = [to_byte(f32(f32(mix[i] * inv) + f32(s3[i] * rockw))) for i in range(3)] + [mat2]
            self.write(f, x, z, fy, final)
            if f32(top - bottom) > 2.0 and (x + z * 3) % 7 == 0 and self.rand() % 30 == 0:
                kind = self.rand() & 3
                if kind == 0:
                    if top <= height and (self.rand() & 3) == 0:
                        self.rand()                       # spawn angle
                        self.spawns.append(("cave_object_0x130b", x, z, fy + 1))
                elif kind == 1:
                    if top <= height:
                        r10 = self.rand() % 10
                        if r10 == 3:
                            self.rand()
                        self.spawns.append(("cave_creature", x, z, fy + 1, r10))
                elif kind == 2:
                    if top <= height:
                        self.spawns.append(("cave_creature_timed", x, z, fy + 1))
                else:
                    self.spawns.append(("cave_object", x, z, int(f32(bottom + 2.0))))
        if ctype == 4:
            dd = land.dist(col, x, z)
            if dd < 0.25:
                y = int(f32(col["base"] - 25.0))
                while f.bottom <= y:
                    v = f.read(y)
                    if (v[3] & 0x1F) in (0, 2):
                        w = WATER
                    else:
                        s = colors.soil_color(x, z, y, f.h, f.rare)
                        w = [to_byte(s[0]), to_byte(s[1]), to_byte(s[2]), 3]
                    self.write(f, x, z, y, w)
                    y -= 1


# ---------------------------------------------------------------- after the column loop (0x5E6B3E .. 0x5E84C3)

def _clamp01(v):
    return 0.0 if v < 0.0 else (1.0 if v > 1.0 else v)


class ZoneFeatures:
    """Terrain passes of generateBiomeContent that follow the column loop, before the first content call:
    canyon spires / rock (carveB 0x5CBE00), generic rock blobs and mesas (carveB / carveA 0x5CB600), roads and
    village streets (0x5F9F70 edge measure), rivers (0x5F9340). The giant tree of Tree (0xC) zones
    (0x5DF960) is vegetation content: recorded as a candidate, its rand() calls are not consumed here.
    """

    def __init__(self, zone, fields, zx, zz, zone_rec, spawn, orientation):
        self.z = zone                # ZonePort (land, colors, rand, write)
        self.f = fields
        self.zx, self.zz = zx, zz
        self.rec = zone_rec          # zone grid record dict(type, sub, ...)
        self.spawn = spawn           # (x, z) floats of world+0x8000F0/F4
        self.orientation = orientation
        self.candidates = []
        self.record_carves = None    # list -> carves are recorded (like zone_oracle.py's stubs) instead of applied

    def top(self, x, z):
        f = self.f[(x, z)]
        return f.bottom + len(f.voxels)

    def far_from_spawn(self, x, z):
        dx = f32(float(x) - self.spawn[0])
        dz = f32(float(z) - self.spawn[1])
        return 3600.0 <= f32(dz * dz) + f32(dx * dx)

    def _shape(self, x, z, y, cx, cz, rx, rz, a_mode, check):
        if a_mode:
            yy = y * (0.05 if check else 0.02)
            k = 0.4 if check else 0.5
            u = f32(f32(noise(z * 0.05, yy) * k) + f32(float(x - cx) / float(rx)))
            v = noise(x * 0.05 + 4374.0, yy + 9898.0) * k + f32(float(z - cz) / float(rz))
        else:
            u = f32(f32(noise(z * 0.05, y * 0.05) * f32(10.0 / rx)) + f32(float(x - cx) / float(rx)))
            v = noise(x * 0.05 + 4374.0, y * 0.05 + 9898.0) * f32(10.0 / rz) + f32(float(z - cz) / float(rz))
        return v * v + u * u <= 1.0

    def _top0(self, x, z, base_h, height, a_mode):
        n = noise(x * 0.01 + 4394.0, z * 0.01 + 8974.0)
        scale = float(height) if a_mode else 20.0
        return int(f32(n * scale) + f32(float(int(f32(float(base_h + height))))))

    def _blocked(self, cx, cz, base_h, rx, rz, height, a_mode):
        """First pass: an explicitly placed (0x80) voxel inside the volume aborts the feature."""
        for x in range(cx - 2 * rx, cx + 2 * rx + 1):
            for z in range(cz - 2 * rz, cz + 2 * rz + 1):
                f = self.f.get((x, z))
                if f is None:
                    continue
                y = self._top0(x, z, base_h, height, a_mode)
                while f.bottom <= y:
                    if self._shape(x, z, y, cx, cz, rx, rz, a_mode, True):
                        w = f.read(y)
                        if (a_mode or (w[3] & 0x1F) != 2) and (w[3] & 0x80):
                            return True
                    y -= 1
        return False

    def carve(self, cx, cz, base_h, rx, rz, height, a_mode):
        """carveB 0x5CBE00 (stone blob, material 6) / carveA 0x5CB600 (mesa, soil→rock gradient, material 5)."""
        if self.record_carves is not None:
            self.record_carves.append({"kind": "A" if a_mode else "B", "cx": cx, "cz": cz, "base_h": base_h,
                                       "rx": rx, "rz": rz, "height": height})
            return
        if self._blocked(cx, cz, base_h, rx, rz, height, a_mode):
            return
        colors = self.z.colors
        for x in range(cx - 2 * rx, cx + 2 * rx + 1):
            for z in range(cz - 2 * rz, cz + 2 * rz + 1):
                top0 = self._top0(x, z, base_h, height, a_mode)
                f = self.f.get((x, z))
                if f is None or top0 < f.bottom:
                    continue
                low = -8 - f.bottom
                y = top0
                while f.bottom <= y:
                    w = f.read(y)
                    if (w[3] & 0x1F) in (0, 2, 4, 9, 5) and (w[3] & 0x40) == 0 \
                            and self._shape(x, z, y, cx, cz, rx, rz, a_mode, False):
                        if y == top0:
                            vox = list(colors.surface_color(x, z, y, f.t, f.h, f.rare))
                        elif a_mode:
                            t = _clamp01(f32(float(low + y) * 0.05))
                            s = colors.soil_color(x, z, y, f.h, f.rare)
                            r = colors.rock_color(x, z, y, f.h, f.rare)
                            k = f32(1.0 - t)
                            vox = [to_byte(f32(f32(r[i] * k) + f32(s[i] * t))) for i in range(3)] + [5]
                        else:
                            s = colors.soil_color(x, z, y, f.h, f.rare)
                            vox = [to_byte(s[0]), to_byte(s[1]), to_byte(s[2]), 6]
                        self.z.write(f, x, z, y, vox)
                    y -= 1

    def run(self):
        land, rand = self.z.land, self.z.rand
        zx, zz = self.zx, self.zz
        x0, z0 = zx * 256, zz * 256
        col = land.column_at(x0, z0)
        ctype = col["type"] if col else 0
        centre = self.f[(x0 + 128, z0 + 128)]
        temperature, humidity = centre.t, centre.h
        if ctype == 6:                                         # Canyon: spires on a 3×3 lattice (float* += 0x40 = 0x100)
            for i in range(0, 0x300, 0x100):
                for j in range(0, 0x300, 0x100):
                    if rand() & 3 == 0:
                        continue
                    px, pz = x0 + i // 3 + 0x2A, z0 + j // 3 + 0x2A
                    w = f32(1.0 - land.dist(col, px, pz))
                    if not (0.0 < w and 0.5 <= f32(w * w)):
                        continue
                    a = rand() % 10 + 20
                    b = rand() % 10 + 20
                    c = (rand() & 15) + 20
                    if self.far_from_spawn(px, pz):
                        self.carve(px, pz, self.top(px, pz), a, b, c, False)
        in_zone = col is not None and (int(col["x"]) >> 8) == zx and (int(col["z"]) >> 8) == zz
        if ctype == 0xB and in_zone:                           # Rock landmark
            h = rand() % 100 + 100
            self.carve(x0 + 128, z0 + 128, self.top(x0 + 128, z0 + 128), 100, 100, h, False)
        elif ctype == 0xC and in_zone:                         # Tree landmark: giant tree (vegetation content)
            rand()
            self.candidates.append(("landmark_giant_tree", x0 + 128, z0 + 128, self.top(x0 + 128, z0 + 128)))
        elif self.rec["type"] not in (4, 1, 3):
            n = rand() & 3
            if (humidity > 0.6 or humidity < 0.3) and (temperature > 0.7 or (temperature < 0.4 and (temperature > 0.2 or humidity < 0.8))):
                n += 2
            for _ in range(n):
                a = rand() % 40 + 10
                b = rand() % 40 + 10
                c = rand() % 25 + 10
                px = rand() % ((64 - a) * 4) + (a + zx * 128) * 2
                pz = rand() % ((64 - b) * 4) + (b + zz * 128) * 2
                if not self.far_from_spawn(px, pz):
                    continue
                if land.height_at_xy(px, pz) > 0.6:
                    continue
                s = land.sample_terrain_height(px, pz)
                if not (f32(1.0 - f32(s * 50.0)) < 0.0):
                    continue
                m = land.mountain(px, pz)
                top = self.top(px, pz)
                if rand() & 1 == 0 or 0.25 <= m:
                    self.carve(px, pz, top, a, b, c, False)
                else:
                    self.carve(px, pz, top, a, b, c // 2, True)
        self.roads(x0, z0)
        self.rivers(x0, z0)

    def roads(self, x0, z0):
        """Roads between regions and village streets: cut to base height, cobbles inside villages (material 11)."""
        land, colors = self.z.land, self.z.colors
        for x in range(x0, x0 + 256):
            for z in range(z0, z0 + 256):
                e = land.rotate_and_place(x, z)
                w = f32(1.0 - f32(e * 50.0))
                v = land.height_at_xy(x, z)
                if w < 0.0:
                    continue
                base = land.base(x, z)
                if base < 0.0:
                    base = 0.0
                f = self.f[(x, z)]
                y = int(f32(base + 1.0))
                k = f32(1.0 - w)
                k = f32(1.0 - f32(f32(k * k) * k))
                n = noise(x * 0.02 + 55432.0, z * 0.02 + 974.0)
                top = int(f32(f32(f32(n + 1.0) * 4.0) + f32(k * 5.0)) + base)
                c = f.read(y)
                if (c[3] & 0x1F) in (0, 2) or (c[3] & 0x40):
                    continue
                n2 = f32(noise(x * 0.05 + 843.0, z * 0.05 + 984.0))
                road = [f32(f32(40.0 * n2) + 140.0)] * 3
                hx, hz = (x // 2) * 0xEA, (z // 2) * 0xEA
                if noise(float(hx + 0x12E2), float(hz + 0xC11A)) > 0.5:
                    n3 = f32(noise(float(hx), float(hz + 0x31)))
                    road = [f32(f32(20.0 * n3) + road[i]) for i in range(3)]
                if self.orientation in (1, 4, 5):
                    road = list(colors.rock_color(x, z, y, f.h, f.rare))
                rock = colors.rock_color(x, z, y, f.h, f.rare)
                inv = f32(1.0 - v)
                rgb = []
                for i in range(3):
                    val = f32(f32(rock[i] * inv) + f32(v * road[i]))
                    val = 0.0 if val < 0.0 else (255.0 if val > 255.0 else val)
                    rgb.append(to_byte(val))
                self.z.write(f, x, z, y - 1, rgb + [11])
                if v > 0.92:
                    top = f.bottom + len(f.voxels)
                yy = y
                while yy < top:
                    if (f.read(yy)[3] & 0x40) == 0:
                        self.z.write(f, x, z, yy, list(AIR_CONST))
                    yy += 1

    def rivers(self, x0, z0):
        """Rivers: water terraced in 5-voxel steps on base height, river bed (material 3), banks cleared."""
        land, colors, rand = self.z.land, self.z.colors, self.z.rand
        for x in range(x0, x0 + 256):
            for z in range(z0, z0 + 256):
                s = land.sample_terrain_height(x, z)
                w = f32(1.0 - f32(s * 50.0))
                if w < 0.0:
                    continue
                if land.height_at_xy(x, z) > 0.95:
                    continue
                f = self.f[(x, z)]
                base = land.base(x, z)
                if base < 0.0:
                    base = 0.0
                step = (int(base) // 5) * 5
                fs = float(step)
                frac = f32(f32(base - fs) / 5.0)
                if 0.5 <= frac:
                    d = f32(1.0 - f32(f32(frac - 0.5) * 4.0))
                    if d < 0.0:
                        d = f32(f32(f32(d + 1.0) * f32(d + 1.0)) - 1.0)
                else:
                    d = f32(frac * 2.0)
                bed = step
                if d < 0.0:
                    bed = int(f32(fs - f32(d * 5.0)))
                y = int(f32(f32(fs - f32(d * 5.0)) + 2.0))
                while y <= step:
                    if (f.read(y)[3] & 0x40) == 0:
                        k = f32(1.0 - d)
                        self.z.write(f, x, z, y, [0, 0, to_byte(f32(f32(k * 255.0) + f32(d * 0.0))), 2])
                    y += 1
                c = f.read(bed)
                if step <= bed and (c[3] & 0x1F) != 2 and (c[3] & 0x40) == 0:
                    sc = colors.soil_color(x, z, bed, f.h, f.rare)
                    self.z.write(f, x, z, bed, [to_byte(sc[0]), to_byte(sc[1]), to_byte(sc[2]), 3])
                    if rand() % 200 == 0:
                        self.candidates.append(("river_object", x, z, bed))
                k = f32(1.0 - w)
                k = f32(1.0 - f32(f32(k * k) * k))
                n = noise(x * 0.02 + 55432.0, z * 0.02 + 974.0)
                top = int(f32(f32(f32(n + 1.0) * 2.0) + f32(k * 5.0)) + base)
                yy = bed + 1
                while yy < top:
                    if (f.read(yy)[3] & 0x40) == 0:
                        self.z.write(f, x, z, yy, list(AIR_CONST))
                    yy += 1
