using System;
using System.Collections.Generic;

namespace CubeGame.Features.World
{
    /// <summary>
    /// World access of the dungeon zone writer in Alpha axes: x, y horizontal, z up (CubeGame: x, z horizontal, y up).
    /// </summary>
    public interface IAlphaDungeonWorld
    {
        /// <summary>World column exists (Alpha world_getChunkCell256 ≠ null).</summary>
        bool HasColumn(int x, int y);

        /// <summary>Lowest stored voxel of the column (+0x10).</summary>
        int Bottom(int x, int y);

        /// <summary>Bottom + stored count (+0x10 + +0x1C).</summary>
        int Top(int x, int y);

        /// <summary>Material (flags &amp; 0x1F) with Alpha's read rule (0x42F7E0 and its static defaults).</summary>
        int Type(int x, int y, int z);

        /// <summary>Flags byte of the voxel read by the same rule (0x42F7E0).</summary>
        int Flags(int x, int y, int z);

        /// <summary>tilemap_writeGlyphColumn (0x44E7A0).</summary>
        void Write(int x, int y, int z, uint voxel);
    }

    /// <summary>One call of the voxel fill 0x5DF600 (return-address site, origin, size, type flag).</summary>
    public readonly struct AlphaDungeonFill
    {
        public AlphaDungeonFill(int site, int x, int y, int z, int sx, int sy, int sz, int type)
        {
            Site = site;
            X = x;
            Y = y;
            Z = z;
            SizeX = sx;
            SizeY = sy;
            SizeZ = sz;
            Type = type;
        }

        public int Site { get; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }
        public int Type { get; }

        public override string ToString() => $"{Site:x},{X},{Y},{Z},{SizeX},{SizeY},{SizeZ},{Type}";
    }

    /// <summary>Result of the column stage (base height, chosen rotation, colours, fills).</summary>
    public sealed class AlphaDungeonBuild
    {
        public int Base { get; internal set; }

        public int Rotation { get; internal set; }

        public (int X, int Y, int Z) Entrance { get; internal set; }

        public byte[] Color1 { get; } = new byte[3];

        public byte[] Color2 { get; } = new byte[3];

        public List<AlphaDungeonFill> Fills { get; } = new List<AlphaDungeonFill>();
    }

    /// <summary>
    /// Alpha dungeon generator 0x5CC510, zone-write column loop 0x5CEA6B…0x5D06F2 (P7-B stage 4a), ported from the bytes
    /// and checked against executed Alpha code (dungeon_oracle.py traces; tools/alpha_models/world/dungeon_columns_port.py
    /// reproduces every fill and the CRT state for subtypes 0–5 and flag 1): four rotation trials over the entrance
    /// surface and water, base height, two rand() colour triples, the subtype rand(), then per volume column — rock 14³,
    /// above-ground walls with crenellations, subtype-2 caps, subtype-3 pyramids, shafts down to the terrain bottom and
    /// the subtype-4 tower. Voxel fills 0x5DF600 draw 3 + sx·sy·sz rand() and write type 6 or 0xD.
    /// PARTIAL: the green channel noise term of a fill (noise2d_sample 0x5EFDB0) and the subtype-4 surface cap
    /// (0x5C56E0 colours) are not ported; floors, furniture and creatures (stage 4b) follow.
    /// </summary>
    public static class AlphaDungeonZoneWriter
    {
        public const string Provenance =
            "EMULATED_ALPHA_CODE: 0x5CEA6B…0x5D06F2 fills and CRT; PARTIAL: fill colour noise 0x5EFDB0, subtype-4 cap";

        private static readonly float RandMax = 32767.0f;

        public static AlphaDungeonBuild WriteColumns(AlphaDungeonRooms cells, IAlphaDungeonWorld world, int x0, int y0,
            AlphaCrtRandom random)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (random == null) throw new ArgumentNullException(nameof(random));

            AlphaDungeonVolume v = cells.Volume;
            int n = v.SizeX;
            int subtype = cells.Subtype;
            var build = new AlphaDungeonBuild();

