using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using CubeGame.Features.World.Foundation;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// G2-A multi-seed regression gate: "same seed + same generation profile = same CubeGame world".
    ///
    /// The digest is integer-only (FNV-1a 64 over exact float bit patterns), so it is comparable across runs of the same
    /// build and changes as soon as a height, a climate cell, a landmark or a zone record changes. Three seeds are frozen
    /// as golden values: an accidental algorithm change fails the suite even if every other test still passes.
    /// </summary>
    public sealed class G2WorldDeterminismGateTests
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        /// <summary>Seeds of different kinds: tiny, prime, the project default, high entropy, the extremes.</summary>
        private static readonly uint[] Seeds =
        {
            1u,
            2u,
            7u,
            666666u,
            424242u,
            0xDEADBEEFu,
            123456789u,
            uint.MaxValue,
        };

        // Frozen values of GenerationDigest(seed) for this build (G2 baseline).
        private const ulong GoldenSeed1 = 16933456655643791213UL;
        private const ulong GoldenSeed666666 = 15266482576104709386UL;
        private const ulong GoldenSeedMax = 640857990642059632UL;

        private const int ColumnSamples = 33;

        private static void Mix(ref ulong hash, ulong value)
        {
            hash ^= value;
            hash *= FnvPrime;
        }

        private static void MixFloat(ref ulong hash, float value) => Mix(ref hash, (ulong)(uint)BitConverter.SingleToInt32Bits(value));

        private static void MixInt(ref ulong hash, int value) => Mix(ref hash, (ulong)(uint)value);

        /// <summary>Terrain + climate + landmark world digest of one seed (no zone voxels: fast gate).</summary>
        private static ulong GenerationDigest(uint seed)
        {
            var terrain = new AlphaTerrainGenerator(seed);
            ulong hash = FnvOffset;
            Mix(ref hash, seed);
            foreach (WorldSeedValue value in new WorldSeedTable(seed).Describe())
            {
                Mix(ref hash, (ulong)(uint)value.Value);
            }

            int start = WorldLayout.StartVoxel;
            for (int i = 0; i < ColumnSamples; i++)
            {
                int x = start + i * 37 - 1000;
                int z = start + i * 53 - 700;
                TerrainColumn column = terrain.Column(x, z);
                MixInt(ref hash, column.Surface);
                Mix(ref hash, column.Rock ? 1UL : 0UL);
                MixFloat(ref hash, column.Mountain);
                MixFloat(ref hash, column.BaseHeight);
                MixFloat(ref hash, terrain.Temperature(x, z));
                MixFloat(ref hash, terrain.Humidity(x, z));
                MixFloat(ref hash, terrain.RareCellTerm(x, z));
                MixFloat(ref hash, terrain.Mountain(x, z));
            }

            for (int i = 0; i < 3; i++)
            {
                int rx = 512 + (i % 2);
                int rz = 512 - (i / 2);
                var key = new RegionKey(rx, rz);
                RegionInfo info = terrain.Region(key);
                MixInt(ref hash, info.Level);
                MixInt(ref hash, info.Variant);
                MixInt(ref hash, info.Orientation);
                MixInt(ref hash, info.Climate.BaseHeight);
                MixFloat(ref hash, info.Climate.Field3);
                MixFloat(ref hash, info.Climate.Field4);
                Mix(ref hash, info.Climate.Flag ? 1UL : 0UL);
                AlphaRegion record = terrain.RegionRecord(rx, rz);
                MixInt(ref hash, record.LevelMin);
                MixInt(ref hash, record.LevelMax);
                int towns = 0;
                for (int zone = 0; zone < record.Zones.Length; zone++)
                {
                    AlphaZoneRecord zoneRecord = record.Zones[zone];
                    MixInt(ref hash, zoneRecord.Type);
                    MixInt(ref hash, zoneRecord.Sub);
                    MixInt(ref hash, zoneRecord.Level);
                    if (zoneRecord.Type == AlphaZoneRecord.Town) towns++;
                }

                MixInt(ref hash, towns);
                for (int column = 0; column < record.Columns.Length; column++)
                {
                    LandmarkColumn landmark = record.Columns[column];
                    MixInt(ref hash, (int)landmark.Type);
                    MixInt(ref hash, landmark.Sub);
                    MixInt(ref hash, landmark.Level);
                    MixFloat(ref hash, landmark.Radius);
                }
            }

            return hash;
        }

        /// <summary>Same digest with one extra voxel on every sampled surface (simulates an algorithm change).</summary>
        private static ulong GenerationDigestTampered(uint seed, int shift)
        {
            var terrain = new AlphaTerrainGenerator(seed);
            ulong hash = FnvOffset;
            Mix(ref hash, seed);
            int start = WorldLayout.StartVoxel;
            for (int i = 0; i < ColumnSamples; i++)
            {
                int x = start + i * 37 - 1000;
                int z = start + i * 53 - 700;
                TerrainColumn column = terrain.Column(x, z);
                MixInt(ref hash, column.Surface + shift);
            }

            return hash;
        }

        [Test]
        public void SameSeedOnTwoFreshGeneratorsIsIdentical()
        {
            ulong first = GenerationDigest(666666u);
            ulong second = GenerationDigest(666666u);
            Assert.AreEqual(first, second, "two generators with the same seed must produce the same world digest");
        }

        [Test]
        public void EverySeedTypeProducesADifferentWorld()
        {
            var digests = new HashSet<ulong>();
            foreach (uint seed in Seeds)
            {
                ulong digest = GenerationDigest(seed);
                Assert.IsTrue(digests.Add(digest), $"seed {seed} collided with another seed's world digest");
            }

            Assert.AreEqual(Seeds.Length, digests.Count);
        }

        [Test]
        public void GoldenDigestsAreFrozenForThisGenerationProfile()
        {
            ulong seedOne = GenerationDigest(1u);
            ulong seedDefault = GenerationDigest(666666u);
            ulong seedMax = GenerationDigest(uint.MaxValue);
            if (GoldenSeed1 != seedOne || GoldenSeed666666 != seedDefault || GoldenSeedMax != seedMax)
            {
                Assert.Fail($"golden digests: seed1={seedOne} seed666666={seedDefault} seedMax={seedMax} " +
                            $"(frozen {GoldenSeed1}/{GoldenSeed666666}/{GoldenSeedMax})");
            }
        }

        [Test]
        public void DigestDetectsAOneVoxelChange()
        {
            ulong baseline = GenerationDigestTampered(666666u, 0);
            ulong changed = GenerationDigestTampered(666666u, 1);
            Assert.AreNotEqual(baseline, changed, "a one-voxel height change must invalidate the gate");
        }

        [Test]
        public void GenerationOrderDoesNotDependOnInterleaving()
        {
            // Two generators used alternately must still agree: no shared static stream may leak between them.
            var a = new AlphaTerrainGenerator(424242u);
            var b = new AlphaTerrainGenerator(424242u);
            int start = WorldLayout.StartVoxel;
            for (int i = 0; i < 10; i++)
            {
                int x = start + i * 11;
                int z = start - i * 13;
                TerrainColumn left = a.Column(x, z);
                TerrainColumn right = b.Column(x, z);
                Assert.AreEqual(left.Surface, right.Surface, "surface mismatch");
                Assert.AreEqual(left.Rock, right.Rock, "rock flag mismatch");
                Assert.AreEqual(BitConverter.SingleToInt32Bits(left.Mountain),
                    BitConverter.SingleToInt32Bits(right.Mountain), "mountain mismatch");
            }
        }

        [Test]
        public void GeneratedZoneVoxelsAreReproducibleAcrossInstances()
        {
            int zoneX = WorldLayout.StartVoxel >> 8;
            int zoneZ = WorldLayout.StartVoxel >> 8;
            ulong first = ZoneDigest(666666u, zoneX, zoneZ);
            ulong second = ZoneDigest(666666u, zoneX, zoneZ);
            ulong other = ZoneDigest(666667u, zoneX, zoneZ);
            Assert.AreEqual(first, second, "the same seed must regenerate an identical zone");
            Assert.AreNotEqual(first, other, "a different seed must produce a different zone");
        }

        /// <summary>Voxel digest of one generated zone over a sparse grid of columns and voxels.</summary>
        private static ulong ZoneDigest(uint seed, int zoneX, int zoneZ)
        {
            var terrain = new AlphaTerrainGenerator(seed);
            ulong hash = FnvOffset;
            int originX = zoneX * AlphaZone.Size;
            int originZ = zoneZ * AlphaZone.Size;
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    int x = originX + i * 31 + 7;
                    int z = originZ + j * 31 + 11;
                    VoxelColumn column = terrain.Voxels(x, z);
                    Assert.IsNotNull(column, "zone column missing");
                    MixInt(ref hash, column.TopStored);
                    MixInt(ref hash, column.Bottom);
                    MixInt(ref hash, column.Count);
                    int step = Math.Max(1, column.Count / 8);
                    for (int y = column.Bottom; y <= column.TopStored; y += step)
                    {
                        Mix(ref hash, column.Read(y));
                    }
                }
            }

            AlphaZone zone = terrain.Zone(zoneX, zoneZ);
            MixInt(ref hash, zone.Spawns.Count);
            MixInt(ref hash, zone.ContentSpawns.Count);
            MixInt(ref hash, zone.PopulationRandomState);
            return hash;
        }
    }
}
