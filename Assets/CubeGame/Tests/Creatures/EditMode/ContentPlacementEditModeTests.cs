// D86 M0-02/M0-06/M0-07 — EditMode checks for the content placement layer:
// the executed Alpha world scale, zone-scoped deterministic planning, surface-anchored placement and
// saved-removal handling. No Unity scene is needed; the world is supplied by a synthetic source.
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class ContentPlacementEditModeTests
    {
        /// <summary>Flat world at a fixed height with optional removal list; the test's stand-in for the generator.</summary>
        sealed class FlatSource : IContentZoneSource
        {
            public uint Seed { get; set; } = 2024u;
            public int SurfaceY = 40;
            public bool HasSurface = true;
            public bool FootprintFree = true;
            public ZoneClimate Climate = new ZoneClimate
            {
                Humidity = 0.5, Temperature = 0.5, CellValue = 0.0, Spacing = 0.5, Level = 6, CellType = 0xB,
            };
            public readonly HashSet<string> RemovedIds = new HashSet<string>();

            public ZoneClimate ClimateOf(WorldCellKey cell) => Climate;

            public bool TrySurfaceVoxel(int voxelX, int voxelZ, out int topSolidY)
            {
                topSolidY = HasSurface ? SurfaceY : 0;
                return HasSurface;
            }

            public bool IsFlatAndFree(int voxelX, int voxelZ, int width, int depth) => FootprintFree;

            public bool IsRemoved(string stableId) => RemovedIds.Contains(stableId);
        }

        static ContentPlanSettings Settings()
        {
            return new ContentPlanSettings
            {
                StagedObjectKinds = new List<int> { 0x6c, 0x77, 0x23, 0x24, 0x25, 0xc, 0x10 },
                CreatureKinds = new List<int> { 0x00, 0x1e },
                DecorKinds = new List<int> { 0x15 },
                StructureTemplates = new List<string> { AlphaStructureTemplates.HouseId },
                VegetationPerZone = 4,
                CreatureSlots = 4,
                DecorSlots = 2,
                PlaceStructures = true,
            };
        }

        [Test]
        public void WorldScaleMatchesExecutedAlphaLayout()
        {
            Assert.IsEmpty(AlphaWorldScale.Validate(), string.Join("\n", AlphaWorldScale.Validate()));
            Assert.AreEqual(256, AlphaWorldScale.ZoneVoxels);
            Assert.AreEqual(64, AlphaWorldScale.ZonesPerRegion);
            Assert.AreEqual(16384, AlphaWorldScale.RegionVoxels);
            Assert.AreEqual(2048, AlphaWorldScale.CellVoxels);
            Assert.AreEqual(16777216, AlphaWorldScale.WorldVoxels);
            // The generated table counts zones, not voxels — using 64 as a voxel size was the defect.
            Assert.AreEqual(AlphaWorldScale.ZonesPerRegion, AlphaContentTables.RegionBlocks,
                "RegionBlocks is zones per region");
            Assert.AreEqual(AlphaWorldScale.ZonesPerAxis, AlphaContentTables.WorldBlocks,
                "WorldBlocks is zones per world axis");
        }

        [Test]
        public void StartZoneSitsInTheCentreRegion()
        {
            var zone = new WorldZoneKey(AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ);
            Assert.IsTrue(AlphaWorldScale.IsInsideWorld(zone));
            var region = AlphaWorldScale.RegionOf(zone);
            Assert.AreEqual(512, region.X);
            Assert.AreEqual(512, region.Z);
            Assert.AreEqual(512, AlphaWorldScale.RegionsPerAxis / 2);
            Assert.AreEqual(32800 * 256 + 128, AlphaWorldScale.StartVoxelX);
            Assert.AreEqual(AlphaWorldScale.StartZoneX, AlphaWorldScale.ZoneOfVoxel(AlphaWorldScale.StartVoxelX));
            Assert.AreEqual(8396928, AlphaWorldScale.StartVoxelX);
            // Cell of the start zone: 32800 / 8 = 4100, i.e. 8 396 800 … 8 398 848 voxels.
            var cell = AlphaWorldScale.CellOf(zone);
            Assert.AreEqual(4100, cell.X);
            Assert.AreEqual(AlphaWorldScale.StartZoneX * 256, AlphaWorldScale.CellOriginVoxel(cell.X));
        }

        [Test]
        public void AbsolutePositionsUseDoubleAndStayRelativeToTheOrigin()
        {
            // A point 8.4 M voxels from the world origin must survive: float would snap to ~0.5 voxel.
            var point = new WorldVoxelPoint(8396928.25, 64.0, 8396928.75);
            Assert.AreEqual(8396928.25, point.X, 1e-9);
            Assert.AreEqual(0.25f, point.LocalX(8396928.0), 1e-6f);
            Assert.AreEqual(0.75f, point.LocalZ(8396928.0), 1e-6f);
            Assert.Less(point.LocalX(8396928.0), 256f);
        }

        [Test]
        public void CellAndZonePlansKeepTheExecutedScale()
        {
            var source = new FlatSource();
            var plan = ZoneContentPlanner.Plan(new WorldZoneKey(0, 0), source, Settings());
            int origin = 0;
            foreach (var item in plan.Items)
            {
                Assert.GreaterOrEqual(item.X, origin, item.StableId);
                Assert.Less(item.X, origin + AlphaWorldScale.ZoneVoxels, item.StableId);
                Assert.GreaterOrEqual(item.Z, origin);
                Assert.Less(item.Z, origin + AlphaWorldScale.ZoneVoxels);
            }

            // Cell-level planners: a cell is 2 048 voxels wide, a region 16 384.
            var vegetation = AlphaVegetationPlanner.Plan(1u, 0, 0, 1, 1, 0.5, 0.5, 0.0, 0.5, 6, 0,
                new List<int> { 0x15, 0x2e, 0x2f });
            Assert.IsNotEmpty(vegetation);
            foreach (var placement in vegetation)
            {
                Assert.GreaterOrEqual(placement.X, AlphaWorldScale.CellVoxels);
                Assert.Less(placement.X, 2 * AlphaWorldScale.CellVoxels);
            }

            var structures = AlphaStructurePlacementPlanner.Plan(1u, 0, 0, 1, 1,
                new List<string> { AlphaStructureTemplates.HouseId }, (x, z, w, d) => true);
            Assert.IsNotEmpty(structures);
            Assert.GreaterOrEqual(structures[0].X, AlphaWorldScale.CellVoxels);
            Assert.Less(structures[0].X, 2 * AlphaWorldScale.CellVoxels);

            var creatures = CreatureSpawnPlanner.Plan(1u, 0, 0, 1, 1, 8, new List<int> { 0x00 });
            foreach (var request in creatures)
            {
                Assert.GreaterOrEqual(request.X, AlphaWorldScale.CellVoxels);
                Assert.Less(request.X, 2 * AlphaWorldScale.CellVoxels);
            }
        }

        [Test]
        public void ZonePlanIsDeterministicAndUnique()
        {
            var source = new FlatSource();
            var zone = new WorldZoneKey(AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ);
            var first = ZoneContentPlanner.Plan(zone, source, Settings());
            var second = ZoneContentPlanner.Plan(zone, source, Settings());

            Assert.IsNotEmpty(first.Items, first.Describe());
            Assert.AreEqual(first.Items.Count, second.Items.Count, first.Describe());
            var ids = new HashSet<string>();
            for (int i = 0; i < first.Items.Count; i++)
            {
                Assert.AreEqual(first.Items[i].StableId, second.Items[i].StableId);
                Assert.AreEqual(first.Items[i].X, second.Items[i].X, 1e-9);
                Assert.AreEqual(first.Items[i].Z, second.Items[i].Z, 1e-9);
                Assert.IsTrue(ids.Add(first.Items[i].StableId), "duplicate id " + first.Items[i].StableId);
            }
            Assert.IsFalse(first.Problems.Exists(p => p.StartsWith("CONTENT_DUPLICATE_ID")), string.Join("\n", first.Problems));
        }

        [Test]
        public void NeighbouringZonesDoNotShareIds()
        {
            var source = new FlatSource();
            var centre = new WorldZoneKey(AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ);
            var seen = new HashSet<string>();
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var zone = new WorldZoneKey(centre.X + dx, centre.Z + dz);
                var plan = ZoneContentPlanner.Plan(zone, source, Settings());
                foreach (var item in plan.Items)
                {
                    Assert.IsTrue(seen.Add(item.StableId), $"id {item.StableId} appears in two zones");
                    Assert.AreEqual(zone, item.Zone);
                }
            }
            Assert.GreaterOrEqual(seen.Count, 9, "every zone contributes at least one object");
        }

        [Test]
        public void PlacementsStandOnTheReportedSurface()
        {
            var source = new FlatSource { SurfaceY = 123 };
            var plan = ZoneContentPlanner.Plan(new WorldZoneKey(AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ),
                source, Settings());
            Assert.IsNotEmpty(plan.Items);
            foreach (var item in plan.Items)
            {
                Assert.AreEqual(123, item.SurfaceY, item.StableId);
                Assert.AreEqual(124.0, item.Anchor.Y, 1e-9, item.StableId);
            }
        }

        [Test]
        public void ZoneWithoutSurfaceProducesNoPlacement()
        {
            var source = new FlatSource { HasSurface = false };
            var plan = ZoneContentPlanner.Plan(new WorldZoneKey(0, 0), source, Settings());
            Assert.IsEmpty(plan.Items, "no invented heights");
            Assert.That(plan.Problems, Has.Some.Contains("CONTENT_NO_SURFACE"));
        }

        [Test]
        public void RemovedObjectsAreNotPlannedAgain()
        {
            var source = new FlatSource();
            var zone = new WorldZoneKey(AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ);
            var first = ZoneContentPlanner.Plan(zone, source, Settings());
            Assert.IsNotEmpty(first.Items);
            var removedId = first.Items[0].StableId;
            source.RemovedIds.Add(removedId);

            var second = ZoneContentPlanner.Plan(zone, source, Settings());
            Assert.IsFalse(second.Items.Exists(i => i.StableId == removedId), "removed content must not return");
            Assert.That(second.Problems, Has.Some.Contains("CONTENT_REMOVED " + removedId));
        }

        [Test]
        public void StructureFootprintAsksTheWorld()
        {
            var blocked = new FlatSource { FootprintFree = false };
            var plan = ZoneContentPlanner.Plan(new WorldZoneKey(AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ),
                blocked, Settings());
            Assert.AreEqual(0, plan.StructureCount, "no building is forced onto an unusable site");
            Assert.That(plan.Problems, Has.Some.Contains("STRUCTURE_NO_FREE_FOOTPRINT"));

            var free = new FlatSource();
            var placed = ZoneContentPlanner.Plan(new WorldZoneKey(AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ),
                free, Settings());
            Assert.AreEqual(1, placed.StructureCount);
            var house = placed.Items.Find(i => i.Kind == ContentPlacementKind.Structure);
            Assert.AreEqual(3, house.Width, "W-17 evidence: house is 3x3x4");
            Assert.AreEqual(4, house.Height);
            Assert.AreEqual(AlphaStructureTemplates.HouseId, house.TemplateId);
            Assert.That(house.StableId, Does.StartWith("structure:z:"));
        }

        [Test]
        public void VegetationUsesAlphaTreeSelectionAndPassiveFaction()
        {
            var source = new FlatSource();
            var plan = ZoneContentPlanner.Plan(new WorldZoneKey(0, 0), source, Settings());
            var vegetation = plan.Items.FindAll(i => i.Kind == ContentPlacementKind.Vegetation);
            Assert.IsNotEmpty(vegetation, plan.Describe());
            foreach (var item in vegetation)
            {
                Assert.That(item.Provenance, Does.Contain("ALPHA_DECOMPILED"));
                Assert.AreEqual((int)CreatureFaction.Passive, item.Faction, item.StableId);
            }
        }

        [Test]
        public void DelegateSourceFailsLoudlyWithoutClimate()
        {
            var source = new DelegateContentZoneSource { Seed = 1u };
            Assert.Throws<System.InvalidOperationException>(() => source.ClimateOf(new WorldCellKey(0, 0)));
            Assert.IsFalse(source.TrySurfaceVoxel(0, 0, out _));
            Assert.IsTrue(source.IsFlatAndFree(0, 0, 3, 3));
            Assert.IsFalse(source.IsRemoved("x"));
        }

        [Test]
        public void CreaturePlanZoneKeepsIdsAndBounds()
        {
            var plan = CreatureSpawnPlanner.PlanZone(3u, AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ, 8,
                new List<int> { 0x00, 0x1e, 0x3f });
            Assert.IsNotEmpty(plan);
            int origin = AlphaWorldScale.ZoneOriginVoxel(AlphaWorldScale.StartZoneX);
            foreach (var request in plan)
            {
                Assert.That(request.StableId, Does.StartWith("spawnz:32800:32800:"));
                Assert.GreaterOrEqual(request.X, origin);
                Assert.Less(request.X, origin + AlphaWorldScale.ZoneVoxels);
                Assert.GreaterOrEqual(request.Z, origin);
                Assert.Less(request.Z, origin + AlphaWorldScale.ZoneVoxels);
            }
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SpawnTable.ZoneSpawnId(-1, 0, 0));
        }
    }
}
