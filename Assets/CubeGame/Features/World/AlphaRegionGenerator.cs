using System;
using System.Collections.Generic;

namespace CubeGame.Features.World
{
    /// <summary>Alpha landmark type of a 2048×2048-voxel column (names from World::ctor_1 in Cube.exe).</summary>
    public enum LandmarkType
    {
        None = 0,
        Village = 1,
        Mountain = 2,
        Forest = 3,
        Lake = 4,
        Ruins = 5,
        Canyon = 6,
        Valley = 7,
        Crater = 8,
        Unnamed10 = 10,
        Rock = 11,
        Tree = 12,
        Peak = 13,
        Zone14 = 14,
        Island = 15,
    }

    /// <summary>One "voxel column 8" record (0x68 bytes in Alpha's Region, 8×8 per region).</summary>
    public sealed class LandmarkColumn
    {
        /// <summary>Landmark centre in voxels (Alpha stores 16.16).</summary>
        public double X { get; internal set; }

        public double Z { get; internal set; }

        /// <summary>+0x10: landmark radius in voxels (0 = column outside the region's territory).</summary>
        public float Radius { get; internal set; }

        /// <summary>+0x14: terrain base height at the centre (clamped to ≥ 0 for processed columns).</summary>
        public float Base { get; internal set; }

        /// <summary>+0x18.</summary>
        public LandmarkType Type { get; internal set; }

        /// <summary>+0x1C: subtype (village: region orientation; forest/ruins: variant; zone 14: zone subtype).</summary>
        public int Sub { get; internal set; }

        /// <summary>+0x20: id (village: climate cell id; type 10: rand id; others: running id).</summary>
        public int Id { get; internal set; }

        /// <summary>+0x24: landmark level (curve of processing order, or the region's minimum level).</summary>
        public int Level { get; internal set; } = 1;

        /// <summary>+0x28: level class (0…4).</summary>
        public int LevelClass { get; internal set; }
    }

    /// <summary>
    /// 16-byte zone record of a region (generateRegion, region+0x18, index lx·64 + lz): +0 type, +1 sub, +4 id,
    /// +8 level, +0xC class. Verified against the executed function (36864 records of the 3×3 centre regions).
    /// </summary>
    public sealed class AlphaZoneRecord
    {
        /// <summary>Plain zone.</summary>
        public const int Plain = 0;

        /// <summary>One of the four town zones nearest a Village centre (Sub = 1…4).</summary>
        public const int Town = 1;

        /// <summary>Settlement zone of a 0xE column (Sub = zone subtype, Id/LevelClass set).</summary>
        public const int Settlement = 3;

        /// <summary>Zone of a type-10 column (Id = the column id).</summary>
        public const int Special10 = 4;

        public int Type { get; internal set; }

        public int Sub { get; internal set; }

        public int Id { get; internal set; }

        /// <summary>Level of the landmark column centred in this zone; 1 elsewhere.</summary>
        public int Level { get; internal set; } = 1;

        public int LevelClass { get; internal set; }
    }

    /// <summary>Region record: level, variant, orientation, creature level range and 64 landmark columns.</summary>
    public sealed class AlphaRegion
    {
        public int Rx { get; internal set; }

        public int Rz { get; internal set; }

        public int Level { get; internal set; }

        public int Variant { get; internal set; }

        public int Orientation { get; internal set; }

        /// <summary>assign_two_dwords (0x5EE4B0): creature level range from the climate cell.</summary>
        public int LevelMin { get; internal set; }

        public int LevelMax { get; internal set; }

        public LandmarkColumn[] Columns { get; } = new LandmarkColumn[64];

        /// <summary>Zone grid, index lx·64 + lz (zone coordinates inside the region, 0…63).</summary>
        public AlphaZoneRecord[] Zones { get; } = new AlphaZoneRecord[64 * 64];

        public AlphaZoneRecord Zone(int lx, int lz) => Zones[lx * 64 + lz];

        /// <summary>Column (cx, cz) inside the region, each 0…7.</summary>
        public LandmarkColumn Column(int cx, int cz) => Columns[cx * 8 + cz];
    }

