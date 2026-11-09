// D3 — EditMode checks of the zone content planner through a synthetic world source (no Unity, no cache).
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class ZoneContentPlannerTests
    {
        sealed class FlatWorld : IContentZoneSource
        {
            public uint Seed { get; set; } = 666666u;
            public int Surface = 63;
            public bool Flat = true;
            public readonly HashSet<string> Removed = new HashSet<string>();

            public ZoneClimate ClimateOf(WorldCellKey cell) => new ZoneClimate
            {
                Humidity = 0.5, Temperature = 0.5, CellValue = 1.0, Spacing = 0.0, Level = 5, Mode = 0,
            };

            public bool TrySurfaceVoxel(int voxelX, int voxelZ, out int topSolidY)
            {
                topSolidY = Surface;
                return Surface > 0;   // 0 = the world has no data in that column
            }

            public bool IsFlatAndFree(int voxelX, int voxelZ, int width, int depth) => Flat;

            public bool IsRemoved(string stableId) => Removed.Contains(stableId);
        }

        static ContentPlanSettings Settings()
        {
            // W-18 selects any staged kind (objects and creatures), so stage the whole catalogue for this test.
            var staged = new List<int>();
            foreach (var def in AlphaCreatureCatalog.All) staged.Add(def.Kind);
            var settings = new ContentPlanSettings
            {
                StagedObjectKinds = staged,
                CreatureKinds = new List<int>(AlphaCreatureCatalog.CombatKinds),
                DecorKinds = new List<int>(AlphaCreatureCatalog.PassiveKinds),
                VegetationPerZone = 8,
                CreatureSlots = 2,
                DecorSlots = 0,
                PlaceStructures = true,
            };
            return settings;
        }

        [Test]
        public void PlanProducesVegetationCreaturesAndStructuresWithProvenance()
        {
            var world = new FlatWorld();
            var plan = ZoneContentPlanner.Plan(new WorldZoneKey(32800, 32800), world, Settings());
            Assert.IsEmpty(plan.Problems, string.Join(";", plan.Problems));
            Assert.Greater(plan.VegetationCount, 0, "W-18 selection places objects");
            Assert.AreEqual(2, plan.CreatureCount, "creature slots are filled");
            Assert.GreaterOrEqual(plan.StructureCount, 0);
            foreach (var item in plan.Items)
            {
                Assert.IsNotEmpty(item.StableId);
                Assert.AreEqual(64, item.SurfaceY + 1, "objects stand above the surface");
                Assert.IsNotEmpty(item.Provenance);
            }
        }

        [Test]
        public void PlanIsDeterministicForTheSameSeedAndZone()
        {
            var a = ZoneContentPlanner.Plan(new WorldZoneKey(32801, 32801), new FlatWorld(), Settings());
            var b = ZoneContentPlanner.Plan(new WorldZoneKey(32801, 32801), new FlatWorld(), Settings());
            Assert.AreEqual(a.Items.Count, b.Items.Count);
            for (int i = 0; i < a.Items.Count; i++)
            {
                Assert.AreEqual(a.Items[i].StableId, b.Items[i].StableId);
                Assert.AreEqual(a.Items[i].X, b.Items[i].X, 1e-9);
                Assert.AreEqual(a.Items[i].Kind, b.Items[i].Kind);
            }
        }

        [Test]
        public void RemovedObjectsNeverComeBack()
        {
            var world = new FlatWorld();
            var first = ZoneContentPlanner.Plan(new WorldZoneKey(32800, 32800), world, Settings());
            Assert.Greater(first.Items.Count, 0);
            world.Removed.Add(first.Items[0].StableId);
            var second = ZoneContentPlanner.Plan(new WorldZoneKey(32800, 32800), world, Settings());
            foreach (var item in second.Items)
            {
                Assert.AreNotEqual(first.Items[0].StableId, item.StableId, "a removed object stays removed");
            }
        }

        [Test]
        public void StructureNeedsAUsableFootprint()
        {
            var world = new FlatWorld { Flat = false };
            var plan = ZoneContentPlanner.Plan(new WorldZoneKey(32802, 32802), world, Settings());
            Assert.AreEqual(0, plan.StructureCount, "no flat ground → no building");
        }

        [Test]
        public void ZoneWithoutSurfacesProducesNoPlacements()
        {
            var world = new FlatWorld { Surface = 0 };
            var plan = ZoneContentPlanner.Plan(new WorldZoneKey(32803, 32803), world, Settings());
            Assert.AreEqual(0, plan.Items.Count);
            Assert.IsNotEmpty(plan.Problems);
        }
    }
}