            // 0x5CEA93…0x5CEDAA: four rotation trials, keep the lowest base under the entrance.
            int bestBase = 0;
            int bestRot = -1;
            int bestEz = 0;
            bool bestWater = false;
            int rot = v.Rotation;
            for (int trial = 0; trial < 4; trial++)
            {
                rot = (rot + 1) & 3;
                v.Rotation = rot;
                int ex = 0;
                int ey = 0;
                int ez = 0;
                for (int z = n - 1; z >= 0; z--)
                {
                    for (int x = 0; x < n; x++)
                    {
                        for (int y = 0; y < n; y++)
                        {
                            if (v.Type(x, y, z) == 4)
                            {
                                ex = x;
                                ey = y;
                                ez = z;
                            }
                        }
                    }
                }

                int wx = 10 * ex + x0 + 5;
                int wy = 10 * ey + y0 + 5;
                if (!world.HasColumn(wx, wy))
                {
                    throw new InvalidOperationException("no column above the dungeon entrance (Alpha abandons the dungeon)");
                }

                int s = world.Top(wx, wy);
                if (world.Type(wx, wy, s) == 0)
                {
                    do
                    {
                        s--;
                    }
                    while (world.Type(wx, wy, s) == 0);
                }

                s++;
                bool water = false;
                for (int dx = 0; dx < 10; dx++)
                {
                    for (int dy = 0; dy < 10; dy++)
                    {
                        for (int dz = 0; dz < 10; dz++)
                        {
                            int t = world.Type(x0 + 10 * ex + dx, y0 + 10 * ey + dy, s - 5 + dz);
                            if (t == 2 || t == 3) water = true;
                        }
                    }
                }

                int candidate = s + (water ? 100 : 0) - 10 * ez;
                if (bestRot < 0 || candidate < bestBase)
                {
                    bestBase = candidate;
                    bestRot = rot;
                    bestEz = ez;
                    bestWater = water;
                    build.Entrance = (ex, ey, ez);
                }
            }

            int baseZ = bestBase;
            if (subtype == 4 || subtype == 5) baseZ += 50;
            if (bestWater) baseZ -= 100;
            if (10 * bestEz + baseZ < 0) baseZ = -10 * bestEz;
            v.Rotation = Math.Max(bestRot, 0);
            build.Base = baseZ;
            build.Rotation = v.Rotation;

            build.Color1[0] = (byte)(random.Next() % 180 + 50);
            build.Color1[1] = (byte)(random.Next() % 180 + 50);
            build.Color1[2] = (byte)(random.Next() % 180 + 50);
            build.Color2[0] = (byte)(random.Next() % 180 + 50);
            build.Color2[1] = (byte)(random.Next() % 180 + 50);
            build.Color2[2] = (byte)(random.Next() % 180 + 50);
            if (subtype == 0 || subtype == 3)
            {
                random.Next();   // jump table 0x5D5FB8 → 0x5CEEAD
            }

            var writer = new Writer(world, random, build);
            byte[] palA = cells.PaletteA;
            byte[] wall = cells.WallColor;
            float scale = cells.PaletteScale;

            int T(int x, int y, int z) => x < 0 || y < 0 || z < 0 || x >= n || y >= n || z >= n ? 1 : v.Type(x, y, z);
            bool Open(int t) => t == 0 || t == 4 || t == 1;

