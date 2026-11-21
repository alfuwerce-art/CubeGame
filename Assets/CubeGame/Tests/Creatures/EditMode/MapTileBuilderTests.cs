// D6 — EditMode checks of the map tile builder (Alpha colours, CubeGame tile contract).
using CubeGame.Presentation.Visual;
using CubeGame.Features.World;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class MapTileBuilderTests
    {
        [Test]
        public void TileHasTheRequestedSizeAndIsRgba()
        {
            var bytes = MapTileBuilder.Render(8, (x, y) => new MapTileBuilder.Sample(10, 20, 30, true));
            Assert.AreEqual(8 * 8 * 4, bytes.Length);
            for (int i = 0; i < bytes.Length; i += 4)
            {
                Assert.AreEqual(10, bytes[i]);
                Assert.AreEqual(20, bytes[i + 1]);
                Assert.AreEqual(30, bytes[i + 2]);
                Assert.AreEqual(255, bytes[i + 3]);
            }
        }

        [Test]
        public void EmptyColumnsAreTransparent()
        {
            var bytes = MapTileBuilder.Render(4, (x, y) => new MapTileBuilder.Sample(255, 255, 255, false));
            for (int i = 3; i < bytes.Length; i += 4)
            {
                Assert.AreEqual(0, bytes[i], "no ground → transparent pixel");
            }
        }

        [Test]
        public void SamplingIsDeterministicAndUsesTheSamplerCoordinates()
        {
            var a = MapTileBuilder.Render(5, (x, y) => new MapTileBuilder.Sample((byte)x, (byte)y, 0, true));
            var b = MapTileBuilder.Render(5, (x, y) => new MapTileBuilder.Sample((byte)x, (byte)y, 0, true));
            CollectionAssert.AreEqual(a, b);
            int i = (3 * 5 + 2) * 4;
            Assert.AreEqual(2, a[i], "column x");
            Assert.AreEqual(3, a[i + 1], "row y");
        }

        [Test]
        public void SurfaceSampleKeepsTheAlphaVoxelColour()
        {
            uint voxel = Voxel.Pack(120, 80, 40, 1);
            var sample = MapTileBuilder.FromSurface(voxel, true);
            Assert.AreEqual(120, sample.R);
            Assert.AreEqual(80, sample.G);
            Assert.AreEqual(40, sample.B);
            Assert.IsTrue(sample.Solid);
            Assert.IsFalse(MapTileBuilder.FromSurface(voxel, false).Solid);
        }

        [Test]
        public void InvalidSizeAndSamplerAreRefused()
        {
            Assert.Throws<System.ArgumentNullException>(() => MapTileBuilder.Render(4, null));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MapTileBuilder.Render(0, (x, y) => default));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MapTileBuilder.Render(2048, (x, y) => default));
        }

        [Test]
        public void ProvenanceSeparatesAlphaColoursFromTheTileContract()
        {
            StringAssert.Contains("COLORS=ALPHA_DATA", MapTileBuilder.Provenance);
            StringAssert.Contains("CUBEGAME_CONTRACT", MapTileBuilder.Provenance);
        }
    }
}
