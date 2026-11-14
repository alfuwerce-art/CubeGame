using System;
using System.Collections.Generic;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Cell volume of an Alpha dungeon (cube::Dungeon, ctor 0x5C38C0): 2 bytes per cell — [0] type (3 room air,
    /// 2 pillar, 4 special), [1] flags (|2 floor mark, |4 dungeon master cell). Index (z·SizeY + y)·SizeX + x.
    /// Rotation (+4) and mirror (+8) are 0 during the room stage.
    /// </summary>
    public sealed class AlphaDungeonVolume
    {
        public AlphaDungeonVolume(int sizeX, int sizeY, int sizeZ)
        {
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            Cells = new byte[2 * sizeX * sizeY * sizeZ];
        }

        public int SizeX { get; }

        public int SizeY { get; }

        public int SizeZ { get; }

        /// <summary>+4: 0…3 quarter turns applied when cells are addressed (0x5FA3C0).</summary>
        public int Rotation { get; internal set; }

        /// <summary>+8: mirror of the second axis.</summary>
        public bool Mirror { get; internal set; }

        /// <summary>Raw cells, the same bytes as Alpha's volume (+0x18).</summary>
        public byte[] Cells { get; }

        /// <summary>Byte offset of cell (x, y, z) after rotation/mirror, or −1 outside (Alpha writes a dummy cell).</summary>
        public int Offset(int x, int y, int z)
        {
            int cx = x;
            int cy = y;
            switch (Rotation & 3)
            {
                case 1:
                    cx = SizeX - y - 1;
                    cy = x;
                    break;
                case 2:
                    cx = SizeX - x - 1;
                    cy = SizeY - y - 1;
                    break;
                case 3:
                    cx = y;
                    cy = SizeY - x - 1;
                    break;
            }

            if (Mirror)
            {
                cy = SizeY - cy - 1;
            }

            if (cx < 0 || cy < 0 || z < 0 || cx >= SizeX || cy >= SizeY || z >= SizeZ)
            {
                return -1;
            }

            return 2 * ((z * SizeY + cy) * SizeX + cx);
        }

        public byte Type(int x, int y, int z)
        {
            int o = Offset(x, y, z);
            return o < 0 ? (byte)0 : Cells[o];
        }

        public byte Flags(int x, int y, int z)
        {
            int o = Offset(x, y, z);
            return o < 0 ? (byte)0 : Cells[o + 1];
        }

        internal void SetType(int x, int y, int z, byte type)
        {
            int o = Offset(x, y, z);
            if (o >= 0) Cells[o] = type;
        }

        internal void AddFlags(int x, int y, int z, byte flags)
        {
            int o = Offset(x, y, z);
            if (o >= 0) Cells[o + 1] |= flags;
        }
    }

    /// <summary>One of the 27 room slots (record 0x1C bytes): flags and the six random parameters A…F.</summary>
    public sealed class AlphaDungeonRoom
    {
        public int I { get; internal set; }

        public int J { get; internal set; }

        public int K { get; internal set; }

        public bool Present { get; internal set; }

        /// <summary>Byte +1: the master's room (its box centre gets the |4 cell).</summary>
        public bool Master { get; internal set; }

        /// <summary>Byte +2 (set together with <see cref="Special"/> for subtypes 4/5).</summary>
        public bool Byte2 { get; internal set; }

        /// <summary>Byte +3: fixed box (A=B=C=0, D,E,F = 0,0,1).</summary>
        public bool Special { get; internal set; }

        /// <summary>A, B, C (origin jitter), D, E, F (size deltas).</summary>
        public int[] Parameters { get; } = new int[6];

        public int[] Origin { get; } = new int[3];

        public int[] Size { get; } = new int[3];

        /// <summary>Third argument of 0x5EF8F0 (pillar block inside the room).</summary>
        public bool Pillars { get; internal set; }
    }

    public sealed class AlphaDungeonRooms
    {
        public int Subtype { get; internal set; }

        public bool Flag { get; internal set; }

        /// <summary>Subtypes 1/2: rand()%3 choice of the extra kind (0x5E, 0x11 or 0x61) of the second kind list.</summary>
        public int ExtraKindChoice { get; internal set; } = -1;

        public AlphaDungeonRoom[] Rooms { get; } = new AlphaDungeonRoom[27];

        /// <summary>Present rooms in Alpha's loop order (i, j, k).</summary>
        public List<AlphaDungeonRoom> Boxes { get; } = new List<AlphaDungeonRoom>();

        /// <summary>Cells marked |4 (the dungeon master's spawn cell).</summary>
        public List<(int X, int Y, int Z)> MasterCells { get; } = new List<(int, int, int)>();

        public AlphaDungeonVolume Volume { get; internal set; }

        /// <summary>Subtypes 0/1/3 with flag 0: rand()%3 room scheme; −1 otherwise.</summary>
        public int Scheme { get; internal set; } = -1;

        /// <summary>Room links (A slot i,j,k → B slot i,j,k) in Alpha's list order.</summary>
        public List<int[]> Links { get; } = new List<int[]>();

        /// <summary>Entrance cell (type 4) in volume coordinates before rotation.</summary>
        public (int X, int Y, int Z) Entrance { get; internal set; }

        /// <summary>Bytes at ebp−0x2BDC…−0x2BDA (three rand()%50+20).</summary>
        public byte[] WallColor { get; } = new byte[3];

        /// <summary>Bytes at ebp−0x2B40…−0x2B3E.</summary>
        public byte[] PaletteA { get; } = new byte[3];

        /// <summary>Bytes at ebp−0x2BA8…−0x2BA6.</summary>
        public byte[] PaletteB { get; } = new byte[3];

        /// <summary>Float at ebp−0x2B6C (0.005; 0.0015 for subtypes 1/2; 0.0025 for subtype 4).</summary>
        public float PaletteScale { get; internal set; }
    }

    /// <summary>
    /// Alpha dungeon generator <c>0x5CC510</c> (P7-B), ported from Cube.exe bytes and checked against the executed
    /// function (tools/alpha_models/world/dungeon_oracle.py, unicorn; fixture alpha_dungeon_layout_oracle_666666.json):
    /// stage 1 = rooms up to <c>0x5CE200</c> — kind-list choice, 27 room slots, subtype/flag schemes, room boxes
    /// <c>0x5EF8F0</c> with their floor marks and pillars, the master cell. EMULATED_ALPHA_CODE on 21 cases
    /// (subtypes 0…5, flag 0/1, three CRT states): every room record, box, master cell, the full 22³ volume and the
    /// CRT state after the stage are identical. Corridors, creatures, chests and voxels are later stages.
    /// The generator continues the zone's CRT stream (Alpha calls it from 0x5E84C3/0x5E84F7).
    /// </summary>
    public static class AlphaDungeonGenerator
    {
        public const int VolumeSize = 22;
        public const string Provenance = "EMULATED_ALPHA_CODE: 0x5CC510 rooms (to 0x5CE200), 21 executed cases";

        public static AlphaDungeonRooms GenerateRooms(int subtype, bool flag, AlphaCrtRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            var layout = new AlphaDungeonRooms { Subtype = subtype, Flag = flag };
            if (subtype == 1 || subtype == 2)
            {
                layout.ExtraKindChoice = random.Next() % 3;
            }

            for (int n = 0; n < 27; n++)
            {
                var room = new AlphaDungeonRoom { I = n / 9, J = n / 3 % 3, K = n % 3 };
                int r1 = random.Next();
                int r2 = random.Next();
                int r3 = random.Next();
                int r4 = random.Next();
                int r5 = random.Next();
                int r6 = random.Next();
                room.Parameters[5] = r1 % 3;
                room.Parameters[4] = r2 % 3 - 1;
                room.Parameters[3] = r3 % 3 - 1;
                room.Parameters[2] = r4 % 3 - 1;
                room.Parameters[1] = r5 & 1;
                room.Parameters[0] = r6 & 1;
                layout.Rooms[n] = room;
            }

            Schemes(layout, subtype, flag, random);

            var volume = new AlphaDungeonVolume(VolumeSize, VolumeSize, VolumeSize);
            layout.Volume = volume;
            foreach (AlphaDungeonRoom room in layout.Rooms)
            {
                if (!room.Present)
                {
                    continue;
                }

                int[] p = room.Parameters;
                if (room.Special)
                {
                    p[0] = p[1] = p[2] = 0;
                    p[3] = 0;
                    p[4] = 0;
                    p[5] = 1;
                }

                room.Origin[0] = 7 * room.I + 2 + p[0];
                room.Origin[1] = 7 * room.J + 2 + p[1];
                room.Origin[2] = 7 * room.K + 3 + p[2];
                room.Size[0] = 3 + p[3];
                room.Size[1] = 3 + p[4];
                room.Size[2] = 1 + p[5];
                room.Pillars = !room.Special && !room.Master && random.Next() % 3 == 0;
                Box(volume, room.Origin, room.Size, room.Pillars, random);
                layout.Boxes.Add(room);
                if (room.Master)
                {
                    int x = room.Origin[0] + room.Size[0] / 2;
                    int y = room.Origin[1] + room.Size[1] / 2;
                    int z = room.Origin[2] + room.Size[2] / 2;
                    volume.AddFlags(x, y, z, 4);
                    layout.MasterCells.Add((x, y, z));
                }
            }

            return layout;
        }

        /// <summary>
        /// Room links per scheme (list at ebp−0x2B7C, read from the executed function; pairs of room slots A → B).
        /// </summary>
        private static readonly int[][] LinksFlag = Links(
            1, 1, 1, 1, 2, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 2, 1, 1, 0, 1, 1, 0, 0, 1, 2, 1, 1, 2, 0, 1);

        private static readonly int[][] LinksScheme0 = Links(
            1, 0, 1, 0, 0, 1, 1, 0, 1, 2, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 2, 1,
            1, 1, 1, 1, 2, 1, 1, 1, 1, 2, 1, 1, 2, 1, 1, 2, 2, 1);

        private static readonly int[][] LinksScheme1 = Links(
            1, 0, 1, 1, 1, 1, 1, 0, 1, 2, 0, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 0, 0, 1, 0, 1, 1, 0, 2, 1,
            0, 2, 1, 1, 2, 1, 1, 2, 1, 2, 2, 1, 2, 2, 1, 2, 1, 1);

        private static readonly int[][] LinksScheme2 = Links(
            1, 0, 1, 0, 0, 1, 1, 0, 1, 2, 0, 1, 2, 0, 1, 2, 1, 1, 0, 0, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1,
            0, 1, 1, 0, 2, 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 2, 1, 1, 2, 1, 1, 2, 2, 1);

        private static readonly int[][] LinksSubtype2 = Links(
            1, 0, 2, 1, 2, 1, 1, 2, 1, 0, 2, 1, 1, 2, 1, 2, 2, 1, 0, 2, 1, 0, 1, 1, 2, 2, 1, 2, 1, 1,
            2, 1, 1, 2, 0, 1, 2, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 2, 0, 0, 1, 0, 1, 1, 0);

        private static readonly int[][] LinksSubtype45 = Links(
            1, 1, 2, 1, 2, 1, 1, 2, 1, 0, 2, 1, 1, 2, 1, 2, 2, 1, 0, 2, 1, 0, 1, 1, 2, 2, 1, 2, 1, 1,
            2, 1, 1, 2, 0, 1, 2, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 2, 0, 0, 1, 0, 1, 1, 0);

        private static int[][] Links(params int[] flat)
        {
            var links = new int[flat.Length / 6][];
            for (int i = 0; i < links.Length; i++)
            {
                links[i] = new int[6];
                Array.Copy(flat, i * 6, links[i], 0, 6);
            }

            return links;
        }

        /// <summary>
        /// Stages 1–3 of 0x5CC510 (to 0x5CEA6B, right before the volume is written into the zone): rooms, corridors
        /// (0x5C52A0 / 0x5EF6D0), entrance cell, volume rotation and mirror, palette, column pass. EMULATED_ALPHA_CODE on
        /// 27 executed cases (all room schemes): volume, rotation, mirror, palette bytes and the CRT state are identical.
        /// </summary>
        public static AlphaDungeonRooms GenerateCells(int subtype, bool flag, AlphaCrtRandom random)
        {
            AlphaDungeonRooms layout = GenerateRooms(subtype, flag, random);
            AlphaDungeonVolume volume = layout.Volume;
            int[][] links = flag ? LinksFlag
                : subtype == 4 || subtype == 5 ? LinksSubtype45
                : subtype == 2 ? LinksSubtype2
                : layout.Scheme == 0 ? LinksScheme0
                : layout.Scheme == 1 ? LinksScheme1
                : LinksScheme2;
            layout.Links.AddRange(links);
            foreach (int[] link in links)
            {
                AlphaDungeonRoom a = layout.Rooms[link[0] * 9 + link[1] * 3 + link[2]];
                AlphaDungeonRoom b = layout.Rooms[link[3] * 9 + link[4] * 3 + link[5]];
                if (a.Present && b.Present)
                {
                    Corridor(volume, a.Origin, a.Size, b.Origin, b.Size, random);
                }
            }

            // 0x5CE3F3: entrance (type 4) in front of room A of the first link, before the volume is turned.
            AlphaDungeonRoom first = layout.Rooms[links[0][0] * 9 + links[0][1] * 3 + links[0][2]];
            int[] fp = first.Parameters;
            layout.Entrance = (7 * links[0][0] + 2 + fp[0], 7 * links[0][1] + 1 + fp[1], 7 * links[0][2] + 3 + fp[2]);
            volume.SetType(layout.Entrance.X, layout.Entrance.Y, layout.Entrance.Z, 4);

            volume.Rotation = random.Next() & 3;
            volume.Mirror = (random.Next() & 1) == 0;
            Palette(layout, subtype, random);
            if (subtype >= 0 && subtype <= 3)
            {
                Columns(volume, subtype, random);
            }

            return layout;
        }

        /// <summary>0x5CE4FB…0x5CE844: wall colour and the two subtype palettes (bytes as Alpha keeps them).</summary>
        private static void Palette(AlphaDungeonRooms layout, int subtype, AlphaCrtRandom random)
        {
            byte[] wall = layout.WallColor;
            byte[] a = layout.PaletteA;
            byte[] b = layout.PaletteB;
            wall[2] = (byte)(random.Next() % 50 + 20);
            wall[1] = (byte)(random.Next() % 50 + 20);
            wall[0] = (byte)(random.Next() % 50 + 20);
            int baseValue = random.Next() % 50;
            if (subtype == 3)
            {
                baseValue = random.Next() % 50 + 100;
            }

            a[2] = (byte)(random.Next() % 50 + 50 + baseValue);
            a[1] = (byte)(random.Next() % 50 + 50 + baseValue);
            a[0] = (byte)(random.Next() % 50 + 50 + baseValue);
            b[2] = (byte)(random.Next() % 50 + 50 + baseValue);
            b[1] = (byte)(random.Next() % 50 + 50 + baseValue);
            b[0] = (byte)(random.Next() % 50 + 50 + baseValue);
            layout.PaletteScale = BitConverter.ToSingle(BitConverter.GetBytes(0x3BA3D70A), 0);
            switch (subtype)
            {
                case 1:
                case 2:
                    a[2] = (byte)(random.Next() % 50 + 50);
                    a[1] = (byte)(random.Next() % 50 + 50);
                    a[0] = (byte)(random.Next() % 20 + 50);
                    b[2] = (byte)(random.Next() % 50 + 50);
                    b[1] = (byte)(random.Next() % 50 + 50);
                    b[0] = (byte)(random.Next() % 20 + 50);
                    layout.PaletteScale = BitConverter.ToSingle(BitConverter.GetBytes(0x3AC49BA6), 0);
                    break;
                case 4:
                    a[2] = (byte)(random.Next() % 50 + 20);
                    a[1] = (byte)(random.Next() % 50 + 80);
                    a[0] = (byte)(random.Next() % 50 + 100);
                    b[2] = (byte)(random.Next() % 50 + 20);
                    b[1] = (byte)(random.Next() % 50 + 80);
                    b[0] = (byte)(random.Next() % 50 + 100);
                    layout.PaletteScale = BitConverter.ToSingle(BitConverter.GetBytes(0x3B23D70A), 0);
                    break;
                case 5:
                    a[0] = 0xFF;
                    a[1] = 0xD2;
                    a[2] = 0x82;
                    b[0] = 0xC8;
                    b[1] = 0xB4;
                    b[2] = 0x50;
                    break;
            }
        }

        /// <summary>
        /// 0x5CE8A4…0x5CEA05 (subtypes 0…3): over every column from the top, the first cell that is neither empty nor
        /// the entrance gets h cells of type 2 above it; h = 1, or rand()%3+2 when rand()%6 == 0 (not for subtype 2).
        /// Addressed through the turned volume.
        /// </summary>
        private static void Columns(AlphaDungeonVolume volume, int subtype, AlphaCrtRandom random)
        {
            int n = volume.SizeX;
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    int h = 1;
                    if (subtype != 2 && random.Next() % 6 == 0)
                    {
                        h = random.Next() % 3 + 2;
                    }

                    int z = volume.SizeZ - 1;
                    while (z >= 0)
                    {
                        byte t = volume.Type(x, y, z);
                        if (t != 4 && t != 0)
                        {
                            break;
                        }

                        z--;
                    }

                    if (z < 0)
                    {
                        continue;
                    }

                    int top = Math.Min(z + h, volume.SizeZ - 1);
                    for (int zz = z + 1; zz <= top; zz++)
                    {
                        volume.SetType(x, y, zz, 2);
                    }
                }
            }
        }

        /// <summary>
        /// 0x5C52A0: corridor between room A and room B along the axis on which they overlap (X first, then Y; none
        /// otherwise) — a main line at m, and with probability ½ each, lines at m−1 and m+1. Limits are kept as in the
        /// bytes, including the Y branch comparing m with X extents (0x5C55BF, 0x5C5682).
        /// </summary>
        private static void Corridor(AlphaDungeonVolume v, int[] a, int[] sa, int[] b, int[] sb, AlphaCrtRandom random)
        {
            int endAx = a[0] + sa[0];
            if (b[0] < endAx && a[0] < b[0] + sb[0])
            {
                int m = b[0] + sb[0] / 2;
                if (m < a[0]) m = a[0];
                if (m >= endAx) m = endAx - 1;
                bool below = b[1] < a[1];
                int fromY = below ? a[1] : a[1] + sa[1] - 1;
                int toY = below ? b[1] + sb[1] : b[1] - 1;
                Line(v, m, fromY, a[2], m, toY, b[2]);
                if ((random.Next() & 1) != 0 && m > a[0] && m > b[0])
                {
                    Line(v, m - 1, fromY, a[2], m - 1, toY, b[2]);
                }

                if ((random.Next() & 1) != 0 && m < a[0] + sa[0] - 1 && m < b[0] + sb[0] - 1)
                {
                    Line(v, m + 1, fromY, a[2], m + 1, toY, b[2]);
                }

                return;
            }

            int endAy = a[1] + sa[1];
            if (b[1] < endAy && a[1] < b[1] + sb[1])
            {
                int m = b[1] + sb[1] / 2;
                if (m < a[1]) m = a[1];
                if (m >= endAy) m = endAy - 1;
                bool left = b[0] < a[0];
                int fromX = left ? a[0] : a[0] + sa[0] - 1;
                int toX = left ? b[0] + sb[0] : b[0] - 1;
                Line(v, fromX, m, a[2], toX, m, b[2]);
                if ((random.Next() & 1) != 0 && m > a[1] && m > b[1])
                {
                    Line(v, fromX, m - 1, a[2], toX, m - 1, b[2]);
                }

                bool wide = left
                    ? m < a[1] + sa[1] - 1 && m < b[0] + sb[0] - 1
                    : m < a[0] + sa[0] - 1 && m < b[0] + sb[0] - 1;
                if ((random.Next() & 1) != 0 && wide)
                {
                    Line(v, fromX, m + 1, a[2], toX, m + 1, b[2]);
                }
            }
        }

        /// <summary>0x5EF6D0: cells from start to end (truncating steps), type 3; a height change opens the cell above
        /// or below and marks the step |1.</summary>
        private static void Line(AlphaDungeonVolume v, int sx, int sy, int sz, int ex, int ey, int ez)
        {
            int dx = ex - sx;
            int dy = ey - sy;
            int dz = ez - sz;
            int m = Math.Max(Math.Abs(dx), Math.Abs(dy));
            int n = m > Math.Abs(dz) ? m : Math.Abs(dz);
            if (n == 0)
            {
                return;
            }

            int previous = sz;
            for (int t = 0; t <= n; t++)
            {
                int x = sx + dx * t / n;
                int y = sy + dy * t / n;
                int z = sz + dz * t / n;
                v.SetType(x, y, z, 3);
                if (z < previous)
                {
                    v.SetType(x, y, z + 1, 3);
                    v.AddFlags(x, y, z, 1);
                }

                if (z > previous)
                {
                    v.SetType(x, y, z - 1, 3);
                    v.AddFlags(x, y, z - 1, 1);
                }

                previous = z;
            }
        }

        /// <summary>Room presence by subtype and flag (0x5CC9xx…0x5CDFC9).</summary>
        private static void Schemes(AlphaDungeonRooms layout, int subtype, bool flag, AlphaCrtRandom random)
        {
            void Set(int i, int j, int k, bool present = true) => layout.Rooms[i * 9 + j * 3 + k].Present = present;
            bool Even() => (random.Next() & 1) == 0;
            void Master(int i, int j, int k) => layout.Rooms[i * 9 + j * 3 + k].Master = true;

            if (flag)
            {
                Set(1, 1, 1);
                Set(1, 2, 1);
                Set(0, 1, 1);
                Set(2, 1, 1);
                Set(0, 0, 1);
                Set(2, 0, 1);
                return;
            }

            if (subtype == 4 || subtype == 5)
            {
                random.Next();
                Set(1, 1, 2);
                layout.Rooms[1 * 9 + 1 * 3 + 2].Byte2 = true;
                layout.Rooms[1 * 9 + 1 * 3 + 2].Special = true;
                LowerTier(Set, Even, Master);
                return;
            }

            if (subtype == 2)
            {
                random.Next();
                Set(1, 0, 2);
                LowerTier(Set, Even, Master);
                return;
            }

            int scheme = random.Next() % 3;
            layout.Scheme = scheme;
            Set(1, 0, 1);
            if (scheme == 1)
            {
                Set(2, 0, 1, Even());
                Set(1, 1, 1);
                Set(0, 1, 1);
                Set(0, 0, 1, Even());
                Set(0, 2, 1);
                Set(1, 2, 1);
                Set(2, 2, 1);
                Master(2, 2, 1);
                Set(2, 1, 1, Even());
            }
            else if (scheme == 2)
            {
                Set(2, 0, 1, Even());
                Set(0, 0, 1);
                Set(0, 1, 1);
                Set(0, 2, 1, Even());
                Set(1, 1, 1);
                Set(2, 1, 1);
                Set(2, 2, 1, Even());
                Set(1, 2, 1);
                Master(1, 2, 1);
            }
            else
            {
                Set(2, 0, 1, Even());
                Set(0, 0, 1);
                Set(0, 1, 1);
                Set(0, 2, 1, Even());
                Set(1, 1, 1);
                Set(1, 2, 1, Even());
                Set(2, 1, 1);
                Set(2, 2, 1);
                Master(2, 2, 1);
            }
        }

        /// <summary>Common part of subtypes 2/4/5 (flag 0): rooms around the lower tier, master at (1,1,0).</summary>
        private static void LowerTier(Action<int, int, int, bool> set, Func<bool> even, Action<int, int, int> master)
        {
            set(1, 2, 1, true);
            set(2, 2, 1, true);
            set(2, 1, 1, true);
            set(2, 0, 1, true);
            set(0, 2, 1, true);
            set(0, 1, 1, even());
            set(0, 0, 0, true);
            set(0, 1, 0, true);
            set(0, 2, 0, even());
            set(1, 1, 0, true);
            master(1, 1, 0);
        }

        /// <summary>0x5EF8F0: room box (type 3), random floor mark |2 on its bottom layer, optional pillar block (type 2).</summary>
        private static void Box(AlphaDungeonVolume volume, int[] origin, int[] size, bool pillars, AlphaCrtRandom random)
        {
            int sx = size[0];
            int sy = size[1];
            int sz = size[2];
            if (sx < 1 || sy < 1 || sz < 1)
            {
                return;
            }

            int ox = origin[0];
            int oy = origin[1];
            int oz = origin[2];
            for (int x = ox; x < ox + sx; x++)
            {
                for (int y = oy; y < oy + sy; y++)
                {
                    for (int z = oz; z < oz + sz; z++)
                    {
                        volume.SetType(x, y, z, 3);
                    }
                }
            }

            if ((random.Next() & 1) != 0)
            {
                int w = random.Next() % sx + 1;
                int h = random.Next() % sy + 1;
                int x0 = ox;
                int y0 = oy;
                if (sx - w > 0) x0 += random.Next() % (sx - w + 1);
                if (sy - h > 0) y0 += random.Next() % (sy - h + 1);
                for (int x = x0; x < x0 + w; x++)
                {
                    for (int y = y0; y < y0 + h; y++)
                    {
                        volume.AddFlags(x, y, oz, 2);
                    }
                }
            }

            if (pillars && sx > 2 && sy > 2)
            {
                int w = random.Next() % (sx - 2) + 1;
                int h = random.Next() % (sy - 2) + 1;
                int x0 = ox + 1;
                int y0 = oy + 1;
                if (sx - w > 1) x0 += random.Next() % (sx - w - 1);
                if (sy - h > 1) y0 += random.Next() % (sy - h - 1);
                for (int x = x0; x < x0 + w; x++)
                {
                    for (int y = y0; y < y0 + h; y++)
                    {
                        for (int z = oz; z < oz + sz; z++)
                        {
                            volume.SetType(x, y, z, 2);
                        }
                    }
                }
            }
        }
    }
}
