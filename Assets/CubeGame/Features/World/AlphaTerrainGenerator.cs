using System;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>Generated data of one terrain column.</summary>
    public readonly struct TerrainColumn
    {
        public TerrainColumn(int surface, bool rock, float mountain, int baseHeight)
        {
            Surface = surface;
            Rock = rock;
            Mountain = mountain;
            BaseHeight = baseHeight;
        }

        /// <summary>Top solid block Y (Alpha <c>surfaceY = int(height)</c>).</summary>
        public int Surface { get; }

        /// <summary>Alpha material 6 (stone) when the ridge/mountain weight exceeds 0.5.</summary>
        public bool Rock { get; }

        public float Mountain { get; }

        public int BaseHeight { get; }
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

        TerrainColumn Column(int blockX, int blockZ);

        RegionInfo Region(RegionKey region);
    }

    /// <summary>
    /// Alpha terrain reconstructed from <c>WorldInfo_generateBiomeContent</c> (0x5E4850), <c>FUN_005D7A70</c>
    /// (climate cell), <c>terrain_generateColumnColor</c> (0x5C5E20, base blending) and
    /// <c>World_placeObjectWithSpacing</c> (0x5EFFA0, mountain mask).
    ///
    /// Evidence per term (see M0_02 report):
    /// * noise, CRT streams, seed table, region level/variant/orientation — DIRECT;
    /// * climate cell base, ridges t1/t2, height = (n+1.5)·60·m + m·t2·8 + base, rock = smoothstep(1.25·m·t2) &gt; 0.5
    ///   — RECONSTRUCTED_FROM_DECOMPILATION (not executed);
    /// * base blending weight (1 − clamp((d² − d²min)·5e-8))² over neighbouring climate anchors in coarse units — RECONSTRUCTED;
    /// * omitted (PARTIAL): additive base noise of 0x5C5E20, the three multipliers of the mountain mask
    ///   (0x5F9340/rotateAndPlace/sampleHeightAtWorldXY), the dome of column types 6/13, column-type 2/6/7 rules;
    /// * noise inputs are block coordinates (unit choice PARTIAL, U1/U2); water level 0 — hypothesis
    ///   (land bases ≥ 1, water cells −100; Alpha stores water per cell, U3/Q-2).
    /// </summary>
    public sealed class AlphaTerrainGenerator : IWorldGenerator
    {
        public const string Id = "cubegame.alpha_terrain/1";

        /// <summary>PARTIAL: see class remarks (Q-2 / U3).</summary>
        public const int DefaultWaterLevel = 0;

        private readonly AlphaWorldSeeds _seeds;
        private readonly AlphaClimateGrid _climate;

        public AlphaTerrainGenerator(uint masterSeed)
        {
            MasterSeed = masterSeed;
            _seeds = new AlphaWorldSeeds(masterSeed);
            _climate = new AlphaClimateGrid(_seeds, new RegionKey(WorldLayout.CenterRegion, WorldLayout.CenterRegion));
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

            // generateRegion prewarms 5×5 climate cells before reseeding, so the region stream is independent of them.
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

        public TerrainColumn Column(int blockX, int blockZ)
        {
            double x = blockX;
            double z = blockZ;

            float baseHeight = BlendedBase(blockX, blockZ);
            float m = Mountain(blockX, blockZ);

            float a = AlphaNoise.Sample(x * 0.04 + 432.0, z * 0.04 + 432.0);
            float b = AlphaNoise.Sample(x * 0.08 + 432.0, z * 0.08 + 432.0);
            float r1 = 1.0f - Math.Abs(a + b * 0.05f);
            r1 = 1.0f - r1 * r1 * r1;
            float t1 = r1 * r1;

            double c = AlphaNoise.Sample(x * 0.08 + 4234.0, z * 0.08 + 234.0) * 0.05;
            float d = AlphaNoise.Sample(x * 0.05 + 423432.0, z * 0.05 + 54352.0);
            float r2 = (float)(1.0 - Math.Abs(c + d));
            r2 = 1.0f - r2 * r2 * r2;
            float t2 = r2 * r2 + t1;

            float mt = m * t2;
            float k = Clamp01(mt * 1.25f);
            float rockWeight = k * 3.0f * k - k * 2.0f * k * k;

            float n = AlphaNoise.Sample(x * 0.01 + 34432.0, z * 0.01 + 8992.0);
            float height = (n + 1.5f) * 60.0f * m + mt * 8.0f + baseHeight;
            return new TerrainColumn((int)height, rockWeight > 0.5f, m, (int)baseHeight);
        }

        /// <summary>Mountain mask (first part of 0x5EFFA0; later multipliers omitted — PARTIAL).</summary>
        public float Mountain(int blockX, int blockZ)
        {
            double x = blockX * 0.01;
            double z = blockZ * 0.01;
            double ox = _seeds.MountainOffsets[0];
            double oz = _seeds.MountainOffsets[1];

            float n0 = AlphaNoise.Sample(x, z);
            float k = 1.0f - n0 * n0;
            float n1 = AlphaNoise.Sample(ox + x + 843.0, oz + z + 984.0) * 0.1f;
            float n2 = AlphaNoise.Sample(blockX * 0.0025 + ox, blockZ * 0.0025 + oz);
            float v = 1.0f - Math.Abs(n1 + n2) * ((1.0f - k * k) * 1.3f + 2.0f);
            float n3 = AlphaNoise.Sample(blockX * 0.005 + 94.0, blockZ * 0.005 + 874.0);
            v *= n3 * 0.4f + 0.6f;
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        /// <summary>Distance-weighted base height of the neighbouring climate anchors (coarse units, 0x5C5E20).</summary>
        public float BlendedBase(int blockX, int blockZ)
        {
            long cx = (long)blockX * WorldLayout.CoarsePerBlock + WorldLayout.CoarsePerBlock / 2;
            long cz = (long)blockZ * WorldLayout.CoarsePerBlock + WorldLayout.CoarsePerBlock / 2;
            int rx0 = (int)FloorDiv(cx - WorldLayout.CoarsePerRegion, WorldLayout.CoarsePerRegion);
            int rx1 = (int)FloorDiv(cx + WorldLayout.CoarsePerRegion, WorldLayout.CoarsePerRegion);
            int rz0 = (int)FloorDiv(cz - WorldLayout.CoarsePerRegion, WorldLayout.CoarsePerRegion);
            int rz1 = (int)FloorDiv(cz + WorldLayout.CoarsePerRegion, WorldLayout.CoarsePerRegion);

            float best = float.MaxValue;
            for (int rx = rx0; rx <= rx1; rx++)
            {
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    AlphaClimateCell cell = _climate.Get(rx, rz);
                    if (cell != null)
                    {
                        float d2 = DistanceSquared(cell, cx, cz);
                        if (d2 < best)
                        {
                            best = d2;
                        }
                    }
                }
            }

            if (best == float.MaxValue)
            {
                return 0;
            }

            float weightSum = 0f;
            float valueSum = 0f;
            for (int rx = rx0; rx <= rx1; rx++)
            {
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    AlphaClimateCell cell = _climate.Get(rx, rz);
                    if (cell == null)
                    {
                        continue;
                    }

                    float t = (DistanceSquared(cell, cx, cz) - best) * 5e-08f;
                    if (t > 1f)
                    {
                        t = 1f;
                    }

                    float w = (1f - t) * (1f - t);
                    weightSum += w;
                    valueSum += cell.BaseHeight * w;
                }
            }

            return valueSum / weightSum;
        }

        private static float DistanceSquared(AlphaClimateCell cell, long cx, long cz)
        {
            float dx = cell.AnchorX - cx;
            float dz = cell.AnchorZ - cz;
            return dx * dx + dz * dz;
        }

        private static long FloorDiv(long value, long divisor)
        {
            long q = value / divisor;
            if (value % divisor != 0 && (value < 0) != (divisor < 0))
            {
                q--;
            }

            return q;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
