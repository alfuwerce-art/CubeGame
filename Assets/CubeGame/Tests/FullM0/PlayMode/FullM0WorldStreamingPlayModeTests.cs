using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CubeGame.Core.Characters;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Gameplay;
using CubeGame.Gameplay.Streaming;
using CubeGame.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// M0-02 in the running game: Alpha terrain (voxel scale) at the centre of the 2^24-voxel world, hero on the
    /// generated ground, chunks streaming in/out while travelling, floating origin keeping scene coordinates small,
    /// a terrain change surviving save → menu → load.
    /// </summary>
    public sealed class FullM0WorldStreamingPlayModeTests
    {
        private string _saveDir;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "cubegame_fullm0_world_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.Destroy(_host);
            }

            foreach (string name in new[] { "Main Camera", "Sun", "WorldRoot", "Player" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null)
                {
                    Object.Destroy(leftover);
                }
            }

            if (Directory.Exists(_saveDir))
            {
                Directory.Delete(_saveDir, true);
            }
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null;
            }
        }

        private GameBootstrap NewGame()
        {
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            return game;
        }

        [UnityTest]
        public IEnumerator HeroStartsOnAlphaTerrainAndChunksStreamWithFloatingOrigin()
        {
            GameBootstrap game = NewGame();
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot0", "Stream", 20131234u, "Walker", CharacterClass.Ranger), game.Status);
            Assert.AreEqual(AlphaTerrainGenerator.WorldRuntimeId, game.Session.World.GeneratorId);
            WorldPosition expectedStart = WorldState.StartPosition(game.WorldState.Generator, game.WorldState.Deltas);
            Assert.AreEqual(expectedStart.X, game.Session.Hero.Position.X, 1e-6);
            int sx = (int)game.Session.Hero.Position.X;
            int sz = (int)game.Session.Hero.Position.Z;
            Assert.AreEqual(1, game.WorldState.Generator.Region(new RegionKey(512, 512)).Level, "start = centre region (Alpha level 1)");

            Transform player = game.PlayerTransform;
            Assert.Less(player.position.magnitude, 600f, "scene coordinates stay small (floating origin)");

            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!motor.IsGrounded && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(motor.IsGrounded, "hero stands on generated Alpha terrain");
            int ground = game.WorldState.SurfaceAt(sx, sz);
            Assert.Less(Mathf.Abs(player.position.y - (ground + 1)), 1.6f, $"hero y {player.position.y} vs column top {ground + 1}");

            game.WorldView.CompletePending();
            ChunkKey focus = game.WorldView.Center;
            Assert.Greater(game.WorldView.LoadedPlants, 0, "AlphaContent vegetation was installed with the chunks");
            Assert.AreEqual(0, game.WorldView.PendingCount, "eligible chunks finish streaming");
            Assert.IsTrue(game.WorldView.IsLoaded(new ChunkKey(focus.X - 18, focus.Z)));
            Assert.IsTrue(game.WorldView.IsLoaded(new ChunkKey(focus.X + 18, focus.Z)));

            // Travel 40 chunks (1280 voxels) east: the origin rebases, far chunks are released and zones recorded.
            Vector2Int originBefore = game.WorldView.Origin;
            ChunkKey startChunk = game.WorldView.Center;
            int targetX = sx + 40 * WorldLayout.ChunkSize;
            int targetGround = Mathf.Max(game.WorldState.SurfaceAt(targetX, sz), 0);
            player.position = game.WorldView.ToScene(targetX + 0.5, targetGround + 3.0, sz + 0.5);
            Physics.SyncTransforms();
            yield return Frames(2);
            game.WorldView.CompletePending();
            yield return Frames(2);

            Assert.AreNotEqual(originBefore, game.WorldView.Origin, "floating origin moved");
            Assert.Less(player.position.magnitude, 600f);
            Assert.AreEqual(targetX + 0.5, game.Session.Hero.Position.X, 0.6, "world position preserved across rebase");
            Assert.IsTrue(game.WorldView.IsLoaded(WorldLayout.ChunkOf(targetX, sz)));
            Assert.IsFalse(game.WorldView.IsLoaded(startChunk), "start chunk released beyond keep radius");
            Assert.LessOrEqual(game.WorldView.LoadedCount, 40 * 40);
            Assert.Greater(game.WorldView.TotalReleased, 0);
            Assert.IsTrue(game.Session.WorldDeltas.IsDiscovered(new ZoneKey(sx >> 8, sz >> 8)));
            Assert.IsTrue(game.Session.WorldDeltas.IsDiscovered(new ZoneKey(targetX >> 8, sz >> 8)));

            // The target column exists in the rebased scene at its generated height (terrain colliders only).
            int targetSurface = game.WorldState.SurfaceAt(targetX, sz);
            Vector3 above = game.WorldView.ToScene(targetX + 0.5, targetSurface + 60.0, sz + 0.5);
            float terrainHit = float.NaN;
            foreach (RaycastHit h in Physics.RaycastAll(above, Vector3.down, 400f))
            {
                if (h.collider.name.StartsWith("Chunk_") && (float.IsNaN(terrainHit) || h.point.y > terrainHit))
                {
                    terrainHit = h.point.y;
                }
            }

            Assert.AreEqual(targetSurface + 1, terrainHit, 0.01f);
            Assert.IsTrue(game.ExitToMenu(false));
        }

        [UnityTest]
        public IEnumerator PlantSupportEditSurvivesChunkReloadAndSaveLoad()
        {
            GameBootstrap game = NewGame();
            yield return null;
            Assert.IsTrue(game.StartNewWorld("slot0", "Vegetation", 666666u, "Walker", CharacterClass.Ranger), game.Status);
            StreamingWorldView view = game.WorldView;
            ChunkKey center = view.Center;
            AlphaPlantPlacement plant = default;
            ChunkKey chosen = default;
            bool found = false;
            for (int x = -2; x <= 2 && !found; x++)
            {
                for (int z = -2; z <= 2 && !found; z++)
                {
                    var chunk = new ChunkKey(center.X + x, center.Z + z);
                    var placements = ChunkVoxels.Sample(game.WorldState.Generator, chunk).Plants;
                    if (placements.Count == 0) continue;
                    plant = placements[0];
                    chosen = chunk;
                    found = true;
                }
            }
            Assert.IsTrue(found, "a loaded starting chunk owns a generated plant");
            string root = $"Chunk_{chosen.X}_{chosen.Z}";
            GameObject plants = GameObject.Find(root + "/Vegetation");
            Assert.IsNotNull(plants);
            int originalVertices = plants.GetComponent<MeshFilter>().sharedMesh.vertexCount;
            Vector3 local = plants.transform.localPosition;
            Vector3 scene = plants.transform.position;
            Vector2Int origin = view.Origin;
            Vector3 shift = view.Rebase(origin + new Vector2Int(512, 0));
            Assert.AreEqual(-512f, shift.x);
            Assert.AreEqual(local, plants.transform.localPosition);
            Assert.AreEqual(scene + shift, plants.transform.position);
            view.Rebase(origin);
            Assert.IsTrue(game.WorldState.Deltas.SetVoxel(plant.X, plant.Y - 1, plant.Z, Voxel.Air, game.Session.Clock.TimeMs));
            view.SetFocus(game.PlayerTransform.position);
            GameObject edited = GameObject.Find(root + "/Vegetation");
            int editedVertices = edited != null ? edited.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0;
            Assert.Less(editedVertices, originalVertices);

            view.SetFocus(view.ToScene(plant.X + 1280, plant.Y + 2, plant.Z));
            Assert.IsFalse(view.IsLoaded(chosen));
            view.LoadAround(view.ToScene(plant.X + 0.5, plant.Y + 2, plant.Z + 0.5), 0);
            Assert.IsTrue(view.IsLoaded(chosen));
            GameObject reloaded = GameObject.Find(root + "/Vegetation");
            Assert.AreEqual(editedVertices, reloaded != null ? reloaded.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0);

            Assert.IsTrue(game.ExitToMenu(true), game.Status);
            yield return null;
            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            Assert.IsTrue(game.WorldState.Deltas.TryGetVoxel(plant.X, plant.Y - 1, plant.Z, out uint saved));
            Assert.AreEqual(Voxel.Air, saved);
            Assert.IsTrue(game.WorldView.IsLoaded(chosen));
            GameObject savedPlants = GameObject.Find(root + "/Vegetation");
            Assert.AreEqual(editedVertices, savedPlants != null ? savedPlants.GetComponent<MeshFilter>().sharedMesh.vertexCount : 0);
            Assert.IsTrue(game.ExitToMenu(false));
        }

        [UnityTest]
        public IEnumerator EditedColliderGetsTheNextBuildEvenWithReadyChunks()
        {
            _host = new GameObject("WorldStreamingRegression");
            var deltas = new WorldDeltaStore();
            var world = new WorldState(new FlatGenerator(), deltas);
            var view = _host.AddComponent<StreamingWorldView>();
            int x = WorldLayout.StartVoxel + 5;
            int z = WorldLayout.StartVoxel + 5;
            view.Initialise(world, new WorldStreamingPolicy(1, 1), null, StreamingWorldView.OriginFor(x, z));
            Vector3 focus = view.ToScene(x + 0.5, 10, z + 0.5);
            view.BuildsPerFrame = 0;
            view.LoadAround(focus, 0);

            // Hold the budget at zero until new chunks demonstrably compete for the next build.
            float deadline = Time.realtimeSinceStartup + 10f;
            while (view.ReadyCount == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.Greater(view.ReadyCount, 0, "new chunk builds must be ready before the edit");
            Assert.AreEqual(1, view.LoadedCount);

            Assert.IsTrue(deltas.SetColumnSurface(x, z, 1, 0));
            view.BuildsPerFrame = 1;
            view.SetFocus(focus);
            Physics.SyncTransforms();

            Assert.AreEqual(1, view.LoadedCount, "the only build was spent on the edited chunk");
            Assert.IsTrue(Physics.Raycast(focus, Vector3.down, out RaycastHit hit, 20f));
            Assert.AreEqual(2f, hit.point.y, 0.01f, "the previous collider is already gone in this frame");
        }

        private sealed class FlatGenerator : IWorldGenerator
        {
            private readonly VoxelColumn _column = new VoxelColumn();

            public FlatGenerator() => _column.SetSurface(4);

            public string GeneratorId => "test.flat";
            public uint MasterSeed => 0;
            public int WaterLevel => 0;
            public TerrainColumn Column(int x, int z) => new TerrainColumn(4, false, 0f, 4f);
            public RegionInfo Region(RegionKey region) => default;
            public VoxelColumn Voxels(int x, int z) => _column;
        }

        [UnityTest]
        public IEnumerator CityAndDungeonContentStreamThroughPilotAndPreserveWorldDeltas()
        {
            GameBootstrap game = NewGame();
            yield return null;
            Assert.IsTrue(game.StartNewWorld("slot0", "World content", 666666u, "Walker", CharacterClass.Ranger), game.Status);
            var terrain = (AlphaTerrainGenerator)game.WorldState.Generator;
            CollectionAssert.AreEqual(new[] { AlphaZoneContentProvider.ProviderKey, AlphaCityContentProvider.ProviderKey,
                AlphaDungeonContentProvider.ProviderKey }, terrain.ContentProviders.Select(p => p.Key));

            const int regionIndex = WorldLayout.StartVoxel / WorldLayout.RegionSize;
            AlphaRegion region = terrain.RegionRecord(regionIndex, regionIndex);
            ZoneKey city = ZoneWithRecord(region, regionIndex, AlphaZoneRecord.Town);
            game.TeleportHero(city.X * WorldLayout.ZoneSize + 128, city.Z * WorldLayout.ZoneSize + 128);
            yield return null;
            LiveSpawn[] services = game.Population.Live.Where(s => s.Zone.Equals(city)
                && s.Definition.BehaviourKey == AlphaCityContentProvider.ServiceBehaviourKey).ToArray();
            Assert.IsNotEmpty(services, "the city provider reaches the scene");
            foreach (LiveSpawn service in services)
            {
                Assert.IsTrue(game.Visuals.TryGetRoot(service.Id, out Transform visual));
                Assert.IsFalse(game.Session.Combat.TryGet(service.Id, out _));
                StringAssert.DoesNotStartWith("Entity_", visual.name, "service IDs are separate from creature kinds");
            }

            ZoneKey dungeon = ZoneWithRecord(region, regionIndex, AlphaZoneRecord.Type3);
            int dungeonX = dungeon.X * WorldLayout.ZoneSize + 128;
            int dungeonZ = dungeon.Z * WorldLayout.ZoneSize + 128;
            game.TeleportHero(dungeonX, dungeonZ);
            yield return null;

            LiveSpawn[] chests = DungeonChests(game, dungeon);
            Assert.AreEqual(3, chests.Length);
            foreach (LiveSpawn chest in chests)
            {
                Assert.AreEqual(StableEntityId.ForZoneSpawn(dungeon, D3ZonePopulation.Channel, chest.Definition.SpawnIndex), chest.Id);
                Assert.IsTrue(game.Visuals.TryGetRoot(chest.Id, out _));
            }

            LiveSpawn removed = chests[0];
            WorldPosition anchor = removed.Definition.Position;
            int x = (int)anchor.X;
            int y = (int)anchor.Y;
            int z = (int)anchor.Z;
            Assert.IsFalse(Voxel.IsSolid(game.WorldState.VoxelAt(x, y, z)), "the chest is inside the carved room");
            uint stone = Voxel.Pack(87, 93, 105, 6);
            Assert.IsTrue(game.EditVoxel(x, y, z, stone), game.Status);
            game.Session.WorldDeltas.Record(new WorldDelta
            {
                Region = removed.Definition.Region,
                Kind = WorldDeltaKind.ObjectRemoved,
                X = dungeon.X,
                Y = removed.Channel * 4096 + removed.Definition.SpawnIndex,
                Z = dungeon.Z,
                Value = 1,
                TimeMs = game.Session.Clock.TimeMs,
            });

            game.TeleportHero(dungeonX + 4 * WorldLayout.ZoneSize, dungeonZ);
            yield return null;
            Assert.IsEmpty(DungeonChests(game, dungeon));
            Assert.IsFalse(game.Visuals.TryGetRoot(removed.Id, out _));

            game.TeleportHero(dungeonX, dungeonZ);
            yield return null;
            AssertChestsRestored(game, dungeon, chests.Skip(1).ToArray());
            Assert.AreEqual(stone, game.WorldState.VoxelAt(x, y, z), "streaming preserves edits inside the dungeon");

            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(game.ExitToMenu(false));
            yield return null;
            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            AssertChestsRestored(game, dungeon, chests.Skip(1).ToArray());
            Assert.AreEqual(stone, game.WorldState.VoxelAt(x, y, z), "loading applies edits on top of the generated dungeon");
            Assert.IsTrue(game.ExitToMenu(false));
        }

        private static ZoneKey ZoneWithRecord(AlphaRegion region, int regionIndex, int recordType)
        {
            int index = System.Array.FindIndex(region.Zones, r => r.Type == recordType);
            Assert.GreaterOrEqual(index, 0, $"fixture seed generates record type {recordType}");
            return new ZoneKey(regionIndex * 64 + index / 64, regionIndex * 64 + index % 64);
        }

        private static LiveSpawn[] DungeonChests(GameBootstrap game, ZoneKey zone) => game.Population.Live
            .Where(s => s.Zone.Equals(zone) && s.Definition.BehaviourKey == AlphaDungeonContentProvider.ChestBehaviourKey)
            .OrderBy(s => s.Definition.SpawnIndex).ToArray();

        private static void AssertChestsRestored(GameBootstrap game, ZoneKey zone, LiveSpawn[] expected)
        {
            LiveSpawn[] restored = DungeonChests(game, zone);
            CollectionAssert.AreEqual(expected.Select(s => s.Id), restored.Select(s => s.Id));
            CollectionAssert.AreEqual(expected.Select(s => s.Definition.Position), restored.Select(s => s.Definition.Position));
            foreach (LiveSpawn chest in restored)
            {
                Assert.IsTrue(game.Visuals.TryGetRoot(chest.Id, out _));
            }
        }

        [UnityTest]
        public IEnumerator RecoveredPopulationUsesGameClockAndPersistsRespawnThroughStreaming()
        {
            GameBootstrap game = NewGame();
            yield return null;
            Assert.IsTrue(game.StartNewWorld("slot0", "Recovered population", 666666u, "Walker", CharacterClass.Warrior), game.Status);
            AlphaSpawnRuntime runtime = game.RecoveredPopulation;
            Assert.IsNotNull(runtime);
            AlphaSpawnTemplate target = runtime.Templates.FirstOrDefault(t => t.Live
                && t.Definition.Faction == AlphaWildSpawnRules.FactionHostile);
            Assert.IsNotNull(target, "a recovered wild template near the hero became live");
            ZoneKey home = target.Zone;
            int startX = home.X * WorldLayout.ZoneSize + 128;
            int startZ = home.Z * WorldLayout.ZoneSize + 128;
            game.TeleportHero(startX, startZ);
            yield return null;
            Assert.AreEqual(AlphaRecoveredPopulation.RecoveredKindProvenance, target.Definition.Provenance);
            Assert.IsTrue(game.Visuals.TryGetRoot(target.Id, out _));
            KillSpawn(game, target);
            Assert.AreEqual(AlphaSpawnLifecycle.RespawnTimerMs, target.RespawnTimerMs);
            game.Session.Clock.StepTicks(3);
            Assert.That(target.RespawnTimerMs, Is.LessThan(AlphaSpawnLifecycle.RespawnTimerMs));

            game.TeleportHero(startX + 4 * WorldLayout.ZoneSize, startZ);
            yield return null;
            Assert.IsFalse(runtime.IsLoaded(home));
            Assert.IsFalse(game.Visuals.TryGetRoot(target.Id, out _), "the corpse releases with its zone");
            int parkedTimer = runtime.SnapshotStates().Single(s => s.Id.Equals(target.Id)).RespawnTimerMs;
            game.Session.Clock.StepTicks(3);
            Assert.AreEqual(parkedTimer, runtime.SnapshotStates().Single(s => s.Id.Equals(target.Id)).RespawnTimerMs);

            int npcX = startX + 40;
            var npcHome = new WorldPosition(npcX, game.WorldState.SurfaceAt(npcX, startZ) + 1, startZ);
            runtime.AddSource(new ScheduledNpcFixture(home, npcHome));
            game.TeleportHero(startX, startZ);
            yield return Frames(2);
            game.Session.Clock.StepTicks(2);
            Assert.IsTrue(runtime.TryGet(target.Id, out AlphaSpawnTemplate returned));
            Assert.IsFalse(returned.Live);
            Assert.That(returned.RespawnTimerMs, Is.GreaterThan(0).And.LessThanOrEqualTo(parkedTimer));

            StableEntityId npcId = StableEntityId.ForZoneSpawn(home, AlphaSpawnRuntime.CreatureChannel, ScheduledNpcFixture.Index);
            Assert.IsTrue(game.Creatures.TryGetAi(npcId, out var npc));
            Assert.AreEqual(AlphaNpcMovementMode.Schedule, npc.AlphaMovement.Mode);
            Assert.AreEqual(game.Session.Clock.WorldTimeMs, npc.AlphaMovement.LastWorldTimeMs);
            Assert.AreEqual(1, npc.AlphaMovement.Schedule.TargetIndex);

            int checkpointTimer = returned.RespawnTimerMs;
            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(game.ExitToMenu(false));
            yield return null;
            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            runtime = game.RecoveredPopulation;
            Assert.IsTrue(runtime.TryGet(target.Id, out AlphaSpawnTemplate loaded));
            Assert.IsFalse(loaded.Live, "load preserves the death on the same day");
            Assert.AreEqual(checkpointTimer, loaded.RespawnTimerMs);

            runtime.RestoreStates(new[] { new AlphaSpawnTemplateState { Id = target.Id, RespawnTimerMs = 1, DayOfDeath = loaded.DayOfDeath } });
            game.Session.Clock.StepTicks(1);
            Assert.IsTrue(loaded.Live, "expiry permits recreation in the same day");
            KillSpawn(game, loaded);

            const int realSecondsUntilDayOneAt02 = 17 * 360;
            const int realSecondsUntilDayOneAt09 = 24 * 360;
            game.Session.Clock.Restore(realSecondsUntilDayOneAt02 * game.Session.Clock.TicksPerSecond);
            game.Session.Clock.StepTicks(1);
            Assert.IsFalse(loaded.Live, "the hostile spawn window is closed at 02:00");
            game.Session.Clock.Restore(realSecondsUntilDayOneAt09 * game.Session.Clock.TicksPerSecond);
            game.Session.Clock.StepTicks(1);
            Assert.IsTrue(loaded.Live, "the next day permits respawn even with time left on the timer");
            Assert.AreEqual(2, loaded.Generation);
            Assert.IsTrue(game.Visuals.TryGetRoot(target.Id, out _));
            Assert.IsTrue(game.Session.Combat.TryGet(target.Id, out _));
            Assert.IsEmpty(game.Session.Events.HandlerErrors);
            Assert.IsTrue(game.ExitToMenu(false));
        }

        private static void KillSpawn(GameBootstrap game, AlphaSpawnTemplate target)
        {
            Assert.IsTrue(game.Session.Combat.TryGet(target.Id, out var combatant));
            combatant.Health.ApplyDamage(combatant.Health.CurrentHealth - 0.01);
            for (int strike = 0; strike < 30 && target.Live; strike++)
            {
                game.Session.Combat.ResolveHit(game.Session.Hero.Id, target.Id, game.Session.Clock.TimeMs, strike);
            }
            Assert.IsFalse(target.Live, "combat death reached the recovered spawn runtime");
            Assert.IsFalse(game.Session.Combat.TryGet(target.Id, out _));
        }

        private sealed class ScheduledNpcFixture : IAlphaSpawnTemplateSource
        {
            public const int Index = 0xFFE;
            private readonly ZoneKey _zone;
            private readonly AlphaSpawnTemplateData _template;

            public ScheduledNpcFixture(ZoneKey zone, WorldPosition position)
            {
                _zone = zone;
                var spawn = new SpawnDefinition
                {
                    Region = new RegionKey(zone.X >> 6, zone.Z >> 6),
                    SpawnIndex = Index,
                    Kind = new KindId(0),
                    Position = position,
                    Faction = (int)CreatureFaction.Npc,
                    BehaviourKey = AlphaRecoveredPopulation.BehaviourKey,
                };
                _template = new AlphaSpawnTemplateData(spawn, 0, 0, schedule: new[]
                {
                    new AlphaScheduleEntry(position, 0),
                    new AlphaScheduleEntry(new WorldPosition(position.X + 8, position.Y, position.Z), 8 * 3_600_000),
                });
            }

            public IReadOnlyList<AlphaSpawnTemplateData> TemplatesForZone(ZoneKey zone) =>
                zone.Equals(_zone) ? new[] { _template } : System.Array.Empty<AlphaSpawnTemplateData>();
        }

        [UnityTest]
        public IEnumerator TerrainEditSurvivesSaveExitAndLoad()
        {
            GameBootstrap game = NewGame();
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot2", "Edit", 99u, "Digger", CharacterClass.Warrior), game.Status);
            int x = (int)game.Session.Hero.Position.X + 5;
            int z = (int)game.Session.Hero.Position.Z + 5;
            int original = game.WorldState.SurfaceAt(x, z);
            int edited = original - 6;

            Assert.IsTrue(game.EditColumn(x, z, edited), game.Status);
            Assert.AreEqual(edited, game.WorldState.SurfaceAt(x, z));
            yield return Frames(10);

            Vector3 above = game.WorldView.ToScene(x + 0.5, original + 30, z + 0.5);
            Assert.IsTrue(Physics.Raycast(above, Vector3.down, out RaycastHit hit, 200f));
            Assert.AreEqual(edited + 1, hit.point.y, 0.01f, "collider follows the world delta");

            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(File.Exists(Path.Combine(_saveDir, "slot2.world.sav")));
            Assert.IsTrue(game.ExitToMenu(false));
            yield return Frames(2);

            Assert.IsTrue(game.LoadWorld("slot2"), game.Status);
            Assert.AreEqual(edited, game.WorldState.SurfaceAt(x, z), "edit restored from the world file");
            yield return Frames(2);
            above = game.WorldView.ToScene(x + 0.5, original + 30, z + 0.5);
            Assert.IsTrue(Physics.Raycast(above, Vector3.down, out hit, 200f));
            Assert.AreEqual(edited + 1, hit.point.y, 0.01f);
            Assert.IsTrue(game.ExitToMenu(false));
        }

        [UnityTest]
        public IEnumerator VoxelEditBuildsColliderAndSurvivesSaveLoad()
        {
            GameBootstrap game = NewGame();
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot0", "Voxel", 99u, "Builder", CharacterClass.Warrior), game.Status);
            int x = (int)game.Session.Hero.Position.X + 7;
            int z = (int)game.Session.Hero.Position.Z - 4;
            int ground = game.WorldState.SurfaceAt(x, z);
            uint stone = Voxel.Pack(90, 90, 90, 6);

            // A floating block two voxels above the ground: the mesher must give it a top face (collider) of its own.
            Assert.IsTrue(game.EditVoxel(x, ground + 3, z, stone), game.Status);
            Assert.AreEqual(ground + 3, game.WorldState.SurfaceAt(x, z));
            yield return Frames(10);

            Vector3 above = game.WorldView.ToScene(x + 0.5, ground + 30, z + 0.5);
            Assert.IsTrue(Physics.Raycast(above, Vector3.down, out RaycastHit hit, 200f));
            Assert.AreEqual(ground + 4, hit.point.y, 0.01f, "collider on the placed voxel");

            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(game.ExitToMenu(false));
            yield return Frames(2);
            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            Assert.AreEqual(stone, game.WorldState.VoxelAt(x, ground + 3, z), "voxel restored from the world file");
            Assert.AreEqual(Voxel.Air, game.WorldState.VoxelAt(x, ground + 2, z));
            yield return Frames(2);
            above = game.WorldView.ToScene(x + 0.5, ground + 30, z + 0.5);
            Assert.IsTrue(Physics.Raycast(above, Vector3.down, out hit, 200f));
            Assert.AreEqual(ground + 4, hit.point.y, 0.01f);
            Assert.IsTrue(game.ExitToMenu(false));
        }
    }
}
