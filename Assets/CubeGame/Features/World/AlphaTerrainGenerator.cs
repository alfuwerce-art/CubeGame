using System;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>Generated data of one terrain column (voxels).</summary>
    public readonly struct TerrainColumn
    {
        public TerrainColumn(int surface, bool rock, float mountain, float baseHeight)
        {
            Surface = surface;
            Rock = rock;
            Mountain = mountain;
            BaseHeight = baseHeight;
        }

        /// <summary>Top solid voxel Y (Alpha <c>surfaceY = int(height)</c>, Field +0x10).</summary>
        public int Surface { get; }

        /// <summary>Alpha material 6 (stone) when smoothstep(1.25·m·t2) &gt; 0.5.</summary>
        public bool Rock { get; }

        /// <summary>Mountain mask m (0x5EFFA0).</summary>
        public float Mountain { get; }

        /// <summary>Base height (0x5C5E20).</summary>
        public float BaseHeight { get; }
    }

    /// <summary>Region record (Alpha <c>generateRegion</c> 0x5DA280).</summary>
    public readonly struct RegionInfo
    {
        public RegionInfo(RegionKey key, int level, int variant, int orientation, AlphaClimateCell climate)
        {
            Key = key;
            Level = level;
            Variant = variant;
            Orientation = orientation;
            Climate = climate;
        }

        public RegionKey Key { get; }

        /// <summary>Region +0xC = <c>FUN_005A4710</c>: 1 in the centre, else 2 + trunc(0.75·distance) (DIRECT).</summary>
        public int Level { get; }

        /// <summary>Region +0x10: rand()%5 when level &gt; 4, else 0 (DIRECT).</summary>
        public int Variant { get; }

        /// <summary>Region +0x14: rand()&amp;3, or 4 + (rand()&amp;1) when climate Field4 &gt; 0.81 (DIRECT).</summary>
        public int Orientation { get; }

        public AlphaClimateCell Climate { get; }
    }

    public interface IWorldGenerator
    {
        string GeneratorId { get; }

        uint MasterSeed { get; }

        /// <summary>Water surface Y; columns whose surface is below it are under water.</summary>
        int WaterLevel { get; }

        TerrainColumn Column(int x, int z);

        RegionInfo Region(RegionKey region);
    }

    /// <summary>
    /// Alpha natural terrain in voxel coordinates.
    ///
    /// <b>Verified against executed Cube.exe code</b> (EMULATED_ALPHA_CODE fixture, FullM0AlphaOracleTests):
    /// climate cells 0x5D7A70, base height 0x5C5E20 (with 0x5F9340, 0x5EEA60, 0x5CADE0, 0x5F9F70, 0x59FC90,
    /// 0x5EF5A0) and the mountain mask 0x5EFFA0 — for a world without region landmark columns.
    /// <b>RECONSTRUCTED</b> (inline in 0x5E4850, constants from the decompilation, inputs verified):
    /// ridges t1/t2, height = (n+1.5)·60·m + m·t2·8 + base, rock flag.
    /// <b>Not yet ported</b> (PARTIAL): region landmark columns (types 1/2/4/5/6/7/10/13 — villages, craters, peaks…),
    /// the dome of types 6/13, the region-cell term 0x5EF040 and the 256-cell height cache (both 0 in a fresh world),
    /// caves, rivers/roads, water level (0 = hypothesis, U3/Q-2), surface colours.
    /// </summary>
    public sealed class AlphaTerrainGenerator : IWorldGenerator
    {
        public const string Id = "cubegame.alpha_terrain/2";

        public const int DefaultWaterLevel = 0;

        private readonly AlphaWorldSeeds _seeds;
        private readonly AlphaClimateGrid _climate;

        public AlphaTerrainGenerator(uint masterSeed)
        {
            MasterSeed = masterSeed;
            _seeds = new AlphaWorldSeeds(masterSeed);
            _climate = new AlphaClimateGrid(_seeds, WorldLayout.StartVoxel, WorldLayout.StartVoxel);
        }

        public string GeneratorId => Id;

        public uint MasterSeed { get; }

        public int WaterLevel => DefaultWaterLevel;

        public AlphaWorldSeeds Seeds => _seeds;

        public AlphaClimateGrid Climate => _climate;

        public RegionInfo Region(RegionKey region)
        {
            if (!WorldLayout.IsInside(region))
            {
                return new RegionInfo(region, 0, 0, 0, null);
            }

            AlphaClimateCell climate = _climate.Get(region.X, region.Z);
            var rng = new AlphaCrtRandom(unchecked(_seeds.WorldSeed + region.Z * 0x400 + region.X));
            int level = Level(region.X, region.Z);
            int variant = 0;
            if (level > 4)
            {
                variant = rng.Next() % 5;
            }

            int orientation = rng.Next() & 3;
            rng.Next(); // local_2AC = rand() % 10000 (consumed; purpose UNKNOWN)
            if (climate != null && climate.Field4 > 0.81f)
            {
                orientation = (rng.Next() & 1) + 4;
            }

            return new RegionInfo(region, level, variant, orientation, climate);
        }

        /// <summary>Alpha <c>FUN_005A4710</c>.</summary>
        public static int Level(int rx, int rz)
        {
            if (rx == 0x200 && rz == 0x200)
            {
                return 1;
            }

            float dz = 0x200 - rz;
            float dx = 0x200 - rx;
            double d = Math.Sqrt(dz * dz + dx * dx);
            return 2 - (int)((float)d * -0.75f);
        }

        public TerrainColumn Column(int x, int z)
        {
            double dx = x;
            double dz = z;
            float baseHeight = Base(x, z);
            float m = Mountain(x, z);

            float a = AlphaNoise.Sample(dx * 0.04 + 432.0, dz * 0.04 + 432.0);
            float b = AlphaNoise.Sample(dx * 0.08 + 432.0, dz * 0.08 + 432.0);
            float r1 = 1.0f - Math.Abs(a + b * 0.05f);
            r1 = 1.0f - r1 * r1 * r1;
            float t1 = r1 * r1;

            double c = AlphaNoise.Sample(dx * 0.08 + 4234.0, dz * 0.08 + 234.0) * 0.05;
            float d = AlphaNoise.Sample(dx * 0.05 + 423432.0, dz * 0.05 + 54352.0);
            float r2 = (float)(1.0 - Math.Abs(c + d));
            r2 = 1.0f - r2 * r2 * r2;
            float t2 = r2 * r2 + t1;

            float mt = m * t2;
            float k = Clamp01(mt * 1.25f);
            float rockWeight = Smooth(k);

            float n = AlphaNoise.Sample(dx * 0.01 + 34432.0, dz * 0.01 + 8992.0);
            float height = (n + 1.5f) * 60.0f * m + mt * 8.0f + baseHeight;
            return new TerrainColumn((int)height, rockWeight > 0.5f, m, baseHeight);
        }

        // ------------------------------------------------------------ 0x5C5E20 base height
        public float Base(int x, int z)
        {
            double fx = x;
            double fz = z;
            float l14c = SquaredHalf(0x80018C, 0x800190, fx, fz, 0.0001);
            float l148 = SquaredHalf(0x800194, 0x800198, fx, fz, 0.0001);
            float l160 = SquaredHalf(0x80019C, 0x8001A0, fx, fz, 0.001);
            float l158 = SquaredHalf(0x8001A4, 0x8001A8, fx, fz, 0.001);
            float l140 = SquaredHalf(0x8001AC, 0x8001B0, fx, fz, 0.002);

            float s = Math.Min(SampleTerrainHeight(x, z) * 4.0f, 1.0f);
            float l150 = Smooth(s);
            l150 = l150 * l150;
            float le4 = l150 * l160;
            float l174 = l150 * l158;
            l150 = l150 * l140;

            // Distance-weighted climate base around the domain-warped point (16.16 offsets).
            float n1 = AlphaNoise.Sample(fz * 0.0005, 3423.0);
            float n2 = AlphaNoise.Sample(fx * 0.0005, 23421.0);
            double px = x + (int)((n1 * 3.0f * 256.0f) * 65536.0f) / 65536.0;
            double pz = z + (int)((n2 * 3.0f * 256.0f) * 65536.0f) / 65536.0;
            int rx0 = (x - 0x4000) >> 14;
            int rx1 = (x + 0x4000) >> 14;
            int rz0 = (z - 0x4000) >> 14;
            int rz1 = (z + 0x4000) >> 14;

            bool found = false;
            float dmin = 0f;
            for (int rx = rx0; rx <= rx1; rx++)
            {
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    AlphaClimateCell cell = _climate.Get(rx, rz);
                    if (cell == null)
                    {
                        continue;
                    }

                    float d2 = AnchorDistance2(cell, px, pz);
                    if (!found || d2 < dmin)
                    {
                        found = true;
                        dmin = d2;
                    }
                }
            }

            float baseValue = 0f;
            float land = 1f;
            if (found)
            {
                float wsum = 0f;
                float wbase = 0f;
                float wland = 0f;
                for (int rx = rx0; rx <= rx1; rx++)
                {
                    for (int rz = rz0; rz <= rz1; rz++)
                    {
                        AlphaClimateCell cell = _climate.Get(rx, rz);
                        if (cell == null)
                        {
                            continue;
                        }

                        float t = Math.Min((AnchorDistance2(cell, px, pz) - dmin) * 5e-08f, 1.0f);
                        float w = (1.0f - t) * (1.0f - t);
                        wsum += w;
                        wbase += cell.BaseHeight * w;
                        if (cell.BaseHeight > 0)
                        {
                            wland += w;
                        }
                    }
                }

                baseValue = wbase / wsum;
                land = wland / wsum;
            }

            float a = AlphaNoise.Sample(_seeds.At(0x8001BC) + fx * 0.0002, _seeds.At(0x8001C0) + fz * 0.0002);
            float l154 = (a + 1.0f) * 100.0f * l148;
            float b = AlphaNoise.Sample(_seeds.At(0x8001B4) + fx * 0.0002, _seeds.At(0x8001B8) + fz * 0.0002);
            l154 = (l154 + (b + 1.0f) * 100.0f * l14c) * land + baseValue;

            // Terrain_sampleHeightAtWorldXY = 0 without landmark columns: no reduction of l150.
            float q = Math.Min(Math.Max(RotateAndPlace(x, z), 0.02f) * 2.0f, 1.0f);
            float f = 1.0f - q;
            float f4 = 1.0f - f * f * f * f;
            float k = f4 * 0.1f + 0.9f;
            l150 = (f4 * 0.5f + 0.5f) * l150;
            l148 = k * le4;
            l14c = k * l174;

            float c1 = AlphaNoise.Sample(_seeds.At(0x8001CC) + fx * 0.002, _seeds.At(0x8001D0) + fz * 0.002);
            float result = (c1 + 1.0f) * 50.0f * l14c;
            float c2 = AlphaNoise.Sample(_seeds.At(0x8001C4) + fx * 0.002, _seeds.At(0x8001C8) + fz * 0.002);
            result += (c2 + 1.0f) * 50.0f * l148;
            float c3 = AlphaNoise.Sample(_seeds.At(0x8001D4) + fx * 0.01, _seeds.At(0x8001D8) + fz * 0.01);
            result = result + (c3 + 1.0f) * 20.0f * l150 + l154;
            return result;
        }

        // ------------------------------------------------------------ 0x5EFFA0 mountain mask
        public float Mountain(int x, int z)
        {
            double nx = x * 0.01;
            double nz = z * 0.01;
            double ox = _seeds.At(0x8001DC);
            double oz = _seeds.At(0x8001E0);

            float n0 = AlphaNoise.Sample(nx, nz);
            float k = 1.0f - n0 * n0;
            float n1 = AlphaNoise.Sample(ox + nx + 843.0, oz + nz + 984.0) * 0.1f;
            float n2 = AlphaNoise.Sample(x * 0.0025 + ox, z * 0.0025 + oz);
            float v = 1.0f - Math.Abs(n1 + n2) * ((1.0f - k * k) * 1.3f + 2.0f);
            float n3 = AlphaNoise.Sample(x * 0.005 + 94.0, z * 0.005 + 874.0);
            v *= n3 * 0.4f + 0.6f;
            if (v < 0f)
            {
                return 0f;
            }

            float s = Math.Min(SampleTerrainHeight(x, z) * 2.0f, 1.0f);
            v = Smooth(s) * v;
            float q = Math.Min(RotateAndPlace(x, z), 1.0f);
            v = Smooth(q) * v;
            // Terrain_sampleHeightAtWorldXY = 0 → factor (1 − smoothstep(0))³ = 1.
            return Smooth(v);
        }

        // ------------------------------------------------------------ helpers
        /// <summary>0x5F9340 (no landmark columns; region-cell and 256-cell terms are 0 in a fresh world).</summary>
        public float SampleTerrainHeight(int x, int z)
        {
            float a = AlphaNoise.Sample(_seeds.At(0x800170) + x * 0.01, _seeds.At(0x800174) + z * 0.01);
            float b = AlphaNoise.Sample(_seeds.At(0x800168) + x * 0.001, _seeds.At(0x80016C) + z * 0.001);
            float v = a * 0.1f + b;
            float c = AlphaNoise.Sample(x * 0.001, z * 0.001);
            v = Math.Abs(v) * ((c + 1.0f) * 0.1f + 0.8f);
            float near = NearestEdge(x, z);
            float k = 1.0f - near * 0.75f;
            if (k > 0f)
            {
                v = k * k * 0.05f + v;
            }

            return v;
        }

        /// <summary>0x5F9F70 without landmark columns: equals the nearest-edge measure.</summary>
        public float RotateAndPlace(int x, int z)
        {
            return NearestEdge(x, z);
        }

        /// <summary>
        /// 0x5EEA60: squared distance (·0.0002) from the warped point (0x59FC90) to the edges between the nearest
        /// climate anchor (0x5CADE0) and the anchors of its four neighbour regions (0x5EF5A0).
        /// </summary>
        public float NearestEdge(int x, int z)
        {
            if (!NearestCell(x, z, out int rx, out int rz))
            {
                return 0f;
            }

            AlphaClimateCell center = _climate.Get(rx, rz);
            double px = center.AnchorX;
            double pz = center.AnchorZ;
            WarpedPoint(x, z, out double bx, out double bz);
            float best = -1f;
            Consider(_climate.Get(rx - 1, rz), px, pz, bx, bz, ref best);
            Consider(_climate.Get(rx + 1, rz), px, pz, bx, bz, ref best);
            Consider(_climate.Get(rx, rz - 1), px, pz, bx, bz, ref best);
            Consider(_climate.Get(rx, rz + 1), px, pz, bx, bz, ref best);
            return best < 0f ? 0f : best * 0.0002f;
        }

        private static void Consider(AlphaClimateCell neighbour, double px, double pz, double bx, double bz, ref float best)
        {
            if (neighbour == null)
            {
                return;
            }

            float d = SegmentDistance2(px, pz, neighbour.AnchorX, neighbour.AnchorZ, bx, bz);
            if (best < 0f || d < best)
            {
                best = d;
            }
        }

        /// <summary>0x5EF5A0: squared distance from b to the segment p→a.</summary>
        private static float SegmentDistance2(double px, double pz, double ax, double az, double bx, double bz)
        {
            double dx3 = bx - px;
            double dx4 = ax - px;
            double dy5 = az - pz;
            double dy2 = bz - pz;
            double len = dx4 * dx4 + dy5 * dy5;
            if (len < 9.999999682655225e-21)
            {
                return (float)(dy2 * dy2 + dx3 * dx3);
            }

            double t = (dy2 * dy5 + dx3 * dx4) / len;
            if (t <= 0.0)
            {
                return (float)(dy2 * dy2 + dx3 * dx3);
            }

            if (t >= 1.0)
            {
                double ex = bx - ax;
                double ey = bz - az;
                return (float)(ey * ey + ex * ex);
            }

            dx3 -= dx4 * t;
            dy2 -= dy5 * t;
            return (float)(dy2 * dy2 + dx3 * dx3);
        }

        /// <summary>0x59FC90 in voxels: point displaced by up to ±500 voxels of noise.</summary>
        private void WarpedPoint(int x, int z, out double wx, out double wz)
        {
            double nx = x * 0.01;
            double nz = z * 0.01;
            double nx2 = x * 0.0005;
            double nz2 = z * 0.0005;
            float a = AlphaNoise.Sample(_seeds.At(0x800204) + nx, _seeds.At(0x800208) + nz);
            float b = AlphaNoise.Sample(_seeds.At(0x8001FC) + nx2, _seeds.At(0x800200) + nz2);
            double hx = ((double)a * 0.1 + b) * 500.0 * 6.103515625e-05 + x * 6.103515625e-05;
            a = AlphaNoise.Sample(_seeds.At(0x800214) + nx, _seeds.At(0x800218) + nz);
            b = AlphaNoise.Sample(_seeds.At(0x80020C) + nx2, _seeds.At(0x800210) + nz2);
            double hz = ((double)a * 0.1 + b) * 500.0 * 6.103515625e-05 + z * 6.103515625e-05;
            wx = hx * 16384.0;
            wz = hz * 16384.0;
        }

        /// <summary>0x5CADE0: region whose climate anchor is nearest to the warped point (integer distance, first wins).</summary>
        private bool NearestCell(int x, int z, out int bestRx, out int bestRz)
        {
            float n1 = AlphaNoise.Sample(z * 0.0005, 3423.0);
            float n2 = AlphaNoise.Sample(x * 0.0005, 23421.0);
            int wx = (int)((float)x + n1 * 3.0f * 256.0f);
            int wz = (int)((float)z + n2 * 3.0f * 256.0f);
            bestRx = bestRz = -1;
            bool found = false;
            int bestDistance = 0;
            for (int rx = (x - 0x4000) >> 14; rx <= (x + 0x4000) >> 14; rx++)
            {
                for (int rz = (z - 0x4000) >> 14; rz <= (z + 0x4000) >> 14; rz++)
                {
                    AlphaClimateCell cell = _climate.Get(rx, rz);
                    if (cell == null)
                    {
                        continue;
                    }

                    float dx = cell.AnchorX - wx;
                    float dz = cell.AnchorZ - wz;
                    int distance = (int)(dz * dz + dx * dx);
                    if (!found || distance < bestDistance)
                    {
                        found = true;
                        bestDistance = distance;
                        bestRx = rx;
                        bestRz = rz;
                    }
                }
            }

            return found;
        }

        private float SquaredHalf(int offsetA, int offsetB, double fx, double fz, double scale)
        {
            float v = AlphaNoise.Sample(_seeds.At(offsetA) + fx * scale, _seeds.At(offsetB) + fz * scale);
            float h = (v + 1.0f) * 0.5f;
            return h * h;
        }

        private static float AnchorDistance2(AlphaClimateCell cell, double px, double pz)
        {
            float dx = (float)(cell.AnchorX - px);
            float dz = (float)(cell.AnchorZ - pz);
            return dz * dz + dx * dx;
        }

        private static float Smooth(float f)
        {
            return f * 3.0f * f - f * 2.0f * f * f;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
