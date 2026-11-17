using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CubeGame.Core.Characters;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Features.World.Foundation;
using CubeGame.Gameplay;
using CubeGame.Gameplay.Streaming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// G2-1 runtime: a CubeGame content world generates its own vegetation, settlement anchors, dungeon prototype and
    /// structure state through the new foundation systems, and the state survives a streaming unload/reload.
    /// </summary>
    public sealed class G2_1ContentBindingPlayModeTests
    {
        private string _saveDir;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "cubegame_g21_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            foreach (string name in new[] { "Main Camera", "Sun", "WorldRoot", "Player" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null) Object.Destroy(leftover);
            }

            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator CubeGameContentIsGeneratedAndStateSurvivesStreaming()
        {
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot0", "Content", 666666u, "Walker", CharacterClass.Ranger),
                game.Status);
            Assert.AreEqual(AlphaTerrainGenerator.CubeGameContentRuntimeId, game.Session.World.GeneratorId);
            yield return Frames(5);

            Assert.IsNotNull(game.ContentRuntime, "the content runtime must exist in a CubeGame world");
            Assert.IsTrue(game.ContentRuntime.IsCubeGameWorld);
            Assert.AreEqual(5, game.EntityDefinitions.Count, "the CubeGame entity catalogue must be registered");
            Assert.IsTrue(game.EntityDefinitions.TryGet("cubegame.npc.merchant", out EntityDefinition merchant));
            Assert.AreEqual(VisualSourceKind.TemporaryAlpha, merchant.Visual.Source,
                "G2-1 uses the temporary Alpha visual placeholder");

            game.WorldView.LoadAround(game.PlayerTransform.position, 2);
            yield return Frames(3);

            // 1. Vegetation belongs to the CubeGame policy and is rendered (temporary Alpha models).
            var terrain = (AlphaTerrainGenerator)game.WorldState.Generator;
            var startZone = WorldLayout.ZoneOf((int)game.Session.Hero.Position.X,
                (int)game.Session.Hero.Position.Z);
            Assert.IsTrue(terrain.TryGetGeneratedZone(startZone.X, startZone.Z, out AlphaZone zone),
                "the start zone must be generated");
            Assert.Greater(zone.Vegetation.Count, 0, "CubeGame vegetation must exist in the start zone");
            Assert.Greater(game.WorldView.LoadedPlants, 0, "plants must reach the rendered chunks");
            Assert.IsNotEmpty(zone.BiomeId);
            string firstPlantId = zone.Vegetation[0].PersistentId;

            // 2. Settlement prototype with anchors spawned through the entity registry.
            Assert.IsTrue(game.ContentRuntime.TryFindSettlement(game.Session.Hero.Position.X,
                game.Session.Hero.Position.Z, 2, out SettlementBlueprint settlement, out ZoneKey settlementZone),
                "a settlement must be reserved within two regions of the start");
            game.TeleportHero(settlement.CenterX, settlement.CenterZ);

            int anchors = 0;
            for (int i = 0; i < 40 && anchors == 0; i++)
            {
                yield return null;
                if (game.Session.Hero == null) break;
                anchors = game.Session == null
                    ? 0
                    : CountAnchors(game);
            }

            Assert.Greater(anchors, 0, "settlement anchors must spawn as definable entities");
            Assert.IsTrue(terrain.TryGetGeneratedZone(settlementZone.X, settlementZone.Z, out AlphaZone town),
                "the settlement zone must be generated");
            Assert.IsNotNull(town.Settlement);
            Assert.AreEqual(settlement.PersistentId, town.Settlement.PersistentId);
            Assert.Greater(town.Settlement.Slots.Count, 0);
            Assert.Greater(town.Settlement.RoadCells.Count, 0);

            // 3. Dungeon prototype: enter the test room, leave through the same entrance.
            Assert.IsTrue(game.ContentRuntime.TryFindDungeon(game.Session.Hero.Position.X,
                game.Session.Hero.Position.Z, 3, out DungeonPrototype dungeon, out ZoneKey dungeonZone),
                "a dungeon entrance must be reserved within three zones");
            Assert.IsTrue(game.ContentRuntime.EnterDungeon(dungeon, out double ex, out double ey, out double ez));
            game.TeleportHeroToPosition(ex, ey, ez);
            yield return Frames(3);
            Assert.IsTrue(dungeon.Contains(game.Session.Hero.Position.X, game.Session.Hero.Position.Y,
                game.Session.Hero.Position.Z), "the hero must stand inside the dungeon room");
            Assert.AreEqual(dungeon.InstanceId, game.ContentRuntime.ActiveDungeonInstanceId);

            Assert.IsTrue(game.ContentRuntime.ExitDungeon(dungeon, out double rx, out double ry, out double rz));
            game.TeleportHeroToPosition(rx, ry, rz);
            yield return Frames(3);
            Assert.AreEqual(string.Empty, game.ContentRuntime.ActiveDungeonInstanceId);
            Assert.Less(System.Math.Abs(game.Session.Hero.Position.X - (dungeon.ReturnX + 0.5)), 4.0);
            Assert.Less(System.Math.Abs(game.Session.Hero.Position.Z - (dungeon.ReturnZ + 0.5)), 4.0);

            // 4. Destroy a persistent structure and a plant; the delta is the state.
            ContentStructureState structure = null;
            if (game.ContentRuntime.TryNearestStructure(game.Session.Hero.Position.X,
                    game.Session.Hero.Position.Z, 1, out ContentStructureState nearest) && nearest.SpawnIndex >= 0)
            {
                structure = nearest;
            }
            else
            {
                foreach (ContentStructureState state in game.ContentRuntime.StructuresOfZone(settlementZone, true))
                {
                    if (state.SpawnIndex >= 0)
                    {
                        structure = state;
                        break;
                    }
                }
            }

            Assert.IsNotNull(structure, "a destroyable structure must exist near the settlement");
            Assert.IsTrue(game.ContentRuntime.DestroyStructure(structure, game.Session.Clock.TimeMs));
            var structureZone = new ZoneKey(structure.ZoneX, structure.ZoneZ);
            Assert.GreaterOrEqual(game.ContentRuntime.RemovedStructuresIn(structureZone), 1);

            // A plant is removed by identity: use the one the start zone generated (known to exist).
            VegetationPlacement removedPlant = zone.Vegetation[0];
            Assert.IsTrue(game.ContentRuntime.RemoveVegetation(startZone, removedPlant,
                game.Session.Clock.TimeMs));
            var plantZone = startZone;
            Assert.GreaterOrEqual(game.ContentRuntime.RemovedVegetationIn(plantZone), 1);

            // 5. Streaming unload/reload: leave the area, come back, the same identities and the same removal state.
            game.TeleportHero(settlement.CenterX + 6 * 256, settlement.CenterZ + 6 * 256);
            yield return new WaitForSeconds(1.5f);
            game.TeleportHero(settlement.CenterX, settlement.CenterZ);
            yield return Frames(5);

            Assert.IsTrue(terrain.TryGetGeneratedZone(startZone.X, startZone.Z, out AlphaZone reloaded),
                "the start zone must regenerate after unload");
            Assert.AreEqual(firstPlantId, reloaded.Vegetation[0].PersistentId,
                "the same seed must regenerate the same vegetation identity");
            Assert.GreaterOrEqual(game.ContentRuntime.RemovedStructuresIn(structureZone), 1,
                "a destroyed structure must stay destroyed after unload/reload");
            Assert.GreaterOrEqual(game.ContentRuntime.RemovedVegetationIn(plantZone), 1,
                "a removed plant must stay removed after unload/reload");

            // 6. Debug snapshot shows the content systems.
            WorldDebugSnapshot snapshot = game.WorldFoundationSnapshot();
            StringAssert.Contains("content zone(", snapshot.Text);
            CollectionAssert.IsEmpty(game.WorldFoundationProblems.ToArray(),
                string.Join("; ", game.WorldFoundationProblems));
            Assert.Greater(zone.VegetationCandidates, zone.Vegetation.Count);
        }

        private static int CountAnchors(GameBootstrap game)
        {
            int count = 0;
            if (game.Session == null) return count;
            var population = game.Population;
            if (population == null) return count;
            foreach (LiveSpawn live in population.Live)
            {
                if (CubeGameContentIds.IsAnchor(live.Definition.PersistentObjectId)) count++;
            }

            return count;
        }
    }
}
