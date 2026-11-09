// CubeGame.Features.Creatures — deterministic creature spawning with stable identity (D86 M0-06B/D).
//
// Alpha's 30-row level spawn table, biome conditions and population limits are NOT decoded (N-19,
// population/despawn unknown), so this planner is an explicit CubeGame contract: deterministic
// placement from the world seed via the accepted DeterministicHash, star rolls from the documented
// chances, stable ids region+cell+slot, and a caller-provided set of eligible kinds (the world
// generator decides which kinds live in a biome; nothing is invented here).
using System;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Core.Characters;
using CubeGame.Core.Generation;
using UnityEngine;

namespace CubeGame.Features.Creatures
{
    public struct CreatureSpawnRequest
    {
        public string StableId;
        public int Kind;
        public int Level;
        public int Stars;
        public int Faction;
        public double X;
        public double Y;
        public double Z;
    }

    public static class CreatureSpawnPlanner
    {
        /// <summary>CUBEGAME_CONTRACT: share of spawn slots that actually produce a creature.</summary>
        public const int OccupancyPercent = 70;
        /// <summary>Alpha level rows (SpawnTable.LevelRows) bound the rolled level.</summary>
        public const int MaxLevel = 30;

        public static List<CreatureSpawnRequest> Plan(uint seed, int regionX, int regionZ, int cellX, int cellZ,
            int maxPerCell, IReadOnlyList<int> eligibleKinds, List<string> problems = null)
        {
            return Plan(seed, regionX, regionZ, cellX, cellZ, maxPerCell, eligibleKinds, null, 0, problems);
        }

        /// <summary>
        /// Same deterministic plan plus passive decor slots (plants/ore/egg). Decor is a CubeGame
        /// contract: Alpha's climate->object table (W-18) is not extracted, so the caller passes the
        /// decor set explicitly and nothing is invented here.
        /// </summary>
        public static List<CreatureSpawnRequest> Plan(uint seed, int regionX, int regionZ, int cellX, int cellZ,
            int maxPerCell, IReadOnlyList<int> eligibleKinds, IReadOnlyList<int> decorKinds, int decorPerCell,
            List<string> problems = null)
        {
            var result = PlanSlots(seed, regionX, regionZ, cellX, cellZ, maxPerCell, eligibleKinds, 0, problems);
            if (decorKinds != null && decorKinds.Count > 0 && decorPerCell > 0)
            {
                var decor = PlanSlots(seed ^ 0x5EEDu, regionX, regionZ, cellX, cellZ, decorPerCell, decorKinds,
                    DecorSlotOffset, problems);
                for (int i = 0; i < decor.Count; i++)
                {
                    var d = decor[i];
                    if (!AlphaContentDatabase.TryCreatureKind(d.Kind, out var def) || !def.Passive)
                        problems?.Add($"SPAWN_DECOR_NOT_PASSIVE 0x{d.Kind:x2}");
                    d.Faction = (int)CreatureFaction.Passive;
                    result.Add(d);
                }
            }
            return result;
        }

        /// <summary>Stable ids of decor slots never collide with creature slots.</summary>
        public const int DecorSlotOffset = 1000;