    /// <summary>
    /// Landmark-column part of Alpha <c>generateRegion</c> (0x5DA280), ported and verified against the executed
    /// function (704 columns of 11 regions identical, tools/alpha_models/world/compare_regions.py).
    ///
    /// CubeGame rule: every region is generated in isolation — the terrain samples taken while placing landmarks
    /// do not see landmarks of other regions. Alpha generates regions in streaming order, so its later regions see
    /// earlier ones (order-dependent); the isolated variant keeps "one seed → one world" (deviation, M0-02 report).
    /// The 16-byte zone grid is ported (<see cref="AlphaZoneRecord"/>). Not ported: stored-region loading (Alpha
    /// reads saved region blobs from its world database; CubeGame stores deltas instead).
    /// </summary>
    public static class AlphaRegionGenerator
    {
        public static AlphaRegion Generate(AlphaTerrainGenerator terrain, int rx, int rz)
        {
            AlphaWorldSeeds seeds = terrain.Seeds;
            AlphaClimateGrid climate = terrain.Climate;
            var rng = new AlphaCrtRandom(unchecked(seeds.WorldSeed + rz * 0x400 + rx));
            var region = new AlphaRegion { Rx = rx, Rz = rz, Level = AlphaTerrainGenerator.Level(rx, rz) };
            for (int i = 0; i < 64; i++)
            {
                region.Columns[i] = new LandmarkColumn();
            }

            for (int i = 0; i < region.Zones.Length; i++)
            {
                region.Zones[i] = new AlphaZoneRecord();
            }

            region.Variant = region.Level > 4 ? rng.Next() % 5 : 0;
            region.Orientation = rng.Next() & 3;
            int nextId = rng.Next() % 10000;
            AlphaClimateCell cell = climate.Get(rx, rz);
            if (cell.Field4 > 0.81f)
            {
                region.Orientation = (rng.Next() & 1) + 4;
            }

            LevelRange(cell, out int levelMin, out int levelMax);
            region.LevelMin = levelMin;
            region.LevelMax = levelMax;
            int anchorCx = (cell.AnchorX >> 11) & 7;
            int anchorCz = (cell.AnchorZ >> 11) & 7;
            var entries = new List<(float Key, int Cx, int Cz)>();

            for (int cx = 0; cx < 8; cx++)
            {
                for (int cz = 0; cz < 8; cz++)
                {
                    LandmarkColumn col = region.Column(cx, cz);
                    int x0 = rx * WorldLayout.RegionSize + cx * 0x800;
                    int z0 = rz * WorldLayout.RegionSize + cz * 0x800;
                    if (cx == anchorCx && cz == anchorCz)
                    {
                        // Single-player world with an open world database: the climate anchor column is the village.
                        col.Type = LandmarkType.Village;
                        col.Sub = region.Orientation;
                        col.Id = cell.Id;
                        float r = rng.Next() % 200 + 0x200;
                        col.Radius = r;
                        double x = cell.AnchorX;
                        double z = cell.AnchorZ;
                        if (x - r - 256.0 < x0)
                        {
                            x = (int)(x0 + r + 256.0f);
                        }

                        if (z - r - 256.0 < z0)
                        {
                            z = (int)(z0 + r + 256.0f);
                        }

                        if (x + r + 256.0 > x0 + 0x800)
                        {
                            x = (int)(x0 + 0x800 - r - 256.0f);
                        }

                        if (z + r + 256.0 > z0 + 0x800)
                        {
                            z = (int)(z0 + 0x800 - r - 256.0f);
                        }

                        col.X = x;
                        col.Z = z;
                        col.Base = Math.Max(terrain.BaseIsolated((int)x, (int)z), 0f);
                        continue;
                    }

                    if (!terrain.NearestCellRegion(x0 + 0x400, z0 + 0x400, out int nrx, out int nrz) || nrx != rx || nrz != rz)
                    {
                        continue;
                    }

                    float radius = (rng.Next() & 0xFF) + 0x200;
                    col.Radius = radius;
                    col.Level = levelMin;
                    PlaceInColumn(rng, col, radius, x0, z0);
                    col.Base = terrain.BaseIsolated((int)col.X, (int)col.Z);
                    if (terrain.NearestCellRegion((int)col.X, (int)col.Z, out nrx, out nrz) && nrx == rx && nrz == rz)
                    {
                        float dist = (float)Math.Sqrt((float)((anchorCz - cz) * (anchorCz - cz)) + (float)((anchorCx - cx) * (anchorCx - cx)));
                        float key = (rng.Next() * 4.0f / 32767.0f + dist) - 2.0f;
                        entries.Add((key, cx, cz));
                    }
                }
            }

            // std::sort by key (keys are distinct in practice; a stable sort gives the same order).
            List<(float Key, int Cx, int Cz)> order = Sorted(entries);
            int j = 0;
            while (j < 64 && order.Count > 0)
            {
                if ((j & 1) != 0)
                {
                    j++;
                    continue;
                }

                (float _, int cx, int cz) = order[0];
                order.RemoveAt(0);
                LandmarkColumn col = region.Column(cx, cz);
                if (col.Base < 0f)
                {
                    col.Base = 0f;
                }

                float frac = j * 0.015625f;
                col.Level = (int)((1.0f / (1.0f - frac) - 1.0f) * 20.0f + 1.0f); // _ftol2: truncation
                if (((j >> 1) & 1) != 0)
                {
                    col.Type = LandmarkType.Zone14;
                    col.Radius = 150f;
                    int zx = (int)col.X & ~0xFF;
                    int zz = (int)col.Z & ~0xFF;
                    col.X = zx + 0x80;
                    col.Z = zz + 0x80;
                    int zoneSub = rng.Next() & 3;
                    int zoneCenterX = (rx * 0x40 + ((zx >> 8) & 0x3F)) * 0x100 + 0x80;
                    int zoneCenterZ = (rz * 0x40 + ((zz >> 8) & 0x3F)) * 0x100 + 0x80;
                    if (terrain.ClimateAverage(zoneCenterX, zoneCenterZ, useField4: true) > 0.8f)
                    {
                        zoneSub = (terrain.ClimateAverage(zoneCenterX, zoneCenterZ, useField4: false) <= 0.8f ? 1 : 0) + 4;
                    }

                    AlphaZoneRecord zone = region.Zone((zx >> 8) & 63, (zz >> 8) & 63);
                    zone.Type = AlphaZoneRecord.Settlement;
                    zone.Sub = zoneSub;
                    zone.Id = rng.Next() % 10000000 + 1;
                    col.Id = nextId;
                    nextId += 1 + rng.Next() % 0x32;
                    col.Sub = zoneSub;
                    zone.LevelClass = LevelClass(rng, col.Level); // the class goes to the zone record, not the column
                    j++;
                    continue;
                }

                bool waterCell = cell.BaseHeight < 0;
                int pick = rng.Next() & 7;
                switch (pick)
                {
                    case 0:
                        col.Type = LandmarkType.Mountain;
                        break;
                    case 1:
                        col.Type = LandmarkType.Forest;
                        col.Sub = rng.Next() % 3;
                        break;
                    case 2:
                        col.Type = waterCell ? LandmarkType.Island : LandmarkType.Lake;
                        break;
                    case 3:
                        col.Type = LandmarkType.Ruins;
                        col.Sub = cell.Field4 <= 0.8f ? 0 : (cell.Field3 <= 0.8f ? (cell.Field3 < 0.2f ? 2 : 0) : 3);
                        float r = (rng.Next() & 0xFF) + 0x100;
                        col.Radius = r;
                        PlaceInColumn(rng, col, r, rx * WorldLayout.RegionSize + cx * 0x800, rz * WorldLayout.RegionSize + cz * 0x800);
                        break;
                    case 4:
                        col.Type = waterCell ? LandmarkType.Island : LandmarkType.Canyon;
                        break;
                    case 5:
                        col.Type = waterCell ? LandmarkType.Island : LandmarkType.Valley;
                        break;
                    default:
                        col.Type = pick == 6 ? LandmarkType.Rock : LandmarkType.Tree;
                        col.Radius = 128f;
                        col.X = ((int)col.X & ~0xFF) + 0x80;
                        col.Z = ((int)col.Z & ~0xFF) + 0x80;
                        break;
                }

                col.LevelClass = LevelClass(rng, col.Level);
                col.Id = nextId;
                nextId += 1 + rng.Next() % 0x32;
                j++;
            }

            for (int pick = 0; pick < 5 && order.Count > 0; pick++)
            {
                int k = rng.Next() % order.Count;
                (float _, int cx, int cz) = order[k];
                order.RemoveAt(k);
                LandmarkColumn col = region.Column(cx, cz);
                col.Type = LandmarkType.Unnamed10;
                col.Id = rng.Next() % 10000000 + 1;
                AlphaZoneRecord zone = region.Zone(((int)col.X >> 8) & 63, ((int)col.Z >> 8) & 63);
                zone.Type = AlphaZoneRecord.Special10;
                zone.Id = col.Id;
            }

            foreach (LandmarkColumn col in region.Columns)
            {
                region.Zone(((int)col.X >> 8) & 63, ((int)col.Z >> 8) & 63).Level = col.Level;
            }

            MarkTownZones(terrain, region);
            return region;
        }

