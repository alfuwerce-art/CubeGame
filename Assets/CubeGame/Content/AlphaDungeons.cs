// CubeGame.Content — dungeon layout foundations (D86 M0-07B).
//
// Proven: dungeon rooms are 3x3x3, room spacing step is 7, corridors are L-shaped, a dungeon chest
// holds exactly 10 items, entrance names are overground-/underground-dungeon01. NOT proven: corridor
// width/carving order, room contents, light/torches, encounter tables. The planner below is therefore
// a deterministic CubeGame contract marked PARTIAL — it produces a connected layout with the proven
// spacing and L-shape so the world layer can instantiate and test it, without claiming Alpha parity.
using System;
using System.Collections.Generic;
using CubeGame.Core.Generation;

namespace CubeGame.Content
{
    public struct AlphaDungeonRoom
    {
        public int Index;
        public int X, Y, Z;   // world block coordinates of the room origin
    }

    public struct AlphaDungeonCorridor
    {
        public int FromIndex, ToIndex;
        public List<(int X, int Z)> Cells;
    }

    public sealed class AlphaDungeonLayout
    {
        public string SeedLabel;
        public int Step;
        public List<AlphaDungeonRoom> Rooms = new List<AlphaDungeonRoom>();
        public List<AlphaDungeonCorridor> Corridors = new List<AlphaDungeonCorridor>();
        public string Status = AlphaDungeonRules.LayoutStatus;
    }

    public static class AlphaDungeonRules
    {
        /// <summary>Q-16: dungeon rooms are 3x3x3 and repeat with step 7.</summary>
        public const int RoomSize = 3;
        public const int RoomStep = 7;
        /// <summary>Q-17 / I-13: a dungeon chest holds exactly 10 items (DIRECT).</summary>
        public const int ChestItemCount = 10;
        public static readonly string[] EntranceNames = { "overground-dungeon01", "underground-dungeon01" };
        public const string LayoutStatus =
            "PARTIAL: room size/step/L-corridors proven; corridor carving order, room contents, light and encounters UNKNOWN";

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (RoomSize != 3) p.Add($"DUNGEON_ROOM_SIZE {RoomSize}");
            if (RoomStep != 7) p.Add($"DUNGEON_STEP {RoomStep}");
            if (ChestItemCount != 10) p.Add($"DUNGEON_CHEST_ITEMS {ChestItemCount}");
            if (EntranceNames.Length != 2) p.Add("DUNGEON_ENTRANCES");
            if (AlphaStructureTemplates.DungeonRoomStep != RoomStep) p.Add("DUNGEON_STEP_MISMATCH");
            return p;
        }
    }

    /// <summary>
    /// Builds a deterministic, connected dungeon skeleton: a random walk over a 5x5 lattice with the
    /// proven step, one room per visited slot and L-shaped corridors between consecutive rooms.
    /// </summary>
    public static class AlphaDungeonLayoutPlanner
    {
        public const int LatticeSize = 5;
        public const int MaxRooms = LatticeSize * LatticeSize;

        public static AlphaDungeonLayout Plan(uint seed, int roomCount, int originX = 0, int originZ = 0,
            List<string> problems = null)
        {
            var layout = new AlphaDungeonLayout { SeedLabel = "seed:" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture), Step = AlphaDungeonRules.RoomStep };
            if (roomCount < 1 || roomCount > MaxRooms)
            {
                problems?.Add($"DUNGEON_ROOM_COUNT {roomCount} outside 1..{MaxRooms}");
                return layout;
            }
            var visited = new HashSet<int>();
            int i = LatticeSize / 2, j = LatticeSize / 2;
            layout.Rooms.Add(Room(layout.Rooms.Count, originX, originZ, i, j));
            visited.Add(i * LatticeSize + j);
            int guard = 0;
            while (layout.Rooms.Count < roomCount && guard++ < 200)
            {
                uint h = DeterministicHash.Combine((int)seed, layout.Rooms.Count, i, j, guard);
                int direction = (int)(h & 3u);
                int ni = i + (direction == 0 ? 1 : direction == 1 ? -1 : 0);
                int nj = j + (direction == 2 ? 1 : direction == 3 ? -1 : 0);
                if (ni < 0 || ni >= LatticeSize || nj < 0 || nj >= LatticeSize) continue;
                if (visited.Contains(ni * LatticeSize + nj)) continue;
                int from = layout.Rooms.Count - 1;
                layout.Rooms.Add(Room(layout.Rooms.Count, originX, originZ, ni, nj));
                visited.Add(ni * LatticeSize + nj);
                layout.Corridors.Add(Corridor(from, layout.Rooms.Count - 1,
                    layout.Rooms[from], layout.Rooms[layout.Rooms.Count - 1]));
                i = ni; j = nj;
            }
            if (layout.Rooms.Count != roomCount)
                problems?.Add($"DUNGEON_LAYOUT_SHORT {layout.Rooms.Count}/{roomCount} (lattice exhausted)");
            return layout;
        }

        static AlphaDungeonRoom Room(int index, int originX, int originZ, int i, int j) => new AlphaDungeonRoom
        {
            Index = index,
            X = originX + i * AlphaDungeonRules.RoomStep,
            Z = originZ + j * AlphaDungeonRules.RoomStep,
            Y = 0,
        };

        /// <summary>L-shaped corridor: along X first, then along Z (or the reverse on the other parity).</summary>
        static AlphaDungeonCorridor Corridor(int from, int to, AlphaDungeonRoom a, AlphaDungeonRoom b)
        {
            var cells = new List<(int X, int Z)>();
            bool xFirst = ((from + to) & 1) == 0;
            int x = a.X, z = a.Z;
            if (xFirst)
            {
                while (x != b.X) { x += Math.Sign(b.X - x); cells.Add((x, z)); }
                while (z != b.Z) { z += Math.Sign(b.Z - z); cells.Add((x, z)); }
            }
            else
            {
                while (z != b.Z) { z += Math.Sign(b.Z - z); cells.Add((x, z)); }
                while (x != b.X) { x += Math.Sign(b.X - x); cells.Add((x, z)); }
            }
            return new AlphaDungeonCorridor { FromIndex = from, ToIndex = to, Cells = cells };
        }
    }
}