        static List<CreatureSpawnRequest> PlanSlots(uint seed, int regionX, int regionZ, int cellX, int cellZ,
            int slots, IReadOnlyList<int> kinds, int slotOffset, List<string> problems)
        {
            var result = new List<CreatureSpawnRequest>();
            if (kinds == null || kinds.Count == 0) { problems?.Add("SPAWN_NO_ELIGIBLE_KINDS"); return result; }
            if (slots < 0) throw new ArgumentOutOfRangeException(nameof(slots));
            int cellSize = AlphaWorldScale.CellVoxels;
            double cellOriginX = AlphaWorldScale.RegionOriginVoxel(regionX) + AlphaWorldScale.CellOriginVoxel(cellX);
            double cellOriginZ = AlphaWorldScale.RegionOriginVoxel(regionZ) + AlphaWorldScale.CellOriginVoxel(cellZ);
            for (int slot = 0; slot < slots; slot++)
            {
                int worldSlot = slotOffset + slot;
                uint h = DeterministicHash.Combine((int)seed, regionX, regionZ, cellX, cellZ, worldSlot);
                if (h % 100u >= OccupancyPercent) continue;
                int kind = kinds[(int)((h >> 8) % (uint)kinds.Count)];
                if (!AlphaContentDatabase.TryCreatureKind(kind, out var def))
                {
                    problems?.Add($"SPAWN_UNKNOWN_KIND 0x{kind:x2}");
                    continue;
                }
                int level = 1 + (int)((h >> 12) % (uint)MaxLevel);
                int stars = SpawnTable.RollStars(h, h >> 8, h >> 16);
                int localX = (int)((h >> 4) % (uint)cellSize);
                int localZ = (int)((h >> 20) % (uint)cellSize);
                var request = new CreatureSpawnRequest
                {
                    StableId = SpawnTable.SpawnId(regionX, regionZ, cellX, cellZ, worldSlot),
                    Kind = kind,
                    Level = level,
                    Stars = stars,
                    Faction = CreatureKinds.FactionOf(kind),
                    X = cellOriginX + localX + 0.5,
                    Y = 0.0,
                    Z = cellOriginZ + localZ + 0.5,
                };
                if (def.Passive && request.Faction != (int)CreatureFaction.Passive)
                    problems?.Add($"SPAWN_PASSIVE_FACTION 0x{kind:x2} {request.Faction}");
                result.Add(request);
            }
            return result;
        }

        /// <summary>
        /// Zone-level slots (streaming unit = 256×256 voxels). Same deterministic rules as the cell plan, but
        /// positions and stable ids belong to one zone, so a zone can be unloaded/reloaded on its own.
        /// Alpha's 30-row level spawn table and population limits are still NOT decoded (N-19) — the caller
        /// passes the eligible kinds and gets a documented CubeGame contract back.
        /// </summary>
        public static List<CreatureSpawnRequest> PlanZone(uint seed, int zoneX, int zoneZ, int slots,
            IReadOnlyList<int> kinds, int slotOffset = 0, List<string> problems = null)
        {
            var result = new List<CreatureSpawnRequest>();
            var zone = new WorldZoneKey(zoneX, zoneZ);
            if (!AlphaWorldScale.IsInsideWorld(zone))
            {
                problems?.Add($"SPAWN_ZONE_OUTSIDE_WORLD {zone}");
                return result;
            }
            if (kinds == null || kinds.Count == 0) { problems?.Add("SPAWN_NO_ELIGIBLE_KINDS"); return result; }
            if (slots < 0) throw new ArgumentOutOfRangeException(nameof(slots));
            int originX = AlphaWorldScale.ZoneOriginVoxel(zoneX);
            int originZ = AlphaWorldScale.ZoneOriginVoxel(zoneZ);
            for (int slot = 0; slot < slots; slot++)
            {
                int worldSlot = slotOffset + slot;
                uint h = DeterministicHash.Combine((int)seed, zoneX, zoneZ, worldSlot, 0x5A);
                if (h % 100u >= OccupancyPercent) continue;
                int kind = kinds[(int)((h >> 8) % (uint)kinds.Count)];
                if (!AlphaContentDatabase.TryCreatureKind(kind, out var def))
                {
                    problems?.Add($"SPAWN_UNKNOWN_KIND 0x{kind:x2}");
                    continue;
                }
                int level = 1 + (int)((h >> 12) % (uint)MaxLevel);
                int stars = SpawnTable.RollStars(h, h >> 8, h >> 16);
                int localX = (int)((h >> 4) % (uint)AlphaWorldScale.ZoneVoxels);
                int localZ = (int)((h >> 20) % (uint)AlphaWorldScale.ZoneVoxels);
                var request = new CreatureSpawnRequest
                {
                    StableId = SpawnTable.ZoneSpawnId(zoneX, zoneZ, worldSlot),
                    Kind = kind,
                    Level = level,
                    Stars = stars,
                    Faction = CreatureKinds.FactionOf(kind),
                    X = originX + localX + 0.5,
                    Y = 0.0,
                    Z = originZ + localZ + 0.5,
                };
                if (def.Passive && request.Faction != (int)CreatureFaction.Passive)
                    problems?.Add($"SPAWN_PASSIVE_FACTION 0x{kind:x2} {request.Faction}");
                result.Add(request);
            }
            return result;
        }
    }

