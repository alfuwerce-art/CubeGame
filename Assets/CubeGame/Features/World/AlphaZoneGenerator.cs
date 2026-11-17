using System;
using System.Threading.Tasks;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Voxel columns of one Alpha zone — port of the column loop of <c>WorldInfo_generateBiomeContent</c> (0x5E4850),
    /// verified against the executed function (tools/alpha_models/world/zone_oracle.py + compare_zone.py):
    /// surface and stone voxels with Alpha colours, the ×8 mountain stone blend, caves (roof/floor/ceiling noise,
    /// water below y = 1, covered-air flag), cave floor colours, lake water (Lake landmark), cave content candidates
    /// (consuming the zone CRT stream exactly like Alpha), then the feature passes of <see cref="AlphaZoneFeatures"/>
    /// (carved rock, roads, rivers). Zone stream: <c>srand(seed + zz·0x10000 + zx)</c>.
    /// </summary>
    public static class AlphaZoneGenerator
    {
        /// <param name="features">false stops after the column loop (state of an oracle probe that stopped there).</param>
        public static AlphaZone Generate(AlphaTerrainGenerator terrain, int zx, int zz, bool features = true)
        {
            return Generate(terrain, zx, zz, features, null);
        }

        /// <param name="recordCarves">non-null: carves are recorded instead of applied (zone_oracle.py dumps).</param>
        public static AlphaZone Generate(AlphaTerrainGenerator terrain, int zx, int zz, bool features, System.Collections.Generic.List<CarveCall> recordCarves)
        {
            var colors = new AlphaColors(terrain);
            var rng = new AlphaCrtRandom(unchecked(terrain.Seeds.WorldSeed + zz * 0x10000 + zx));
            var zone = new AlphaZone(zx, zz);
            int x0 = zx * AlphaZone.Size;
            int z0 = zz * AlphaZone.Size;

            // Base heights of the zone plus one border row/column (Alpha's 257×257 grid).
            const int span = AlphaZone.Size + 1;
            var baseGrid = new float[span * span];
            Parallel.For(0, span, lx =>
            {
                for (int lz = 0; lz < span; lz++)
                {
                    baseGrid[lx * span + lz] = terrain.Base(x0 + lx, z0 + lz);
                }
            });

            LandmarkColumn zoneColumn = terrain.ColumnAt(x0, z0);
            LandmarkType ztype = zoneColumn?.Type ?? LandmarkType.None;
            var caves = new CaveInfo[AlphaZone.Size * AlphaZone.Size];

            // Columns are independent: generate them in parallel (rows), then consume the zone CRT stream in Alpha's
            // order (x outer, z inner) for the cave content candidates — identical result to the sequential loop.
            Parallel.For(0, AlphaZone.Size, lx =>
            {
                for (int lz = 0; lz < AlphaZone.Size; lz++)
                {
                    int x = x0 + lx;
                    int z = z0 + lz;
                    var column = new VoxelColumn
                    {
                        Temperature = terrain.Temperature(x, z),
                        Humidity = terrain.Humidity(x, z),
                        Rare = terrain.RareCellTerm(x, z),
                    };
                    int index = lx + lz * AlphaZone.Size;
                    zone.Columns[index] = column;
                    float b = baseGrid[lx * span + lz];
                    bool steep = Math.Abs(b - baseGrid[(lx + 1) * span + lz]) > 0.3f || Math.Abs(b - baseGrid[lx * span + lz + 1]) > 0.3f;
                    caves[index] = GenerateColumn(terrain, colors, column, x, z, b, steep, zoneColumn, ztype);
                }
            });

            for (int lx = 0; lx < AlphaZone.Size; lx++)
            {
                for (int lz = 0; lz < AlphaZone.Size; lz++)
                {
                    CaveInfo cave = caves[lx + lz * AlphaZone.Size];
                    if (cave.Open)
                    {
                        ConsumeCaveContent(rng, zone, x0 + lx, z0 + lz, cave);
                    }
                }
            }

            if (features)
            {
                AlphaZoneFeatures.Apply(terrain, colors, rng, zone, baseGrid, span, recordCarves);
            }

            if (features && terrain.ContentProviders.Count > 0)
            {
                var context = new ZoneContentContext(terrain, colors, zone, rng);
                foreach (IZoneContentProvider provider in terrain.ContentProviders)
                {
                    provider.Populate(context);
                }

                if (AlphaTerrainGenerator.UsesP2Environment(terrain.GeneratorId))
                {
                    AlphaZoneFeatures.PlaceRiverStones(context, colors);
                    AlphaZoneTrees.Populate(context);
                    zone.PopulationRandomState = rng.State;
                }
                if (AlphaTerrainGenerator.UsesP2Environment(terrain.GeneratorId) ||
                    terrain.GeneratorId == AlphaTerrainGenerator.PreviousWorldRuntimeId)
                    AlphaGroundVegetation.Populate(context);
            }

            return zone;
        }

        private struct CaveInfo
        {
            public bool Open;
            public float Top;
            public float Bottom;
            public float Height;
            public int FloorY;
        }

        /// <summary>The rand()-driven content switch of the cave branch (spawns are content: candidates only).</summary>
        private static void ConsumeCaveContent(AlphaCrtRandom rng, AlphaZone zone, int x, int z, CaveInfo cave)
        {
            if (!(cave.Top - cave.Bottom > 2.0f && (x + z * 3) % 7 == 0 && rng.Next() % 30 == 0))
            {
                return;
            }

            int kind = rng.Next() & 3;
            if (kind == 0)
            {
                if (cave.Top <= cave.Height && (rng.Next() & 3) == 0)
                {
                    rng.Next(); // spawn yaw
                    zone.Spawns.Add(new ZoneSpawnCandidate("cave_object_0x130b", x, cave.FloorY + 1, z, 0));
                }
            }
            else if (kind == 1)
            {
                if (cave.Top <= cave.Height)
                {
                    int variant = rng.Next() % 10;
                    if (variant == 3)
                    {
                        variant = 100 + rng.Next() % 100;
                    }

                    zone.Spawns.Add(new ZoneSpawnCandidate("cave_creature", x, cave.FloorY + 1, z, variant));
                }
            }
            else if (kind == 2)
            {
                if (cave.Top <= cave.Height)
                {
                    zone.Spawns.Add(new ZoneSpawnCandidate("cave_creature_timed", x, cave.FloorY + 1, z, 0));
                }
            }
            else
            {
                zone.Spawns.Add(new ZoneSpawnCandidate("cave_object", x, (int)(cave.Bottom + 2.0f), z, 0));
            }
        }

        private static float Smooth(float f) => f * 3.0f * f - f * 2.0f * f * f;

        /// <summary>Smoothstep evaluated in double, as Alpha's x87 code does before the result is stored.</summary>
        private static double SmoothD(double f) => f * 3.0 * f - f * 2.0 * f * f;

        private static int Byte(float v) => (int)v & 0xFF;

        private static CaveInfo GenerateColumn(AlphaTerrainGenerator terrain, AlphaColors colors,
            VoxelColumn col, int x, int z, float b, bool steep, LandmarkColumn landmark, LandmarkType ltype)
        {
            var cave = default(CaveInfo);
            double dx = x;
            double dz = z;
            float m = terrain.Mountain(x, z);
            float a = AlphaNoise.Sample(dx * 0.04 + 432.0, dz * 0.04 + 432.0);
            float bb = AlphaNoise.Sample(dx * 0.08 + 432.0, dz * 0.08 + 432.0);
            float r1 = (float)(1.0f - Math.Abs((float)(a + (float)(bb * 0.05f))));
            r1 = (float)(1.0f - (float)(r1 * r1) * r1);
            float t1 = (float)(r1 * r1);
            double c = AlphaNoise.Sample(dx * 0.08 + 4234.0, dz * 0.08 + 234.0) * 0.05;
            float d = AlphaNoise.Sample(dx * 0.05 + 423432.0, dz * 0.05 + 54352.0);
            float r2 = (float)(1.0 - Math.Abs(c + d));
            r2 = (float)(1.0f - (float)(r2 * r2) * r2);
            float t2 = (float)((float)(r2 * r2) + t1);

            float md = m;
            if (ltype == LandmarkType.Canyon || ltype == LandmarkType.Peak)
            {
                float ff = Math.Min(AlphaTerrainGenerator.Falloff(terrain.LandmarkDistance2(landmark, x, z)) * 2.0f, 1.0f);
                ff = (float)(1.0f - (float)(ff * ff));
                float k = (float)(1.0f - (float)(ff * ff));
                float s = (float)((float)(AlphaNoise.Sample(dx * 0.01 + 985.0, dz * 0.01 + 98584.0) * 1.3f) + 1.0f);
                s = s < 0f ? 0f : (s > 1f ? 1f : s);
                md = (float)((float)((float)(SmoothD(s) * k) * 0.4f) + m);
            }

            float mt = (float)(md * t2);
            float kk = (float)(mt * 1.25f);
            kk = kk < 0f ? 0f : (kk > 1f ? 1f : kk);
            float rockw = (float)SmoothD(kk);
            float n = AlphaNoise.Sample(dx * 0.01 + 34432.0, dz * 0.01 + 8992.0);
            // Alpha rounds every partial sum to float (Mono may keep double temporaries: explicit casts).
            float height = (float)((float)((float)((float)((float)(n + 1.5f) * 60.0f) * m) + (float)(mt * 8.0f)) + b);
            int surf = (int)height;

            uint sc = colors.SurfaceColor(x, z, surf, col.Temperature, col.Humidity, col.Rare, col.Humidity);
            int mat = Voxel.Flags(sc);
            if (rockw > 0.5f)
            {
                mat = 6;
            }

            colors.SoilColor(x, z, surf, col.Humidity, col.Rare, out float sr, out float sg, out float sb);
            float inv = 1.0f - rockw;
            int tr = Byte(Voxel.R(sc) * inv + sr * rockw);
            int tg = Byte(Voxel.G(sc) * inv + sg * rockw);
            int tb = Byte(Voxel.B(sc) * inv + sb * rockw);
            md *= 8.0f; // local_12f8 *= 8, clamped to 1
            if (md > 1.0f)
            {
                md = 1.0f;
            }

            uint v0;
            if (!steep)
            {
                colors.RockColor(x, z, surf, col.Humidity, col.Rare, out float rr, out float rg, out float rb);
                float km = 1.0f - md;
                float half = md * 0.5f;
                v0 = Voxel.Pack(Byte((tr + sr) * half + rr * km), Byte((tg + sg) * half + rg * km), Byte((tb + sb) * half + rb * km), 6);
            }
            else
            {
                v0 = Voxel.Pack(tr, tg, tb, mat);
            }

            col.Reset(surf, v0, Voxel.Pack(tr, tg, tb, mat));

            // Caves.
            float roof = (AlphaNoise.Sample(dx * 0.01 + 435.0, dz * 0.01 + 847.0) + 1.0f) * 20.0f + b;
            if (roof < height)
            {
                var seeds = terrain.Seeds;
                float ca = AlphaNoise.Sample(dx * 0.005 + seeds.At(0x800178), dz * 0.005 + seeds.At(0x80017C));
                float cb = AlphaNoise.Sample(seeds.At(0x800178) + dx * 0.01, seeds.At(0x80017C) + dz * 0.01);
                float fv = 1.0f - Math.Abs(ca + cb) * 4.0f;
                if (fv < 0f)
                {
                    return cave;
                }

                float g = 1.0f - fv * fv;
                float s1 = 1.0f - g * g;
                float s2 = 1.0f - g * g * g * g;
                float c3 = AlphaNoise.Sample(dx * 0.03 + 7635.0, dz * 0.03 + 123847.0);
                float tt = (c3 * 10.0f + ((height - roof) - 20.0f)) / 10.0f;
                tt = tt < 0f ? 0f : (tt > 1f ? 1f : tt);
                float q = 1.0f - tt * tt;
                float rocky = 1.0f - q * q;
                float u = 1.0f - tt;
                u = u * u * u * u * u * u * u;
                float top = ((m * 20.0f + 4.0f) * s1 + roof) * (1.0f - u) + (height + 2.0f) * u;
                float floorRaw = t2 * m * 6.0f + roof;
                float bottom = (1.0f - s2) * height + s2 * floorRaw + 2.0f;
                if (floorRaw < height - 2.0f)
                {
                    float inv2 = 1.0f - rocky;
                    int y = surf;
                    for (int i = 0; i < 2; i++)
                    {
                        uint v = col.Read(y);
                        colors.SoilColor(x, z, (int)(i + height), col.Humidity, col.Rare, out float pr, out float pg, out float pb);
                        Write(colors, col, x, z, y,
                            Voxel.Pack(Byte(Voxel.R(v) * rocky + pr * inv2), Byte(Voxel.G(v) * rocky + pg * inv2), Byte(Voxel.B(v) * rocky + pb * inv2), Voxel.Flags(v)));
                        y++;
                    }
                }

                if (top <= bottom)
                {
                    return cave;
                }

                int coverFlag = top < height ? 0x40 : 0;
                for (int y = (int)bottom; y < top; y++)
                {
                    Write(colors, col, x, z, y, y < 1 ? Voxel.Pack(0, 0, 0, 2) : Voxel.Pack(0, 0, 0, coverFlag));
                }

                int fy = (int)(bottom - 1.0f);
                uint sc2 = colors.SurfaceColor(x, z, fy, col.Temperature, col.Humidity, col.Rare, col.Humidity);
                int mat2 = Voxel.Flags(sc2);
                if (rockw > 0.5f || rocky > 0.5f)
                {
                    mat2 = 6;
                }

                float k74 = 1.0f - rocky;
                colors.SoilColor(x, z, fy, col.Humidity, col.Rare, out float qr, out float qg, out float qb);
                colors.RockColor(x, z, fy, col.Humidity, col.Rare, out float kr, out float kg, out float kb);
                float k98 = rocky * 0.4f;
                int mr = Byte((qr + kr) * k98 + Voxel.R(sc2) * k74);
                int mg = Byte((qg + kg) * k98 + Voxel.G(sc2) * k74);
                int mb = Byte((qb + kb) * k98 + Voxel.B(sc2) * k74);
                Write(colors, col, x, z, fy, Voxel.Pack(Byte(mr * inv + qr * rockw), Byte(mg * inv + qg * rockw), Byte(mb * inv + qb * rockw), mat2));

                cave = new CaveInfo { Open = true, Top = top, Bottom = bottom, Height = height, FloorY = fy };
            }

            if (ltype == LandmarkType.Lake && terrain.LandmarkDistance2(landmark, x, z) < 0.25f)
            {
                int y = (int)(landmark.Base - 25.0f);
                while (col.Bottom <= y)
                {
                    uint v = col.Read(y);
                    int m3 = Voxel.Material(v);
                    uint w;
                    if (m3 == 0 || m3 == 2)
                    {
                        w = Voxel.Water;
                    }
                    else
                    {
                        colors.SoilColor(x, z, y, col.Humidity, col.Rare, out float pr, out float pg, out float pb);
                        w = Voxel.Pack(Byte(pr), Byte(pg), Byte(pb), 3);
                    }

                    Write(colors, col, x, z, y, w);
                    y--;
                }
            }

            return cave;
        }

        /// <summary>tilemap_writeGlyphColumn (0x44E7A0): air below y = 1 becomes water; gaps below are filled with stone.</summary>
        internal static void Write(AlphaColors colors, VoxelColumn col, int x, int z, int y, uint v)
        {
            if (!(Voxel.Flags(v) != 0 || y < col.Bottom + col.Count))
            {
                return;
            }

            int oldBottom = col.Bottom;
            if (y < 1 && (Voxel.Flags(v) & 0x5F) == 0)
            {
                v = Voxel.Water;
            }

            col.SetAt(y - oldBottom, v);
            for (int yy = y + 1; yy < oldBottom; yy++)
            {
                colors.SoilColor(x, z, yy, col.Humidity, col.Rare, out float r, out float g, out float b);
                col.SetAt(yy - col.Bottom, Voxel.Pack(Byte(r), Byte(g), Byte(b), 6));
            }
        }
    }
}