            for (int cx = 0; cx < n; cx++)
            {
                for (int cy = 0; cy < n; cy++)
                {
                    int wx = x0 + 10 * cx;
                    int wy = y0 + 10 * cy;
                    bool roof = false;
                    bool entrance = false;
                    int topCell = 0;
                    int minSurface = world.Top(wx, wy);
                    for (int dx = 0; dx < 10; dx++)
                    {
                        for (int dy = 0; dy < 10; dy++)
                        {
                            minSurface = Math.Min(minSurface, world.Top(wx + dx, wy + dy));
                        }
                    }

                    for (int cz = n - 1; cz >= 0; cz--)
                    {
                        int type = T(cx, cy, cz);
                        if (type == 0)
                        {
                            continue;
                        }

                        if (!roof)
                        {
                            topCell = baseZ + 10 * (cz + 1) - (type == 4 ? 10 : 0);
                        }

                        if (type == 4)
                        {
                            entrance = true;
                            roof = true;
                            continue;
                        }

                        if (!roof && 10 * cz + 5 + baseZ > minSurface)
                        {
                            if (subtype == 2)
                            {
                                writer.Fill(0x5CF0CC, wx, wy, 10 * cz + 11 + baseZ, 10, 10, 3, palA, 0f, 0);
                            }
                            else if (subtype == 0 || subtype == 1 || subtype == 3)
                            {
                                int top = baseZ + 10 * (cz + 1);
                                if (Open(T(cx - 1, cy, cz)) && Open(T(cx - 1, cy, cz + 1)))
                                {
                                    writer.Fill(0x5CF1B2, wx - 3, wy - 3, top, 2, 16, 3, palA, 0f, 0);
                                    for (int k = 0; k < 8; k++)
                                    {
                                        int yy = wy - 2 + 2 * k;
                                        writer.Crenel(wx - 3, yy, top + 3);
                                        writer.Fill(0x5CF286, wx - 3, yy - 1, top + 3, 1, 1, 1, palA, 0f, 0);
                                    }
                                }

                                if (Open(T(cx + 1, cy, cz)) && Open(T(cx + 1, cy, cz + 1)))
                                {
                                    int x2 = x0 + 10 * (cx + 1);
                                    writer.Fill(0x5CF37E, x2 + 1, wy - 3, top, 2, 16, 3, palA, 0f, 0);
                                    for (int k = 0; k < 8; k++)
                                    {
                                        int yy = wy - 1 + 2 * k;
                                        writer.Crenel(x2 + 2, yy, top + 3);
                                        writer.Fill(0x5CF455, x2 + 2, yy - 1, top + 3, 1, 1, 1, palA, 0f, 0);
                                    }
                                }

                                if (Open(T(cx, cy - 1, cz)) && Open(T(cx, cy - 1, cz + 1)))
                                {
                                    writer.Fill(0x5CF541, wx - 3, wy - 3, top, 16, 2, 3, palA, 0f, 0);
                                    for (int k = 0; k < 8; k++)
                                    {
                                        int xx = wx - 2 + 2 * k;
                                        writer.Crenel(xx, wy - 3, top + 3);
                                        writer.Fill(0x5CF616, xx - 1, wy - 3, top + 3, 1, 1, 1, palA, 0f, 0);
                                    }
                                }

                                if (Open(T(cx, cy + 1, cz)) && Open(T(cx, cy + 1, cz + 1)))
                                {
                                    int y2 = y0 + 10 * (cy + 1);
                                    writer.Fill(0x5CF70D, wx - 3, y2 + 1, top, 16, 2, 3, palA, 0f, 0);
                                    for (int k = 0; k < 8; k++)
                                    {
                                        int xx = wx - 1 + 2 * k;
                                        writer.Crenel(xx, y2 + 2, top + 3);
                                        writer.Fill(0x5CF7E5, xx - 1, y2 + 2, top + 3, 1, 1, 1, palA, 0f, 0);
                                    }
                                }

                                if (subtype == 3 && Open(T(cx - 1, cy, cz)) && Open(T(cx + 1, cy, cz))
                                    && Open(T(cx, cy - 1, cz)) && Open(T(cx, cy + 1, cz)))
                                {
                                    writer.Fill(0x5CF92D, wx - 2, wy - 2, baseZ + 10 * (cz + 1), 14, 14, 4, wall, 0f, 0);
                                    for (int b = 14, i = 0; b > 0; b -= 2, i++)
                                    {
                                        writer.Fill(0x5CF98E, wx - 2 + i, wy - 2 + i, baseZ + 10 * cz + 14 + 2 * i, b, b, 2,
                                            wall, 0f, 0);
                                    }
                                }
                            }
                        }

                        writer.Fill(0x5CFB4D, wx - 2, wy - 2, baseZ + 10 * cz - 2, 14, 14, 14, palA, scale, 0);
                        roof = true;
                    }

                    if (!(roof || subtype == 4 || subtype == 5) || !world.HasColumn(wx, wy))
                    {
                        continue;
                    }

                    int minBottom = world.Bottom(wx, wy);
                    for (int dx = -2; dx < 12; dx++)
                    {
                        for (int dy = -2; dy < 12; dy++)
                        {
                            if (world.HasColumn(wx + dx, wy + dy))
                            {
                                minBottom = Math.Min(minBottom, world.Bottom(wx + dx, wy + dy));
                            }
                        }
                    }

                    if (subtype >= 0 && subtype <= 3)
                    {
                        writer.Fill(0x5D06C2, wx - 2, wy - 2, minBottom, 14, 14, topCell - minBottom, palA, scale,
                            entrance ? 0x80 : 0);
                    }
                    else if (subtype == 4)
                    {
                        int half = n / 2 - 1;
                        int dxa = Math.Abs(cx - half);
                        int dya = Math.Abs(cy - half);
                        int steps = n / 2 - Math.Max(dxa, dya) - 1;
                        if (cx == half && cy == half) steps--;
                        int h = baseZ + 10 * (bestEz - 5 + steps);
                        if (dxa <= 1 && dya <= 1) h += 2;
                        writer.Fill(0x5CFD77, wx - 2, wy - 2, minBottom, 14, 14, h - minBottom - 4, palA, scale, 0);
                        if (h - minBottom > 0)
                        {
                            writer.Fill(0x5CFDDF, wx - 1, wy - 1, h - 4, 12, 12, 3, palA, 0f, 0);
                            writer.Fill(0x5CFE2D, wx, wy, h - 1, 10, 10, 3, palA, 0f, 0);
                            // PARTIAL: surface-coloured cap of 0x5CFE41…0x5CFFAF (noise(x·0.02, y·0.02) > 0, no rand()).
                        }
                    }
                }
            }

