// CubeGame.Content — dungeon chest anchors (D86 M0-07B).
//
// Evidence: a dungeon chest contains exactly 10 items (AlphaDungeonRules.ChestItemCount, Q-17/I-13).
// The Alpha code does not expose chest *positions* in the recovered contracts, so the anchor is placed at a
// room centre and labelled CUBEGAME_CONTRACT; the caller may override the anchor with its own rule.
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    public struct AlphaDungeonChestPlacement
    {
        public string StableId;
        public int RoomIndex;
        public double X, Y, Z;
    }

    public static class AlphaDungeonChests
    {
        public const string AnchorStatus =
            "CUBEGAME_CONTRACT: room-centre anchor; Alpha chest positions not recovered";

        /// <summary>Place one chest per room for the first <paramref name="chests"/> rooms of the layout.</summary>
        public static List<AlphaDungeonChestPlacement> Plan(AlphaDungeonLayout layout, int chests, uint seed,
            int regionX, int regionZ, List<string> problems = null)
        {
            var result = new List<AlphaDungeonChestPlacement>();
            if (layout == null || layout.Rooms == null || layout.Rooms.Count == 0)
            {
                problems?.Add("CHEST_LAYOUT_WITHOUT_ROOMS");
                return result;
            }
            if (chests <= 0) return result;
            if (chests > layout.Rooms.Count)
            {
                problems?.Add($"CHEST_COUNT_EXCEEDS_ROOMS {chests} > {layout.Rooms.Count}");
                chests = layout.Rooms.Count;
            }
            double half = AlphaDungeonRules.RoomSize * 0.5;
            for (int i = 0; i < chests; i++)
            {
                var room = layout.Rooms[i];
                result.Add(new AlphaDungeonChestPlacement
                {
                    StableId = $"chest:{regionX}:{regionZ}:{seed:x8}:{i}",
                    RoomIndex = i,
                    X = room.X + half,
                    Y = 0.5,
                    Z = room.Z + half,
                });
            }
            return result;
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (AlphaDungeonRules.ChestItemCount != 10)
                p.Add($"CHEST_ITEM_COUNT {AlphaDungeonRules.ChestItemCount} != 10");
            var layout = AlphaDungeonLayoutPlanner.Plan(7u, 4, 0, 0);
            if (layout == null || layout.Rooms.Count == 0) { p.Add("CHEST_TEST_LAYOUT"); return p; }
            var chests = Plan(layout, 2, 7u, 0, 0);
            if (chests.Count != 2) p.Add($"CHEST_PLAN {chests.Count} != 2");
            var again = Plan(layout, 2, 7u, 0, 0);
            for (int i = 0; i < Math.Min(chests.Count, again.Count); i++)
                if (chests[i].StableId != again[i].StableId || chests[i].X != again[i].X)
                    p.Add("CHEST_PLAN_NON_DETERMINISTIC");
            double size = AlphaDungeonRules.RoomSize;
            foreach (var chest in chests)
            {
                var room = layout.Rooms[chest.RoomIndex];
                if (chest.X < room.X || chest.X > room.X + size ||
                    chest.Z < room.Z || chest.Z > room.Z + size)
                    p.Add($"CHEST_OUTSIDE_ROOM {chest.RoomIndex}");
            }
            var over = new List<string>();
            Plan(layout, layout.Rooms.Count + 3, 7u, 0, 0, over);
            if (!over.Exists(s => s.StartsWith("CHEST_COUNT_EXCEEDS_ROOMS"))) p.Add("CHEST_OVERFLOW_NOT_REPORTED");
            return p;
        }

        public static string Summary() =>
            $"chests itemsPerChest={AlphaDungeonRules.ChestItemCount} anchor=CUBEGAME_CONTRACT";
    }
}
