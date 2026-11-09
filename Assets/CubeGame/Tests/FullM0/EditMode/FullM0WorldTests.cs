using System;
using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Gameplay;
using CubeGame.Persistence;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>M0-02: Alpha generation primitives, terrain determinism, streaming policy, world deltas and their save.</summary>
    public sealed class FullM0WorldTests
    {
        [Test]
        public void CrtRandomMatchesMsvcSequence()
        {
            var rng = new AlphaCrtRandom(1);
            CollectionAssert.AreEqual(new[] { 41, 18467, 6334 }, new[] { rng.Next(), rng.Next(), rng.Next() });
        }

        [Test]
        public void NoiseNodeValuesFollowAlphaHashAndStayInRange()
        {
            // At integer coordinates the cosine weights are 0, so the value is the lattice node itself.
            Assert.AreEqual(-0.2817909838631749, AlphaNoise.Sample(0.0, 0.0), 1e-7);
            var random = new Random(7);
            for (int i = 0; i < 5000; i++)
            {
                double x = random.NextDouble() * 100000.0;
                double y = random.NextDouble() * 100000.0;
                float v = AlphaNoise.Sample(x, y);
                Assert.That(v, Is.GreaterThan(-1.0001f).And.LessThanOrEqualTo(1.0001f));
                Assert.AreEqual(v, AlphaNoise.Sample(x, y));
            }

            // Continuity across a lattice edge (cosine interpolation, no jumps).
            Assert.AreEqual(AlphaNoise.Sample(4.999999, 3.5), AlphaNoise.Sample(5.0, 3.5), 1e-4);
        }

        [Test]
        public void RegionLevelFollowsDistanceFormula()
        {
            Assert.AreEqual(1, AlphaTerrainGenerator.Level(512, 512));
            Assert.AreEqual(2, AlphaTerrainGenerator.Level(513, 512));
            Assert.AreEqual(8, AlphaTerrainGenerator.Level(520, 512));
            Assert.AreEqual(2 + (int)(0.75f * (float)Math.Sqrt(200.0)), AlphaTerrainGenerator.Level(502, 502));
        }

        [Test]
        public void SeedsAndClimateCellsAreDeterministic()
        {
            var a = new AlphaWorldSeeds(12345u);
            var b = new AlphaWorldSeeds(12345u);
            Assert.AreEqual(a.WorldSeed, b.WorldSeed);
            CollectionAssert.AreEqual(a.Tables, b.Tables);
            Assert.AreEqual(20, a.Tables.Length);
            Assert.That(a.WorldSeed, Is.InRange(0, 0x7FFF));
            Assert.AreNotEqual(a.WorldSeed, new AlphaWorldSeeds(54321u).WorldSeed);

            var grid1 = new AlphaClimateGrid(a, new RegionKey(512, 512));
            var grid2 = new AlphaClimateGrid(b, new RegionKey(512, 512));
            int water = 0;
            for (int rx = 480; rx < 544; rx++)
            {
                for (int rz = 480; rz < 544; rz += 3)
                {
                    AlphaClimateCell c1 = grid1.Get(rx, rz);
                    AlphaClimateCell c2 = grid2.Get(rx, rz);
                    Assert.AreEqual(c1.BaseHeight, c2.BaseHeight);
                    Assert.AreEqual(c1.AnchorX, c2.AnchorX);
                    Assert.That(c1.AnchorX, Is.InRange(rx * WorldLayout.CoarsePerRegion, (rx + 1) * WorldLayout.CoarsePerRegion));
                    Assert.That(c1.AnchorZ, Is.InRange(rz * WorldLayout.CoarsePerRegion, (rz + 1) * WorldLayout.CoarsePerRegion));
                    Assert.IsTrue(c1.IsWater ? c1.BaseHeight == -100 : c1.BaseHeight >= 1, $"base {c1.BaseHeight}");
                    if (c1.IsWater)
                    {
                        water++;
                    }
                }
            }

            Assert.Greater(water, 0, "Alpha water cells (base −100) exist in a 64×22 sample");
            AlphaClimateCell spawn = grid1.Get(512, 512);
            Assert.That(spawn.BaseHeight, Is.GreaterThanOrEqualTo(1), "start region is land");
            Assert.IsNull(grid1.Get(-1, 0));
            Assert.IsNull(grid1.Get(1024, 0));
        }

        [Test]
        public void TerrainIsDeterministicSeedDependentAndContinuous()
        {
            var g1 = new AlphaTerrainGenerator(777u);
            var g2 = new AlphaTerrainGenerator(777u);
            var other = new AlphaTerrainGenerator(778u);
            int differences = 0;
            int previous = g1.Column(WorldLayout.StartBlock - 1000, WorldLayout.StartBlock).Surface;
            int maxStep = 0;
            float maxBaseStep = 0f;
            float previousBase = g1.BlendedBase(WorldLayout.StartBlock - 1000, WorldLayout.StartBlock);
            for (int x = WorldLayout.StartBlock - 999; x < WorldLayout.StartBlock + 1000; x++)
            {
                TerrainColumn c = g1.Column(x, WorldLayout.StartBlock);
                Assert.AreEqual(c.Surface, g2.Column(x, WorldLayout.StartBlock).Surface);
                Assert.That(c.Surface, Is.InRange(-110, 450));
                if (c.Surface != other.Column(x, WorldLayout.StartBlock).Surface)
                {
                    differences++;
                }

                maxStep = Math.Max(maxStep, Math.Abs(c.Surface - previous));
                previous = c.Surface;
                float b = g1.BlendedBase(x, WorldLayout.StartBlock);
                maxBaseStep = Math.Max(maxBaseStep, Math.Abs(b - previousBase));
                previousBase = b;
            }

            Assert.Greater(differences, 1000, "another seed gives another world");

            // Alpha's weight (1 − (d² − d²min)·5e-8)² in coarse units blends neighbouring climate cells over only
            // ~2–3 blocks, so cell borders form steps (PARTIAL: to be compared with Alpha visuals). The step is
            // bounded by the spread of cell bases (−100…≈160).
            Assert.LessOrEqual(maxBaseStep, 261f);
            Assert.LessOrEqual(maxStep, 400);

            int start = g1.Column(WorldLayout.StartBlock, WorldLayout.StartBlock).Surface;
            Assert.GreaterOrEqual(start, g1.WaterLevel, "hero starts on land");
        }

        [Test]
        public void RegionRecordIsDeterministic()
        {
            var g = new AlphaTerrainGenerator(31337u);
            RegionInfo center = g.Region(new RegionKey(512, 512));
            Assert.AreEqual(1, center.Level);
            Assert.AreEqual(0, center.Variant);
            RegionInfo far = g.Region(new RegionKey(530, 512));
            Assert.AreEqual(far.Orientation, new AlphaTerrainGenerator(31337u).Region(new RegionKey(530, 512)).Orientation);
            Assert.That(far.Variant, Is.InRange(0, 4));
            Assert.That(far.Orientation, Is.InRange(0, 5));
        }

        [Test]
        public void StreamingPolicyWantsNearestFirstAndReleasesWithHysteresis()
        {
            var policy = new WorldStreamingPolicy(2, 3);
            var center = new RegionKey(512, 512);
            List<RegionKey> wanted = policy.Wanted(center);
            Assert.AreEqual(25, wanted.Count);
            Assert.AreEqual(center, wanted[0]);
            Assert.AreEqual(25, new HashSet<RegionKey>(wanted).Count, "no duplicates");
            Assert.IsFalse(policy.ShouldRelease(new RegionKey(515, 512), center));
            Assert.IsTrue(policy.ShouldRelease(new RegionKey(516, 512), center));
            Assert.AreEqual(9, policy.Wanted(new RegionKey(0, 0)).Count, "world edge is clipped");
        }

        [Test]
        public void ThousandStepWalkKeepsDeltasAndNeverDuplicatesRegions()
        {
            var world = new WorldState(new AlphaTerrainGenerator(5u), new WorldDeltaStore());
            var policy = new WorldStreamingPolicy(2, 3);
            var loaded = new HashSet<RegionKey>();
            Assert.IsTrue(world.Deltas.SetColumnSurface(32800, 32800, 77, 0));
            var random = new Random(99);
            var center = new RegionKey(512, 512);
            for (int step = 0; step < 1000; step++)
            {
                center = new RegionKey(
                    Math.Max(500, Math.Min(524, center.X + random.Next(-1, 2))),
                    Math.Max(500, Math.Min(524, center.Z + random.Next(-1, 2))));
                loaded.RemoveWhere(r => policy.ShouldRelease(r, center));
                foreach (RegionKey r in policy.Wanted(center))
                {
                    loaded.Add(r);
                }

                Assert.LessOrEqual(loaded.Count, 49);
            }

            Assert.AreEqual(77, world.SurfaceAt(32800, 32800), "delta survives any number of unloads");
            Assert.AreEqual(1, world.Deltas.Count);
        }

        [Test]
        public void DeltaStoreOverridesValidatesAndTracksDiscovery()
        {
            var store = new WorldDeltaStore();
            var changed = new List<RegionKey>();
            store.RegionChanged += changed.Add;
            Assert.IsTrue(store.SetColumnSurface(100, 200, 10, 1));
            Assert.IsTrue(store.SetColumnSurface(100, 200, 12, 2));
            Assert.AreEqual(1, store.Count, "later edit of the same column replaces the earlier one");
            Assert.IsTrue(store.TryGetColumnSurface(100, 200, out int s));
            Assert.AreEqual(12, s);
            Assert.IsFalse(store.SetColumnSurface(-1, 0, 5, 0));
            Assert.IsFalse(store.SetColumnSurface(0, 0, 5000, 0));
            Assert.AreEqual(2, changed.Count);
            Assert.IsTrue(store.MarkDiscovered(new RegionKey(3, 4)));
            Assert.IsFalse(store.MarkDiscovered(new RegionKey(3, 4)));
            Assert.IsFalse(store.MarkDiscovered(new RegionKey(-1, 4)));
        }

        private sealed class MemoryStore : ISaveStore
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();

            public bool Exists(string key) => Files.ContainsKey(key);

            public string Read(string key) => Files.TryGetValue(key, out string v) ? v : null;

            public void Write(string key, string contents) => Files[key] = contents;

            public void Delete(string key) => Files.Remove(key);
        }

        private static GameSlotStore Slots(ISaveStore store) =>
            new GameSlotStore(store, new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());

        private static GameSession Running()
        {
            var session = new GameSession();
            session.EnterMainMenu();
            session.BeginNewWorld("slot0", new NewWorldRequest
            {
                WorldName = "Deltas",
                Seed = 42u,
                GeneratorId = AlphaTerrainGenerator.Id,
                HeroName = "Cy",
                Class = CharacterClass.Warrior,
            }, 1L);
            session.CompleteLoading();
            return session;
        }

        [Test]
        public void WorldDeltasAndDiscoveryRoundTripThroughSlot()
        {
            var store = new MemoryStore();
            GameSession session = Running();
            session.WorldDeltas.SetColumnSurface(32801, 32799, 55, 10);
            session.WorldDeltas.SetColumnSurface(40000, 1000, -20, 11);
            session.WorldDeltas.MarkDiscovered(new RegionKey(512, 512));
            session.WorldDeltas.MarkDiscovered(new RegionKey(513, 512));
            Assert.IsTrue(Slots(store).Save(session, 3L).IsSuccess);
            StringAssert.StartsWith("CUBEGAME_WORLD|1|", store.Files["slot0.world"]);

            SlotLoadResult loaded = Slots(store).Load("slot0", AlphaTerrainGenerator.Id);
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            Assert.AreEqual(2, loaded.WorldDeltas.Count);
            Assert.IsTrue(loaded.WorldDeltas.TryGetColumnSurface(40000, 1000, out int s));
            Assert.AreEqual(-20, s);
            Assert.IsTrue(loaded.WorldDeltas.IsDiscovered(new RegionKey(513, 512)));
            Assert.AreEqual(2, loaded.WorldDeltas.Discovered.Count);

            var restored = new GameSession();
            restored.EnterMainMenu();
            Assert.IsTrue(restored.BeginLoadedWorld("slot0", loaded.World, loaded.Hero, loaded.ClockTick, loaded.WorldDeltas));
            var state = new WorldState(new AlphaTerrainGenerator(42u), restored.WorldDeltas);
            Assert.AreEqual(55, state.SurfaceAt(32801, 32799));
        }

        [Test]
        public void TornOrMissingWorldFileIsRefused()
        {
            var store = new MemoryStore();
            GameSession session = Running();
            session.WorldDeltas.SetColumnSurface(32800, 32800, 60, 1);
            Assert.IsTrue(Slots(store).Save(session, 1L).IsSuccess);
            string firstWorld = store.Files["slot0.world"];

            session.WorldDeltas.SetColumnSurface(32800, 32800, 61, 2);
            Assert.IsTrue(Slots(store).Save(session, 2L).IsSuccess);
            store.Files["slot0.world"] = firstWorld;
            Assert.AreEqual(SlotLoadStatus.PairMismatch, Slots(store).Load("slot0", AlphaTerrainGenerator.Id).Status);

            store.Files.Remove("slot0.world");
            Assert.AreEqual(SlotLoadStatus.PairMismatch, Slots(store).Load("slot0", AlphaTerrainGenerator.Id).Status);
        }

        [Test]
        public void PilotGeneratorSaveIsRefusedNotRegenerated()
        {
            var store = new MemoryStore();
            var session = new GameSession();
            session.EnterMainMenu();
            session.BeginNewWorld("slot0", new NewWorldRequest { Seed = 1u, GeneratorId = "cubegame.pilot_heightfield/1" }, 0L);
            session.CompleteLoading();
            Assert.IsTrue(Slots(store).Save(session, 0L).IsSuccess);
            Assert.AreEqual(SlotLoadStatus.GeneratorMismatch, Slots(store).Load("slot0", AlphaTerrainGenerator.Id).Status);
            Assert.IsFalse(WorldState.TryCreateGenerator("cubegame.pilot_heightfield/1", 1u, out _));
        }
    }
}
