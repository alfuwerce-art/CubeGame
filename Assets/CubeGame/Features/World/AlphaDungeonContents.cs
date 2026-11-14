using System;
using System.Collections.Generic;
using CubeGame.Features.Items;

namespace CubeGame.Features.World
{
    /// <summary>Which list of the zone record an object of the dungeon generator goes to.</summary>
    public enum AlphaDungeonObjectList
    {
        /// <summary>zone+0x04 (0x4862E0): lights, hanging and wall decor, stands.</summary>
        Decor,

        /// <summary>zone+0x0C (0x5A0E00): niche lights and furniture from 0x5F6E20.</summary>
        Static,

        /// <summary>zone+0x18 (0x66ADD0): a creature; here the chest (role 6, kind 0x8F).</summary>
        Creature,

        /// <summary>zone+0x30 (0x44D590): an item lying on a shelf or table.</summary>
        GroundItem,
    }

    /// <summary>Object record pushed by the dungeon generator. Coordinates are Alpha world voxels (x, y horizontal,
    /// z up); <see cref="Size"/> is the furniture box of 0x5F6E20 (+0x24).</summary>
    public sealed class AlphaDungeonObject
    {
        public AlphaDungeonObject(AlphaDungeonObjectList list, int type, double x, double y, double z, int orientation = 0)
        {
            List = list;
            Type = type;
            X = x;
            Y = y;
            Z = z;
            Orientation = orientation;
        }

        public AlphaDungeonObjectList List { get; }
        public int Type { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public int Orientation { get; }
        public float SizeX { get; set; }
        public float SizeY { get; set; }
        public float SizeZ { get; set; }
        public float Angle { get; set; }
        public int Level { get; set; }

        /// <summary>Creature flags word +0x7A (the master carries 0x1000 | 0x200).</summary>
        public int Flags { get; set; }

        /// <summary>Creature role +0x28 (1 hostile, 3 with the generator flag, 6 chest).</summary>
        public int Role { get; set; }

        /// <summary>Creature quality +0x58.</summary>
        public int Quality { get; set; }

        /// <summary>Group leader a follower walks after (0x43AB80 target = leader id).</summary>
        public AlphaDungeonObject Leader { get; set; }

        /// <summary>Waypoints of the creature's walk behaviour 0x58CFA0 (0x5BF4F0), world voxels.</summary>
        public List<(double X, double Y, double Z)> Route { get; } = new List<(double X, double Y, double Z)>();
        public List<AlphaItemRecord> Items { get; } = new List<AlphaItemRecord>();

        public override string ToString() => $"{List}:{Type:x}@{X},{Y},{Z}";
    }

    /// <summary>A branch of 0x5CC510 stage 4b that is not ported yet was reached.</summary>
    public sealed class AlphaDungeonNotPortedException : Exception
    {
        public AlphaDungeonNotPortedException(string where) : base("not ported: " + where)
        {
        }
    }

