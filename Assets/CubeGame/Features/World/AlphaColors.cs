using System;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Alpha terrain colours, verified byte-for-byte against the executed functions
    /// (tools/alpha_models/world/compare_colors.py, 300 samples): grass base 0x5EE540, cold tint 0x5F9BC0,
    /// dry tint 0x5C4520, ice 0x5F9E50, soil 0x5F9620 ("vec3_store4"), rock 0x5C7140, surface 0x5C56E0.
    /// Humidity / rare term are the zone Field values of the column (+8 / +0xC).
    /// </summary>
    public sealed class AlphaColors
    {
        private readonly AlphaTerrainGenerator _terrain;
        private readonly AlphaWorldSeeds _seeds;

        public AlphaColors(AlphaTerrainGenerator terrain)
        {
            _terrain = terrain;
            _seeds = terrain.Seeds;
        }

        private static float N(double x, double y) => AlphaNoise.Sample(x, y);

        private static float C255(float v) => v < 0f ? 0f : (v > 255f ? 255f : v);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public void GrassColor(int x, int z, out float r, out float g, out float b)
        {
            float f3 = (N(x * 0.04, z * 0.04) + 1.0f) * 0.5f;
            float nb = N(x * 0.005 + 45645.0, z * 0.005 + 456456.0);
            float rr = -(nb * 120.0f);
            if (rr > 0f)
            {
                rr = 0f;
            }

            float f4 = N(x * 0.02 + 89648.0, z * 0.02 + 1649.0) * 80.0f + 1.0f;
            float nd = N(x * 0.005 + 342.0, z * 0.005 + 23423.0);
            float f6 = 1.0f - f3;
            float f5 = f4 * f3;
            r = C255(f5 + f6 * 200.0f + rr);
            g = C255(f3 * 120.0f + f6 * 230.0f + nd * 30.0f);
            b = C255(f5 + f6 * f4 + nb * 120.0f);
        }

        private void ColdColor(int x, int z, out float r, out float g, out float b)
        {
            float a = N(_seeds.At(0x800274) + x * 0.03, _seeds.At(0x800278) + z * 0.03);
            float bb = N(_seeds.At(0x80027C) + x * 0.003, _seeds.At(0x800280) + z * 0.003);
            float f4 = (a + 1.0f) * 0.5f * 80.0f;
            float f3 = (bb + 1.0f) * 0.5f;
            float c = N(x * 0.01 + 493.0, z * 0.01 + 789.0);
            float f5 = 1.0f - f3;
            r = C255(f5 * 255.0f + f3 * 255.0f + f4);
            g = C255(f5 * (c * 40.0f + 200.0f) + f3 * 150.0f + f4);
            b = C255(f5 * 100.0f + f3 * 50.0f + f4);
        }

        private void DryColor(int x, int z, out float r, out float g, out float b)
        {
            float a = N(_seeds.At(0x800274) + x * 0.03, _seeds.At(0x800278) + z * 0.03);
            float bb = N(_seeds.At(0x80027C) + x * 0.01, _seeds.At(0x800280) + z * 0.01);
            float f5 = (a + 1.0f) * 0.5f * 80.0f;
            float f4 = (bb + 1.0f) * 0.5f;
            float f6 = (1.0f - f4) * 240.0f;
            r = C255(f6 + f4 * 240.0f + f5);
            g = C255(f6 + f4 * 180.0f + f5);
            b = C255((1.0f - f4) * 100.0f + f4 * 50.0f + f5);
        }

        private static void IceColor(int x, int z, out float r, out float g, out float b)
        {
            float f3 = (N(x * 0.04, z * 0.04) + 1.0f) * 0.5f;
            float f2 = 1.0f - f3;
            r = C255(f3 * 190.0f + f2 * 100.0f);
            g = C255(f3 * 220.0f + f2 * 180.0f);
            b = C255(f3 * 255.0f + f2 * 255.0f);
        }

        /// <summary>0x5F9620 ("vec3_store4"): soil / dirt colour at level y.</summary>
        public void SoilColor(int x, int z, int y, float humidity, float rare, out float r, out float g, out float b)
        {
            float a = N(z * 0.1 + 98984.0, y * 0.4);
            float bb = N(x * 0.1, y * 0.4);
            float h = humidity;
            float r0 = 120f;
            float g0 = 120f;
            float b0 = 130f;
            if (h >= 0.5f)
            {
                if (h > 0.75f)
                {
                    h = (h - 0.75f) * 4.0f;
                    float n = N(h * 0.001 + 6544.0, z * 0.001 + 123.0);
                    g0 = (1.0f - h) * 120.0f;
                    b0 = (1.0f - h) * 130.0f + h * 100.0f;
                    r0 = g0 + h * 200.0f;
                    g0 = g0 + (n * 20.0f + 130.0f) * h;
                }
            }
            else
            {
                h *= 2.0f;
                float k = 1.0f - h;
                r0 = k * 10.0f + h * 160.0f;
                g0 = k * 20.0f + h * 160.0f;
                b0 = k * 50.0f + h * 170.0f;
            }

            float n5 = N(_seeds.At(0x80022C) + x * 0.005, _seeds.At(0x800230) + z * 0.005);
            float baseV = (((a + bb) * 0.5f) + 1.0f) * 0.5f * 160.0f;
            r = C255(baseV + r0 + 0.0f);
            g = C255(baseV + g0 + 0.0f);
            b = C255(baseV + b0 + (n5 + 1.0f) * 10.0f);
            if (rare > 0f)
            {
                float v = N(x * 0.05, z * 0.05) * 0.1f + N(x * 0.02, z * 0.02);
                if (v > 1.0f)
                {
                    v = 1.0f;
                }

                float k7 = 1.0f - Math.Abs(v);
                k7 = k7 * k7 * k7 * k7 * k7;
                float inv = 1.0f - rare;
                float s = N(x * 0.1, z * 0.1);
                r = r * inv + (k7 * 200.0f + 10.0f) * rare;
                g = g * inv + ((s + 1.0f) * k7 * 50.0f + 10.0f) * rare;
                b = b * inv + rare * 10.0f;
            }
        }

        /// <summary>0x5C7140: rock colour at level y.</summary>
        public void RockColor(int x, int z, int y, float humidity, float rare, out float r, out float g, out float b)
        {
            float a = N(z * 0.1 + 98984.0, y * 0.4 + 8437.0);
            float bb = N(x * 0.1, y * 0.4);
            float k = ((a + bb) * 0.5f + 1.0f) * 0.5f;
            double dx = x * 0.01;
            double dz = z * 0.01;
            float n7 = N(_seeds.At(0x80025C) + dx, _seeds.At(0x800260) + dz);
            float n8 = N(_seeds.At(0x800264) + dx, _seeds.At(0x800268) + dz);
            float n9 = N(_seeds.At(0x80026C) + dx, _seeds.At(0x800270) + dz);
            float k60 = k * 60.0f;
            float n10 = N(_seeds.At(0x80024C) + dx, _seeds.At(0x800250) + dz);
            float n11 = N(_seeds.At(0x800254) + dx, _seeds.At(0x800258) + dz);
            b = C255(k * 0.0f + 60.0f + n9 * 20.0f);
            r = C255(k60 + n10 * 20.0f + 180.0f + n7 * 20.0f);
            g = C255(k60 + n11 * 20.0f + 100.0f + n8 * 20.0f);
            float h = humidity;
            if (h > 0.75f)
            {
                float t = (h - 0.75f) * 4.0f;
                float inv = 1.0f - t;
                SoilColor(x, z, y, humidity, rare, out float sr, out float sg, out float sb);
                r = sr * t * 0.9f + r * inv;
                g = sg * t * 0.9f + g * inv;
                b = sb * t * 0.9f + b * inv;
            }

            if (h < 0.2f)
            {
                float t = 1.0f - h * 4.0f;
                float inv = 1.0f - t;
                SoilColor(x, z, y, humidity, rare, out float sr, out float sg, out float sb);
                IceColor(x, z, out float ir, out float ig, out float ib);
                float half = t * 0.5f;
                r = (ir + sr) * half + r * inv;
                g = (ig + sg) * half + g * inv;
                b = (ib + sb) * half + b * inv;
            }

            if (rare > 0f)
            {
                float inv = 1.0f - rare;
                SoilColor(x, z, y, humidity, rare, out float sr, out float sg, out float sb);
                r = r * inv + sr * rare;
                g = g * inv + sg * rare;
                b = b * inv + sb * rare;
            }
        }

        /// <summary>0x5C56E0: surface voxel (r, g, b, flags) for a column with the given Field climate.</summary>
        public uint SurfaceColor(int x, int z, int y, float temperature, float humidity, float rare, float fieldHumidity)
        {
            int material = 4;
            GrassColor(x, z, out float r, out float g, out float b);
            float k2c = Clamp01((_terrain.SampleTerrainHeight(x, z) * 10.0f - 0.3f) * 1.5f);
            if (humidity > 0.8f)
            {
                float t = Math.Min((humidity - 0.8f) / 0.1f, 1.0f);
                float n = Clamp01(N(x * 0.03, z * 0.03));
                n = 1.0f - n * n;
                float mix = (1.0f - n * n) * 0.8f * t;
                float inv = 1.0f - mix;
                SoilColor(x, z, y, fieldHumidity, rare, out float sr, out float sg, out float sb);
                r = sr * mix + r * inv;
                g = sg * mix + g * inv;
                b = sb * mix + b * inv;
            }

            if (temperature < 0.2f && humidity > 0.75f)
            {
                material = 9;
                float t = Clamp01((1.0f - temperature / 0.2f) * (humidity - 0.75f) * 4.0f * 10.0f);
                LandmarkColumn column = _terrain.ColumnAt(x, z);
                if (column != null && column.Type == LandmarkType.Lake)
                {
                    float d = Clamp01(_terrain.LandmarkDistance2(column, x, z) - 0.5f);
                    d = 1.0f - d * d;
                    t = (1.0f - d * d) * t;
                }

                t *= k2c;
                if (t <= 1.0f)
                {
                    if (t < 0.5f)
                    {
                        material = 4;
                    }
                }
                else
                {
                    t = 1.0f;
                }

                float inv = 1.0f - t;
                ColdColor(x, z, out float cr, out float cg, out float cb);
                r = cr * t + r * inv;
                g = cg * t + g * inv;
                b = cb * t + b * inv;
            }

            float f = Clamp01((N(x * 0.01 + 854.0, z * 0.01 + 985.0) + (0xF - y) / 10.0f) - 0.5f);
            f = 1.0f - f * f;
            float k = 1.0f - f * f;
            if (humidity < 0.2f)
            {
                k *= humidity / 0.2f;
            }

            if (k > 0f)
            {
                float inv = 1.0f - k;
                DryColor(x, z, out float dr, out float dg, out float db);
                r = dr * k + r * inv;
                g = dg * k + g * inv;
                b = db * k + b * inv;
                material = 9;
            }

            if (humidity < 0.3f)
            {
                material = 10;
                float t = Math.Min(1.0f - (humidity - 0.29f) / 0.01f, 1.0f);
                float inv = 1.0f - t;
                IceColor(x, z, out float ir, out float ig, out float ib);
                r = ir * t + r * inv;
                g = ig * t + g * inv;
                b = ib * t + b * inv;
            }

            if (rare > 0f)
            {
                float inv = 1.0f - rare;
                SoilColor(x, z, y, fieldHumidity, rare, out float sr, out float sg, out float sb);
                r = sr * rare + r * inv;
                g = sg * rare + g * inv;
                b = sb * rare + b * inv;
                if (rare > 0.5f)
                {
                    material = 12;
                }
            }

            return Voxel.Pack((int)r, (int)g, (int)b, material | 0x20);
        }
    }
}
