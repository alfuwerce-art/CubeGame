using CubeGame.Core.Generation;
using CubeGame.Core.Rendering;
using NUnit.Framework;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// Geometry determinism and integrity of the Core chunk mesh builder
    /// (S01-PILOT P2: identical seed/region -> identical geometry, other seed -> a different one).
    /// </summary>
    public sealed class ChunkMeshTests
    {
        private const uint SeedA = 666666u;
        private const uint SeedB = 666667u;

        [Test]
        public void SameSeedProducesIdenticalMesh()
        {
            var field = new PilotHeightField(SeedA);
            MeshData first = ChunkMeshBuilder.Build(field, 0, 0);
            MeshData second = ChunkMeshBuilder.Build(field, 0, 0);

            Assert.Greater(first.VertexCount, 0, "mesh must not be empty");
            Assert.AreEqual(first.VertexCount, second.VertexCount);
            Assert.AreEqual(first.IndexCount, second.IndexCount);
            Assert.AreEqual(first.ComputeFingerprint(), second.ComputeFingerprint());
        }

        [Test]
        public void DifferentSeedProducesDifferentMesh()
        {
            MeshData first = ChunkMeshBuilder.Build(new PilotHeightField(SeedA), 0, 0);
            MeshData second = ChunkMeshBuilder.Build(new PilotHeightField(SeedB), 0, 0);
            Assert.AreNotEqual(first.ComputeFingerprint(), second.ComputeFingerprint());
        }

        [Test]
        public void IndicesStayInsideVertexBuffer()
        {
            MeshData mesh = ChunkMeshBuilder.Build(new PilotHeightField(SeedA), 1, -1);
            Assert.Greater(mesh.IndexCount, 0);
            Assert.AreEqual(0, mesh.IndexCount % 3, "index count must be a multiple of 3");

            for (int i = 0; i < mesh.IndexCount; i++)
            {
                Assert.GreaterOrEqual(mesh.Indices[i], 0);
                Assert.Less(mesh.Indices[i], mesh.VertexCount);
            }
        }

        [Test]
        public void ChunkEdgesAreContinuousBetweenNeighbours()
        {
            var field = new PilotHeightField(SeedA);
            int size = PilotWorldLayout.ChunkSize;
            MeshData left = ChunkMeshBuilder.Build(field, 0, 0);
            MeshData right = ChunkMeshBuilder.Build(field, 1, 0);

            // Both meshes sample the shared plane x = size; all heights on it must come from the
            // same deterministic field, therefore the maximum X of the left chunk equals the
            // minimum X of the right chunk.
            float leftMaxX = float.MinValue;
            for (int i = 0; i < left.VertexCount; i++)
            {
                leftMaxX = System.Math.Max(leftMaxX, left.Vertices[i * 3]);
            }

            float rightMinX = float.MaxValue;
            for (int i = 0; i < right.VertexCount; i++)
            {
                rightMinX = System.Math.Min(rightMinX, right.Vertices[i * 3]);
            }

            Assert.AreEqual(0f, Offset(leftMaxX, size), 0.0001f, "left chunk must not exceed its area");
            Assert.AreEqual(0f, Offset(rightMinX, size), 0.0001f, "right chunk must start at the shared plane");
            Assert.AreEqual(leftMaxX, rightMinX, 0.0001f);
        }

        [Test]
        public void MeshAreaMatchesChunkSize()
        {
            MeshData mesh = ChunkMeshBuilder.Build(new PilotHeightField(SeedA), -2, 3);
            int size = PilotWorldLayout.ChunkSize;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                float x = mesh.Vertices[i * 3];
                float z = mesh.Vertices[i * 3 + 2];
                minX = System.Math.Min(minX, x);
                maxX = System.Math.Max(maxX, x);
                minZ = System.Math.Min(minZ, z);
                maxZ = System.Math.Max(maxZ, z);
            }

            Assert.AreEqual(-2 * size, minX, 0.0001f);
            Assert.AreEqual(-1 * size, maxX, 0.0001f);
            Assert.AreEqual(3 * size, minZ, 0.0001f);
            Assert.AreEqual(4 * size, maxZ, 0.0001f);
        }

        private static float Offset(float value, float reference)
        {
            return System.Math.Abs(value - reference);
        }
    }
}
