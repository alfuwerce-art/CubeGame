using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CubeGame.Features.World;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// Voxel columns of a zone against EXECUTED Cube.exe (status EMULATED_ALPHA_CODE): the column loop of
    /// generateBiomeContent run in the x86 emulator up to the decorations call (layers, colours, flags, caves, lake
    /// water). Fixture = numbers only (packed voxel uints, climate fields). Colour channels may differ by 1 (float).
    /// Also covers the voxel world deltas (block and column edits on copies, never on generated columns).
    /// </summary>
    public sealed class FullM0ZoneOracleTests
    {
        [Serializable]
        private sealed class Column
        {
            public int lx;
            public int lz;
            public float t;
            public float h;
            public int bottom;
            public List<long> vox;
        }

        [Serializable]
        private sealed class Carve
        {
            public string kind;
            public int cx;
            public int cz;
            public int base_h;
            public int rx;
            public int rz;
            public int height;
        }

        [Serializable]
        private sealed class Fixture
        {
            public string status;
            public int master_seed;
            public List<int> zone;
            public List<Column> columns;

            /// <summary>Carve calls the oracle recorded instead of executing (shapes: carve_oracle.py).</summary>
            public List<Carve> carves_skipped;
        }

        private static Fixture Load(string name)
        {
            string path = Path.IsPathRooted(name) ? name : Path.Combine(Application.dataPath, "CubeGame/Tests/FullM0/EditMode/Fixtures", name);
            Assert.IsTrue(File.Exists(path), path);
            Fixture fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
            Assert.AreEqual("EMULATED_ALPHA_CODE", fixture.status);
            return fixture;
        }

        private static void AssertZoneMatches(string fixtureName, int minColumns, bool features)
        {
            Fixture fixture = Load(fixtureName);
            var generator = new AlphaTerrainGenerator((uint)fixture.master_seed);
            int zx = fixture.zone[0];
            int zz = fixture.zone[1];
            var timer = Stopwatch.StartNew();
            var carves = features ? new List<CarveCall>() : null;
            AlphaZone zone = AlphaZoneGenerator.Generate(generator, zx, zz, features, carves);
            timer.Stop();
            if (features)
            {
                var expected = new List<CarveCall>();
                foreach (Carve c in fixture.carves_skipped ?? new List<Carve>())
                {
                    expected.Add(new CarveCall(c.kind[0], c.cx, c.cz, c.base_h, c.rx, c.rz, c.height));
                }

                Debug.Log($"[FullM0] zone ({zx},{zz}) carve calls alpha {expected.Count} cubegame {carves.Count}");
                CollectionAssert.AreEqual(expected, carves, "carve calls (kind, centre, base, size) in Alpha order");
            }

            Debug.Log($"[FullM0] zone ({zx},{zz}) generated in {timer.ElapsedMilliseconds} ms, cave content candidates {zone.Spawns.Count}");

            int mismatches = 0;
            int flips = 0;
            int voxels = 0;
            foreach (Column e in fixture.columns)
            {
                VoxelColumn c = zone.Columns[e.lx + e.lz * AlphaZone.Size];
                string at = $"zone ({zx},{zz}) column ({e.lx},{e.lz})";
                Assert.AreEqual(e.t, c.Temperature, 1e-5f, at + " temperature");
                Assert.AreEqual(e.h, c.Humidity, 1e-5f, at + " humidity");
                if (Math.Abs(e.bottom - c.Bottom) == 1 && e.vox.Count == c.Count)
                {
                    // Surface height within ~1e-6 of an integer: the base-height port (≤ 2.3e-3 from executed Alpha,
                    // II-c) can land on the other side of the (int) truncation. Counted and bounded, not hidden.
                    Debug.Log($"[FullM0] {at}: truncation flip, alpha bottom {e.bottom} cubegame {c.Bottom}");
                    flips++;
                    continue;
                }

                if (e.bottom != c.Bottom || e.vox.Count != c.Count)
                {
                    if (mismatches < 10)
                    {
                        Debug.Log($"[FullM0] {at}: alpha bottom {e.bottom} count {e.vox.Count}, cubegame bottom {c.Bottom} count {c.Count}");
                    }

                    mismatches++;
                    continue;
                }

                int differing = 0;
                int firstDiffering = -1;
                for (int i = 0; i < e.vox.Count; i++)
                {
                    if (!SameVoxel((uint)e.vox[i], c.Voxels[i]))
                    {
                        differing++;
                        firstDiffering = firstDiffering < 0 ? i : firstDiffering;
                    }
                }

                if (differing == 1 && firstDiffering == 0 && e.vox.Count > 1)
                {
                    // Only the lowest voxel (stone blend vs top colour) differs: the steep test |Δbase| > 0.3 sits on
                    // its threshold (e.g. 0.2999954) and the base-height port lands on the other side. Bounded below.
                    Debug.Log($"[FullM0] {at}: steep-threshold flip, alpha 0x{(uint)e.vox[0]:X8} cubegame 0x{c.Voxels[0]:X8}");
                    flips++;
                    voxels += e.vox.Count;
                    continue;
                }

                for (int i = 0; i < e.vox.Count; i++)
                {
                    uint expected = (uint)e.vox[i];
                    uint actual = c.Voxels[i];
                    voxels++;
                    if (!SameVoxel(expected, actual))
                    {
                        if (mismatches < 10)
                        {
                            Debug.Log($"[FullM0] {at} y={e.bottom + i}: alpha 0x{expected:X8} cubegame 0x{actual:X8}");
                        }

                        mismatches++;
                    }
                }
            }

            Debug.Log($"[FullM0] zone ({zx},{zz}): {fixture.columns.Count} columns, {voxels} voxels, mismatches {mismatches}, truncation flips {flips}");
            Assert.AreEqual(0, mismatches, "columns/voxels differing from executed Alpha code");
            Assert.LessOrEqual(flips, Math.Max(1, fixture.columns.Count / 10000), "threshold flips above 0.01 % (at least one allowed)");
            Assert.GreaterOrEqual(fixture.columns.Count, minColumns);
        }

        private static bool SameVoxel(uint expected, uint actual)
        {
            if (Voxel.Flags(expected) == Voxel.Flags(actual) && Voxel.Material(expected) == 0)
            {
                // Air: only flags matter. The dumps were taken before the oracle set Alpha's CRT-initialised air
                // constant (255,255,255,0); they store (0,0,0,0) where the road/river clearing wrote air.
                return true;
            }

            return Voxel.Flags(expected) == Voxel.Flags(actual)
                   && Math.Abs(Voxel.R(expected) - Voxel.R(actual)) <= 1
                   && Math.Abs(Voxel.G(expected) - Voxel.G(actual)) <= 1
                   && Math.Abs(Voxel.B(expected) - Voxel.B(actual)) <= 1;
        }

        [Test]
        public void StartZoneColumnsMatchExecutedAlphaCode()
        {
            // Probe stopped inside the column loop (first 600 columns): compare before the feature passes.
            AssertZoneMatches("alpha_zone_oracle_666666_32800_32800.json", 600, features: false);
        }

        [Serializable]
        private sealed class GridRecord
        {
            public int lx;
            public int lz;
            public int type;
            public int sub;
            public int id;
            public int level;
            public int cls;
        }

        [Serializable]
        private sealed class GridRegion
        {
            public int rx;
            public int rz;
            public List<GridRecord> zones;
        }

        [Serializable]
        private sealed class GridFixture
        {
            public string status;
            public int master_seed;
            public List<GridRegion> regions;
        }

        [Test]
        public void ZoneGridMatchesExecutedGenerateRegion()
        {
            string path = Path.Combine(Application.dataPath, "CubeGame/Tests/FullM0/EditMode/Fixtures/alpha_zone_grid_oracle_666666.json");
            GridFixture fixture = JsonUtility.FromJson<GridFixture>(File.ReadAllText(path));
            Assert.AreEqual("EMULATED_ALPHA_CODE", fixture.status);
            var generator = new AlphaTerrainGenerator((uint)fixture.master_seed);
            int compared = 0;
            int towns = 0;
            foreach (GridRegion expected in fixture.regions)
            {
                AlphaRegion region = generator.RegionRecord(expected.rx, expected.rz);
                var special = new Dictionary<int, GridRecord>();
                foreach (GridRecord r in expected.zones)
                {
                    special[r.lx * 64 + r.lz] = r;
                }

                for (int i = 0; i < 4096; i++)
                {
                    GridRecord e = special.TryGetValue(i, out GridRecord s) ? s : new GridRecord { lx = i >> 6, lz = i & 63, level = 1 };
                    AlphaZoneRecord z = region.Zones[i];
                    string at = $"region ({expected.rx},{expected.rz}) zone ({e.lx},{e.lz})";
                    Assert.AreEqual(e.type, z.Type, at + " type");
                    Assert.AreEqual(e.sub, z.Sub, at + " sub");
                    Assert.AreEqual(e.id, z.Id, at + " id");
                    Assert.AreEqual(e.level, z.Level, at + " level");
                    Assert.AreEqual(e.cls, z.LevelClass, at + " class");
                    towns += z.Type == AlphaZoneRecord.Town ? 1 : 0;
                    compared++;
                }
            }

            Assert.AreEqual(9 * 4096, compared);
            Assert.AreEqual(9 * 4, towns, "four town zones per region village");
        }

        /// <summary>Canyon zone after all terrain passes: committed subset of the executed dump + all 7 carve calls.</summary>
        [Test]
        public void CanyonZoneMatchesExecutedAlphaCode()
        {
            AssertZoneMatches("alpha_zone_oracle_666666_32796_32796.json", 1000, features: true);
        }

        /// <summary>Zone with a road between regions and a river: committed subset (road/river columns first).</summary>
        [Test]
        public void RoadAndRiverZoneMatchesExecutedAlphaCode()
        {
            AssertZoneMatches("alpha_zone_oracle_666666_32782_32806.json", 1400, features: true);
        }

        /// <summary>Town zone of the start village (streets, cleared core): committed subset.</summary>
        [Test]
        public void VillageStreetZoneMatchesExecutedAlphaCode()
        {
            AssertZoneMatches("alpha_zone_oracle_666666_32804_32804.json", 1400, features: true);
        }

        /// <summary>River zone (terraced water, river bed, cleared banks): committed subset.</summary>
        [Test]
        public void RiverZoneMatchesExecutedAlphaCode()
        {
            AssertZoneMatches("alpha_zone_oracle_666666_32816_32808.json", 1400, features: true);
        }

        /// <summary>
        /// Complete zones (all 65536 columns incl. carves, roads, rivers) against local oracle dumps in .scratch
        /// (not committed: 8 MB each). Ignored when the dump is absent; the committed subset runs always.
        /// </summary>
        [TestCase("zone_canyon.json")]
        [TestCase("zone_lake.json")]
        [TestCase("zone_river.json")]
        [TestCase("zone_village.json")]
        [TestCase("zone_roadriver.json")]
        [TestCase("zone_carves.json")]
        public void CompleteZoneMatchesLocalOracleDump(string dump)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../.scratch/m0", dump));
            if (!File.Exists(path))
            {
                Assert.Ignore("local oracle dump absent: " + path);
            }

            AssertZoneMatches(path, 65536, features: true);
        }

        [Test]
        public void ZoneGenerationIsDeterministicAndCached()
        {
            var a = new AlphaTerrainGenerator(666666u);
            var b = new AlphaTerrainGenerator(666666u);
            int x = WorldLayout.StartVoxel;
            int z = WorldLayout.StartVoxel;
            var timer = Stopwatch.StartNew();
            AlphaZone za = a.Zone(x >> 8, z >> 8);
            Debug.Log($"[FullM0] start zone with feature passes generated in {timer.ElapsedMilliseconds} ms, candidates {za.Spawns.Count}");
            Assert.AreSame(za, a.Zone(x >> 8, z >> 8), "cached zone reused");
            AlphaZone zb = b.Zone(x >> 8, z >> 8);
            for (int i = 0; i < AlphaZone.Size * AlphaZone.Size; i += 97)
            {
                CollectionAssert.AreEqual(za.Columns[i].Voxels, zb.Columns[i].Voxels);
                Assert.AreEqual(za.Columns[i].Bottom, zb.Columns[i].Bottom);
            }

            Assert.AreEqual(za.Spawns.Count, zb.Spawns.Count, "ordered CRT pass is deterministic despite parallel columns");
            Assert.IsNotNull(a.Voxels(x, z));
            Assert.IsNull(a.Voxels(-1, z));
        }

        private sealed class PillarProvider : IZoneContentProvider
        {
            public string Key => "test.pillar";

            public int FirstRandom;

            public void Populate(ZoneContentContext context)
            {
                FirstRandom = context.Random.Next();
                int x = context.Zone.Zx * AlphaZone.Size + 10;
                int z = context.Zone.Zz * AlphaZone.Size + 10;
                int top = context.Zone.Column(x, z).TopSolid();
                for (int y = top + 1; y <= top + 5; y++)
                {
                    Assert.IsTrue(context.Write(x, y, z, Voxel.Pack(10, 20, 30, 0x86)));
                }

                Assert.IsFalse(context.Write(x - 20, top, z, Voxel.DefaultSolid), "writes outside the zone are dropped");
                context.Spawns.Add(new CubeGame.Features.Contracts.SpawnDefinition { SpawnIndex = 1, Provenance = "TEST" });
            }
        }

        [Test]
        public void ContentProvidersRunAfterTerrainWithTheZoneStream()
        {
            int x = WorldLayout.StartVoxel;
            int z = WorldLayout.StartVoxel;
            var plain = new AlphaTerrainGenerator(666666u);
            int top = plain.Voxels(x - 118, z - 118).TopSolid();
            var a = new PillarProvider();
            var b = new PillarProvider();
            var withA = new AlphaTerrainGenerator(666666u, new IZoneContentProvider[] { a });
            var withB = new AlphaTerrainGenerator(666666u, new IZoneContentProvider[] { b });
            VoxelColumn column = withA.Voxels(x - 118, z - 118);
            withB.Voxels(x - 118, z - 118);
            Assert.AreEqual(top + 5, column.TopSolid(), "pillar of five voxels on the generated ground");
            Assert.AreEqual(Voxel.Pack(10, 20, 30, 0x86), column.Read(top + 3));
            Assert.AreEqual(1, withA.Zone(x >> 8, z >> 8).ContentSpawns.Count);
            Assert.AreEqual(a.FirstRandom, b.FirstRandom, "zone stream continues deterministically after the terrain passes");
        }

        [Test]
        public void VoxelEditsApplyToCopiesAndOverrideGeneratedColumns()
        {
            var generator = new AlphaTerrainGenerator(666666u);
            var world = new WorldState(generator, new WorldDeltaStore());
            int x = WorldLayout.StartVoxel;
            int z = WorldLayout.StartVoxel;
            VoxelColumn generated = generator.Voxels(x, z);
            int top = generated.TopSolid();
            Assert.AreEqual(top, world.SurfaceAt(x, z));
            Assert.IsTrue(Voxel.IsSolid(world.VoxelAt(x, top, z)));

            // Dig the top voxel out, then place a block two above the ground.
            Assert.IsTrue(world.Deltas.SetVoxel(x, top, z, Voxel.Air, 1));
            Assert.AreEqual(top - 1, world.SurfaceAt(x, z));
            uint stone = Voxel.Pack(90, 90, 90, 6);
            Assert.IsTrue(world.Deltas.SetVoxel(x, top + 2, z, stone, 2));
            Assert.AreEqual(top + 2, world.SurfaceAt(x, z));
            Assert.AreEqual(stone, world.VoxelAt(x, top + 2, z));
            Assert.AreEqual(Voxel.Air, world.VoxelAt(x, top + 1, z));
            Assert.AreEqual(top, generator.Voxels(x, z).TopSolid(), "generated column itself is never mutated");

            // A later edit of the same voxel replaces the earlier one; a column edit lowers the ground first.
            Assert.IsTrue(world.Deltas.SetVoxel(x, top + 2, z, Voxel.Air, 3));
            Assert.IsTrue(world.Deltas.SetColumnSurface(x, z, top - 5, 4));
            Assert.AreEqual(top - 5, world.SurfaceAt(x, z));
            Assert.AreEqual(3, world.Deltas.Count, "surface delta + two distinct voxel deltas");

            // Raising a column fills with its top material.
            int x2 = x + 3;
            int top2 = generator.Voxels(x2, z).TopSolid();
            Assert.IsTrue(world.Deltas.SetColumnSurface(x2, z, top2 + 4, 5));
            Assert.AreEqual(top2 + 4, world.SurfaceAt(x2, z));
            Assert.IsTrue(Voxel.IsSolid(world.VoxelAt(x2, top2 + 2, z)));
            Assert.IsFalse(world.Deltas.SetVoxel(x, 5000, z, stone, 6), "out of the vertical range");
        }
    }
}
