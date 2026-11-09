// CubeGame.Features.Creatures — the pluggable runtime placement of world content (D86 M0-02/M0-06/M0-07).
//
// One component owns "what stands in the loaded zones":
//   * loads a zone (256×256 voxels, the Alpha/M0-02b streaming unit), asks ZoneContentPlanner for its
//     deterministic content and realises it: passive Alpha objects and creatures through the existing
//     CreatureSpawner/CreatureAgent/AlphaActorView path (visuals + AI + state store), structures through
//     a footprint placeholder anchored on the surface the world reported;
//   * unloads a zone: destroys the objects but saves NPC state, so identity and health survive;
//   * reload produces exactly the same ids (deterministic planning) and never duplicates an object;
//   * removed/destroyed content is remembered (here and in the world source) and does not come back;
//   * keeps every Transform near the floating origin: absolute positions are `double` world voxels,
//     the Transform only holds the difference, so the 2^24-voxel world does not lose precision.
//
// Boundaries: this component reads world facts through IContentZoneSource and never writes terrain,
// climate, saves or the world's own delta store. It does not compute combat, economy or loot — the
// spawned entities are the same ones the gameplay layer already owns.
using System;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;
using UnityEngine;

namespace CubeGame.Features.Creatures
{
    /// <summary>
    /// Footprint placeholder of a structure. The template size is Alpha evidence (W-17: house 3×3×4), the
    /// mesh is an explicit CubeGame placeholder — no Alpha geometry is claimed or published.
    /// </summary>
    public sealed class ContentStructureView : MonoBehaviour
    {
        public const string GeometryStatus = "PLACEHOLDER_NOT_ALPHA: cuboid footprint, interior/parts UNKNOWN";

        public string StableId { get; private set; } = string.Empty;
        public string TemplateId { get; private set; } = string.Empty;
        public double AbsoluteX { get; private set; }
        public double AbsoluteY { get; private set; }
        public double AbsoluteZ { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Depth { get; private set; }
        public string Status => GeometryStatus;

        internal void Initialize(ContentPlacement placement)
        {
            StableId = placement.StableId;
            TemplateId = placement.TemplateId ?? string.Empty;
            AbsoluteX = placement.X;
            AbsoluteY = placement.SurfaceY + 1;
            AbsoluteZ = placement.Z;
            Width = Math.Max(1, placement.Width);
            Height = Math.Max(1, placement.Height);
            Depth = Math.Max(1, placement.Depth);
            gameObject.name = "Structure_" + StableId;
            transform.localScale = new Vector3(Width, Height, Depth);
            ApplyOrigin(0, 0);
        }

        /// <summary>Place the cuboid so that its base sits on the anchor and the Transform stays local.</summary>
        public void ApplyOrigin(double originX, double originZ)
        {
            transform.position = new Vector3(
                (float)(AbsoluteX - originX), (float)(AbsoluteY + Height * 0.5), (float)(AbsoluteZ - originZ));
        }
    }

    /// <summary>Loads/unloads zone content in one place; the game owns the scene and calls it.</summary>
    public sealed class WorldContentStreamer : MonoBehaviour
    {
        [Header("World inputs (read-only)")]
        public uint Seed = 1;
        public IContentZoneSource Source;
        public AlphaActorLibrary ActorLibrary;
        public Func<ICreatureAi> AiFactory;
        public ContentPlanSettings Settings;

        [Header("Streaming (the game decides which zones to load)")]
        public bool TickCreatures = true;

        /// <summary>Floating origin in absolute world voxels (the scene stays around zero).</summary>
        public double WorldOriginX { get; private set; }
        public double WorldOriginZ { get; private set; }

        /// <summary>NPC state of unloaded entities; the game layer persists this blob store.</summary>
        public readonly CreatureStateStore NpcStates = new CreatureStateStore();

        /// <summary>Content the game removed (killed/despawned/used up) — never planned again.</summary>
        public readonly HashSet<string> Removed = new HashSet<string>(StringComparer.Ordinal);

