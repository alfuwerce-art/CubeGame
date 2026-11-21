using CubeGame.Core.Generation;
using NUnit.Framework;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// Determinism cases TC-GEN-01..TC-GEN-04 of
    /// docs/research/alpha2013/GENERATION_MOVEMENT_SPEC.md section 3.
    /// </summary>
    public sealed class TerrainDeterminismTests
    {
        private const uint SeedA = 666666u;
        private const uint SeedB = 666667u;

        [Test]
        public void SameSeedProducesIdenticalHeights_TC_GEN_01()
        {
            var first = new PilotHeightField(SeedA);
            var second = new PilotHeightField(SeedA);

            for (int z = -64; z <= 96; z++)
            {
                for (int x = -64; x <= 96; x++)
                {
                    Assert.AreEqual(first.HeightAt(x, z), second.HeightAt(x, z), $"height mismatch at ({x},{z})");
                }
            }
        }

        [Test]
        public void DifferentSeedChangesHeights_TC_GEN_02()
        {
            var first = new PilotHeightField(SeedA);
            var second = new PilotHeightField(SeedB);

            int total = 0;
            int different = 0;
            for (int z = 0; z < 256; z += 4)
            {
                for (int x = 0; x < 256; x += 4)
                {
                    total++;
                    if (first.HeightAt(x, z) != second.HeightAt(x, z))
                    {
                        different++;
                    }
                }
            }

            double ratio = different / (double)total;
            Assert.GreaterOrEqual(ratio, 0.90, $"only {ratio:P1} of columns differ between seeds");
        }

        [Test]
        public void AdjacentChunksShareEdgeHeights_TC_GEN_03()
        {
            var field = new PilotHeightField(SeedA);
            int size = PilotWorldLayout.ChunkSize;

            // Right edge of chunk (0,0) is the left edge of chunk (1,0).
            for (int localZ = 0; localZ <= size; localZ++)
            {
                int worldX = size;
                int worldZ = localZ;
                Assert.AreEqual(
                    field.HeightAt(worldX, worldZ),
                    new PilotHeightField(SeedA).HeightAt(worldX, worldZ),
                    $"edge height mismatch at ({worldX},{worldZ})");
            }

            // Height field must not depend on chunk assignment at all.
            for (int i = 0; i < 64; i++)
            {
                int x = i * 7 - 100;
                int z = i * 11 - 200;
                Assert.AreEqual(field.HeightAt(x, z), new PilotHeightField(SeedA).HeightAt(x, z));
            }
        }

        [Test]
        public void HeightsStayInsideConfiguredRange()
        {
            var field = new PilotHeightField(SeedA);
            for (int z = -512; z <= 512; z += 16)
            {
                for (int x = -512; x <= 512; x += 16)
                {
                    int height = field.HeightAt(x, z);
                    Assert.GreaterOrEqual(height, PilotWorldLayout.MinHeight);
                    Assert.LessOrEqual(height, PilotWorldLayout.MaxHeight);
                }
            }
        }

        [Test]
        public void NegativeCoordinatesAreSupported()
        {
            var field = new PilotHeightField(SeedA);
            for (int i = -8; i <= 8; i++)
            {
                int height = field.HeightAt(i * 37 - 149, i * -53 + 211);
                Assert.GreaterOrEqual(height, PilotWorldLayout.MinHeight);
                Assert.LessOrEqual(height, PilotWorldLayout.MaxHeight);
            }
        }
    }
}
