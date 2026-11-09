using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using CubeGame.Presentation.Rendering;
using UnityEngine;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>
    /// Presentation of the streamed world: one GameObject (mesh + collider, water) per 32×32-voxel chunk around a
    /// focus point. Generator voxel columns are sampled on worker threads (the generator is pure; its climate cache is
    /// locked); deltas, meshes and colliders are applied on the main thread with a per-frame budget.
    ///
    /// Floating origin: Alpha coordinates reach 2^24 voxels, beyond float precision, so chunk objects are placed at
    /// (chunk origin − <see cref="Origin"/>). <see cref="Rebase"/> moves the origin and every chunk; the caller moves
    /// the remaining scene objects (player, camera) by the same offset.
    /// Releasing a chunk beyond the keep radius records its zone as discovered (Alpha W-12).
    /// </summary>
    public sealed class StreamingWorldView : MonoBehaviour
    {
        private readonly Dictionary<ChunkKey, ChunkView> _loaded = new Dictionary<ChunkKey, ChunkView>();
        private readonly HashSet<ChunkKey> _inFlight = new HashSet<ChunkKey>();
        private readonly ConcurrentQueue<ChunkVoxels> _ready = new ConcurrentQueue<ChunkVoxels>();
        private readonly List<ChunkKey> _wanted = new List<ChunkKey>();
        private readonly HashSet<ChunkKey> _dirty = new HashSet<ChunkKey>();
        private readonly Dictionary<ChunkKey, ChunkVoxels> _heights = new Dictionary<ChunkKey, ChunkVoxels>();

        private WorldState _world;
        private WorldStreamingPolicy _policy;
        private Material _terrainMaterial;
        private ChunkKey _center;
        private bool _hasCenter;
        private int _generation;
        private int _maxWorkers;

        private sealed class ChunkView
        {
            public GameObject Root;
            public Mesh Terrain;
            public Mesh Water;
            public int Vertices;
        }

        /// <summary>Scene position (0,0,0) corresponds to this voxel (x, z); y is not shifted.</summary>
        public Vector2Int Origin { get; private set; }

        public int BuildsPerFrame { get; set; } = 4;

        public int LoadedCount => _loaded.Count;

        public int PendingCount => _wanted.Count + _inFlight.Count + _ready.Count;

        public long TotalBuilt { get; private set; }

        public long TotalReleased { get; private set; }

        public ChunkKey Center => _center;

        public WorldState World => _world;

        public int LoadedVertices
        {
            get
            {
                int n = 0;
                foreach (ChunkView view in _loaded.Values)
                {
                    n += view.Vertices;
                }

                return n;
            }
        }

        public bool IsLoaded(ChunkKey chunk) => _loaded.ContainsKey(chunk);

        public void Initialise(WorldState world, WorldStreamingPolicy policy, Material terrainMaterial, Vector2Int origin)
        {
            Clear();
            _world = world;
            _policy = policy;
            _terrainMaterial = terrainMaterial;
            Origin = origin;
            _maxWorkers = Math.Max(1, Environment.ProcessorCount - 1);
            _world.Deltas.RegionChanged += OnRegionChanged;
        }

        public static Vector2Int OriginFor(double worldX, double worldZ)
        {
            int size = WorldLayout.ChunkSize;
            return new Vector2Int(WorldLayout.FloorDiv((int)Math.Floor(worldX), size) * size, WorldLayout.FloorDiv((int)Math.Floor(worldZ), size) * size);
        }

        public Vector3 ToScene(double worldX, double worldY, double worldZ)
        {
            return new Vector3((float)(worldX - Origin.x), (float)worldY, (float)(worldZ - Origin.y));
        }

        public void ToWorld(Vector3 scene, out double x, out double y, out double z)
        {
            x = scene.x + (double)Origin.x;
            y = scene.y;
            z = scene.z + (double)Origin.y;
        }

        /// <summary>Moves the origin to <paramref name="newOrigin"/>; returns the scene offset to apply to other objects.</summary>
        public Vector3 Rebase(Vector2Int newOrigin)
        {
            var shift = new Vector3(Origin.x - newOrigin.x, 0f, Origin.y - newOrigin.y);
            Origin = newOrigin;
            foreach (KeyValuePair<ChunkKey, ChunkView> pair in _loaded)
            {
                pair.Value.Root.transform.localPosition = ChunkScenePosition(pair.Key);
            }

            return shift;
        }

        /// <summary>Builds every chunk within <paramref name="radius"/> chunks synchronously (spawn, load).</summary>
        public void LoadAround(Vector3 sceneFocus, int radius)
        {
            ChunkKey center = ChunkAt(sceneFocus);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    var key = new ChunkKey(center.X + dx, center.Z + dz);
                    if (WorldLayout.IsInside(key) && !_loaded.ContainsKey(key))
                    {
                        Install(ChunkVoxels.Sample(_world.Generator, key));
                    }
                }
            }

            SetFocus(sceneFocus);
        }

        /// <summary>Waits (blocking) until no chunk is pending — tests and loading screens only.</summary>
        public void CompletePending(int timeoutMs = 60000)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            int budget = BuildsPerFrame;
            BuildsPerFrame = int.MaxValue;
            try
            {
                while (PendingCount > 0 && timer.ElapsedMilliseconds < timeoutMs)
                {
                    Pump();
                    Thread.Sleep(1);
                }
            }
            finally
            {
                BuildsPerFrame = budget;
            }
        }

        public ChunkKey ChunkAt(Vector3 scene)
        {
            ToWorld(scene, out double x, out _, out double z);
            return WorldLayout.ChunkOf((int)Math.Floor(x), (int)Math.Floor(z));
        }

        /// <summary>Per-frame streaming step for the focus (scene position).</summary>
        public void SetFocus(Vector3 sceneFocus)
        {
            if (_world == null)
            {
                return;
            }

            ChunkKey center = ChunkAt(sceneFocus);
            if (!_hasCenter || !center.Equals(_center))
            {
                _center = center;
                _hasCenter = true;
                _world.Deltas.MarkDiscovered(new ZoneKey((center.X * WorldLayout.ChunkSize) >> 8, (center.Z * WorldLayout.ChunkSize) >> 8));
                Replan();
            }

            Pump();
        }

        public void Clear()
        {
            if (_world != null)
            {
                _world.Deltas.RegionChanged -= OnRegionChanged;
            }

            _generation++;
            foreach (ChunkKey key in new List<ChunkKey>(_loaded.Keys))
            {
                Release(key, false);
            }

            while (_ready.TryDequeue(out _))
            {
            }

            _inFlight.Clear();
            _wanted.Clear();
            _dirty.Clear();
            _heights.Clear();
            _hasCenter = false;
            _world = null;
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Replan()
        {
            foreach (ChunkKey key in new List<ChunkKey>(_loaded.Keys))
            {
                if (_policy.ShouldRelease(key, _center))
                {
                    Release(key, true);
                }
            }

            _wanted.Clear();
            foreach (ChunkKey key in _policy.Wanted(_center))
            {
                if (!_loaded.ContainsKey(key) && !_inFlight.Contains(key))
                {
                    _wanted.Add(key);
                }
            }
        }

        private void Pump()
        {
            // Start workers, nearest first.
            while (_inFlight.Count < _maxWorkers && _wanted.Count > 0)
            {
                ChunkKey key = _wanted[0];
                _wanted.RemoveAt(0);
                if (_loaded.ContainsKey(key))
                {
                    continue;
                }

                _inFlight.Add(key);
                int generation = _generation;
                IWorldGenerator generator = _world.Generator;
                Task.Run(() =>
                {
                    ChunkVoxels heights = ChunkVoxels.Sample(generator, key);
                    if (generation == _generation)
                    {
                        _ready.Enqueue(heights);
                    }
                });
            }

            int budget = BuildsPerFrame;
            while (budget > 0 && _ready.TryDequeue(out ChunkVoxels heights))
            {
                _inFlight.Remove(heights.Key);
                if (_policy.ShouldRelease(heights.Key, _center) || _loaded.ContainsKey(heights.Key))
                {
                    continue;
                }

                Install(heights);
                budget--;
            }

            foreach (ChunkKey dirty in new List<ChunkKey>(_dirty))
            {
                if (budget <= 0)
                {
                    break;
                }

                _dirty.Remove(dirty);
                if (_heights.TryGetValue(dirty, out ChunkVoxels generated))
                {
                    Release(dirty, false);
                    Install(generated);
                    budget--;
                }
            }
        }

        private void OnRegionChanged(RegionKey region)
        {
            // Deltas carry their own column; rebuild every loaded chunk whose 1-column border touches an edited column.
            foreach (WorldDelta delta in _world.Deltas.DeltasOf(region))
            {
                if (delta.Kind != WorldDeltaKind.ColumnSurfaceSet && delta.Kind != WorldDeltaKind.BlockSet)
                {
                    continue;
                }

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        ChunkKey key = WorldLayout.ChunkOf(delta.X + dx, delta.Z + dz);
                        if (_loaded.ContainsKey(key))
                        {
                            _dirty.Add(key);
                        }
                    }
                }
            }
        }

        private Vector3 ChunkScenePosition(ChunkKey key)
        {
            return new Vector3(key.X * WorldLayout.ChunkSize - Origin.x, 0f, key.Z * WorldLayout.ChunkSize - Origin.y);
        }

        private void Install(ChunkVoxels generated)
        {
            // Keep the generator output untouched for later rebuilds; deltas go onto a copy.
            ChunkVoxels heights = generated.Copy();
            heights.ApplyDeltas(_world.Deltas);
            ChunkMesher.Result data = ChunkMesher.Build(heights);
            ChunkKey key = heights.Key;

            var root = new GameObject($"Chunk_{key.X}_{key.Z}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = ChunkScenePosition(key);

            Mesh terrain = UnityMeshFactory.Create(data.Terrain, $"ChunkMesh_{key.X}_{key.Z}");
            root.AddComponent<MeshFilter>().sharedMesh = terrain;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PilotMaterials.Terrain(_terrainMaterial);
            root.AddComponent<MeshCollider>().sharedMesh = terrain;

            Mesh water = null;
            if (data.Water.VertexCount > 0)
            {
                water = UnityMeshFactory.Create(data.Water, $"WaterMesh_{key.X}_{key.Z}");
                var waterObject = new GameObject("Water");
                waterObject.transform.SetParent(root.transform, false);
                waterObject.AddComponent<MeshFilter>().sharedMesh = water;
                waterObject.AddComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;
            }

            _loaded[key] = new ChunkView
            {
                Root = root,
                Terrain = terrain,
                Water = water,
                Vertices = data.Terrain.VertexCount + data.Water.VertexCount,
            };
            _heights[key] = generated;
            TotalBuilt++;
        }

        private void Release(ChunkKey key, bool countAsUnload)
        {
            if (!_loaded.TryGetValue(key, out ChunkView view))
            {
                return;
            }

            _loaded.Remove(key);
            _heights.Remove(key);
            if (countAsUnload)
            {
                TotalReleased++;
                _world?.Deltas.MarkDiscovered(new ZoneKey((key.X * WorldLayout.ChunkSize) >> 8, (key.Z * WorldLayout.ChunkSize) >> 8));
            }

            if (view.Root != null)
            {
                Destroy(view.Root);
            }

            if (view.Terrain != null)
            {
                Destroy(view.Terrain);
            }

            if (view.Water != null)
            {
                Destroy(view.Water);
            }
        }
    }
}