    /// <summary>
    /// Spawns the planned creatures, keeps their blob when they leave the active radius and restores it
    /// on return. Off-screen *simulation* is a CubeGame extension and is not claimed here.
    /// </summary>
    public sealed class CreatureSpawner : MonoBehaviour
    {
        public uint Seed = 1;
        public int RegionX, RegionZ, CellX, CellZ;
        public int MaxPerCell = 4;
        /// <summary>Passive decor slots per cell (plants/ore/egg). W-18 climate table is UNKNOWN, so this
        /// is a CubeGame contract and the decor set comes from the staged visuals.</summary>
        public int DecorPerCell = 4;
        public float DespawnRadius = 90f;
        public bool SpawnOnStart = true;

        /// <summary>
        /// Floating origin in absolute world voxels. The state keeps absolute `double` coordinates; the
        /// Transform only ever holds the local difference, so an 8.4 M voxel world position does not lose
        /// precision in a 32-bit float. Default 0 = legacy absolute placement.
        /// </summary>
        public double WorldOriginX;
        public double WorldOriginZ;

        /// <summary>Shared state blob store (the zone streamer shares one store across unload/reload).</summary>
        public CreatureStateStore SharedStore;

        /// <summary>The game injects its AI factory (Features.AI) — no compile-time dependency in this module.</summary>
        public Func<ICreatureAi> AiFactory;
        public AlphaActorLibrary ActorLibrary;
        /// <summary>Optional ground query (x, z) → y from the world generator; null keeps the planned Y.</summary>
        public Func<int, int, float> GroundHeight;

        public readonly CreatureStateStore Store = new CreatureStateStore();
        public readonly Dictionary<string, CreatureAgent> Spawned = new Dictionary<string, CreatureAgent>(StringComparer.Ordinal);
        public readonly List<string> Problems = new List<string>();
        public string Status { get; private set; } = "NOT_STARTED";
        public int SpawnedCount => Spawned.Count;

        CreatureStateStore ActiveStore => SharedStore ?? Store;

        void Start()
        {
            if (SpawnOnStart) Spawn();
        }

        public List<CreatureSpawnRequest> Spawn()
        {
            Problems.Clear();
            var library = ActorLibrary ?? AlphaActorLibrary.Shared;
            var kinds = CreatureVisualCatalog.AvailableKinds(library);
            if (kinds.Count == 0)
            {
                Status = "NO_STAGED_VISUALS " + (library != null ? library.Diagnosis : "NO_LIBRARY");
                return new List<CreatureSpawnRequest>();
            }
            var combat = new List<int>();
            var decor = new List<int>();
            foreach (var kind in kinds)
                (CreatureKinds.IsPassiveObject(kind) ? decor : combat).Add(kind);
            var primary = combat.Count > 0 ? combat : kinds;
            var plan = CreatureSpawnPlanner.Plan(Seed, RegionX, RegionZ, CellX, CellZ, MaxPerCell, primary, decor, DecorPerCell, Problems);
            foreach (var request in plan) SpawnOne(request, library);
            Status = $"SPAWNED {Spawned.Count}/{plan.Count} " + (Problems.Count > 0 ? "problems=" + Problems.Count : "problems=0");
            return plan;
        }

