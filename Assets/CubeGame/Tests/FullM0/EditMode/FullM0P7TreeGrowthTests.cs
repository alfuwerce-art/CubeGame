using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using CubeGame.Features.World;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    public sealed class FullM0P7TreeGrowthTests
    {
        [Serializable]
        private sealed class ValleyFixture
        {
            public string status;
            public uint seed;
            public int zoneX;
            public int zoneZ;
            public long beforeTrees;
            public long afterTrees;
            public long[] trees;
        }

        private const int GrowthStride = 8;

        [TestCase(8399918, 8399900, 64, 10, 20, 2214495820u, 134685627u, 205, 6841,
            "4e2fbfac8a844dbcf0b2043332e0a577f549b3f2a022603f7cd6da866545e3a3")]
        [TestCase(8399881, 8399881, 76, 7, 14, 4167882435u, 857677693u, 74, 1079,
            "c0bfb0443f0ddbea8ceb0d0f756be13d49e6f4b4b7db2c2a5619503c73092082")]
        public void KindFiveMatchesAlphaDrawsAndGeometry(int x, int z, int y, int radius, int height,
            uint crtIn, uint crtOut, int expectedDraws, int expectedVoxels, string expectedHash)
        {
            var terrain = new AlphaTerrainGenerator(666666);
            var zone = new AlphaZone(32812, 32812);
            for (int i = 0; i < zone.Columns.Length; i++)
                zone.Columns[i] = new VoxelColumn { Temperature = 0.59797055f, Humidity = 0.7230934f };

            var random = new AlphaCrtRandom(unchecked((int)crtIn));
            int draws = 0;
            random.AfterDraw = _ => draws++;
            GrowKindFive(terrain, zone, random, x, y, z, radius, height);

            Assert.AreEqual(expectedDraws, draws, "draw count");
            Assert.AreEqual(crtOut, unchecked((uint)random.State), "CRT after growth");
            AssertGeometry(zone, expectedVoxels, expectedHash);
        }

        [Test]
        public void KindFiveMatchesCrtForAllValleyGrowthCalls()
        {
            ValleyFixture fixture = ReadValleyFixture();
            // The zone oracle records but does not apply the earlier terrain carves.
            var terrain = new AlphaTerrainGenerator(fixture.seed);
            AlphaZone zone = AlphaZoneGenerator.Generate(terrain, fixture.zoneX, fixture.zoneZ, true, new List<CarveCall>());
            long[] data = fixture.trees;
            int tested = 0;
            for (int i = 0; i < data.Length; i += GrowthStride)
            {
                if (data[i + 5] != 5)
                    continue;
                var random = new AlphaCrtRandom(unchecked((int)data[i + 6]));
                GrowKindFive(terrain, zone, random, (int)data[i], (int)data[i + 2], (int)data[i + 1],
                    (int)data[i + 3], (int)data[i + 4]);
                Assert.AreEqual(data[i + 7], unchecked((uint)random.State), $"CRT after tree {i / GrowthStride}");
                tested++;
            }
            Assert.AreEqual(45, tested);
        }

        [Test]
        public void ValleyTreesMatchAllGrowthCallsOnOneStream()
        {
            ValleyFixture fixture = ReadValleyFixture();
            var provider = new TreePassProbe();
            var terrain = new AlphaTerrainGenerator(fixture.seed, new[] { provider });
            AlphaZoneGenerator.Generate(terrain, fixture.zoneX, fixture.zoneZ, true, new List<CarveCall>());

            Assert.AreEqual(fixture.beforeTrees, provider.BeforeTrees, "CRT on entry to trees");
            long[] expected = fixture.trees;
            for (int i = 0; i < Math.Min(expected.Length / GrowthStride, provider.Growth.Count); i++)
            {
                var row = new ArraySegment<long>(expected, i * GrowthStride, GrowthStride);
                CollectionAssert.AreEqual(row, provider.Growth[i], $"growth call {i + 1} (X, Z, Y, radius, height, kind, CRT in, CRT out)");
            }
            Assert.AreEqual(expected.Length / GrowthStride, provider.Growth.Count, "number of growth calls");
            Assert.AreEqual(fixture.afterTrees, provider.AfterTrees, "CRT after trees");
        }

        private sealed class TreePassProbe : IZoneContentProvider
        {
            public string Key => nameof(TreePassProbe);
            public uint BeforeTrees { get; private set; }
            public uint AfterTrees { get; private set; }
            public List<long[]> Growth { get; } = new List<long[]>();

            public void Populate(ZoneContentContext context)
            {
                AlphaZoneSurfaceDecorations.Populate(context);
                BeforeTrees = unchecked((uint)context.Random.State);
                Type type = typeof(AlphaZoneGenerator).Assembly.GetType("CubeGame.Features.World.AlphaZoneTrees", true);
                MethodInfo run = type.GetMethod("PopulateBeforePatrols", BindingFlags.Static | BindingFlags.NonPublic);
                Action<int, int, int, int, int, int, uint, uint> onGrowth = (x, z, y, r, h, k, input, output) =>
                    Growth.Add(new long[] { x, z, y, r, h, k, input, output });
                run.Invoke(null, new object[] { context, null, onGrowth });
                AfterTrees = unchecked((uint)context.Random.State);
            }
        }

        private static ValleyFixture ReadValleyFixture()
        {
            string path = Path.Combine(Application.dataPath, "CubeGame/Tests/FullM0/EditMode/Fixtures",
                "alpha_tree_growth_32812_32812.json");
            var fixture = JsonUtility.FromJson<ValleyFixture>(File.ReadAllText(path));
            Assert.AreEqual("EMULATED_ALPHA_CODE", fixture.status);
            Assert.AreEqual(99 * GrowthStride, fixture.trees.Length);
            return fixture;
        }

        private static void GrowKindFive(AlphaTerrainGenerator terrain, AlphaZone zone, AlphaCrtRandom random,
            int x, int y, int z, int radius, int height)
        {
            var context = (ZoneContentContext)Activator.CreateInstance(typeof(ZoneContentContext),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { terrain, new AlphaColors(terrain), zone, random }, null);
            Type trees = typeof(AlphaZoneGenerator).Assembly.GetType("CubeGame.Features.World.AlphaZoneTrees", true);
            MethodInfo grow = trees.GetMethod("Grow", BindingFlags.Static | BindingFlags.NonPublic);
            grow.Invoke(null, new object[] { context, x, y, z, radius, height, 5 });
        }

        private static void AssertGeometry(AlphaZone zone, int expectedVoxels, string expectedHash)
        {
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
                        int flags = Voxel.Flags(column.Read(y));
                        if (flags != 0x27 && flags != 0x28)
                            continue;
                        writer.Write(x);
                        writer.Write(z);
                        writer.Write(y);
                        writer.Write((byte)flags);
                    }
                }
            }
            using var sha256 = SHA256.Create();
            Assert.AreEqual(expectedVoxels, geometry.Length / 13, "unique wood and leaf voxels");
            string hash = BitConverter.ToString(sha256.ComputeHash(geometry.ToArray())).Replace("-", "").ToLowerInvariant();
            Assert.AreEqual(expectedHash, hash, "wood and leaf coordinates");
        }
    }
}