    /// <summary>
    /// Alpha dungeon generator 0x5CC510, contents stage 4b (cell pass from 0x5D0979), in progress (P7-B). Ported and
    /// checked draw by draw against executed Alpha code (FullM0P7DungeonContentsTests): the cuboid carving 0x59ABA0 of
    /// room and entrance cells, wall niches with lights, stairs, floors and carpets, the 1/40 type-7 objects. Branches
    /// not ported yet throw <see cref="AlphaDungeonNotPortedException"/> with their address.
    /// Known limitation: the oracle has no model registry, so structures 0x5F0CE0 (models 0x9F0/0x9F1 under ceilings)
    /// are absent there as well — PARTIAL until the models are loaded.
    /// </summary>
    public static class AlphaDungeonContents
    {
        public static List<AlphaDungeonObject> Write(AlphaDungeonRooms cells, AlphaDungeonBuild build,
            IAlphaDungeonWorld world, int x0, int y0, bool flag, AlphaCrtRandom random,
            int zoneKind = 1, int level = 1, int quality = 0)
        {
            AlphaDungeonVolume v = cells.Volume;
            int n = v.SizeX;
            int subtype = cells.Subtype;
            int baseZ = build.Base;
            var objects = new List<AlphaDungeonObject>();
            byte[] palB = cells.PaletteB;
            float scale = cells.PaletteScale;
            var writer = new Fills(world, random);
            byte[] carpet = { build.Color1[2], build.Color1[1], build.Color1[0] };
            byte[] border = { build.Color2[2], build.Color2[1], build.Color2[0] };

            (byte T, byte F) C(int x, int y, int z)
            {
                if (x < 0 || y < 0 || z < 0 || x >= n || y >= n || z >= n) return (1, 0);
                return (v.Type(x, y, z), v.Flags(x, y, z));
            }

            bool Solid(int t) => t != 0 && t != 2;

            if (subtype == 5)
            {
                throw new AlphaDungeonNotPortedException("0x5D0705 subtype-5 mound");
            }

            for (int cx = 0; cx < n; cx++)
            {
                for (int cy = 0; cy < n; cy++)
                {
                    for (int cz = 0; cz < n; cz++)
                    {
                        (byte t, byte fl) = C(cx, cy, cz);
                        if (t != 3 && t != 4)
                        {
                            continue;
                        }

                        int wx = x0 + 10 * cx;
                        int wy = y0 + 10 * cy;
                        int wz = baseZ + 10 * cz;
                        Carve(world, wx, wy, wz, 10, 10, 10, subtype == 1 || subtype == 2 || subtype == 4);

                        if ((subtype == 0 || subtype == 1 || subtype == 3) && t != 4 && (fl & 1) == 0)
                        {
                            int zn = wz + 2;
                            Niche(0x5D0BC7, 0, -1, wx + 5, wy - 3, wx + 4, wy - 2);
                            Niche(0x5D0D7E, 0, 1, wx + 5, wy + 13, wx + 4, wy + 10);
                            Niche(0x5D0F22, -1, 0, wx - 3, wy + 5, wx - 2, wy + 4);
                            Niche(0x5D10CE, 1, 0, wx + 13, wy + 5, wx + 10, wy + 4);

                            void Niche(int site, int dx, int dy, int px, int py, int cutX, int cutY)
                            {
                                random.At(site, site);
                                if ((random.Next() & 1) != 0 && C(cx + dx, cy + dy, cz).T == 0
                                    && !Solid(world.Type(px, py, zn)))
                                {
                                    Carve(world, cutX, cutY, zn, 2, 2, 4, false);
                                    objects.Add(new AlphaDungeonObject(AlphaDungeonObjectList.Static, 4, cutX, cutY, zn));
                                }
                            }
                        }

                        if ((fl & 1) != 0)
                        {
                            // 0x5D12A1…0x5D175D: 11-step stairs climbing towards the room above (palette B)
                            if (C(cx + 1, cy, cz).T != 3 && C(cx - 1, cy, cz).T == 3 && C(cx + 1, cy, cz + 1).T == 3)
                                for (int k = 0; k < 11; k++) writer.Fill(wx - 1 + k, wy, wz - 1 + k, 11 - k, 10, 1, palB, scale, 0);
                            if (C(cx - 1, cy, cz).T != 3 && C(cx + 1, cy, cz).T == 3 && C(cx - 1, cy, cz + 1).T == 3)
                                for (int k = 0; k < 11; k++) writer.Fill(wx, wy, wz - 1 + k, 11 - k, 10, 1, palB, scale, 0);
                            if (C(cx, cy + 1, cz).T != 3 && C(cx, cy - 1, cz).T == 3 && C(cx, cy + 1, cz + 1).T == 3)
                                for (int k = 0; k < 11; k++) writer.Fill(wx, wy - 1 + k, wz - 1 + k, 10, 11 - k, 1, palB, scale, 0);
                            if (C(cx, cy - 1, cz).T != 3 && C(cx, cy + 1, cz).T == 3 && C(cx, cy - 1, cz + 1).T == 3)
                                for (int k = 0; k < 11; k++) writer.Fill(wx, wy, wz - 1 + k, 10, 11 - k, 1, palB, scale, 0);
                        }

                        if (subtype == 4 && t == 4)
                        {
                            continue;
                        }

                        bool ceiling = true;
                        if (C(cx, cy, cz - 1).T != 3)
                        {
                            writer.Fill(wx, wy, wz, 10, 10, 1, palB, scale, 0);
                            if ((subtype == 0 || subtype == 3) && (fl & 2) != 0)
                            {
                                int x1 = wx, x2 = wx + 10, y1 = wy, y2 = wy + 10;
                                bool a = (C(cx - 1, cy, cz).F & 2) == 0;
                                if (a) x1 += 2;
                                bool b = (C(cx + 1, cy, cz).F & 2) == 0;
                                if (b) x2 -= 2;
                                bool c = (C(cx, cy - 1, cz).F & 2) == 0;
                                if (c) y1 += 2;
                                bool d = (C(cx, cy + 1, cz).F & 2) == 0;
                                if (d) y2 -= 2;
                                Box(world, x1, y1, wz, x2 - x1, y2 - y1, 1, carpet);
                                if (a) Box(world, x1, y1, wz, 1, y2 - y1, 1, border);
                                if (b) Box(world, x2 - 1, y1, wz, 1, y2 - y1, 1, border);
                                if (c) Box(world, x1, y1, wz, x2 - x1, 1, 1, border);
                                if (d) Box(world, x1, y2 - 1, wz, x2 - x1, 1, 1, border);
                            }

                            if (t == 4)
                            {
                                ceiling = false;
                            }
                            else if ((fl & 1) == 0)
                            {
                                random.At(0x5D1AFF, 0x5D1BE9);
                                if (!flag && random.Next() % 40 == 0)
                                {
                                    random.Next();
                                    random.Next();
                                    objects.Add(new AlphaDungeonObject(AlphaDungeonObjectList.Decor, 7, wx + 5, wy + 5, wz + 1));
                                }
                                else
                                {
                                    Furnish(cells, world, objects, cx, cy, cz, x0, y0, baseZ, zoneKind, level, quality, random);
                                }
                            }
                        }

                        // 0x5D3611: under a non-room cell — structures 0x5F0CE0 (absent: no model registry, PARTIAL);
                        // subtype 3: rand()%10 == 0 → hanging object 0x38.
                        random.At(0x5D360B, 0x5D3AC3);
                        if (ceiling && t != 4 && C(cx, cy, cz + 1).T != 3 && subtype == 3 && random.Next() % 10 == 0)
                        {
                            objects.Add(new AlphaDungeonObject(AlphaDungeonObjectList.Decor, 0x38, wx + 5, wy + 5, wz + 9));
                        }

                        // 0x5D3AC3: master of the dungeon
                        if ((fl & 4) != 0)
                        {
                            objects.Add(Master(subtype, wx, wy, wz, level, quality, random));
                        }
                    }
                }
            }

            // 0x5D3D9C: subtype 4 tower top (0x5D3DB1) and subtype 5 mound finish (0x5D42E0) are not ported yet
            if (subtype == 4)
            {
                throw new AlphaDungeonNotPortedException(PostPass);
            }

            if (subtype == 1 || subtype == 2)
            {
                Erode(world, x0, y0, v.SizeX, v.SizeY);
            }

            Populate(cells, baseZ, objects, x0, y0, flag, level, quality, random);
            return objects;
        }

