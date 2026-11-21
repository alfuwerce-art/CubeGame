// CubeGame.Features.Creatures — carving a dungeon skeleton into the world (D3 step 5).
//
// Proven (Q-16): dungeon rooms are 3×3×3, the room step is 7 and corridors are L-shaped; a dungeon chest holds
// 10 items (Q-17). NOT proven: carving order, corridor width/height, room contents, lighting and encounters —
// the layout itself is a deterministic CubeGame contract (AlphaDungeonLayoutPlanner).
//
// This carver writes AIR through a delegate, so it is a pure function: a test can verify the rooms and the
// connections without a world. Ground/floor of a room is kept (only the volume above it is cleared), which is
// the minimal rule needed to walk inside.
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>Carves dungeon rooms and L-corridors of a layout into the world.</summary>
    public static class AlphaDungeonCarver
    {
        public const string Provenance =
            "PARTIAL: Q-16 room 3×3×3, step 7, L-corridors proven; carving order/width, contents, light UNKNOWN";

        /// <summary>Height of a carved corridor (CUBEGAME_CONTRACT: rooms are 3 high, corridors 2).</summary>
        public const int CorridorHeight = 2;

        /// <summary>Carves rooms and corridors; returns the number of voxels cleared.</summary>
        public static int Carve(AlphaDungeonLayout layout, int baseY, Action<int, int, int, uint> write)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (write == null) throw new ArgumentNullException(nameof(write));
            int cleared = 0;
            foreach (var room in layout.Rooms)
            {
                for (int x = 0; x < AlphaDungeonRules.RoomSize; x++)
                for (int z = 0; z < AlphaDungeonRules.RoomSize; z++)
                for (int y = 1; y <= AlphaDungeonRules.RoomSize; y++)
                {
                    write(room.X + x, baseY + y, room.Z + z, Voxel.Air);
                    cleared++;
                }
            }

            foreach (var corridor in layout.Corridors)
            {
                if (corridor.Cells == null) continue;
                foreach (var cell in corridor.Cells)
                for (int y = 1; y <= CorridorHeight; y++)
                {
                    write(cell.X, baseY + y, cell.Z, Voxel.Air);
                    cleared++;
                }
            }

            return cleared;
        }

        /// <summary>Cells the carver would clear (tests and previews).</summary>
        public static List<(int X, int Y, int Z)> Cells(AlphaDungeonLayout layout, int baseY)
        {
            var result = new List<(int, int, int)>();
            Carve(layout, baseY, (x, y, z, _) => result.Add((x, y, z)));
            return result;
        }
    }
}
