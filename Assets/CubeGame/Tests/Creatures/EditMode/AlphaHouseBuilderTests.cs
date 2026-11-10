// D3 step 3 — EditMode checks of the reconstructed house geometry (proven footprint, CubeGame voxels).
using System.Collections.Generic;
using System.Linq;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaHouseBuilderTests
    {
        const int Width = 3, Depth = 3, Height = 4;

        [Test]
        public void ReconstructedHouseHasFloorRoofAndWallsWithinGivenDimensions()
        {
            // Door on +X, so the -Z/+Z walls and the -X wall are complete.
            var voxels = AlphaHouseBuilder.Voxels(0, 10, 0, Width, Depth, Height, doorSide: 1);
            var positions = new HashSet<(int, int, int)>(voxels.Select(v => (v.X, v.Y, v.Z)));
            Assert.AreEqual(Width * Depth, voxels.Count(v => v.Y == 10), "floor covers the footprint");
            Assert.AreEqual(Width * Depth, voxels.Count(v => v.Y == 10 + Height - 1), "roof covers the footprint");
            for (int y = 11; y <= 12; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Assert.IsTrue(positions.Contains((x, y, 0)), $"back wall at {x},{y}");
                    Assert.IsTrue(positions.Contains((x, y, Depth - 1)), $"front wall at {x},{y}");
                }

                Assert.IsTrue(positions.Contains((0, y, 1)), $"left wall at {y}");
                Assert.IsFalse(positions.Contains((Width - 1, y, 1)), $"right wall has the doorway at {y}");
            }
        }

        [Test]
        public void DoorwayIsOpenOnTheChosenSideAndClosedElsewhere()
        {
            var front = AlphaHouseBuilder.Voxels(0, 0, 0, Width, Depth, Height, doorSide: 2);
            var frontSet = new HashSet<(int, int, int)>(front.Select(v => (v.X, v.Y, v.Z)));
            Assert.IsFalse(frontSet.Contains((1, 1, Depth - 1)), "door on +Z side is open");
            Assert.IsFalse(frontSet.Contains((1, 2, Depth - 1)), "door is two voxels high");
            Assert.IsTrue(frontSet.Contains((1, 1, 0)), "the opposite wall stays closed");

            var left = AlphaHouseBuilder.Voxels(0, 0, 0, Width, Depth, Height, doorSide: 3);
            var leftSet = new HashSet<(int, int, int)>(left.Select(v => (v.X, v.Y, v.Z)));
            Assert.IsFalse(leftSet.Contains((0, 1, 1)), "-X door is open");
            Assert.IsTrue(leftSet.Contains((1, 1, Depth - 1)), "+Z wall stays closed");
        }

        [Test]
        public void GeometryStaysInsideTheFootprint()
        {
            var voxels = AlphaHouseBuilder.Voxels(100, 50, 200, Width, Depth, Height);
            foreach (var v in voxels)
            {
                Assert.GreaterOrEqual(v.X, 100);
                Assert.Less(v.X, 100 + Width);
                Assert.GreaterOrEqual(v.Z, 200);
                Assert.Less(v.Z, 200 + Depth);
                Assert.GreaterOrEqual(v.Y, 50);
                Assert.Less(v.Y, 50 + Height);
            }
        }

        [Test]
        public void SameInputGivesTheSameHouse()
        {
            var a = AlphaHouseBuilder.Voxels(5, 5, 5, Width, Depth, Height);
            var b = AlphaHouseBuilder.Voxels(5, 5, 5, Width, Depth, Height);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void TooSmallFootprintIsRefused()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                AlphaHouseBuilder.Build(0, 0, 0, 2, 3, 4, (x, y, z, v) => { }));
        }

        [Test]
        public void ProvenanceSaysReconstructedNotAlpha()
        {
            StringAssert.Contains("RECONSTRUCTED_FROM_DIMENSIONS", AlphaHouseBuilder.Provenance);
            StringAssert.Contains("not Alpha parity", AlphaHouseBuilder.Provenance);
        }
    }
}