        /// <summary>Message of the stop at the post-pass (tests compare the draws made up to it).</summary>
        public const string PostPass = "0x5D3D9C post-pass";

        /// <summary>Behaviour key of the dungeon master spawn (Alpha +0x10E8 → creature +0x1D38).</summary>
        public const string MasterBehaviourKey = "alpha_dungeon_master";

        /// <summary>Behaviour key of the dungeon's room creatures (groups, followers, guards).</summary>
        public const string CreatureBehaviourKey = "alpha_dungeon_creature";

        /// <summary>True for the cells 0x5EEA40 treats as open: empty (0), column (2) and the outside (1).</summary>
        private static bool Open(int t) => t == 0 || t == 2 || t == 1;

        /// <summary>
        /// 0x5D1C1A…0x5D360B: furnishing of a room cell with a floor (not a stair, no type-7 object).
        /// Under an open cell: hanging object 0x37 (subtypes 2/4) and wall decor 0x39/0x3A on four sides (subtypes
        /// 1/2). Then, per side with an open neighbour (1/3): a stand 0x5F8960 against a solid wall (1/2), furniture
        /// 0x5F6E20 (7/8) or a chest (1/8). Side +y skips the furniture roll after a stand roll (as the bytes do).
        /// Finally every new furniture piece is stocked: 0x0A gets equipment, shelves and tables (0x0C, 0x0D,
        /// 0x23–0x25) get items on 1/10 of their cells.
        /// </summary>
        private static void Furnish(AlphaDungeonRooms cells, IAlphaDungeonWorld world, List<AlphaDungeonObject> objects,
            int cx, int cy, int cz, int x0, int y0, int baseZ, int zoneKind, int level, int quality, AlphaCrtRandom random)
        {
            AlphaDungeonVolume v = cells.Volume;
            int n = v.SizeX;
            int subtype = cells.Subtype;

            int T(int x, int y, int z) => x < 0 || y < 0 || z < 0 || x >= n || y >= n || z >= n ? 1 : v.Type(x, y, z);

            int wx = x0 + 10 * cx, wy = y0 + 10 * cy, wz = baseZ + 10 * cz;
            int firstStatic = objects.Count;
            float top = baseZ + 10 * (cz + 1);

            // 0x5D1C1A: under an open cell
            random.At(0x5D1C1A, 0x5D1D94);
            if (Open(T(cx, cy, cz + 1)))
            {
                if (random.Next() % 3 == 0 && (subtype == 4 || subtype == 2))
                {
                    float angle = random.Next() / 32767f * 360f;
                    float s = random.Next() * 0.04f / 32767f + 0.08f;
                    float z = top - s * 20f;
                    int y = wy + 2 + random.Next() % 5;
                    int x = wx + 2 + random.Next() % 5;
                    objects.Add(new AlphaDungeonObject(AlphaDungeonObjectList.Decor, 0x37, x, y, z) { Angle = angle, SizeX = s });
                }

                random.At(0x5D1D94, 0x5D2328);
                if (subtype == 2 || subtype == 1)
                {
                    // sides −x (0°), +x (180°), +y (270°), −y (90°)
                    WallDecor(-1, 0, 0f);
                    WallDecor(1, 0, 180f);
                    WallDecor(0, 1, 270f);
                    WallDecor(0, -1, 90f);
                }
            }

            void WallDecor(int dx, int dy, float angle)
            {
                if (random.Next() % 3 != 0 || !Open(T(cx + dx, cy + dy, cz)))
                {
                    return;
                }

                float s = random.Next() * 0.05f / 32767f + 0.08f;
                float z = top - s * 20f;
                float x, y;
                if (dx != 0)
                {
                    y = wy + 2 + random.Next() % 5;
                    x = dx < 0 ? wx + s * 10f : wx + 10 - s * 10f;
                }
                else
                {
                    y = dy < 0 ? wy + s * 10f : wy + 10 - s * 10f;
                    x = wx + 2 + random.Next() % 5;
                }

                int type = 0x39 + (random.Next() & 1);
                objects.Add(new AlphaDungeonObject(AlphaDungeonObjectList.Decor, type, x, y, z) { Angle = angle, SizeX = s });
            }

            // 0x5D2328…0x5D30CB: stands, furniture and chests along the four walls
            Side(0x5D2328, 0x5D2687, -1, 0, 270f, 1);
            Side(0x5D2687, 0x5D29F2, 1, 0, 90f, 3);
            Side(0x5D29F2, 0x5D2D57, 0, -1, 0f, 2);
            Side(0x5D2D57, 0x5D30CB, 0, 1, 180f, 0);

            void Side(int from, int to, int dx, int dy, float angle, int orient)
            {
                random.At(from, to);
                if (random.Next() % 3 != 0 || !Open(T(cx + dx, cy + dy, cz)))
                {
                    return;
                }

                // coordinate across the wall at `inset` from it, and the random one along it (rand()&3 + 3)
                (double X, double Y) At(double inset)
                {
                    int along = (random.Next() & 3) + 3;
                    if (dx != 0)
                    {
                        return (dx < 0 ? wx + inset : wx + 10 - inset, wy + along);
                    }

                    return (wx + along, dy < 0 ? wy + inset : wy + 10 - inset);
                }

                if ((random.Next() & 1) != 0)
                {
                    (double sx, double sy) = At(0.5);
                    // 0x42F860/0x43B480: the voxel one step further out must be solid
                    int px = (int)Math.Floor(sx + dx), py = (int)Math.Floor(sy + dy);
                    if (world.Type(px, py, wz + 2) != 0 && world.Type(px, py, wz + 2) != 2)
                    {
                        objects.Add(new AlphaDungeonObject(AlphaDungeonObjectList.Decor, random.Callee(() => StandType(subtype, random)), sx, sy,
                            wz + 2) { Angle = angle });
                    }

                    if (dx == 0 && dy > 0)
                    {
                        return; // 0x5D2ED5 / 0x5D30C5: side +y ends after a stand roll
                    }
                }

                if ((random.Next() & 7) != 0)
                {
                    (double fx, double fy) = At(0.5);
                    objects.Add(random.Callee(() => MakeFurniture(fx, fy, wz + 1, orient, zoneKind, random)));
                }
                else
                {
                    (double kx, double ky) = At(1.0);
                    var chest = new AlphaDungeonObject(AlphaDungeonObjectList.Creature, 0x8F, kx, ky, wz + 1) { Level = level, Role = 6 };
                    chest.Items.AddRange(random.Callee(() => AlphaLoot.ChestContents(random, level)));
                    objects.Add(chest);
                }
            }

            // 0x5D30CB…0x5D3605: stock the furniture placed in this cell
            random.At(0x5D30CB, 0x5D360B);
            for (int i = firstStatic; i < objects.Count; i++)
            {
                AlphaDungeonObject o = objects[i];
                if (o.List != AlphaDungeonObjectList.Static)
                {
                    continue;
                }

                if (o.Type == 0x0A)
                {
                    // 0x5D30F3: (rand()&3) + 1 pieces of equipment of quality + 1; the rack is marked (+0x30 = 2)
                    int count = (random.Next() & 3) + 1;
                    for (int k = 0; k < count; k++) o.Items.Add(random.Callee(() => AlphaLoot.RandomEquipment(random, level, quality + 1)));
                    continue;
                }

                if (o.Type != 0x0D && o.Type != 0x0C && o.Type != 0x23 && o.Type != 0x24 && o.Type != 0x25)
                {
                    continue;
                }

                for (int gx = 0; o.SizeX > gx; gx++)
                {
                    for (int gy = 0; o.SizeY > gy; gy++)
                    {
                        if (random.Next() % 10 != 0)
                        {
                            continue;
                        }

                        AlphaItemRecord item = ShelfItem(level, quality, random);
                        var g = new AlphaDungeonObject(AlphaDungeonObjectList.GroundItem, item.Type,
                            o.X + gx - o.SizeX * 0.5f + 0.5f, o.Y + gy - o.SizeY * 0.5f + 0.5f, o.Z + o.SizeZ)
                        {
                            Angle = random.Next() * 360f / 32767f,
                        };
                        g.Items.Add(item);
                        objects.Add(g);
                    }
                }
            }
        }

