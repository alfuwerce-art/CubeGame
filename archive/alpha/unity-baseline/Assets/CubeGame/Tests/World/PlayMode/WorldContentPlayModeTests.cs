// D86 M0-09A (partial) — the DeepSeek content systems run together in one PlayMode scenario:
// structure placement, dungeon skeleton, NPC service data, creature+decor spawning with Alpha visuals,
// ticking, and stable identity across despawn/respawn. This is not the full game loop (Claude owns the
// scene/session); it proves the content pipeline is wired, not isolated demos.
using System.Collections;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Features.AI;
using CubeGame.Features.Creatures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.World.PlayMode
{
    public class WorldContentPlayModeTests
    {
        sealed class Target : MonoBehaviour, ICreatureTargetSource
        {
            public string TargetId => "player";
            public int Faction => (int)CreatureFaction.Player;
            public bool Alive => true;
            public Vector3 Position => transform.position;
        }

        [UnityTest]
        public IEnumerator WorldContentPipelineRunsEndToEnd()
        {
            var source = AlphaContentSource.Resolve();
            if (source.State != AlphaContentState.Ready) Assert.Ignore(source.Diagnosis);
            var root = new GameObject("WorldContentRoot");
            var targetGo = new GameObject("PlayerTarget");
            var target = targetGo.AddComponent<Target>();
            CreatureTargets.Register(target);
            var problems = new List<string>();
            try
            {
                Assert.IsEmpty(AlphaNpcServices.Validate(), string.Join("\n", AlphaNpcServices.Validate()));
                Assert.GreaterOrEqual(AlphaNpcServices.All().Count, 8, "town service furniture is available to the interaction layer");

                var placements = AlphaStructurePlacementPlanner.Plan(2024u, 0, 0, 0, 0,
                    new[] { AlphaStructureTemplates.HouseId }, (x, z, w, d) => true, problems);
                Assert.AreEqual(1, placements.Count, string.Join("\n", problems));
                Assert.AreEqual(3, placements[0].Width);
                Assert.AreEqual(4, placements[0].Height);

                var dungeon = AlphaDungeonLayoutPlanner.Plan(2024u, 5, 0, 0, problems);
                Assert.AreEqual(5, dungeon.Rooms.Count);
                Assert.AreEqual(4, dungeon.Corridors.Count);

                var spawnerGo = new GameObject("Spawner");
                spawnerGo.transform.SetParent(root.transform, false);
                var spawner = spawnerGo.AddComponent<CreatureSpawner>();
                spawner.SpawnOnStart = false;
                spawner.Seed = 2024u;
                spawner.MaxPerCell = 6;
                spawner.DecorPerCell = 6;
                spawner.AiFactory = () => new CreatureAi();
                spawner.ActorLibrary = new AlphaActorLibrary(source);
                var plan = spawner.Spawn();
                Assert.IsNotEmpty(plan, spawner.Status);
                Assert.GreaterOrEqual(spawner.SpawnedCount, 1);
                Assert.IsTrue(plan.Exists(p => p.Faction == (int)CreatureFaction.Passive), "decor slots are part of the plan");

                targetGo.transform.position = spawner.transform.position + new Vector3(2f, 0f, 0f);
                for (int i = 0; i < 60; i++) spawner.TickAll(1f / 30f);
                bool posed = false;
                foreach (var agent in spawner.Spawned.Values)
                    if (agent.View != null && agent.View.Ready) posed = true;
                Assert.IsTrue(posed, "spawned content builds real Alpha visuals");

                int removed = spawner.DespawnBeyond(new Vector3(1000f, 0f, 1000f));
                Assert.GreaterOrEqual(removed, 1);
                var first = plan.Find(p => spawner.Store.Contains(p.StableId));
                Assert.IsNotNull(first, "despawned content keeps its state blob");
                var again = spawner.SpawnOne(first, spawner.ActorLibrary);
                Assert.AreEqual(first.StableId, again.State.StableId);
                Assert.AreEqual(first.Kind, again.State.Kind);
                yield return null;
            }
            finally
            {
                CreatureTargets.Clear();
                Object.Destroy(root);
                Object.Destroy(targetGo);
            }
        }
    }
}
