using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using CubeGame.Presentation.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>
    /// Presentation of the streamed world: one GameObject (mesh + collider, water) per 32×32-voxel chunk around a
    /// focus point. Workers generate columns and mesh data. The main thread applies edits and installs the views.
    ///
    /// Floating origin: Alpha coordinates reach 2^24 voxels, beyond float precision, so chunk objects are placed at
    /// (chunk origin − <see cref="Origin"/>). <see cref="Rebase"/> moves the origin and every chunk; the caller moves
    /// the remaining scene objects (player, camera) by the same offset.
    /// The gameplay path uses the Alpha terrain worker's window, zone dependencies and vertex limit.
    /// </summary>
    public sealed class StreamingWorldView : MonoBehaviour
    {
        private static readonly int DaylightBrightness = Shader.PropertyToID("_DaylightBrightness");
        private static readonly int HorizonInverseRange = Shader.PropertyToID("_HorizonInverseRange");
        private static readonly int HorizonColor = Shader.PropertyToID("_HorizonColor");
        private readonly Dictionary<ChunkKey, ChunkView> _loaded = new Dictionary<ChunkKey, ChunkView>();
        private readonly HashSet<ChunkKey> _inFlight = new HashSet<ChunkKey>();
        private ConcurrentQueue<ChunkBuild> _ready = new ConcurrentQueue<ChunkBuild>();
        private readonly List<ChunkBuild> _completed = new List<ChunkBuild>();
        private readonly List<ChunkKey> _wanted = new List<ChunkKey>();
        private readonly HashSet<ChunkKey> _eligible = new HashSet<ChunkKey>();
        private readonly HashSet<ChunkKey> _edited = new HashSet<ChunkKey>();
        private readonly HashSet<ChunkKey> _dirty = new HashSet<ChunkKey>();
        private readonly Queue<ChunkKey> _dirtyOrder = new Queue<ChunkKey>();
        private readonly Dictionary<ChunkKey, ChunkVoxels> _heights = new Dictionary<ChunkKey, ChunkVoxels>();

        private WorldState _world;
        private WorldStreamingPolicy _policy;
        private AlphaTerrainGenerator _terrain;
        private Camera _camera;
        private float _horizonRange;
        private Task _zoneWorker;
        private List<ZoneKey> _zoneOrder = new List<ZoneKey>();
        private List<ChunkKey> _window = new List<ChunkKey>();
        private Material _terrainMaterial;
        private Material _waterMaterial;
        private AlphaVegetationMeshes _vegetation;
        private bool _ownsTerrainMaterial;
        private ChunkKey _center;
        private bool _hasCenter;
        private int _maxWorkers;
        private int _focusX;
        private int _focusZ;
        private bool _planDirty;
        private int _vertices;
        private long _vertexBudget;
        private double _lastEviction;

        private sealed class ChunkView
        {
            public GameObject Root;
            public Mesh Terrain;
            public Mesh Water;
            public Mesh Vegetation;
            public int PlantCount;
            public int Vertices;
        }

        private sealed class ChunkBuild
        {
            public ChunkKey Key;
            public ChunkVoxels Voxels;
            public ChunkMesher.Result Meshes;
            public Exception Error;
        }

        /// <summary>Scene position (0,0,0) corresponds to this voxel (x, z); y is not shifted.</summary>
        public Vector2Int Origin { get; private set; }

        public int BuildsPerFrame { get; set; } = 4;

        public int LoadedCount => _loaded.Count;

        public int PendingCount => _wanted.Count + _inFlight.Count + _dirty.Count + (_zoneWorker != null ? 1 : 0);
        public int ReadyCount => _ready.Count + _completed.Count;

        public long TotalBuilt { get; private set; }

        public long TotalReleased { get; private set; }

        public ChunkKey Center => _center;

        public WorldState World => _world;

        public int LoadedVertices => _vertices;
        public int LoadedPlants { get; private set; }
        public long VertexBudget => _vertexBudget;
        public float HorizonRange => _horizonRange;

        public bool IsLoaded(ChunkKey chunk) => _loaded.ContainsKey(chunk);

        public void Initialise(WorldState world, WorldStreamingPolicy policy, Material terrainMaterial, Vector2Int origin,
            Camera camera = null)
        {
            Clear();
            _world = world;
            _policy = policy;
            _terrain = policy.UsesAlphaWindow ? world.Generator as AlphaTerrainGenerator : null;
            _vegetation = new AlphaVegetationMeshes();
            _camera = camera;
            _horizonRange = 0.0001f;
            _terrain?.UseStreamingRetention();
            _lastEviction = Time.realtimeSinceStartupAsDouble;
            SetRenderDistance(50);
            Material source = PilotMaterials.Terrain(terrainMaterial);
            _ownsTerrainMaterial = source.HasProperty(DaylightBrightness);
            _terrainMaterial = _ownsTerrainMaterial ? new Material(source) : source;
            if (source.shader.name == "CubeGame/PilotVertexColor")
            {
                _waterMaterial = new Material(source) { renderQueue = (int)RenderQueue.Transparent };
                _waterMaterial.SetOverrideTag("RenderType", "Transparent");
                _waterMaterial.SetFloat("_UseVertexAlpha", 1f);
                _waterMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                _waterMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                _waterMaterial.SetFloat("_ZWrite", 0f);
            }
            Origin = origin;
            _maxWorkers = Math.Max(1, Environment.ProcessorCount - 1);
            foreach (WorldDelta delta in _world.Deltas.All())
            {
                if (delta.Kind == WorldDeltaKind.BlockSet || delta.Kind == WorldDeltaKind.ColumnSurfaceSet)
                {
                    OnTerrainColumnChanged(delta.X, delta.Z);
                }
            }
            _world.Deltas.TerrainColumnChanged += OnTerrainColumnChanged;
        }

        public void SetRenderDistance(int renderDistance)
        {
            _vertexBudget = WorldStreamingPolicy.AlphaVertexBudget(renderDistance);
            _planDirty = true;
        }

        private void LateUpdate()
        {
            if (_camera == null || !_hasCenter || !_policy.UsesAlphaWindow || !_ownsTerrainMaterial) return;

            // 0x488EE0 starts at the half width of the window, then tests missing chunk origins.
            Vector3 eye = _camera.transform.position;
            float halfWidth = WorldStreamingPolicy.AlphaChunkRadius * WorldLayout.ChunkSize;
            float missingDistanceSquared = halfWidth * halfWidth;
            foreach (ChunkKey key in _window)
            {
                if (_loaded.ContainsKey(key)) continue;

                Vector3 position = ChunkScenePosition(key);
                float dx = position.x - eye.x;
                float dz = position.z - eye.z;
                missingDistanceSquared = Mathf.Min(missingDistanceSquared, dx * dx + dz * dz);
            }
            float targetRange = Mathf.Max(0.0001f, Mathf.Sqrt(missingDistanceSquared) - 22.4f);
            float weight = 1f - Mathf.Pow(1f - 0.001f, Time.unscaledDeltaTime * 1000f);
            _horizonRange = Mathf.Lerp(_horizonRange, targetRange, weight);
            _terrainMaterial.SetFloat(HorizonInverseRange, 1f / _horizonRange);
            _terrainMaterial.SetColor(HorizonColor, _camera.backgroundColor);
            if (_waterMaterial != null)
            {
                _waterMaterial.SetFloat(HorizonInverseRange, 1f / _horizonRange);
                _waterMaterial.SetColor(HorizonColor, _camera.backgroundColor);
            }
        }

        public void SetDaylightBrightness(float brightness)
        {
            if (_ownsTerrainMaterial)
            {
                _terrainMaterial.SetFloat(DaylightBrightness, brightness);
            }

            if (_waterMaterial != null)
            {
                _waterMaterial.SetFloat(DaylightBrightness, brightness);
            }
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
                        Install(ChunkVoxels.Sample(_world.Generator, key), null);
                    }
                }
            }

            SetFocus(sceneFocus);
        }

        /// <summary>Waits (blocking) until no chunk is pending — tests and loading screens only.</summary>
        public void CompletePending(int timeoutMs = 60000)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (PendingCount > 0 && timer.ElapsedMilliseconds < timeoutMs)
            {
                Pump(drain: true);
                Thread.Sleep(1);
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

            ToWorld(sceneFocus, out double x, out _, out double z);
            int voxelX = (int)Math.Floor(x);
            int voxelZ = (int)Math.Floor(z);
            if (_terrain != null && (!_hasCenter || voxelX != _focusX || voxelZ != _focusZ))
            {
                _focusX = voxelX;
                _focusZ = voxelZ;
                _zoneOrder = WorldStreamingPolicy.GenerationOrder(voxelX, voxelZ);
            }

            ChunkKey center = WorldLayout.ChunkOf(voxelX, voxelZ);
            if (!_hasCenter || !center.Equals(_center))
            {
                _center = center;
                _hasCenter = true;
                _world.Deltas.MarkDiscovered(WorldLayout.ZoneOf(voxelX, voxelZ));
                _window = _policy.Wanted(_center);
                _planDirty = true;
            }

            Pump();
        }

        public void Clear()
        {
            if (_world != null)
            {
                _world.Deltas.TerrainColumnChanged -= OnTerrainColumnChanged;
            }

            foreach (ChunkKey key in new List<ChunkKey>(_loaded.Keys))
            {
                Release(key, false);
            }

            // Finishing workers can only enqueue into the queue of their original world.
            _ready = new ConcurrentQueue<ChunkBuild>();
            _completed.Clear();
            _terrain = null;
            _vegetation?.Clear();
            _vegetation = null;
            _camera = null;
            _zoneWorker = null;
            _zoneOrder.Clear();
            _window.Clear();
            _eligible.Clear();
            _edited.Clear();

            _inFlight.Clear();
            _wanted.Clear();
            _dirty.Clear();
            _dirtyOrder.Clear();
            _heights.Clear();
            _hasCenter = false;
            _world = null;
            if (_ownsTerrainMaterial)
            {
                Destroy(_terrainMaterial);
                _ownsTerrainMaterial = false;
            }
            _terrainMaterial = null;
            if (_waterMaterial != null)
            {
                Destroy(_waterMaterial);
                _waterMaterial = null;
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Replan()
        {
            foreach (ChunkKey key in new List<ChunkKey>(_loaded.Keys))
            {
                if (_policy.ShouldRelease(key, _center)) Release(key, true);
            }

            _wanted.Clear();
            _eligible.Clear();
            long vertices = 0;
            foreach (ChunkKey key in _window)
            {
                if (_policy.UsesAlphaWindow && vertices > _vertexBudget)
                {
                    Release(key, true);
                    continue;
                }
                if (_loaded.TryGetValue(key, out ChunkView view))
                {
                    vertices += view.Vertices;
                    continue;
                }
                if (_terrain == null || HasSourceZones(key))
                {
                    _eligible.Add(key);
                    if (!_inFlight.Contains(key)) _wanted.Add(key);
                }
            }
            _wanted.Reverse();
            _planDirty = false;
        }

        private bool HasSourceZones(ChunkKey key)
        {
            int x = key.X * WorldLayout.ChunkSize;
            int z = key.Z * WorldLayout.ChunkSize;
            ZoneKey first = WorldLayout.ZoneOf(x - 1, z - 1);
            ZoneKey last = WorldLayout.ZoneOf(x + WorldLayout.ChunkSize, z + WorldLayout.ChunkSize);
            for (int zx = first.X; zx <= last.X; zx++)
            {
                for (int zz = first.Z; zz <= last.Z; zz++)
                {
                    var zone = new ZoneKey(zx, zz);
                    if (WorldLayout.IsInside(zone) && !_terrain.IsZoneGenerated(zone)) return false;
                }
            }
            return true;
        }

        private void PumpZones()
        {
            if (_terrain == null) return;

            double now = Time.realtimeSinceStartupAsDouble;
            if ((now - _lastEviction) * 1000 > WorldStreamingPolicy.AlphaEvictionIntervalMs)
            {
                _lastEviction = now;
                _terrain.EvictOutsideStreamingRange(WorldLayout.ZoneOf(_focusX, _focusZ), _world.Deltas);
                _planDirty = true;
            }

            if (_zoneWorker != null)
            {
                if (!_zoneWorker.IsCompleted) return;

                Task completed = _zoneWorker;
                _zoneWorker = null;
                completed.GetAwaiter().GetResult();
                _planDirty = true;
            }
            foreach (ZoneKey zone in _zoneOrder)
            {
                if (_terrain.IsZoneGenerated(zone)) continue;

                AlphaTerrainGenerator terrain = _terrain;
                _zoneWorker = Task.Run(() => terrain.Zone(zone.X, zone.Z));
                break;
            }
        }

        private void Pump(bool drain = false)
        {
            PumpZones();
            if (_planDirty) Replan();

            int budget = drain || _policy.UsesAlphaWindow ? int.MaxValue : BuildsPerFrame;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            // A ready backlog must not delay edits to colliders already used by gameplay.
            while (budget > 0 && _dirtyOrder.Count > 0)
            {
                ChunkKey dirty = _dirtyOrder.Dequeue();
                _dirty.Remove(dirty);
                if (_heights.TryGetValue(dirty, out ChunkVoxels generated))
                {
                    Release(dirty, false);
                    Install(generated, null);
                    if (_policy.UsesAlphaWindow && _vertices > _vertexBudget) _planDirty = true;
                    budget--;
                }
            }

            if (_planDirty) Replan();
            if (budget > 0) InstallReady(budget, drain, timer);
            QueueBuilds();
        }

        private void InstallReady(int budget, bool drain, System.Diagnostics.Stopwatch timer)
        {
            while (_ready.TryDequeue(out ChunkBuild ready)) _completed.Add(ready);
            _completed.Sort((a, b) => WorldStreamingPolicy.CompareLoadPriority(b.Key, a.Key, _center));
            while (budget > 0 && _completed.Count > 0)
            {
                int next = _completed.Count - 1;
                ChunkBuild build = _completed[next];
                _completed.RemoveAt(next);
                _inFlight.Remove(build.Key);
                if (build.Error != null) throw new InvalidOperationException($"Terrain build failed for {build.Key}", build.Error);
                if (!_eligible.Contains(build.Key) || _loaded.ContainsKey(build.Key)) continue;

                Install(build.Voxels, build.Meshes);
                budget--;
                if (_policy.UsesAlphaWindow && _vertices > _vertexBudget) Replan();
                if (_policy.UsesAlphaWindow && !drain && timer.ElapsedMilliseconds >= WorldStreamingPolicy.AlphaWorkerDelayMs) break;
            }
        }

        private void QueueBuilds()
        {
            while (_inFlight.Count < _maxWorkers && _wanted.Count > 0)
            {
                int next = _wanted.Count - 1;
                ChunkKey key = _wanted[next];
                _wanted.RemoveAt(next);
                if (_loaded.ContainsKey(key)) continue;

                _inFlight.Add(key);
                ConcurrentQueue<ChunkBuild> ready = _ready;
                IWorldGenerator generator = _world.Generator;
                Task.Run(() =>
                {
                    var build = new ChunkBuild { Key = key };
                    try
                    {
                        build.Voxels = ChunkVoxels.Sample(generator, key);
                        build.Meshes = ChunkMesher.Build(build.Voxels);
                    }
                    catch (Exception exception)
                    {
                        build.Error = exception;
                    }
                    ready.Enqueue(build);
                });
            }
        }

        private void OnTerrainColumnChanged(int x, int z)
        {
            // A chunk mesh also reads the one-column border around its own columns.
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    ChunkKey key = WorldLayout.ChunkOf(x + dx, z + dz);
                    _edited.Add(key);
                    if (_loaded.ContainsKey(key) && _dirty.Add(key))
                    {
                        _dirtyOrder.Enqueue(key);
                    }
                }
            }
        }

        private Vector3 ChunkScenePosition(ChunkKey key)
        {
            return new Vector3(key.X * WorldLayout.ChunkSize - Origin.x, 0f, key.Z * WorldLayout.ChunkSize - Origin.y);
        }

        private void Install(ChunkVoxels generated, ChunkMesher.Result data)
        {
            ChunkKey key = generated.Key;
            ChunkVoxels heights = generated;
            if (_edited.Contains(key))
            {
                // Apply the current deltas even if an edit arrived while the worker was meshing.
                heights = generated.Copy();
                heights.ApplyDeltas(_world.Deltas);
                data = ChunkMesher.Build(heights);
            }
            data ??= ChunkMesher.Build(generated);

            var root = new GameObject($"Chunk_{key.X}_{key.Z}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = ChunkScenePosition(key);

            Mesh terrain = UnityMeshFactory.Create(data.Terrain, $"ChunkMesh_{key.X}_{key.Z}");
            root.AddComponent<MeshFilter>().sharedMesh = terrain;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _terrainMaterial;
            root.AddComponent<MeshCollider>().sharedMesh = terrain;

            Mesh water = null;
            if (data.Water.VertexCount > 0)
            {
                water = UnityMeshFactory.Create(data.Water, $"WaterMesh_{key.X}_{key.Z}");
                var waterObject = new GameObject("Water");
                waterObject.transform.SetParent(root.transform, false);
                waterObject.AddComponent<MeshFilter>().sharedMesh = water;
                waterObject.AddComponent<MeshRenderer>().sharedMaterial = _waterMaterial ?? _terrainMaterial;
            }

            Mesh vegetation = _vegetation.Build(heights, out int plantCount);
            if (vegetation != null)
            {
                var plants = new GameObject("Vegetation");
                plants.transform.SetParent(root.transform, false);
                plants.AddComponent<MeshFilter>().sharedMesh = vegetation;
                plants.AddComponent<MeshRenderer>().sharedMaterial = _terrainMaterial;
                LoadedPlants += plantCount;
            }

            _loaded[key] = new ChunkView
            {
                Root = root,
                Terrain = terrain,
                Water = water,
                Vegetation = vegetation,
                PlantCount = plantCount,
                Vertices = data.Terrain.VertexCount + data.Water.VertexCount,
            };
            _heights[key] = generated;
            _vertices += _loaded[key].Vertices;
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
            _vertices -= view.Vertices;
            LoadedPlants -= view.PlantCount;
            if (countAsUnload)
            {
                TotalReleased++;
                if (!_policy.UsesAlphaWindow)
                {
                    _world?.Deltas.MarkDiscovered(WorldLayout.ZoneOf(key.X * WorldLayout.ChunkSize, key.Z * WorldLayout.ChunkSize));
                }
            }

            if (view.Root != null)
            {
                view.Root.SetActive(false);
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
            if (view.Vegetation != null)
            {
                Destroy(view.Vegetation);
            }
        }
    }
}