        /// <summary>A leader list and a follower list of 0x5CC510 (group list 0x2BF4, built at 0x5CC5E3).</summary>
        public sealed class MonsterGroup
        {
            public MonsterGroup(int[] leaders, int[] followers)
            {
                Leaders = leaders;
                Followers = followers;
            }

            public int[] Leaders { get; }
            public int[] Followers { get; }
        }

        /// <summary>
        /// Monster groups by subtype (byte-verified 0x5CC5EA…0x5CC93C): 0/4 — {0x2E | 0x13}; 1/2 — {0x0F, 0x10 | 0x60}
        /// and {extra | —} with extra = 0x61, 0x5E, 0x11 for <see cref="AlphaDungeonRooms.ExtraKindChoice"/> 0, 1, 2;
        /// 3 — {2, 3 | 0x13}; 5 — {0x11, 0x51 | 0x3E, 0x1E}.
        /// </summary>
        public static List<MonsterGroup> MonsterGroups(int subtype, int extraKindChoice)
        {
            switch (subtype)
            {
                case 1:
                case 2:
                    int extra = extraKindChoice == 1 ? 0x5E : extraKindChoice == 2 ? 0x11 : 0x61;
                    return new List<MonsterGroup>
                    {
                        new MonsterGroup(new[] { 0x0F, 0x10 }, new[] { 0x60 }),
                        new MonsterGroup(new[] { extra }, new int[0]),
                    };
                case 3:
                    return new List<MonsterGroup> { new MonsterGroup(new[] { 2, 3 }, new[] { 0x13 }) };
                case 5:
                    return new List<MonsterGroup> { new MonsterGroup(new[] { 0x11, 0x51 }, new[] { 0x3E, 0x1E }) };
                default:
                    return new List<MonsterGroup> { new MonsterGroup(new[] { 0x2E }, new[] { 0x13 }) };
            }
        }

