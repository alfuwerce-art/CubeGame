// D86 M0-02/M0-06/M0-07 — PlayMode proof of the runtime content placement:
// real GameObjects on the reported surface, deterministic ids, unload/reload without duplicates,
// saved removals, NPC state across unloads, scene stability over repeated streaming, floating origin.
// The world is the synthetic tall-world source; the visuals come from the local Alpha cache when present
// and are allowed to be absent (positions, ids and state are what the contract promises).
using System.Collections;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Creatures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.World.PlayMode
{
    public class WorldContentStreamerPlayModeTests
    {
        const int SurfaceY = 64;
        static readonly WorldZoneKey StartZone = new WorldZoneKey(AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ);

        static DelegateContentZoneSource FlatWorld()
        {
            return new DelegateContentZoneSource
            {
                Seed = 2024u,
                Climate = cell => new ZoneClimate
                {
                    Humidity = 0.5, Temperature = 0.5, CellValue = 0.0, Spacing = 0.5, Level = 6, CellType = 0xB,
                },
                Surface = (x, z) => SurfaceY,
                FootprintFree = (x, z, w, d) => true,
                Removed = id => false,
            };
        }

        static ContentPlanSettings Settings()
        {
            return new ContentPlanSettings
            {
                StagedObjectKinds = new List<int> { 0x15, 0x2e, 0x2f },
                CreatureKinds = new List<int> { 0x00, 0x1e, 0x3f },
                DecorKinds = new List<int> { 24 },
                StructureTemplates = new List<string> { AlphaStructureTemplates.HouseId },
                VegetationPerZone = 4,
                CreatureSlots = 4,
                DecorSlots = 2,
                PlaceStructures = true,
            };
        }

        static WorldContentStreamer CreateStreamer(out GameObject root)
        {
            root = new GameObject("WorldContentRoot");
            var streamer = root.AddComponent<WorldContentStreamer>();
            streamer.Seed = 2024u;
            streamer.Source = FlatWorld();
            streamer.Settings = Settings();
            streamer.TickCreatures = false;
            streamer.AiFactory = () => null;
            streamer.SetWorldOrigin(AlphaWorldScale.StartVoxelX, AlphaWorldScale.StartVoxelZ);
            return streamer;
        }

        static ContentStructureView[] Structures(GameObject root) =>
            root.GetComponentsInChildren<ContentStructureView>(true);

        [UnityTest]
        public IEnumerator ZoneLoadsRealObjectsOnTheReportedSurface()
        {
            var streamer = CreateStreamer(out var root);
            try
            {
                Assert.IsTrue(streamer.LoadZone(StartZone));
                var plan = streamer.PlanFor(StartZone);
                Assert.Greater(plan.Items.Count, 0, plan.Describe());
                Assert.Greater(plan.StructureCount, 0, "a structure candidate was planned");
                Assert.Greater(plan.CreatureCount, 0, "creatures are part of the zone content");

                int objects = streamer.LoadedObjectCount;
                Assert.AreEqual(plan.Items.Count, objects, streamer.Describe());

                foreach (var agent in streamer.Spawner.Spawned.Values)
                {
                    Assert.IsNotNull(agent.State);
                    Assert.AreEqual(SurfaceY + 1, agent.State.Y, 1e-6, agent.State.StableId);
                    Assert.AreEqual(SurfaceY + 1f, agent.transform.position.y, 1e-3f, agent.State.StableId);
                    // The transform is local to the floating origin, not an absolute 8.4 M voxel coordinate.
                    Assert.Less(Mathf.Abs(agent.transform.position.x - (float)(agent.State.X - AlphaWorldScale.StartVoxelX)), 1e-2f);
                }

                var structures = Structures(root);
                Assert.AreEqual(plan.StructureCount, structures.Length);
                int zoneOrigin = AlphaWorldScale.ZoneOriginVoxel(StartZone.X);
                foreach (var view in structures)
                {
                    Assert.AreEqual(SurfaceY + 1, view.AbsoluteY, 1e-9);
                    Assert.GreaterOrEqual(view.AbsoluteX, zoneOrigin);
                    Assert.Less(view.AbsoluteX, zoneOrigin + AlphaWorldScale.ZoneVoxels);
                    Assert.GreaterOrEqual(view.AbsoluteZ, zoneOrigin);
                    Assert.Less(view.AbsoluteZ, zoneOrigin + AlphaWorldScale.ZoneVoxels);
                }

                // Loading the same zone again must not duplicate anything.
                Assert.IsFalse(streamer.LoadZone(StartZone), "already loaded");
                Assert.AreEqual(objects, streamer.LoadedObjectCount);
                Assert.AreEqual(objects, streamer.Spawner.SpawnedCount + Structures(root).Length);
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator UnloadKeepsStateAndReloadRestoresTheSameIdentities()
        {
            var streamer = CreateStreamer(out var root);
            try
            {
                Assert.IsTrue(streamer.LoadZone(StartZone));
                var plan = streamer.PlanFor(StartZone);
                var creatures = new List<string>(streamer.Spawner.Spawned.Keys);
                Assert.IsNotEmpty(creatures, streamer.Describe());

                string damaged = creatures[0];
                streamer.Spawner.Spawned[damaged].State.Health = 1.0;
                string removed = creatures.Count > 1
                    ? creatures[1]
                    : plan.Items.Find(i => i.Kind == ContentPlacementKind.Structure)?.StableId;
                Assert.IsNotNull(removed);
                Assert.IsTrue(streamer.MarkRemoved(removed));
                yield return null;

                Assert.IsTrue(streamer.UnloadZone(StartZone));
                yield return null;
                Assert.AreEqual(0, streamer.LoadedObjectCount);
                Assert.AreEqual(0, streamer.Spawner.SpawnedCount);
                Assert.AreEqual(0, Structures(root).Length);
                Assert.IsTrue(streamer.NpcStates.Contains(damaged), "creature state survives the unload");

                Assert.IsTrue(streamer.LoadZone(StartZone));
                Assert.IsTrue(streamer.IsSpawned(damaged));
                Assert.AreEqual(1.0, streamer.Spawner.Spawned[damaged].State.Health, 1e-9, "health restored");
                Assert.IsFalse(streamer.IsSpawned(removed), "removed content must not come back");
                Assert.AreEqual(plan.Items.Count - 1, streamer.LoadedObjectCount,
                    "same plan minus the removed object, no duplicates");
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator RepeatedLoadUnloadKeepsTheSceneStable()
        {
            var streamer = CreateStreamer(out var root);
            try
            {
                streamer.LoadZone(StartZone);
                yield return null;
                streamer.UnloadZone(StartZone);
                yield return null;

                int baselineChildren = root.transform.childCount;
                int baselineStates = streamer.NpcStates.Count;

                for (int cycle = 0; cycle < 10; cycle++)
                {
                    Assert.IsTrue(streamer.LoadZone(StartZone), "cycle " + cycle);
                    Assert.Greater(streamer.LoadedObjectCount, 0, "cycle " + cycle);
                    yield return null;
                    Assert.IsTrue(streamer.UnloadZone(StartZone), "cycle " + cycle);
                    yield return null;
                    Assert.AreEqual(0, streamer.LoadedObjectCount, "cycle " + cycle);
                    Assert.AreEqual(0, Structures(root).Length, "cycle " + cycle);
                    Assert.AreEqual(0, streamer.Spawner.SpawnedCount, "cycle " + cycle);
                }

                Assert.AreEqual(baselineChildren, root.transform.childCount, "no leftover GameObjects");
                Assert.AreEqual(baselineStates, streamer.NpcStates.Count, "the state store does not grow");
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator FloatingOriginKeepsTransformsLocalAndWorldDataAbsolute()
        {
            var streamer = CreateStreamer(out var root);
            try
            {
                Assert.IsTrue(streamer.LoadZone(StartZone));
                var agents = new List<CreatureAgent>(streamer.Spawner.Spawned.Values);
                Assert.IsNotEmpty(agents, streamer.Describe());
                var absolute = new List<double>(agents.Count);
                foreach (var agent in agents)
                {
                    absolute.Add(agent.State.X);
                    Assert.Less(Mathf.Abs(agent.transform.position.x), AlphaWorldScale.ZoneVoxels + 1f);
                }

                double moved = AlphaWorldScale.StartVoxelX + 1_000_000.0;
                streamer.SetWorldOrigin(moved, moved);

                for (int i = 0; i < agents.Count; i++)
                {
                    var agent = agents[i];
                    Assert.AreEqual(absolute[i], agent.State.X, 1e-9, "absolute world position is preserved as double");
                    Assert.AreEqual(agent.State.X - moved, (double)agent.transform.position.x, 0.5,
                        "the transform is the local difference");
                }

                foreach (var view in Structures(root))
                    Assert.AreEqual(view.AbsoluteX - moved, (double)view.transform.position.x, 0.5);

                streamer.SetWorldOrigin(AlphaWorldScale.StartVoxelX, AlphaWorldScale.StartVoxelZ);
                foreach (var agent in agents)
                    Assert.AreEqual(agent.State.X - AlphaWorldScale.StartVoxelX, (double)agent.transform.position.x, 0.5);
                yield return null;
            }
            finally
            {
                Object.Destroy(root);
            }
        }
    }
}
