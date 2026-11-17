using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    public sealed class FullM0P7SurfaceDecorationsTests
    {
        private sealed class DecorationProvider : IZoneContentProvider
        {
            public string Key => nameof(DecorationProvider);
            public List<int> Sites { get; } = new List<int>();
            public uint InitialState { get; private set; }
            public uint FinalState { get; private set; }
            public uint FirstBoulderDraw { get; private set; }
            public IReadOnlyList<AlphaSurfaceProp> Props { get; private set; }
            public Action<ZoneContentContext> SetUpZone { get; set; }

            public void Populate(ZoneContentContext context)
            {
                SetUpZone?.Invoke(context);
                AlphaCrtRandom random = context.Random;
                InitialState = unchecked((uint)random.State);
                random.AfterDraw = stream =>
                {
                    Sites.Add(stream.SiteFrom);
                    if (stream.SiteFrom == 0x5E8FBB)
                        FirstBoulderDraw = unchecked((uint)stream.State);
                };
                Props = AlphaZoneSurfaceDecorations.Populate(context);
                FinalState = unchecked((uint)random.State);
                random.AfterDraw = null;
            }
        }

        [TestCase(32795, 32804, 1774511535u, 3230306503u, 7316,
            "1ab12186fd997792c3a1ca8682a32a8e994e2615ae4ac41ea6ce68dcf72f331e")]
        [TestCase(32812, 32812, 3810746539u, 3731548354u, 0,
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
        public void MatchesAlphaSurfaceDrawsAndBoulderGeometry(int zoneX, int zoneZ, uint expectedIn, uint expectedOut,
            int expectedVoxelCount, string expectedGeometryHash)
        {
            var provider = new DecorationProvider();
            var terrain = new AlphaTerrainGenerator(666666, new[] { provider });
            AlphaZone zone = AlphaZoneGenerator.Generate(terrain, zoneX, zoneZ, true, new List<CarveCall>());
            var sites = new List<int>();
            if (zoneX == 32795)
            {
                for (int i = 0; i < 10; i++)
                    sites.AddRange(new[] { 0x5E8DE1, 0x5E8E1C, 0x5AD40B });
            }
            sites.Add(0x5E8FBB);
            if (zoneX == 32795)
            {
                for (int i = 0; i < 5; i++)
                    sites.AddRange(new[] { 0x5E9058, 0x5E9072, 0x5E9089, 0x5E90BB, 0x5E90D9 });
            }
            Assert.AreEqual(expectedIn, provider.InitialState, "CRT after terrain");
            CollectionAssert.AreEqual(sites, provider.Sites, "surface decoration CRT return addresses");
            Assert.AreEqual(expectedOut, provider.FinalState, "CRT after surface decoration");
            Assert.IsEmpty(provider.Props);
            AssertBoulderGeometry(zone, expectedVoxelCount, expectedGeometryHash);
        }

        private static void AssertBoulderGeometry(AlphaZone zone, int expectedVoxelCount, string expectedHash)
        {
            // Same ordering and coordinate format as boulder_voxel_hash in landmark_patrol_oracle.py.
            using var geometry = new MemoryStream();
            using var writer = new BinaryWriter(geometry);
            int startX = zone.Zx * AlphaZone.Size, startZ = zone.Zz * AlphaZone.Size;
            for (int x = startX; x < startX + AlphaZone.Size; x++)
            {
                for (int z = startZ; z < startZ + AlphaZone.Size; z++)
                {
                    VoxelColumn column = zone.Column(x, z);
                    for (int y = column.Bottom; y <= column.TopStored; y++)
                    {
                        if ((Voxel.Flags(column.Read(y)) & 0x7F) != 0x26)
                            continue;
                        writer.Write(x);
                        writer.Write(z);
                        writer.Write(y);
                    }
                }
            }
            using var sha256 = SHA256.Create();
            Assert.AreEqual(expectedVoxelCount, geometry.Length / 12, "unique boulder voxels");
            string hash = BitConverter.ToString(sha256.ComputeHash(geometry.ToArray())).Replace("-", "").ToLowerInvariant();
            Assert.AreEqual(expectedHash, hash, "boulder coordinates");
        }

        [Test]
        public void MatchesAlphaPropClusterOnFlatGround()
        {
            const int x = 8395638, z = 8398007;
            var provider = new DecorationProvider
            {
                SetUpZone = context =>
                {
                    for (int px = x - 8; px <= x + 8; px++)
                    {
                        for (int pz = z - 8; pz <= z + 8; pz++)
                            context.Write(px, 99, pz, Voxel.Pack(15, 15, 15, 0x24));
                    }
                },
            };
            var terrain = new AlphaTerrainGenerator(666666, new[] { provider });
            AlphaZoneGenerator.Generate(terrain, 32795, 32804, true, new List<CarveCall>());

            // 0x5AD320 with draw state 1923529121, at (x+.5, 100, z+.5) on a 17×17 pad.
            var expected = new (int, WorldPosition, int, float, float)[]
            {
                (0x41, new WorldPosition(x + 0.5, 100, z + 0.5), 0, 2.4f, 0.5f),
                (0x10, new WorldPosition(x - 3, 100, z - 3), 0, 1f, 0.5f),
                (0x45, new WorldPosition(x - 3, 100, z + 4), 0, 2f, 0.1f),
                (0x10, new WorldPosition(x + 4, 100, z - 3), 3, 1f, 0.5f),
                (0x10, new WorldPosition(x + 4, 100, z + 4), 2, 1f, 0.5f),
            };
            var actual = new List<(int, WorldPosition, int, float, float)>();
            foreach (AlphaSurfaceProp prop in provider.Props)
                actual.Add((prop.ObjectId, prop.Position, prop.Orientation, prop.Width, prop.Height));
            CollectionAssert.AreEqual(expected, actual);
            CollectionAssert.AreEqual(new[]
            {
                0x5E8DE1, 0x5E8E1C, 0x5AD40B,
                0x5AD635, 0x5AD700, 0x5AD635, 0x5AD700, 0x5AD635, 0x5AD700, 0x5AD635, 0x5AD700, 0x5E8FBB,
            }, provider.Sites.GetRange(0, 12), "prop CRT return addresses through the next pass");
            Assert.AreEqual(unchecked(3557982376u * 214013u + 2531011u), provider.FirstBoulderDraw,
                "CRT advances by one call from the end of the prop cluster");
        }
    }
}