        /// <summary>0x5FA440: volume storage coordinates → generator coordinates (inverse of 0x5FA3C0).</summary>
        private static (int X, int Y) FromStorage(AlphaDungeonVolume v, int x, int y)
        {
            if (v.Mirror)
            {
                y = v.SizeY - y - 1;
            }

            switch (v.Rotation & 3)
            {
                case 1: return (y, v.SizeY - x - 1);
                case 2: return (v.SizeX - x - 1, v.SizeY - y - 1);
                case 3: return (v.SizeX - y - 1, x);
                default: return (x, y);
            }
        }

        /// <summary>
        /// 0x5D4817…0x5D4AA3 (subtypes 1/2, no rand()): per world column of the volume area the top
        /// <c>−min(0, 2·noise(0.1x, 0.1y) + 8·noise(0.05x, 0.05y))</c> voxels that carry 0x40 without 0x80 (the dungeon's
        /// own fills) become air — the voxel 0x76DDF8 that the CRT initializer 0x6FAE30 sets to FF FF FF 00.
        /// </summary>
        private static void Erode(IAlphaDungeonWorld world, int x0, int y0, int sizeX, int sizeY)
        {
            uint air = Voxel.Pack(0xFF, 0xFF, 0xFF, 0);
            for (int i = 0; i < sizeX; i++)
            {
                for (int j = 0; j < sizeY; j++)
                {
                    for (int x = x0 + 10 * i; x < x0 + 10 * i + 10; x++)
                    {
                        for (int y = y0 + 10 * j; y < y0 + 10 * j + 10; y++)
                        {
                            if (!world.HasColumn(x, y))
                            {
                                continue;
                            }

                            float f = AlphaNoise.Sample(x * 0.1, y * 0.1) * 2f;
                            f += AlphaNoise.Sample(x * 0.05, y * 0.05) * 8f;
                            if (f > 0f)
                            {
                                f = 0f;
                            }

                            int top = world.Top(x, y);
                            int limit = (int)(top + f);
                            for (int z = top; z >= limit; z--)
                            {
                                int flags = world.Flags(x, y, z);
                                if ((flags & 0x40) != 0 && (flags & 0x80) == 0)
                                {
                                    world.Write(x, y, z, air);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 0x5D4AA9…0x5D5F34: per present room (3×3×3 grid, not byte +2) with d = room size in cells and its four floor
        /// corners mapped by 0x5FA440 —
        /// a group when dx·dy &gt; 6: group <c>rand()%n</c>, corner <c>rand()&amp;3</c>, leader kind <c>rand()%n</c>
        /// (with the generator flag: role 3, kind <c>rand()&amp;1</c>), patrol route <c>rand()%3</c> (4 corners forward,
        /// 4 backward, or c, c+1, c+2, c+1), <c>rand()&amp;1</c> potions, then <c>rand()%3+1</c> followers;
        /// guards of the master kinds when the room is not the master's: <c>(rand()&amp;1)+1</c> rings (+1 when
        /// dx·dy &gt; 7) from corner <c>rand()&amp;3</c> on, each <c>(rand()&amp;1)+2</c> creatures at radius 2; and when
        /// dx, dy &gt; 2 and <c>rand()&amp;1</c>, one more ring at the room centre if that cell is a room cell.
        /// Positions: cell centre (x+5, y+5), floor + 1. Ring offsets are computed in float like the original.
        /// </summary>
        private static void Populate(AlphaDungeonRooms cells, int baseZ, List<AlphaDungeonObject> objects, int x0, int y0,
            bool flag, int level, int quality, AlphaCrtRandom random)
        {
            AlphaDungeonVolume v = cells.Volume;
            List<MonsterGroup> groups = MonsterGroups(cells.Subtype, cells.ExtraKindChoice);
            int[] guardKinds = MasterKinds(cells.Subtype);
            var corners = new (int X, int Y, int Z)[4];

            (double X, double Y, double Z) At((int X, int Y, int Z) c) => (x0 + 10 * c.X + 5, y0 + 10 * c.Y + 5, baseZ + 10 * c.Z + 1);

            AlphaDungeonObject Creature(int kind, double x, double y, double z, int role)
            {
                var o = new AlphaDungeonObject(AlphaDungeonObjectList.Creature, kind, x, y, z) { Level = level, Role = role };
                if (role == 1)
                {
                    o.Flags = 0x1000;
                    o.Quality = quality;
                }

                return o;
            }

            int Kind(int[] list, out int role)
            {
                if (flag)
                {
                    role = 3;
                    return random.Next() & 1;
                }

                role = 1;
                return list[(int)((uint)random.Next() % (uint)list.Length)];
            }

            void Potions(AlphaDungeonObject o)
            {
                int n = random.Next() & 1;
                for (int i = 0; i < n; i++) o.Items.Add(new AlphaItemRecord(1, 1, level: o.Level));
            }

            void Ring((int X, int Y, int Z) c, int m)
            {
                (double cx, double cy, double cz) = At(c);
                for (int k = 0; k < m; k++)
                {
                    float angle = (float)(2 * k * Math.PI / m);
                    float x = (float)cx + (float)Math.Cos(angle) * 2f;
                    float y = (float)cy + (float)Math.Sin(angle) * 2f;
                    int kind = Kind(guardKinds, out int role);
                    AlphaDungeonObject o = Creature(kind, x, y, cz, role);
                    o.Angle = (float)(angle / Math.PI * 180.0 + 90.0);
                    o.Route.Add((x, y, cz));
                    Potions(o);
                    objects.Add(o);
                }
            }

            for (int a = 0; a < 3; a++)
            {
                for (int b = 0; b < 3; b++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        AlphaDungeonRoom room = cells.Rooms[a * 9 + b * 3 + c];
                        if (room == null || !room.Present || room.Byte2)
                        {
                            continue;
                        }

                        int[] o0 = room.Origin;
                        int dx = room.Size[0], dy = room.Size[1];
                        (int, int)[] raw =
                        {
                            (o0[0], o0[1]), (o0[0] + dx - 1, o0[1]), (o0[0] + dx - 1, o0[1] + dy - 1), (o0[0], o0[1] + dy - 1),
                        };
                        for (int i = 0; i < 4; i++)
                        {
                            (int x, int y) = FromStorage(v, raw[i].Item1, raw[i].Item2);
                            corners[i] = (x, y, o0[2]);
                        }

                        // 0x5D4CA5: a group with a patrol route
                        random.At(0x5D4AA9, 0x5D563A);
                        if (dx * dy > 6 && groups.Count > 0)
                        {
                            MonsterGroup g = groups[(int)((uint)random.Next() % (uint)groups.Count)];
                            if (g.Leaders.Length > 0)
                            {
                                int e = random.Next() & 3;
                                (double lx, double ly, double lz) = At(corners[e & 3]);
                                int kind = Kind(g.Leaders, out int role);
                                AlphaDungeonObject leader = Creature(kind, lx, ly, lz, role);
                                switch (random.Next() % 3)
                                {
                                    case 0:
                                        for (int k = 0; k < 4; k++) leader.Route.Add(At(corners[(e + k) & 3]));
                                        break;
                                    case 1:
                                        for (int k = 0; k < 4; k++) leader.Route.Add(At(corners[3 - ((e + k) & 3)]));
                                        break;
                                    default:
                                        leader.Route.Add(At(corners[e & 3]));
                                        leader.Route.Add(At(corners[(e + 1) & 3]));
                                        leader.Route.Add(At(corners[(e + 2) & 3]));
                                        leader.Route.Add(At(corners[(e + 1) & 3]));
                                        break;
                                }

                                Potions(leader);
                                objects.Add(leader);
                                if (!flag && g.Followers.Length > 0)
                                {
                                    int n = random.Next() % 3 + 1;
                                    for (int i = 0; i < n; i++)
                                    {
                                        int fk = g.Followers[(int)((uint)random.Next() % (uint)g.Followers.Length)];
                                        AlphaDungeonObject f = Creature(fk, lx, ly, lz, 1);
                                        f.Leader = leader;
                                        f.Route.Add((lx, ly, lz));
                                        objects.Add(f);
                                    }
                                }
                            }
                        }

                        // 0x5D5640: guards of the master kinds
                        if (guardKinds.Length == 0 || room.Master)
                        {
                            continue;
                        }

                        random.At(0x5D563A, 0x5D5EF2);
                        int rings = (random.Next() & 1) + 1;
                        int dir = random.Next() & 3;
                        if (dx * dy > 7)
                        {
                            rings++;
                        }

                        for (int r = 0; r < rings; r++, dir++)
                        {
                            Ring(corners[dir & 3], (random.Next() & 1) + 2);
                        }

                        if (dx > 2 && dy > 2 && (random.Next() & 1) != 0)
                        {
                            var centre = ((corners[0].X + corners[2].X) / 2, (corners[0].Y + corners[2].Y) / 2,
                                (corners[0].Z + corners[2].Z) / 2);
                            if (v.Type(centre.Item1, centre.Item2, centre.Item3) == 3)
                            {
                                Ring(centre, (random.Next() & 1) + 2);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Master kinds of 0x5CC510 (list 0x2BE8 built at 0x5CC5E3 by subtype, byte-verified).</summary>
        public static int[] MasterKinds(int subtype)
        {
            switch (subtype)
            {
                case 1:
                case 2:
                    return new[] { 0x0F, 0x10 };
                case 3:
                    return new[] { 2, 3 };
                case 5:
                    return new[] { 0x4E, 0x4D };
                default:
                    return new[] { 0x0B, 0x0C };
            }
        }

        /// <summary>
        /// 0x5D3ACD…0x5D3D40: the master of the dungeon on the |4 cell — role 1, flags 0x1000 | 0x200, level of the
        /// zone (at least 1) at (x+4.5, y+4.5, z+1); kind <c>(unsigned)rand() % count</c> of <see cref="MasterKinds"/>;
        /// (float sums like the original, so the .5 rounds away at world scale); one piece of <see cref="AlphaLoot.RandomEquipment"/> with rarity <c>RollRarity(quality, force)</c>;
        /// <c>rand()&amp;3</c> potions 1/1 of its level. Its spawn record (type 6) goes to zone+0x48 without rand().
        /// </summary>
        private static AlphaDungeonObject Master(int subtype, int wx, int wy, int wz, int level, int quality, AlphaCrtRandom random)
        {
            random.At(0x5D3ACD, 0x5D3D45);
            int lvl = Math.Max(1, level);
            int[] kinds = MasterKinds(subtype);
            int kind = kinds[(int)((uint)random.Next() % (uint)kinds.Length)];
            var master = new AlphaDungeonObject(AlphaDungeonObjectList.Creature, kind, (float)((float)wx + 4.5f), (float)((float)wy + 4.5f), wz + 1)
            {
                Level = lvl,
                Role = 1,
                Quality = quality,
                Flags = 0x1000 | 0x200,
            };
            int rarity = random.Callee(() => AlphaLoot.RollRarity(random, quality, true));
            master.Items.Add(random.Callee(() => AlphaLoot.RandomEquipment(random, lvl, rarity)));
            int potions = random.Next() & 3;
            for (int i = 0; i < potions; i++) master.Items.Add(new AlphaItemRecord(1, 1, level: lvl));
            return master;
        }

        /// <summary>0x5F8960: stand kind by dungeon subtype (byte-verified switch 0x5F8AB8).</summary>
        private static int StandType(int subtype, AlphaCrtRandom random)
        {
            switch (subtype)
            {
                case 0:
                    return (random.Next() & 1) != 0 ? 0x36 : 0x32;
                case 1:
                case 2:
                    switch (random.Next() % 3)
                    {
                        case 0: return 0x32;
                        case 1: return 0x31;
                        default: return 0x34;
                    }

                case 3:
                    return 0x30;
                case 4:
                    return (random.Next() & 1) != 0 ? (random.Next() & 3) + 0x2C : 0x36;
                default:
                    return 0x36;
            }
        }

        /// <summary>
        /// 0x5F6E20 (byte-verified): 1/50 weapon rack 0x0A (1.5×1×1, turned by two); otherwise rand()%3 — 0: shelf
        /// rand()%3 + 0x20/0x26/0x29/0x23 by zone kind 3/4/5/other (2×1×1.57); 1: table 0x0C/0x0D/0x0E (3×3×1) moved
        /// out by 1 + r; 2: small piece 0x10/0x0F/0x11 (1×1×0.5) moved out by r (r = rand()/32767 voxels).
        /// </summary>
        private static AlphaDungeonObject MakeFurniture(double x, double y, double z, int orient, int zoneKind, AlphaCrtRandom random)
        {
            int type;
            float sx, sy, sz, shift = 0f;
            if (random.Next() % 50 == 0)
            {
                type = 0x0A;
                sx = 1.5f;
                sy = 1f;
                sz = 1f;
                shift = 0.5f;
                orient = (orient + 2) & 3;
            }
            else
            {
                switch (random.Next() % 3)
                {
                    case 0:
                        int b = zoneKind == 3 ? 0x20 : zoneKind == 4 ? 0x26 : zoneKind == 5 ? 0x29 : 0x23;
                        type = random.Next() % 3 + b;
                        sx = 2f;
                        sy = 1f;
                        sz = 1.57f;
                        break;
                    case 1:
                        type = zoneKind == 4 ? 0x0D : zoneKind == 5 ? 0x0E : 0x0C;
                        sx = 3f;
                        sy = 3f;
                        sz = 1f;
                        shift = random.Next() / 32767f + 1f;
                        break;
                    default:
                        type = zoneKind == 4 ? 0x0F : zoneKind == 5 ? 0x11 : 0x10;
                        sx = 1f;
                        sy = 1f;
                        sz = 0.5f;
                        shift = random.Next() / 32767f;
                        break;
                }
            }

            // orientation 0: −y, 1: +x, 2: +y, 3: −x (fixed-point shift, truncated)
            shift = (int)(shift * 65536f) / 65536f;
            switch (orient)
            {
                case 0: y -= shift; break;
                case 1: x += shift; break;
                case 2: y += shift; break;
                default: x -= shift; break;
            }

            return new AlphaDungeonObject(AlphaDungeonObjectList.Static, type, x, y, z, orient) { SizeX = sx, SizeY = sy, SizeZ = sz };
        }

        /// <summary>0x5D3292…0x5D3444: one item on a shelf cell.</summary>
        private static AlphaItemRecord ShelfItem(int level, int quality, AlphaCrtRandom random)
        {
            if (random.Next() % 6 != 0)
            {
                switch (random.Next() & 3)
                {
                    case 0: return new AlphaItemRecord(0x0B, 0x1A);
                    case 1: return new AlphaItemRecord(0x12, 0, seed: random.Next() % 3); // subtype: stale local 0x2B2C (UNKNOWN)
                    case 2: return new AlphaItemRecord(0x0B, 0x09, modifier: random.Next() % 3 + 0x19);
                    default: return new AlphaItemRecord(0x01, 0x07);
                }
            }

            switch (random.Next() % 6)
            {
                case 0: return new AlphaItemRecord(0x01, 0x01);
                case 1: return new AlphaItemRecord(0x01, 0x04);
                case 2: return new AlphaItemRecord(0x01, 0x05);
                case 3:
                    float e = (random.Next() * 2f / 32767f + 1f) * (level * 0.25f);
                    int v = (int)(float)Math.Pow(2.0, e);
                    int coins = random.Next() % (v * 2 + 2);
                    return new AlphaItemRecord(AlphaLoot.TypeCoin, 0, modifier: 0x0A, level: coins);
                case 4:
                    return random.Callee(() => AlphaLoot.RandomEquipment(random, level, quality));
                default:
                    return random.Callee(() => AlphaLoot.GenerateFormula(random, level, quality));
            }
        }

        /// <summary>0x59ABA0: covered air (flags 0xC0) over the box; with <paramref name="rough"/> the faces bite into the
        /// walls where noise(y·0.1 + 13x, z·0.2) / noise(x·0.1 + 13y, z·0.2) &gt; 0.5. No rand().</summary>
        internal static void Carve(IAlphaDungeonWorld w, int x, int y, int z, int sx, int sy, int sz, bool rough)
        {
            uint air = Voxel.Pack(0, 0, 0, 0xC0);
            for (int ix = 0; ix < sx; ix++)
            {
                for (int iy = 0; iy < sy; iy++)
                {
                    for (int iz = sz - 1; iz >= 0; iz--)
                    {
                        int X = x + ix, Y = y + iy, Z = z + iz;
                        w.Write(X, Y, Z, air);
                        if (!rough) continue;
                        if (ix == 0 && AlphaNoise.Sample(Y * 0.1 + X * 13, Z * 0.2) > 0.5f) w.Write(X - 1, Y, Z, air);
                        if (iy == 0 && AlphaNoise.Sample(X * 0.1 + Y * 13, Z * 0.2) > 0.5f) w.Write(X, Y - 1, Z, air);
                        if (ix == sx - 1 && AlphaNoise.Sample(Y * 0.1 + X * 13, Z * 0.2) > 0.5f) w.Write(X + 1, Y, Z, air);
                        if (iy == sy - 1 && AlphaNoise.Sample(X * 0.1 + Y * 13, Z * 0.2) > 0.5f) w.Write(X, Y + 1, Z, air);
                    }
                }
            }
        }

        /// <summary>0x5CB550: solid box of one colour (flags 0x46), z descending. No rand().</summary>
        internal static void Box(IAlphaDungeonWorld w, int x, int y, int z, int sx, int sy, int sz, byte[] rgb)
        {
            uint v = Voxel.Pack(rgb[0], rgb[1], rgb[2], 0x46);
            for (int ix = 0; ix < sx; ix++)
            {
                for (int iy = 0; iy < sy; iy++)
                {
                    for (int iz = sz - 1; iz >= 0; iz--)
                    {
                        w.Write(x + ix, y + iy, z + iz, v);
                    }
                }
            }
        }

        /// <summary>0x5DF600 voxel fill (same rule as the column stage).</summary>
        internal sealed class Fills
        {
            private readonly IAlphaDungeonWorld _world;
            private readonly AlphaCrtRandom _random;
            private readonly int[] _jitter = new int[23];

            public Fills(IAlphaDungeonWorld world, AlphaCrtRandom random)
            {
                _world = world;
                _random = random;
            }

            public void Fill(int x, int y, int z, int sx, int sy, int sz, byte[] color, float p, int type)
            {
                int from = _random.SiteFrom, to = _random.SiteTo;
                _random.At(AlphaCrtRandom.SiteOutside, AlphaCrtRandom.SiteOutside);
                for (int i = 0; i < 23; i++) _jitter[i] = (i & 7) == 0 ? _random.Next() % 20 : 0;
                for (int ix = 0; ix < sx; ix++)
                {
                    for (int iy = 0; iy < sy; iy++)
                    {
                        for (int iz = sz - 1; iz >= 0; iz--)
                        {
                            int j = _jitter[((7 * iz + ix) / 2 + ((iy + 7 * iz + ix) / 2) * 7) % 23];
                            int flags = p < _random.Next() / 32767.0f ? (type | 0x46) : (type | 0x4D);
                            _world.Write(x + ix, y + iy, z + iz,
                                Voxel.Pack(Math.Min(255, color[0] + j), Math.Min(255, color[1] + j), Math.Min(255, color[2] + j),
                                    flags & 0xFF));
                        }
                    }
                }

                _random.At(from, to);
            }
        }
    }
}