        /// <summary>The four zones (of the column's 8×8) with the highest village weight (1 − d²)² become town zones 1…4.</summary>
        private static void MarkTownZones(AlphaTerrainGenerator terrain, AlphaRegion region)
        {
            var entries = new List<(float w, int lx, int lz)>(64);
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    LandmarkColumn col = region.Column(i, j);
                    if (col.Type != LandmarkType.Village)
                    {
                        continue;
                    }

                    entries.Clear();
                    for (int dx = 0; dx < 8; dx++)
                    {
                        for (int dz = 0; dz < 8; dz++)
                        {
                            int x = (i + region.Rx * 8) * 0x800 + 0x80 + dx * 0x100;
                            int z = (j + region.Rz * 8) * 0x800 + 0x80 + dz * 0x100;
                            float w = AlphaTerrainGenerator.Falloff(terrain.LandmarkDistance2(col, x, z));
                            int at = entries.Count;
                            while (at > 0 && entries[at - 1].w < w)
                            {
                                at--; // stable descending insertion
                            }

                            entries.Insert(at, (w, i * 8 + dx, j * 8 + dz));
                        }
                    }

                    for (int sub = 1; sub <= 4; sub++)
                    {
                        AlphaZoneRecord zone = region.Zone(entries[sub - 1].lx, entries[sub - 1].lz);
                        zone.Type = AlphaZoneRecord.Town;
                        zone.Sub = sub;
                    }
                }
            }
        }

        /// <summary>assign_two_dwords (0x5EE4B0).</summary>
        public static void LevelRange(AlphaClimateCell cell, out int min, out int max)
        {
            min = 1;
            max = 10;
            if (cell.Field4 < 0.2f)
            {
                min = 10;
                max = 20;
            }

            if (cell.Field3 < 0.2f && cell.Field4 > 0.8f)
            {
                min = 15;
                max = 25;
            }

            if (cell.Field3 > 0.8f && cell.Field4 > 0.8f)
            {
                min = 10;
                max = 20;
            }

            if (cell.Flag)
            {
                min = 20;
                max = 30;
            }
        }

        private static int LevelClass(AlphaCrtRandom rng, int level)
        {
            if (level < 5)
            {
                return 0;
            }

            if (level < 10)
            {
                return rng.Next() & 1;
            }

            if (level < 15)
            {
                return rng.Next() % 3;
            }

            if (level > 18)
            {
                return (rng.Next() & 3) + 1;
            }

            return rng.Next() % 3 + 1;
        }

        /// <summary>Random centre keeping the disk (radius + 256) inside the 2048-voxel column (z drawn first).</summary>
        private static void PlaceInColumn(AlphaCrtRandom rng, LandmarkColumn col, float radius, int x0, int z0)
        {
            float span = 2048.0f - (radius + 256.0f) * 2.0f;
            int oz = (int)(rng.Next() * span / 32767.0f * 65536.0f);
            col.Z = z0 + ((int)(radius * 65536.0f) + (long)oz) / 65536.0 + 256.0;
            int ox = (int)(rng.Next() * span / 32767.0f * 65536.0f);
            col.X = x0 + ((int)((radius + 256.0f) * 65536.0f) + (long)ox) / 65536.0;
        }

        private static List<(float Key, int Cx, int Cz)> Sorted(List<(float Key, int Cx, int Cz)> entries)
        {
            var copy = new List<(float Key, int Cx, int Cz)>(entries);
            copy.Sort((a, b) => a.Key.CompareTo(b.Key));
            return copy;
        }
    }
}
