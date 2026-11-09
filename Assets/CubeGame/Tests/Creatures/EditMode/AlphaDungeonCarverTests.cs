// D3 step 5 — EditMode checks of the dungeon carver (rooms 3×3×3, L-corridors, deterministic).
using System.Collections.Generic;
using System.Linq;
using CubeGame.Content;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaDungeonCarverTests
    {
        const uint Seed = 424242u;

        [Test]
        public void RoomInteriorIsClearedAndTheFloorKept()
        {
            var layout = AlphaDungeonLayoutPlanner.Plan(Seed, 3);
            var cells = new HashSet<(int, int, int)>(AlphaDungeonCarver.Cells(layout, 40));
            var room = layout.Rooms[0];
            for (int x = 0; x < AlphaDungeonRules.RoomSize; x++)
            for (int z = 0; z < AlphaDungeonRules.RoomSize; z++)
            for (int y = 1; y <= AlphaDungeonRules.RoomSize; y++)
            {
                Assert.IsTrue(cells.Contains((room.X + x, 40 + y, room.Z + z)), $"room voxel {x},{y},{z}");
            }

            Assert.IsFalse(cells.Contains((room.X, 40, room.Z)), "the floor voxel stays");
        }

        [Test]
        public void CorridorsAreTwoHighAndConnectTheRooms()
        {
            var layout = AlphaDungeonLayoutPlanner.Plan(Seed, 4);
            var cells = new HashSet<(int, int, int)>(AlphaDungeonCarver.Cells(layout, 10));
            var roomColumns = new HashSet<(int, int)>();
            foreach (var room in layout.Rooms)
            for (int x = 0; x < AlphaDungeonRules.RoomSize; x++)
            for (int z = 0; z < AlphaDungeonRules.RoomSize; z++)
            {
                roomColumns.Add((room.X + x, room.Z + z));
            }

            Assert.IsNotEmpty(layout.Corridors);
            foreach (var corridor in layout.Corridors)
            {
                Assert.IsNotEmpty(corridor.Cells);
                foreach (var cell in corridor.Cells)
                {
                    Assert.IsTrue(cells.Contains((cell.X, 11, cell.Z)), $"corridor bottom {cell}");
                    Assert.IsTrue(cells.Contains((cell.X, 12, cell.Z)), $"corridor top {cell}");
                    if (!roomColumns.Contains((cell.X, cell.Z)))
                    {
                        Assert.IsFalse(cells.Contains((cell.X, 13, cell.Z)), "corridors are two voxels high");
                    }
                }
            }
        }

        [Test]
        public void CarvingIsDeterministicAndCountsWhatItClears()
        {
            var layout = AlphaDungeonLayoutPlanner.Plan(Seed, 3);
            var a = AlphaDungeonCarver.Cells(layout, 5);
            var b = AlphaDungeonCarver.Cells(layout, 5);
            CollectionAssert.AreEqual(a, b);
            int cleared = AlphaDungeonCarver.Carve(layout, 5, (x, y, z, v) => { });
            Assert.AreEqual(a.Count, cleared);
            Assert.Greater(cleared, layout.Rooms.Count * AlphaDungeonRules.RoomSize * AlphaDungeonRules.RoomSize);
        }

        [Test]
        public void CarvedVolumeNeverContainsTheFloorLevel()
        {
            var layout = AlphaDungeonLayoutPlanner.Plan(Seed, 2);
            foreach (var cell in AlphaDungeonCarver.Cells(layout, 0))
            {
                Assert.Greater(cell.Y, 0, "the base level (floor) is never cleared");
            }
        }

        [Test]
        public void ProvenanceStaysHonest()
        {
            StringAssert.Contains("PARTIAL", AlphaDungeonCarver.Provenance);
            StringAssert.Contains("UNKNOWN", AlphaDungeonCarver.Provenance);
        }
    }
}
