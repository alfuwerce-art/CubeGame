"""Reference port of Alpha terrain colour functions (verified against execution by compare_colors.py).

Functions take voxel coordinates (x, z), the voxel level y, and the climate of the column. `climate` supplies
humidity/temperature/rare-cell values exactly as the zone Field cell (+8, +4, +0xC) or the samplers do.
"""
from tools.alpha_models.world.terrain_port import f32, noise


def clamp255(v):
    return 0.0 if v < 0.0 else (255.0 if v > 255.0 else v)


def clamp3(r, g, b):
    return [clamp255(f32(r)), clamp255(f32(g)), clamp255(f32(b))]


class ColorPort:
    def __init__(self, seeds, landmarks):
        self.T = lambda off: seeds[off]
        self.land = landmarks          # LandmarkPort: humidity, temperature, region_cell, sample_terrain_height, column_at

    # 0x5EE540 World_getColumnDataAt: grass base colour
    def column_color(self, x, z):
        a = noise(x * 0.04, z * 0.04)
        f3 = f32((a + 1.0) * 0.5)
        b = noise(x * 0.005 + 45645.0, z * 0.005 + 456456.0)
        r = f32(-f32(b * 120.0))
        if r > 0.0:
            r = 0.0
        c = noise(x * 0.02 + 89648.0, z * 0.02 + 1649.0)
        f4 = f32(c * 80.0 + 1.0)
        d = noise(x * 0.005 + 342.0, z * 0.005 + 23423.0)
        f6 = f32(1.0 - f3)
        f5 = f32(f4 * f3)
        r = f32(f32(f5 + f32(f6 * 200.0)) + r)
        g = f32(f32(f32(f3 * 120.0) + f32(f6 * 230.0)) + f32(d * 30.0))
        bl = f32(f32(f5 + f32(f6 * f4)) + f32(b * 120.0))
        return clamp3(r, g, bl)

    # 0x5F9BC0 vec3_computeNoiseOffset: snow-ish tint for cold wet columns
    def cold_color(self, x, z):
        a = noise(self.T(0x800274) + x * 0.03, self.T(0x800278) + z * 0.03)
        b = noise(self.T(0x80027C) + x * 0.003, self.T(0x800280) + z * 0.003)
        f4 = f32(f32((a + 1.0) * 0.5) * 80.0)
        f3 = f32((b + 1.0) * 0.5)
        c = noise(x * 0.01 + 493.0, z * 0.01 + 789.0)
        f5 = f32(1.0 - f3)
        r = f32(f32(f5 * 255.0) + f32(f3 * 255.0) + f4)
        g = f32(f32(f5 * f32(c * 40.0 + 200.0)) + f32(f3 * 150.0) + f4)
        bl = f32(f32(f5 * 100.0) + f32(f3 * 50.0) + f4)
        return clamp3(r, g, bl)

    # 0x5C4520 terrain_biomeColorFromNoise: dry/sand tint
    def dry_color(self, x, z):
        a = noise(self.T(0x800274) + x * 0.03, self.T(0x800278) + z * 0.03)
        b = noise(self.T(0x80027C) + x * 0.01, self.T(0x800280) + z * 0.01)
        f5 = f32(f32((a + 1.0) * 0.5) * 80.0)
        f4 = f32((b + 1.0) * 0.5)
        f6 = f32((1.0 - f4) * 240.0)
        r = f32(f32(f6 + f32(f4 * 240.0)) + f5)
        g = f32(f32(f6 + f32(f4 * 180.0)) + f5)
        bl = f32(f32(f32((1.0 - f4) * 100.0) + f32(f4 * 50.0)) + f5)
        return clamp3(r, g, bl)

    # 0x5F9E50 vec2_store: water/ice blue
    def ice_color(self, x, z):
        a = noise(x * 0.04, z * 0.04)
        f3 = f32((a + 1.0) * 0.5)
        f2 = f32(1.0 - f3)
        return clamp3(f32(f3 * 190.0) + f32(f2 * 100.0), f32(f3 * 220.0) + f32(f2 * 180.0),
                      f32(f3 * 255.0) + f32(f2 * 255.0))

    # 0x5F9620 vec3_store4: soil/dirt colour at level y
    def soil_color(self, x, z, y, humidity, rare):
        a = noise(z * 0.1 + 98984.0, y * 0.4)
        b = noise(x * 0.1, y * 0.4)
        h = humidity
        r0, g0, b0 = 120.0, 120.0, 130.0
        if h >= 0.5:
            if h > 0.75:
                h = f32((h - 0.75) * 4.0)
                n = noise(h * 0.001 + 6544.0, z * 0.001 + 123.0)
                g0 = f32((1.0 - h) * 120.0)
                b0 = f32(f32((1.0 - h) * 130.0) + f32(h * 100.0))
                r0 = f32(g0 + f32(h * 200.0))
                g0 = f32(g0 + f32(f32(n * 20.0 + 130.0) * h))
        else:
            h = f32(h * 2.0)
            k = f32(1.0 - h)
            r0 = f32(f32(k * 10.0) + f32(h * 160.0))
            g0 = f32(f32(k * 20.0) + f32(h * 160.0))
            b0 = f32(f32(k * 50.0) + f32(h * 170.0))
        n5 = noise(self.T(0x80022C) + x * 0.005, self.T(0x800230) + z * 0.005)
        base = f32(f32(f32(f32((a + b) * 0.5) + 1.0) * 0.5) * 160.0)
        rgb = clamp3(f32(base + r0 + 0.0), f32(base + g0 + 0.0), f32(f32(base + b0) + f32((n5 + 1.0) * 10.0)))
        if rare > 0.0:
            p = noise(x * 0.05, z * 0.05)
            q = noise(x * 0.02, z * 0.02)
            v = f32(f32(p * 0.1) + q)
            if v > 1.0:
                v = 1.0
            k7 = f32(1.0 - abs(v))
            k7 = f32(k7 * k7 * k7 * k7 * k7)
            inv = f32(1.0 - rare)
            s = noise(x * 0.1, z * 0.1)
            rgb = [f32(f32(rgb[0] * inv) + f32(f32(k7 * 200.0 + 10.0) * rare)),
                   f32(f32(rgb[1] * inv) + f32(f32(f32(f32(s + 1.0) * k7) * 50.0 + 10.0) * rare)),
                   f32(f32(rgb[2] * inv) + f32(rare * 10.0))]
        return rgb

    # 0x5C7140 terrain_rockColor_blend
    def rock_color(self, x, z, y, humidity, rare):
        a = noise(z * 0.1 + 98984.0, y * 0.4 + 8437.0)
        b = noise(x * 0.1, y * 0.4)
        k = f32(f32(f32((a + b) * 0.5) + 1.0) * 0.5)
        dx, dz = x * 0.01, z * 0.01
        n7 = noise(self.T(0x80025C) + dx, self.T(0x800260) + dz)
        n8 = noise(self.T(0x800264) + dx, self.T(0x800268) + dz)
        n9 = noise(self.T(0x80026C) + dx, self.T(0x800270) + dz)
        k60 = f32(k * 60.0)
        n10 = noise(self.T(0x80024C) + dx, self.T(0x800250) + dz)
        n11 = noise(self.T(0x800254) + dx, self.T(0x800258) + dz)
        bl = f32(f32(f32(k * 0.0) + 60.0) + f32(n9 * 20.0))
        r = f32(f32(f32(k60 + f32(n10 * 20.0)) + 180.0) + f32(n7 * 20.0))
        g = f32(f32(f32(k60 + f32(n11 * 20.0)) + 100.0) + f32(n8 * 20.0))
        out = clamp3(r, g, bl)
        h = humidity
        if h > 0.75:
            t = f32((h - 0.75) * 4.0)
            inv = f32(1.0 - t)
            s = self.soil_color(x, z, y, self.land.humidity(x, z), rare)
            out = [f32(f32(f32(s[i] * t) * 0.9) + f32(out[i] * inv)) for i in range(3)]
        if h < 0.2:
            t = f32(1.0 - h * 4.0)
            inv = f32(1.0 - t)
            s = self.soil_color(x, z, y, self.land.humidity(x, z), rare)
            ice = self.ice_color(x, z)
            half = f32(t * 0.5)
            out = [f32(f32(f32(ice[i] + s[i]) * half) + f32(out[i] * inv)) for i in range(3)]
        if rare > 0.0:
            inv = f32(1.0 - rare)
            s = self.soil_color(x, z, y, self.land.humidity(x, z), rare)
            out = [f32(f32(out[i] * inv) + f32(s[i] * rare)) for i in range(3)]
        return out

    # 0x5C56E0 terrain_surfaceColor_blend -> (r, g, b, material byte)
    def surface_color(self, x, z, y, temperature, humidity, rare):
        material = 4
        rgb = self.column_color(x, z)
        sth = self.land.sample_terrain_height(x, z)
        k2c = f32(f32(f32(sth * 10.0) - 0.3) * 1.5)
        k2c = 0.0 if k2c < 0.0 else (1.0 if k2c > 1.0 else k2c)
        if humidity > 0.8:
            t = min(f32((humidity - 0.8) / 0.1), 1.0)
            n = noise(x * 0.03, z * 0.03)
            n = 0.0 if n < 0.0 else (1.0 if n > 1.0 else n)
            n = f32(1.0 - n * n)
            mix = f32(f32(f32(1.0 - n * n) * 0.8) * t)
            inv = f32(1.0 - mix)
            s = self.soil_color(x, z, y, self.land.humidity(x, z), rare)
            rgb = [f32(f32(s[i] * mix) + f32(rgb[i] * inv)) for i in range(3)]
        if temperature < 0.2 and humidity > 0.75:
            material = 9
            t = f32(f32(f32(f32(1.0 - temperature / 0.2) * f32(humidity - 0.75)) * 4.0) * 10.0)
            t = 0.0 if t < 0.0 else (1.0 if t > 1.0 else t)
            col = self.land.column_at(x, z)
            if col is not None and col["type"] == 4:
                d = f32(self.land.dist(col, x, z) - 0.5)
                d = 0.0 if d < 0.0 else (1.0 if d > 1.0 else d)
                d = f32(1.0 - d * d)
                t = f32(f32(1.0 - d * d) * t)
            t = f32(t * k2c)
            if t <= 1.0:
                if t < 0.5:
                    material = 4
            else:
                t = 1.0
            inv = f32(1.0 - t)
            c = self.cold_color(x, z)
            rgb = [f32(f32(c[i] * t) + f32(rgb[i] * inv)) for i in range(3)]
        n = noise(x * 0.01 + 854.0, z * 0.01 + 985.0)
        f = f32(f32(n + f32(float(0xF - y) / 10.0)) - 0.5)
        f = 0.0 if f < 0.0 else (1.0 if f > 1.0 else f)
        f = f32(1.0 - f * f)
        k = f32(1.0 - f * f)
        if humidity < 0.2:
            k = f32(k * f32(humidity / 0.2))
        if k > 0.0:
            inv = f32(1.0 - k)
            d = self.dry_color(x, z)
            rgb = [f32(f32(d[i] * k) + f32(rgb[i] * inv)) for i in range(3)]
            material = 9
        if humidity < 0.3:
            material = 10
            k = min(f32(1.0 - f32((humidity - 0.29) / 0.01)), 1.0)
            inv = f32(1.0 - k)
            ice = self.ice_color(x, z)
            rgb = [f32(f32(ice[i] * k) + f32(rgb[i] * inv)) for i in range(3)]
        if rare > 0.0:
            inv = f32(1.0 - rare)
            s = self.soil_color(x, z, y, self.land.humidity(x, z), rare)
            rgb = [f32(f32(s[i] * rare) + f32(rgb[i] * inv)) for i in range(3)]
            if rare > 0.5:
                material = 12
        return [int(rgb[0]) & 0xFF, int(rgb[1]) & 0xFF, int(rgb[2]) & 0xFF, material | 0x20]