        public readonly List<string> Problems = new List<string>();

        sealed class ZoneInstance
        {
            public ContentPlan Plan;
            public readonly List<string> SpawnedIds = new List<string>();
            public readonly List<ContentStructureView> Structures = new List<ContentStructureView>();
        }

        readonly Dictionary<WorldZoneKey, ZoneInstance> _zones = new Dictionary<WorldZoneKey, ZoneInstance>();
        CreatureSpawner _spawner;

        public CreatureSpawner Spawner => EnsureSpawner();

        public int LoadedZoneCount => _zones.Count;

        public int LoadedObjectCount
        {
            get
            {
                int n = 0;
                foreach (var zone in _zones.Values) n += zone.SpawnedIds.Count;
                return n;
            }
        }

        void Update()
        {
            if (TickCreatures) Tick(Time.deltaTime);
        }

        CreatureSpawner EnsureSpawner()
        {
            if (_spawner != null) return _spawner;
            var go = transform.Find("CreatureSpawner");
            GameObject target = go != null ? go.gameObject : new GameObject("CreatureSpawner");
            target.transform.SetParent(transform, false);
            target.transform.localPosition = Vector3.zero;
            _spawner = target.GetComponent<CreatureSpawner>();
            if (_spawner == null) _spawner = target.AddComponent<CreatureSpawner>();
            _spawner.SpawnOnStart = false;
            _spawner.SharedStore = NpcStates;
            _spawner.ActorLibrary = ActorLibrary ?? AlphaActorLibrary.Shared;
            _spawner.AiFactory = AiFactory;
            _spawner.SetWorldOrigin(WorldOriginX, WorldOriginZ);
            return _spawner;
        }

        ContentPlanSettings EffectiveSettings()
        {
            if (Settings != null) return Settings;
            Settings = ContentPlanSettings.FromLibrary(ActorLibrary ?? AlphaActorLibrary.Shared);
            return Settings;
        }

        /// <summary>Plan a zone without touching the scene (used for inspection and tests).</summary>
        public ContentPlan PlanFor(WorldZoneKey zone)
        {
            if (Source == null) throw new InvalidOperationException("CONTENT_SOURCE_MISSING");
            var plan = ZoneContentPlanner.Plan(zone, Source, EffectiveSettings());
            Problems.AddRange(plan.Problems);
            return plan;
        }

        /// <summary>Load one zone. Returns false when it was already loaded (no duplicates by construction).</summary>
        public bool LoadZone(WorldZoneKey zone)
        {
            if (_zones.ContainsKey(zone)) return false;
            var plan = PlanFor(zone);
            var instance = new ZoneInstance { Plan = plan };
            var spawner = EnsureSpawner();
            foreach (var item in plan.Items)
            {
                if (Removed.Contains(item.StableId) || (Source != null && Source.IsRemoved(item.StableId)))
                {
                    Problems.Add($"CONTENT_SKIPPED_REMOVED {item.StableId}");
                    continue;
                }
                switch (item.Kind)
                {
                    case ContentPlacementKind.Vegetation:
                    case ContentPlacementKind.Creature:
                        var request = new CreatureSpawnRequest
                        {
                            StableId = item.StableId,
                            Kind = item.Species,
                            Level = item.Level,
                            Stars = item.Stars,
                            Faction = item.Faction,
                            X = item.X,
                            Y = item.SurfaceY + 1,
                            Z = item.Z,
                        };
                        if (spawner.Spawned.ContainsKey(item.StableId)) continue;
                        spawner.SpawnOne(request, spawner.ActorLibrary);
                        instance.SpawnedIds.Add(item.StableId);
                        break;
                    case ContentPlacementKind.Structure:
                        var view = SpawnStructure(item);
                        instance.Structures.Add(view);
                        instance.SpawnedIds.Add(item.StableId);
                        break;
                }
            }
            _zones[zone] = instance;
            return true;
        }

