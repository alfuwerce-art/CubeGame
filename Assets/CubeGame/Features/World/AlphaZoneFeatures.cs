using System;
using System.Threading.Tasks;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Terrain passes of <c>WorldInfo_generateBiomeContent</c> (0x5E4850) that follow the column loop and precede the
    /// first content call (0x5E84C3), continuing the zone CRT stream:
    /// Canyon spires and the Rock landmark (<c>World_carveTerrainFeatureB</c> 0x5CBE00), generic stone blobs / mesas
    /// of plain zones (carveB / <c>World_carveTerrainFeatureA</c> 0x5CB600), roads between regions and village streets
    /// (edge measure 0x5F9F70; cobbles inside villages, material 11), rivers (0x5F9340; water terraced in 5-voxel
    /// steps, river bed material 3, banks cleared).
    /// The giant tree of a Tree (0xC) zone (<c>World_generateWaterOrPathFeature</c> 0x5DF960, vegetation) is a content
    /// candidate for the vegetation provider (DeepSeek #13): its rand() calls follow in Alpha but no later terrain
    /// pass of the zone depends on them.
    /// CubeGame rule: features stay inside their zone (Alpha also writes into neighbour zones that happen to be loaded).
    /// Reference: tools/alpha_models/world/zone_port.py (ZoneFeatures), compared with the executed function.
    /// </summary>
    internal static class AlphaZoneFeatures
    {
        internal static void Apply(AlphaTerrainGenerator terrain, AlphaColors colors, AlphaCrtRandom rng, AlphaZone zone, float[] baseGrid, int span,
            System.Collections.Generic.List<CarveCall> recordCarves = null)
        {
            int zx = zone.Zx;
            int zz = zone.Zz;
            int x0 = zx * AlphaZone.Size;
            int z0 = zz * AlphaZone.Size;
            AlphaRegion region = terrain.RegionRecord(zx >> 6, zz >> 6);
            AlphaZoneRecord record = region.Zone(zx & 63, zz & 63);
            LandmarkColumn col = terrain.ColumnAt(x0, z0);
            LandmarkType ctype = col?.Type ?? LandmarkType.None;
            VoxelColumn centre = zone.Column(x0 + 128, z0 + 128);
            float temperature = centre.Temperature;
            float humidity = centre.Humidity;

            if (ctype == LandmarkType.Canyon)
            {
                // 3×3 lattice: the decompiled counters are float* (+= 0x40 elements = 0x100), i/3 = 0, 85, 170.
                for (int i = 0; i < 0x300; i += 0x100)
                {
                    for (int j = 0; j < 0x300; j += 0x100)
                    {
                        if ((rng.Next() & 3) == 0)
                        {
                            continue;
                        }

                        int px = x0 + i / 3 + 0x2A;
                        int pz = z0 + j / 3 + 0x2A;
                        float w = 1.0f - terrain.LandmarkDistance2(col, px, pz);
                        if (!(0.0f < w && 0.5f <= (float)(w * w)))
                        {
                            continue;
                        }

                        int a = rng.Next() % 10 + 20;
                        int b = rng.Next() % 10 + 20;
                        int c = (rng.Next() & 15) + 20;
                        if (FarFromSpawn(px, pz))
                        {
                            CarveOrRecord(recordCarves, terrain, colors, zone, px, pz, Top(zone, px, pz), a, b, c, false);
                        }
                    }
                }
            }

            bool landmarkInZone = col != null && ((int)col.X >> 8) == zx && ((int)col.Z >> 8) == zz;
            if (ctype == LandmarkType.Rock && landmarkInZone)
            {
                int h = rng.Next() % 100 + 100;
                CarveOrRecord(recordCarves, terrain, colors, zone, x0 + 128, z0 + 128, Top(zone, x0 + 128, z0 + 128), 100, 100, h, false);
            }
            else if (ctype == LandmarkType.Tree && landmarkInZone)
            {
                rng.Next();
                zone.Spawns.Add(new ZoneSpawnCandidate("landmark_giant_tree", x0 + 128, Top(zone, x0 + 128, z0 + 128), z0 + 128, 0));
            }
            else if (record.Type != AlphaZoneRecord.Special10 && record.Type != AlphaZoneRecord.Town && record.Type != AlphaZoneRecord.Type3)
            {
                int n = rng.Next() & 3;
                if ((humidity > 0.6f || humidity < 0.3f) && (temperature > 0.7f || (temperature < 0.4f && (temperature > 0.2f || humidity < 0.8f))))
                {
                    n += 2;
                }

                for (int k = 0; k < n; k++)
                {
                    int a = rng.Next() % 40 + 10;
                    int b = rng.Next() % 40 + 10;
                    int c = rng.Next() % 25 + 10;
                    int px = rng.Next() % ((64 - a) * 4) + (a + zx * 128) * 2;
                    int pz = rng.Next() % ((64 - b) * 4) + (b + zz * 128) * 2;
                    if (!FarFromSpawn(px, pz) || terrain.HeightAtXY(px, pz) > 0.6f)
                    {
                        continue;
                    }

                    float s = terrain.SampleTerrainHeight(px, pz);
                    if (!((float)(1.0f - (float)(s * 50.0f)) < 0.0f))
                    {
                        continue;
                    }

                    float m = terrain.Mountain(px, pz);
                    int top = Top(zone, px, pz);
                    if ((rng.Next() & 1) == 0 || 0.25f <= m)
                    {
                        CarveOrRecord(recordCarves, terrain, colors, zone, px, pz, top, a, b, c, false);
                    }
                    else
                    {
                        CarveOrRecord(recordCarves, terrain, colors, zone, px, pz, top, a, b, c / 2, true);
                    }
                }
            }

            // Roads, then rivers: each column only touches itself, so columns run in parallel; the river pass draws
            // rand() % 200 for every written river bed (content candidate) in Alpha's order afterwards.
            var riverBed = new int[AlphaZone.Size * AlphaZone.Size];
            int orientation = region.Orientation;
            Parallel.For(0, AlphaZone.Size, lx =>
            {
                for (int lz = 0; lz < AlphaZone.Size; lz++)
                {
                    int x = x0 + lx;
                    int z = z0 + lz;
                    VoxelColumn column = zone.Columns[lx + lz * AlphaZone.Size];
                    float baseHeight = baseGrid[lx * span + lz];
                    Road(terrain, colors, column, x, z, baseHeight, orientation);
                    riverBed[lx + lz * AlphaZone.Size] = River(terrain, colors, column, x, z, baseHeight);
                }
            });

            for (int lx = 0; lx < AlphaZone.Size; lx++)
            {
                for (int lz = 0; lz < AlphaZone.Size; lz++)
                {
                    int bed = riverBed[lx + lz * AlphaZone.Size];
                    if (bed != int.MinValue && rng.Next() % 200 == 0)
                    {
                        zone.Spawns.Add(new ZoneSpawnCandidate("river_object", x0 + lx, bed, z0 + lz, 0));
                    }
                }
            }
        }

        private static int Byte(float v) => (int)v & 0xFF;

        private static void CarveOrRecord(System.Collections.Generic.List<CarveCall> record, AlphaTerrainGenerator terrain, AlphaColors colors, AlphaZone zone,
            int cx, int cz, int baseH, int rx, int rz, int height, bool mesa)
        {
            if (record != null)
            {
                record.Add(new CarveCall(mesa ? 'A' : 'B', cx, cz, baseH, rx, rz, height));
                return;
            }

            Carve(terrain, colors, zone, cx, cz, baseH, rx, rz, height, mesa);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static float N(double a, double b) => AlphaNoise.Sample(a, b);

        /// <summary>world+0x8000F0/F4 is the player spawn; CubeGame's fixed start (centre of zone 32800).</summary>
        private static bool FarFromSpawn(int x, int z)
        {
            float dx = (float)x - WorldLayout.StartVoxel;
            float dz = (float)z - WorldLayout.StartVoxel;
            return 3600.0f <= (float)(dz * dz) + (float)(dx * dx);
        }

        private static bool Inside(AlphaZone zone, int x, int z)
        {
            int lx = x - zone.Zx * AlphaZone.Size;
            int lz = z - zone.Zz * AlphaZone.Size;
            return lx >= 0 && lz >= 0 && lx < AlphaZone.Size && lz < AlphaZone.Size;
        }

        /// <summary>One above the highest stored voxel (Field +0x10 + +0x1C).</summary>
        private static int Top(AlphaZone zone, int x, int z)
        {
            VoxelColumn c = zone.Column(x, z);
            return c.Bottom + c.Count;
        }

        private static int Top0(int x, int z, int baseH, int height, bool mesa)
        {
            float n = N(x * 0.01 + 4394.0, z * 0.01 + 8974.0);
            float scale = mesa ? height : 20.0f;
            return (int)((float)(n * scale) + (float)(int)(float)(baseH + height));
        }

        private static bool Shape(int x, int z, int y, int cx, int cz, int rx, int rz, bool mesa, bool check)
        {
            float u;
            double v;
            float fu = (float)(x - cx) / rx;
            float fv = (float)(z - cz) / rz;
            if (mesa)
            {
                double yy = y * (check ? 0.05 : 0.02);
                float k = check ? 0.4f : 0.5f;
                u = (float)(N(z * 0.05, yy) * k) + fu;
                v = N(x * 0.05 + 4374.0, yy + 9898.0) * (double)k + fv;
            }
            else
            {
                u = (float)(N(z * 0.05, y * 0.05) * (float)(10.0f / rx)) + fu;
                v = N(x * 0.05 + 4374.0, y * 0.05 + 9898.0) * (double)(float)(10.0f / rz) + fv;
            }

            return v * v + (double)u * u <= 1.0;
        }

        /// <summary>carveB (stone, material 6) / carveA (mesa, soil→rock gradient, material 5).</summary>
        private static void Carve(AlphaTerrainGenerator terrain, AlphaColors colors, AlphaZone zone, int cx, int cz, int baseH, int rx, int rz, int height, bool mesa)
        {
            // Pass 1: an explicitly placed (0x80) voxel inside the volume aborts the whole feature.
            for (int x = cx - 2 * rx; x <= cx + 2 * rx; x++)
            {
                for (int z = cz - 2 * rz; z <= cz + 2 * rz; z++)
                {
                    if (!Inside(zone, x, z))
                    {
                        continue;
                    }

                    VoxelColumn f = zone.Column(x, z);
                    for (int y = Top0(x, z, baseH, height, mesa); f.Bottom <= y; y--)
                    {
                        if (Shape(x, z, y, cx, cz, rx, rz, mesa, true))
                        {
                            uint w = f.Read(y);
                            if ((mesa || Voxel.Material(w) != 2) && (Voxel.Flags(w) & 0x80) != 0)
                            {
                                return;
                            }
                        }
                    }
                }
            }

            for (int x = cx - 2 * rx; x <= cx + 2 * rx; x++)
            {
                for (int z = cz - 2 * rz; z <= cz + 2 * rz; z++)
                {
                    if (!Inside(zone, x, z))
                    {
                        continue;
                    }

                    int top0 = Top0(x, z, baseH, height, mesa);
                    VoxelColumn f = zone.Column(x, z);
                    int low = -8 - f.Bottom;
                    for (int y = top0; f.Bottom <= y; y--)
                    {
                        uint w = f.Read(y);
                        int m = Voxel.Material(w);
                        if (!((m == 0 || m == 2 || m == 4 || m == 9 || m == 5) && (Voxel.Flags(w) & 0x40) == 0)
                            || !Shape(x, z, y, cx, cz, rx, rz, mesa, false))
                        {
                            continue;
                        }

                        uint vox;
                        if (y == top0)
                        {
                            vox = colors.SurfaceColor(x, z, y, f.Temperature, f.Humidity, f.Rare, f.Humidity);
                        }
                        else if (mesa)
                        {
                            float t = Clamp01((float)(low + y) * 0.05f);
                            colors.SoilColor(x, z, y, f.Humidity, f.Rare, out float sr, out float sg, out float sb);
                            colors.RockColor(x, z, y, f.Humidity, f.Rare, out float rr, out float rg, out float rb);
                            float k = 1.0f - t;
                            vox = Voxel.Pack(Byte((float)(rr * k) + (float)(sr * t)), Byte((float)(rg * k) + (float)(sg * t)), Byte((float)(rb * k) + (float)(sb * t)), 5);
                        }
                        else
                        {
                            colors.SoilColor(x, z, y, f.Humidity, f.Rare, out float sr, out float sg, out float sb);
                            vox = Voxel.Pack(Byte(sr), Byte(sg), Byte(sb), 6);
                        }

                        AlphaZoneGenerator.Write(colors, f, x, z, y, vox);
                    }
                }
            }
        }

        private static void Road(AlphaTerrainGenerator terrain, AlphaColors colors, VoxelColumn f, int x, int z, float baseHeight, int orientation)
        {
            float e = terrain.RotateAndPlace(x, z);
            float w = 1.0f - (float)(e * 50.0f);
            if (w < 0.0f)
            {
                return;
            }

            float v = terrain.HeightAtXY(x, z);
            float b = baseHeight < 0.0f ? 0.0f : baseHeight;
            int y = (int)(float)(b + 1.0f);
            float k = 1.0f - w;
            k = 1.0f - (float)((float)(k * k) * k);
            float n = N(x * 0.02 + 55432.0, z * 0.02 + 974.0);
            int top = (int)((float)((float)((float)(n + 1.0f) * 4.0f) + (float)(k * 5.0f)) + b);
            uint c = f.Read(y);
            if (Voxel.Material(c) == 0 || Voxel.Material(c) == 2 || (Voxel.Flags(c) & 0x40) != 0)
            {
                return;
            }

            float n2 = N(x * 0.05 + 843.0, z * 0.05 + 984.0);
            float road = (float)(40.0f * n2) + 140.0f;
            float roadR = road, roadG = road, roadB = road;
            int hx = (x / 2) * 0xEA;
            int hz = (z / 2) * 0xEA;
            if (N(hx + 0x12E2, hz + 0xC11A) > 0.5f)
            {
                float n3 = N(hx, hz + 0x31);
                float add = 20.0f * n3;
                roadR += add;
                roadG += add;
                roadB += add;
            }

            colors.RockColor(x, z, y, f.Humidity, f.Rare, out float rr, out float rg, out float rb);
            if (orientation == 1 || orientation == 4 || orientation == 5)
            {
                roadR = rr;
                roadG = rg;
                roadB = rb;
            }

            float inv = 1.0f - v;
            uint vox = Voxel.Pack(
                Byte(ClampByte((float)(rr * inv) + (float)(v * roadR))),
                Byte(ClampByte((float)(rg * inv) + (float)(v * roadG))),
                Byte(ClampByte((float)(rb * inv) + (float)(v * roadB))),
                11);
            AlphaZoneGenerator.Write(colors, f, x, z, y - 1, vox);
            if (v > 0.92f)
            {
                top = f.Bottom + f.Count;
            }

            for (int yy = y; yy < top; yy++)
            {
                if ((Voxel.Flags(f.Read(yy)) & 0x40) == 0)
                {
                    AlphaZoneGenerator.Write(colors, f, x, z, yy, Voxel.ClearedAir);
                }
            }
        }

        private static float ClampByte(float v) => v < 0.0f ? 0.0f : (v > 255.0f ? 255.0f : v);

        /// <summary>Returns the river-bed Y when a bed voxel was written (Alpha then draws rand() % 200), else int.MinValue.</summary>
        private static int River(AlphaTerrainGenerator terrain, AlphaColors colors, VoxelColumn f, int x, int z, float baseHeight)
        {
            float s = terrain.SampleTerrainHeight(x, z);
            float w = 1.0f - (float)(s * 50.0f);
            if (w < 0.0f || terrain.HeightAtXY(x, z) > 0.95f)
            {
                return int.MinValue;
            }

            float b = baseHeight < 0.0f ? 0.0f : baseHeight;
            int step = ((int)b / 5) * 5;
            float fs = step;
            float frac = (float)(b - fs) / 5.0f;
            float d;
            if (0.5f <= frac)
            {
                d = 1.0f - (float)((float)(frac - 0.5f) * 4.0f);
                if (d < 0.0f)
                {
                    d = (float)((float)(d + 1.0f) * (float)(d + 1.0f)) - 1.0f;
                }
            }
            else
            {
                d = (float)(frac * 2.0f);
            }

            int bed = step;
            if (d < 0.0f)
            {
                bed = (int)(float)(fs - (float)(d * 5.0f));
            }

            for (int y = (int)(float)((float)(fs - (float)(d * 5.0f)) + 2.0f); y <= step; y++)
            {
                if ((Voxel.Flags(f.Read(y)) & 0x40) == 0)
                {
                    float k = 1.0f - d;
                    AlphaZoneGenerator.Write(colors, f, x, z, y, Voxel.Pack(0, 0, Byte((float)(k * 255.0f) + (float)(d * 0.0f)), 2));
                }
            }

            int written = int.MinValue;
            uint c = f.Read(bed);
            if (step <= bed && Voxel.Material(c) != 2 && (Voxel.Flags(c) & 0x40) == 0)
            {
                colors.SoilColor(x, z, bed, f.Humidity, f.Rare, out float sr, out float sg, out float sb);
                AlphaZoneGenerator.Write(colors, f, x, z, bed, Voxel.Pack(Byte(sr), Byte(sg), Byte(sb), 3));
                written = bed;
            }

            float kk = 1.0f - w;
            kk = 1.0f - (float)((float)(kk * kk) * kk);
            float n = N(x * 0.02 + 55432.0, z * 0.02 + 974.0);
            int top = (int)((float)((float)((float)(n + 1.0f) * 2.0f) + (float)(kk * 5.0f)) + b);
            for (int yy = bed + 1; yy < top; yy++)
            {
                if ((Voxel.Flags(f.Read(yy)) & 0x40) == 0)
                {
                    AlphaZoneGenerator.Write(colors, f, x, z, yy, Voxel.ClearedAir);
                }
            }

            return written;
        }
    }

    /// <summary>Arguments of one carve call (for comparisons with oracle dumps that record carves instead of running them).</summary>
    public readonly struct CarveCall : IEquatable<CarveCall>
    {
        public CarveCall(char kind, int cx, int cz, int baseH, int rx, int rz, int height)
        {
            Kind = kind;
            Cx = cx;
            Cz = cz;
            BaseH = baseH;
            Rx = rx;
            Rz = rz;
            Height = height;
        }

        public char Kind { get; }

        public int Cx { get; }

        public int Cz { get; }

        public int BaseH { get; }

        public int Rx { get; }

        public int Rz { get; }

        public int Height { get; }

        public bool Equals(CarveCall o) => Kind == o.Kind && Cx == o.Cx && Cz == o.Cz && BaseH == o.BaseH && Rx == o.Rx && Rz == o.Rz && Height == o.Height;

        public override bool Equals(object obj) => obj is CarveCall o && Equals(o);

        public override int GetHashCode() => Cx * 31 + Cz;

        public override string ToString() => $"carve{Kind}({Cx},{Cz}, base {BaseH}, r {Rx}x{Rz}, h {Height})";
    }
}
