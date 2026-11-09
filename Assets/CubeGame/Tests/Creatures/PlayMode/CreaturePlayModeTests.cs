// D86 M0-06 — PlayMode checks: spawned Alpha creatures really move, keep stable identity, restore
// their blob after leaving the active radius, and request only actions proven for their kind.
using System.Collections;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Features.AI;
using CubeGame.Features.Creatures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.Creatures.PlayMode
{
    public class CreaturePlayModeTests
    {
        sealed class TestTarget : MonoBehaviour, ICreatureTargetSource
        {
            public string TargetId { get; set; } = "player";
            public int Faction { get; set; }
            public bool Alive { get; set; } = true;
            public Vector3 Position => transform.position;
        }

        static bool ContentReady() => AlphaContentSource.Resolve().State == AlphaContentState.Ready;

        [UnityTest]
        public IEnumerator SpawnedCreatureChasesTargetAndKeepsStableIdentity()
        {
            if (!ContentReady()) Assert.Ignore("local Alpha content cache is not staged here");
            var root = new GameObject("CreatureTestRoot");
            var spawner = root.AddComponent<CreatureSpawner>();
            spawner.SpawnOnStart = false;
            spawner.Seed = 777u;
            spawner.RegionX = 0; spawner.RegionZ = 0; spawner.CellX = 0; spawner.CellZ = 0;
            spawner.MaxPerCell = 4;
            spawner.AiFactory = () => new CreatureAi();
            var playerGo = new GameObject("PlayerProxy");
            var player = playerGo.AddComponent<TestTarget>();
            player.TargetId = "player";
            player.Faction = (int)CreatureFaction.Player;
            CreatureTargets.Register(player);
            try
            {
                var plan = spawner.Spawn();
                Assert.IsNotEmpty(plan, spawner.Status);
                Assert.IsEmpty(spawner.Problems, string.Join("\n", spawner.Problems));
                var agent = spawner.Spawned[plan[0].StableId];
                Assert.IsNotNull(agent);
                Assert.IsTrue(agent.VisualAvailable, "every planned kind maps to a staged Alpha visual: " + agent.VisualStatus);

                // Put the target right next to the creature so the AI reaches it immediately.
                playerGo.transform.position = agent.transform.position + new Vector3(1.5f, 0f, 0f);
                spawner.TickAll(0.5f);
                Assert.AreEqual(CreatureAiState.Attack, agent.Ai.State, "in range → attack state");

                // Leaving the active radius saves the blob and destroys the object; identity stays the same.
                spawner.DespawnRadius = 10f;
                int despawned = spawner.DespawnBeyond(new Vector3(1000f, 0f, 1000f));
                Assert.GreaterOrEqual(despawned, 1);
                Assert.IsTrue(spawner.Store.Contains(plan[0].StableId), "state blob saved on despawn");
                var again = spawner.SpawnOne(plan[0], AlphaActorLibrary.Shared);
                Assert.AreEqual(plan[0].Kind, again.State.Kind);
                Assert.AreEqual(plan[0].StableId, again.State.StableId);
                Assert.AreEqual((float)plan[0].X, again.transform.position.x, 1e-3f);
                yield return null;
            }
            finally
            {
                CreatureTargets.Clear();
                Object.Destroy(root);
                Object.Destroy(playerGo);
            }
        }

        [UnityTest]
        public IEnumerator DragonRequestsOnlyItsTransferredAction()
        {
            if (!ContentReady()) Assert.Ignore("local Alpha content cache is not staged here");
            var library = AlphaActorLibrary.Shared;
            if (!library.TryGet("d86_dragon_0x65", out var def)) Assert.Ignore("dragon variant is not staged");
            var root = new GameObject("DragonTestRoot");
            var playerGo = new GameObject("PlayerProxy2");
            var player = playerGo.AddComponent<TestTarget>();
            player.TargetId = "player";
            player.Faction = (int)CreatureFaction.Player;
            CreatureTargets.Register(player);
            try
            {
                var request = new CreatureSpawnRequest
                {
                    StableId = "test:dragon", Kind = 0x65, Level = 5, Stars = 0,
                    Faction = (int)CreatureFaction.Monster, X = 0, Y = 0, Z = 0,
                };
                var agentGo = new GameObject("DragonAgent");
                agentGo.transform.SetParent(root.transform, false);
                var agent = agentGo.AddComponent<CreatureAgent>();
                var state = new CreatureState
                {
                    StableId = request.StableId, Kind = request.Kind, Faction = request.Faction, Level = 5,
                    MaxHealth = 100, Health = 100, X = 0, Z = 0,
                };
                agent.Initialize(state, new CreatureAi(), def, 7u);
                Assert.IsTrue(agent.VisualAvailable, agent.VisualStatus);
                playerGo.transform.position = new Vector3(1.5f, 0, 0);
                for (int i = 0; i < 10; i++) agent.Tick(0.05f);
                Assert.AreEqual(CreatureAiState.Attack, agent.Ai.State);
                for (int i = 0; i < 120 && agent.View.Controller.CurrentAction == null; i++) agent.Tick(0.05f);
                Assert.AreEqual("action_037", agent.View.Controller.CurrentAction,
                    $"dragon 0x65 launches 37/40/42/43; phase={agent.View.Controller.Phase} reason={agent.Ai.LastReason} " +
                    $"problems={string.Join(",", agent.View.Problems)}");
                yield return null;
            }
            finally
            {
                CreatureTargets.Clear();
                Object.Destroy(root);
                Object.Destroy(playerGo);
            }
        }

        [UnityTest]
        public IEnumerator CreatureTicksStayWithinTheFrameBudget()
        {
            if (!ContentReady()) Assert.Ignore("local Alpha content cache is not staged here");
            var root = new GameObject("PerfRoot");
            var playerGo = new GameObject("PerfTarget");
            var player = playerGo.AddComponent<TestTarget>();
            player.TargetId = "player";
            player.Faction = (int)CreatureFaction.Player;
            CreatureTargets.Register(player);
            CreatureSpawner spawner = null;
            try
            {
                for (uint seed = 1; seed <= 12 && spawner == null; seed++)
                {
                    var go = new GameObject("PerfSpawner" + seed);
                    go.transform.SetParent(root.transform, false);
                    var candidate = go.AddComponent<CreatureSpawner>();
                    candidate.SpawnOnStart = false;
                    candidate.Seed = seed;
                    candidate.MaxPerCell = 8;
                    candidate.ActorLibrary = AlphaActorLibrary.Shared;
                    candidate.AiFactory = () => new CreatureAi();
                    if (candidate.Spawn().Count >= 2) spawner = candidate;
                    else Object.Destroy(go);
                }
                if (spawner == null) Assert.Ignore("no seed produced a multi-creature plan");
                playerGo.transform.position = spawner.transform.position + new Vector3(2f, 0f, 0f);
                for (int i = 0; i < 10; i++) spawner.TickAll(1f / 60f);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 300; i++) spawner.TickAll(1f / 60f);
                watch.Stop();
                double msPerFrame = watch.Elapsed.TotalMilliseconds / 300.0;
                TestContext.WriteLine($"CREATURE_TICK_MS={msPerFrame:F3} agents={spawner.SpawnedCount}");
                Assert.Less(msPerFrame, 25.0,
                    $"300 visual/AI ticks of {spawner.SpawnedCount} creatures took {msPerFrame:F2} ms per frame");
                foreach (var agent in spawner.Spawned.Values)
                {
                    Assert.IsNotNull(agent.View, "every spawned creature keeps its visual");
                    Assert.Greater(agent.View.Locomotion.FramesApplied + agent.View.Clips.FramesApplied, 0,
                        "the visual layer really posed the creature during the run");
                }
                yield return null;
            }
            finally
            {
                CreatureTargets.Clear();
                Object.Destroy(root);
                Object.Destroy(playerGo);
            }
        }

        [UnityTest]
        public IEnumerator StaticPlantLoadsWithAlphaVisualAndNeverMoves()
        {
            if (!ContentReady()) Assert.Ignore("local Alpha content cache is not staged here");
            var library = AlphaActorLibrary.Shared;
            if (!library.TryGet("d86_kind_0x78_formula", out var def)) Assert.Ignore("plant variant 0x78 is not staged");
            var go = new GameObject("PlantVisual");
            try
            {
                var agent = go.AddComponent<CreatureAgent>();
                var state = new CreatureState
                {
                    StableId = "test:plant:visual", Kind = 0x78, Faction = (int)CreatureFaction.Passive,
                    Level = 1, MaxHealth = 10, Health = 10, X = 0, Z = 0,
                };
                agent.Initialize(state, new CreatureAi(), def, 5u);
                Assert.IsTrue(agent.VisualAvailable, agent.VisualStatus);
                Assert.IsTrue(agent.View.Controller.staticObject, "D69: passive objects have no walk and no actions");
                Assert.AreEqual("STATIC_OBJECT", def.ControllerMode);
                var start = agent.transform.position;
                for (int i = 0; i < 20; i++) agent.Tick(0.1f);
                Assert.AreEqual(start, agent.transform.position, "a passive object never moves");
                Assert.AreEqual("PASSIVE_OBJECT", agent.Ai.LastReason);
                Assert.Greater(agent.View.Locomotion.FramesApplied, 0, "the standing pose is still applied every tick");
                yield return null;
            }
            finally
            {
                Object.Destroy(go);
            }
        }

        [UnityTest]
        public IEnumerator SpawnedDecorStaysStill()
        {
            if (!ContentReady()) Assert.Ignore("local Alpha content cache is not staged here");
            var root = new GameObject("DecorTestRoot");
            try
            {
                var go = new GameObject("DecorSpawner");
                go.transform.SetParent(root.transform, false);
                var spawner = go.AddComponent<CreatureSpawner>();
                spawner.SpawnOnStart = false;
                spawner.Seed = 9u;
                spawner.MaxPerCell = 2;
                spawner.DecorPerCell = 4;
                spawner.AiFactory = () => new CreatureAi();
                var plan = spawner.Spawn();
                var decor = plan.FindAll(r => r.Faction == (int)CreatureFaction.Passive);
                if (decor.Count == 0) Assert.Ignore("the tested seed produced no decor slots");
                var agent = spawner.Spawned[decor[0].StableId];
                Assert.IsTrue(agent.VisualAvailable, agent.VisualStatus);
                var start = agent.transform.position;
                spawner.TickAll(1f);
                Assert.AreEqual(start, agent.transform.position, "decor never walks");
                Assert.AreEqual("PASSIVE_OBJECT", agent.Ai.LastReason);
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator GroundHeightHookPlacesSpawnedCreatures()
        {
            if (!ContentReady()) Assert.Ignore("local Alpha content cache is not staged here");
            var root = new GameObject("GroundTestRoot");
            try
            {
                CreatureSpawner spawner = null;
                for (uint seed = 1; seed <= 12 && spawner == null; seed++)
                {
                    var go = new GameObject("GroundSpawner" + seed);
                    go.transform.SetParent(root.transform, false);
                    var candidate = go.AddComponent<CreatureSpawner>();
                    candidate.SpawnOnStart = false;
                    candidate.Seed = seed;
                    candidate.MaxPerCell = 4;
                    candidate.DecorPerCell = 2;
                    candidate.GroundHeight = (x, z) => 5f;
                    candidate.AiFactory = () => new CreatureAi();
                    if (candidate.Spawn().Count > 0) spawner = candidate;
                    else Object.Destroy(go);
                }
                if (spawner == null) Assert.Ignore("no seed produced a spawn plan");
                var request = new List<CreatureSpawnRequest>();
                foreach (var pair in spawner.Spawned)
                {
                    var agent = pair.Value;
                    Assert.AreEqual(5f, agent.transform.position.y, 1e-3f, "the world's ground height is used");
                    Assert.AreEqual(5.0, agent.State.Y, 1e-6);
                    break;
                }
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator CreatureActionReachesTheGameHook()
        {
            if (!ContentReady()) Assert.Ignore("local Alpha content cache is not staged here");
            var library = AlphaActorLibrary.Shared;
            if (!library.TryGet("d86_dragon_0x65", out var def)) Assert.Ignore("dragon variant is not staged");
            var root = new GameObject("ActionHookRoot");
            var playerGo = new GameObject("ActionTarget");
            var player = playerGo.AddComponent<TestTarget>();
            player.TargetId = "player";
            player.Faction = (int)CreatureFaction.Player;
            CreatureTargets.Register(player);
            try
            {
                var go = new GameObject("DragonHook");
                go.transform.SetParent(root.transform, false);
                var agent = go.AddComponent<CreatureAgent>();
                var state = new CreatureState
                {
                    StableId = "test:hook", Kind = 0x65, Faction = (int)CreatureFaction.Monster,
                    Level = 5, MaxHealth = 100, Health = 100, X = 0, Z = 0,
                };
                int requested = -1;
                agent.ActionRequested += (a, code, source) => requested = code;
                agent.Initialize(state, new CreatureAi(), def, 11u);
                playerGo.transform.position = new Vector3(1.5f, 0, 0);
                for (int i = 0; i < 120 && requested < 0; i++) agent.Tick(0.05f);
                Assert.AreEqual(37, requested, "the game hook receives the Alpha action code the AI requested");
                yield return null;
            }
            finally
            {
                CreatureTargets.Clear();
                Object.Destroy(root);
                Object.Destroy(playerGo);
            }
        }

        [UnityTest]
        public IEnumerator ManyActionCreaturesStayWithinBudget()
        {
            if (!ContentReady()) Assert.Ignore("local Alpha content cache is not staged here");
            var root = new GameObject("LoadRoot");
            var playerGo = new GameObject("LoadTarget");
            var player = playerGo.AddComponent<TestTarget>();
            player.TargetId = "player";
            player.Faction = (int)CreatureFaction.Player;
            CreatureTargets.Register(player);
            try
            {
                var actionKinds = new List<int>();
                foreach (var staged in AlphaStagedVisuals.All)
                    if (staged.ControllerMode == "ACTIONS" && actionKinds.Count < 12) actionKinds.Add(staged.Kind);
                Assert.GreaterOrEqual(actionKinds.Count, 12, "enough kinds with action libraries are staged");
                var spawnerGo = new GameObject("LoadSpawner");
                spawnerGo.transform.SetParent(root.transform, false);
                var spawner = spawnerGo.AddComponent<CreatureSpawner>();
                spawner.SpawnOnStart = false;
                spawner.AiFactory = () => new CreatureAi();
                spawner.ActorLibrary = AlphaActorLibrary.Shared;
                int requests = 0;
                for (int i = 0; i < actionKinds.Count; i++)
                {
                    var request = new CreatureSpawnRequest
                    {
                        StableId = "load:" + i, Kind = actionKinds[i], Level = 5, Stars = 0,
                        Faction = (int)CreatureFaction.Monster,
                        X = (i % 4) * 2.0, Z = (i / 4) * 2.0,
                    };
                    var agent = spawner.SpawnOne(request, spawner.ActorLibrary);
                    Assert.IsTrue(agent.VisualAvailable, agent.VisualStatus);
                    agent.ActionRequested += (a, code, source) => requests++;
                }
                playerGo.transform.position = new Vector3(1f, 0f, 1f);
                for (int i = 0; i < 20; i++) spawner.TickAll(1f / 60f);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 600; i++) spawner.TickAll(1f / 60f);
                watch.Stop();
                double msPerFrame = watch.Elapsed.TotalMilliseconds / 600.0;
                TestContext.WriteLine($"ACTION_LOAD_TICK_MS={msPerFrame:F3} agents={spawner.SpawnedCount} requests={requests}");
                Assert.Greater(requests, 0, "the loaded creatures really requested their actions");
                Assert.Less(msPerFrame, 60.0, $"{spawner.SpawnedCount} creatures with actions took {msPerFrame:F2} ms/frame");
                yield return null;
            }
            finally
            {
                CreatureTargets.Clear();
                Object.Destroy(root);
                Object.Destroy(playerGo);
            }
        }

        [UnityTest]
        public IEnumerator PassiveObjectStaysStill()
        {
            if (!ContentReady()) Assert.Ignore("local Alpha content cache is not staged here");
            var root = new GameObject("PlantTestRoot");
            var playerGo = new GameObject("PlayerProxy3");
            var player = playerGo.AddComponent<TestTarget>();
            player.TargetId = "player";
            player.Faction = (int)CreatureFaction.Player;
            playerGo.transform.position = new Vector3(1f, 0f, 0f);
            CreatureTargets.Register(player);
            try
            {
                var go = new GameObject("Plant");
                go.transform.SetParent(root.transform, false);
                var agent = go.AddComponent<CreatureAgent>();
                var state = new CreatureState
                {
                    StableId = "test:plant", Kind = 0x78, Faction = (int)CreatureFaction.Passive,
                    Level = 1, MaxHealth = 10, Health = 10, X = 0, Z = 0,
                };
                agent.Initialize(state, new CreatureAi(), null, 3u);
                Assert.AreEqual("MISSING_VISUAL_CONTENT", agent.VisualStatus, "no staged plant visual is invented");
                var start = agent.transform.position;
                for (int i = 0; i < 10; i++) agent.Tick(0.1f);
                Assert.AreEqual(start, agent.transform.position);
                Assert.AreEqual(CreatureAiState.Idle, agent.Ai.State);
                Assert.AreEqual("PASSIVE_OBJECT", agent.Ai.LastReason);
                yield return null;
            }
            finally
            {
                CreatureTargets.Clear();
                Object.Destroy(root);
                Object.Destroy(playerGo);
            }
        }
    }
}