        ContentStructureView SpawnStructure(ContentPlacement item)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<ContentStructureView>();
            view.Initialize(item);
            view.ApplyOrigin(WorldOriginX, WorldOriginZ);
            return view;
        }

        /// <summary>Unload one zone: destroy the objects, keep NPC state and remember what was removed.</summary>
        public bool UnloadZone(WorldZoneKey zone)
        {
            if (!_zones.TryGetValue(zone, out var instance)) return false;
            var spawner = EnsureSpawner();
            foreach (var id in instance.SpawnedIds)
            {
                if (spawner.Spawned.ContainsKey(id)) spawner.Despawn(id);   // saves the state blob
            }
            foreach (var structure in instance.Structures)
            {
                if (structure != null) Destroy(structure.gameObject);
            }
            instance.SpawnedIds.Clear();
            instance.Structures.Clear();
            _zones.Remove(zone);
            return true;
        }

        public bool IsLoaded(WorldZoneKey zone) => _zones.ContainsKey(zone);

        public bool IsSpawned(string stableId)
        {
            if (_spawner != null && _spawner.Spawned.ContainsKey(stableId)) return true;
            foreach (var zone in _zones.Values)
                if (zone.Structures.Exists(v => v != null && v.StableId == stableId)) return true;
            return false;
        }

        /// <summary>Load a square ring of zones around a centre; returns how many zones became loaded.</summary>
        public int LoadRing(WorldZoneKey centre, int radius)
        {
            int loaded = 0;
            for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                var zone = new WorldZoneKey(centre.X + dx, centre.Z + dz);
                if (!AlphaWorldScale.IsInsideWorld(zone)) continue;
                if (LoadZone(zone)) loaded++;
            }
            return loaded;
        }

        /// <summary>Unload every loaded zone outside a square ring; returns how many zones were released.</summary>
        public int UnloadOutside(WorldZoneKey centre, int radius)
        {
            var drop = new List<WorldZoneKey>();
            foreach (var zone in _zones.Keys)
            {
                if (Math.Abs(zone.X - centre.X) > radius || Math.Abs(zone.Z - centre.Z) > radius) drop.Add(zone);
            }
            foreach (var zone in drop) UnloadZone(zone);
            return drop.Count;
        }

        /// <summary>Move the floating origin; every live object is repositioned, absolute data stays intact.</summary>
        public void SetWorldOrigin(double originX, double originZ)
        {
            WorldOriginX = originX;
            WorldOriginZ = originZ;
            if (_spawner != null) _spawner.SetWorldOrigin(originX, originZ);
            foreach (var zone in _zones.Values)
                foreach (var structure in zone.Structures)
                    if (structure != null) structure.ApplyOrigin(originX, originZ);
        }

        public void Tick(float deltaSeconds)
        {
            if (_spawner != null) _spawner.TickAll(deltaSeconds);
        }

        /// <summary>
        /// The game removed an entity (killed, picked up, mined). It disappears now and is never planned again.
        /// </summary>
        public bool MarkRemoved(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return false;
            Removed.Add(stableId);
            bool existed = false;
            if (_spawner != null && _spawner.Spawned.ContainsKey(stableId))
            {
                _spawner.Despawn(stableId);
                existed = true;
            }
            foreach (var zone in _zones.Values)
            {
                zone.SpawnedIds.Remove(stableId);
                for (int i = zone.Structures.Count - 1; i >= 0; i--)
                {
                    var view = zone.Structures[i];
                    if (view == null || view.StableId != stableId) continue;
                    Destroy(view.gameObject);
                    zone.Structures.RemoveAt(i);
                    existed = true;
                }
            }
            return existed;
        }

        public string Describe() =>
            $"zones={_zones.Count} objects={LoadedObjectCount} origin=({WorldOriginX:0.#},{WorldOriginZ:0.#}) " +
            $"removed={Removed.Count} npcStates={NpcStates.Count} problems={Problems.Count}";
    }
}