        public CreatureAgent SpawnOne(CreatureSpawnRequest request, AlphaActorLibrary library)
        {
            var go = new GameObject("Creature_" + request.StableId);
            go.transform.SetParent(transform, false);
            double spawnY = GroundHeight != null ? GroundHeight((int)request.X, (int)request.Z) : request.Y;
            go.transform.position = new Vector3(
                (float)(request.X - WorldOriginX), (float)spawnY, (float)(request.Z - WorldOriginZ));
            var agent = go.AddComponent<CreatureAgent>();
            var state = new CreatureState
            {
                StableId = request.StableId,
                Kind = request.Kind,
                Faction = request.Faction,
                Level = request.Level,
                Stars = request.Stars,
                MaxHealth = CharacterStats.CalcCreatureMaxHealth(CharacterStats.DefaultBaseHealth, request.Level, request.Stars),
                X = request.X,
                Y = spawnY,
                Z = request.Z,
            };
            state.Health = state.MaxHealth;
            if (ActiveStore.TryRestore(request.StableId, out var saved, out var error))
            {
                // Position comes from the world (spawn anchor); the rest is the persisted creature state.
                state.Health = saved.Health;
                state.Stars = saved.Stars;
                foreach (var pair in saved.Threat) state.Threat[pair.Key] = pair.Value;
            }
            else if (!string.IsNullOrEmpty(error) && error != "entity blob not found")
            {
                Problems.Add($"SPAWN_STATE_RESTORE {request.StableId} {error}");
            }

            AlphaActorDefinition actor = null;
            if (CreatureVisualCatalog.TryResolve(request.Kind, library, out var actorId, out var reason) && library.TryGet(actorId, out var def))
            {
                actor = def;
            }
            else
            {
                Problems.Add($"SPAWN_VISUAL {request.StableId} {reason}");
            }
            // Passive Alpha objects (D69) are stopped by the injected AI itself (`PASSIVE_OBJECT`), so the
            // spawner keeps handing every entity the same AI factory the game gave it.
            agent.Initialize(state, AiFactory?.Invoke(), actor,
                (uint)request.StableId.GetHashCode(), WorldOriginX, WorldOriginZ);
            Spawned[request.StableId] = agent;
            return agent;
        }

        /// <summary>Spawn a plan as given (the world content streamer feeds a zone-scoped plan here).</summary>
        public int SpawnPlan(IReadOnlyList<CreatureSpawnRequest> plan, AlphaActorLibrary library = null)
        {
            var target = library ?? ActorLibrary ?? AlphaActorLibrary.Shared;
            int count = 0;
            foreach (var request in plan)
            {
                if (Spawned.ContainsKey(request.StableId)) continue;   // no duplicates on reload
                SpawnOne(request, target);
                count++;
            }
            return count;
        }

        /// <summary>Destroy one creature, keeping its state blob (identity returns on the next load).</summary>
        public bool Despawn(string stableId)
        {
            if (!Spawned.TryGetValue(stableId, out var agent)) return false;
            if (agent != null && agent.State != null) ActiveStore.Save(agent.State);
            Spawned.Remove(stableId);
            if (agent != null) Destroy(agent.gameObject);
            return true;
        }

        /// <summary>Move the floating origin and reposition every live creature.</summary>
        public void SetWorldOrigin(double originX, double originZ)
        {
            WorldOriginX = originX;
            WorldOriginZ = originZ;
            foreach (var agent in Spawned.Values) if (agent != null) agent.SetWorldOrigin(originX, originZ);
        }

        /// <summary>Save and destroy creatures outside the active radius; identity comes back on respawn.</summary>
        public int DespawnBeyond(Vector3 center)
        {
            var remove = new List<string>();
            foreach (var pair in Spawned)
            {
                var agent = pair.Value;
                if (agent == null) { remove.Add(pair.Key); continue; }
                if (Vector3.Distance(center, agent.transform.position) <= DespawnRadius) continue;
                if (agent.State != null) ActiveStore.Save(agent.State);
                remove.Add(pair.Key);
            }
            foreach (var id in remove)
            {
                if (Spawned.TryGetValue(id, out var agent) && agent != null) Destroy(agent.gameObject);
                Spawned.Remove(id);
            }
            return remove.Count;
        }

        public void TickAll(float deltaSeconds)
        {
            foreach (var agent in Spawned.Values) if (agent != null) agent.Tick(deltaSeconds);
        }

        public string Describe() => Status + " " + CreatureVisualCatalog.Describe();
    }
}
