// D3 — EditMode checks of the zone content stage (placements → SpawnDefinition) and of its settings.
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaZoneContentProviderTests
    {
        static readonly WorldZoneKey Zone = new WorldZoneKey(32800, 32800);

        static ContentPlacement Placement(ContentPlacementKind kind, int species, int faction = 0)
        {
            return new ContentPlacement
            {
                StableId = "veg:1",
                Kind = kind,
                Species = species,
                Zone = Zone,
                X = 8396800.0,
                Z = 8396800.0,
                SurfaceY = 63,
                Faction = faction,
                Level = 4,
                Provenance = AlphaVegetationPlanner.Status,
            };
        }

        [Test]
        public void PassiveObjectBecomesStaticObjectSpawn()
        {
            int passiveKind = AlphaCreatureCatalog.PassiveKinds[0];
            var spawn = AlphaZoneContentProvider.ToSpawn(Placement(ContentPlacementKind.Vegetation, passiveKind), Zone, 0);
            Assert.AreEqual(AlphaZoneContentProvider.PassiveBehaviourKey, spawn.BehaviourKey);
            Assert.AreEqual(CreatureFaction.Passive, (CreatureFaction)spawn.Faction);
            Assert.AreEqual(passiveKind, spawn.Kind.Value);
            Assert.AreEqual(64.0, spawn.Position.Y, "the object stands on the voxel above the surface");
            Assert.AreEqual(-1, spawn.ClassId);
        }

        [Test]
        public void CreatureKeepsItsBehaviourKeyAndFaction()
        {
            int kind = AlphaCreatureCatalog.CombatKinds[0];
            var spawn = AlphaZoneContentProvider.ToSpawn(
                Placement(ContentPlacementKind.Creature, kind, (int)CreatureFaction.Monster), Zone, 3);
            Assert.AreEqual(AlphaZoneContentProvider.CreatureBehaviourKey, spawn.BehaviourKey);
            Assert.AreEqual((int)CreatureFaction.Monster, spawn.Faction);
            Assert.AreEqual(3, spawn.SpawnIndex);
            Assert.AreEqual(4, spawn.Level);
        }

        [Test]
        public void StructureGetsItsOwnBehaviourKeyAndHonestProvenance()
        {
            var spawn = AlphaZoneContentProvider.ToSpawn(Placement(ContentPlacementKind.Structure, 0), Zone, 5);
            Assert.AreEqual(AlphaZoneContentProvider.StructureBehaviourKey, spawn.BehaviourKey);
            StringAssert.Contains("PENDING", spawn.Provenance);
            StringAssert.Contains("CUBEGAME_CONTRACT", spawn.Provenance);
        }

        [Test]
        public void StructureGeometryOriginAndDoorComeFromThePlacement()
        {
            var placement = Placement(ContentPlacementKind.Structure, 0);
            placement.X = 8396810.0;
            placement.Z = 8396820.0;
            placement.SurfaceY = 63;
            placement.Width = 3;
            placement.Depth = 3;
            placement.Height = 4;
            placement.Yaw = 180f;
            var origin = AlphaZoneContentProvider.HouseOrigin(placement);
            Assert.AreEqual(8396808, origin.X, "origin is floor(centre - width/2)");
            Assert.AreEqual(8396818, origin.Z);
            Assert.AreEqual(64, origin.Y, "the house stands on the voxel above the surface");
            Assert.AreEqual(2, AlphaZoneContentProvider.DoorSide(placement), "180° → +Z door");
            placement.Yaw = -90f;
            Assert.AreEqual(3, AlphaZoneContentProvider.DoorSide(placement), "negative rotation is normalised");
        }

        [Test]
        public void RegionAndPositionFollowTheZone()
        {
            var spawn = AlphaZoneContentProvider.ToSpawn(Placement(ContentPlacementKind.Vegetation, 0x78), Zone, 1);
            Assert.AreEqual(new RegionKey(32800 / AlphaWorldScale.ZonesPerRegion, 32800 / AlphaWorldScale.ZonesPerRegion), spawn.Region);
            Assert.AreEqual(8396800.0, spawn.Position.X);
            Assert.AreEqual(8396800.0, spawn.Position.Z);
        }

        [Test]
        public void SpawnIndexMustFitTheStableId()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                AlphaZoneContentProvider.ToSpawn(Placement(ContentPlacementKind.Vegetation, 0x78), Zone, 0x1000));
        }

        [Test]
        public void SettingsFromLibrarySplitPassiveObjectsFromCreatures()
        {
            var library = CubeGame.AlphaModels.AlphaActorLibrary.Shared;
            var settings = ContentPlanSettings.FromLibrary(library);
            Assert.IsNotEmpty(settings.StagedObjectKinds, "staged passive objects feed the W-18 selection");
            Assert.IsNotEmpty(settings.CreatureKinds, "staged combat kinds feed the creature slots");
            foreach (int kind in settings.StagedObjectKinds)
            {
                Assert.IsTrue(CreatureKinds.IsPassiveObject(kind));
            }
        }
    }
}