            return build;
        }

        private sealed class Writer
        {
            private readonly IAlphaDungeonWorld _world;
            private readonly AlphaCrtRandom _random;
            private readonly AlphaDungeonBuild _build;
            private readonly int[] _jitter = new int[23];

            public Writer(IAlphaDungeonWorld world, AlphaCrtRandom random, AlphaDungeonBuild build)
            {
                _world = world;
                _random = random;
                _build = build;
            }

            /// <summary>0x5CF1D0 crenel step: empty or water above the wall becomes covered air (0x40).</summary>
            public void Crenel(int x, int y, int z)
            {
                int t = _world.Type(x, y, z);
                if (t == 0 || t == 2)
                {
                    _world.Write(x, y, z, Voxel.Pack(0, 0, 0, 0x40));
                }
            }

            /// <summary>0x5DF600: 3 rand() for the jitter table, one per voxel (x, y, z descending) for the material.</summary>
            public void Fill(int site, int x, int y, int z, int sx, int sy, int sz, byte[] color, float p, int type)
            {
                _build.Fills.Add(new AlphaDungeonFill(site, x, y, z, sx, sy, sz, type));
                for (int i = 0; i < 23; i++)
                {
                    _jitter[i] = (i & 7) == 0 ? _random.Next() % 20 : 0;
                }

                for (int ix = 0; ix < sx; ix++)
                {
                    for (int iy = 0; iy < sy; iy++)
                    {
                        for (int iz = sz - 1; iz >= 0; iz--)
                        {
                            int j = _jitter[((7 * iz + ix) / 2 + ((iy + 7 * iz + ix) / 2) * 7) % 23];
                            int flags = p < _random.Next() / RandMax ? (type | 0x46) : (type | 0x4D);
                            int r = Clamp(color[0] + j);
                            int g = Clamp(color[1] + j);   // PARTIAL: Alpha adds noise2d_sample·(120 − g)
                            int b = Clamp(color[2] + j);
                            _world.Write(x + ix, y + iy, z + iz, Voxel.Pack(r, g, b, flags & 0xFF));
                        }
                    }
                }
            }

            private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;
        }
    }
}

namespace CubeGame.Features.World
{
    /// <summary>
    /// <see cref="IAlphaDungeonWorld"/> over one generated CubeGame zone: Alpha x, y → CubeGame x, z; Alpha z → height.
    /// Columns outside the zone do not exist (Alpha's reader then returns stone, type 1); writes outside are dropped.
    /// </summary>
    public sealed class AlphaZoneDungeonWorld : IAlphaDungeonWorld
    {
        private readonly AlphaZone _zone;
        private readonly AlphaColors _colors;

        public AlphaZoneDungeonWorld(AlphaTerrainGenerator terrain, AlphaZone zone)
        {
            _zone = zone ?? throw new System.ArgumentNullException(nameof(zone));
            _colors = new AlphaColors(terrain ?? throw new System.ArgumentNullException(nameof(terrain)));
        }

        private VoxelColumn Column(int x, int y)
        {
            int lx = x - _zone.Zx * AlphaZone.Size;
            int ly = y - _zone.Zz * AlphaZone.Size;
            return lx < 0 || ly < 0 || lx >= AlphaZone.Size || ly >= AlphaZone.Size ? null : _zone.Column(x, y);
        }

        public bool HasColumn(int x, int y) => Column(x, y) != null;

        public int Bottom(int x, int y) => Column(x, y).Bottom;

        public int Top(int x, int y)
        {
            VoxelColumn c = Column(x, y);
            return c.Bottom + c.Count;
        }

        public int Type(int x, int y, int z)
        {
            VoxelColumn c = Column(x, y);
            return c == null ? 1 : Voxel.Material(c.Read(z));
        }

        public int Flags(int x, int y, int z)
        {
            VoxelColumn c = Column(x, y);
            return c == null ? 1 : Voxel.Flags(c.Read(z));
        }

        public void Write(int x, int y, int z, uint voxel)
        {
            VoxelColumn c = Column(x, y);
            if (c != null)
            {
                AlphaZoneGenerator.Write(_colors, c, x, y, z, voxel);
            }
        }
    }
}
