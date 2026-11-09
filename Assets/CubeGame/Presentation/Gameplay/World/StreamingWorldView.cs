using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using CubeGame.Presentation.Rendering;
using UnityEngine;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>
    /// Presentation of the streamed world: one GameObject (mesh + collider, water mesh) per 64×64 region around a focus
    /// point. Builds at most <see cref="BuildsPerFrame"/> regions per frame (nearest first), releases regions beyond the
    /// keep radius and marks them discovered (Alpha writes "discovered" on zone unload, W-12). Rebuilds a loaded region
    /// when a world delta touches it. Owns no game state: everything comes from <see cref="WorldState"/>.
    /// </summary>
    public sealed class StreamingWorldView : MonoBehaviour
    {
        private readonly Dictionary<RegionKey, RegionView> _loaded = new Dictionary<RegionKey, RegionView>();
        private readonly List<RegionKey> _queue = new List<RegionKey>();
        private readonly HashSet<RegionKey> _dirty = new HashSet<RegionKey>();

        private WorldState _world;
        private WorldStreamingPolicy _policy;
        private Material _terrainMaterial;
        private RegionKey _center;
        private bool _hasCenter;

        private sealed class RegionView
        {
            public GameObject Root;
            public Mesh Terrain;
            public Mesh Water;
            public int Vertices;
        }

        public int BuildsPerFrame { get; set; } = 2;

        public int LoadedCount => _loaded.Count;

        public int PendingCount => _queue.Count;

        public long TotalBuilt { get; private set; }

        public long TotalReleased { get; private set; }

        public double LastBuildMilliseconds { get; private set; }

        public RegionKey Center => _center;

        public WorldState World => _world;

        public int LoadedVertices
        {
            get
            {
                int n = 0;
                foreach (RegionView view in _loaded.Values)
                {
                    n += view.Vertices;
                }

                return n;
            }
        }

        public bool IsLoaded(RegionKey region) => _loaded.ContainsKey(region);

        public IEnumerable<RegionKey> LoadedRegions => _loaded.Keys;

        public void Initialise(WorldState world, WorldStreamingPolicy policy, Material terrainMaterial)
        {
            Clear();
            _world = world;
            _policy = policy;
            _terrainMaterial = terrainMaterial;
            _world.Deltas.RegionChanged += OnRegionChanged;
        }

        /// <summary>Builds every region within <paramref name="radius"/> of the focus synchronously (spawn, load).</summary>
        public void LoadAround(Vector3 focus, int radius)
        {
            RegionKey center = RegionAt(focus);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    var key = new RegionKey(center.X + dx, center.Z + dz);
                    if (WorldLayout.IsInside(key) && !_loaded.ContainsKey(key))
                    {
                        Build(key);
                    }
                }
            }

            SetFocus(focus);
        }

        /// <summary>Per-frame streaming step for the given focus point.</summary>
        public void SetFocus(Vector3 focus)
        {
            if (_world == null)
            {
                return;
            }

            RegionKey center = RegionAt(focus);
            if (!_hasCenter || !center.Equals(_center))
            {
                _center = center;
                _hasCenter = true;
                _world.Deltas.MarkDiscovered(center);
                Replan();
            }

            int budget = BuildsPerFrame;
            foreach (RegionKey dirty in new List<RegionKey>(_dirty))
            {
                if (!_loaded.ContainsKey(dirty))
                {
                    _dirty.Remove(dirty);
                    continue;
                }

                if (budget <= 0)
                {
                    break;
                }

                _dirty.Remove(dirty);
                Release(dirty, false);
                Build(dirty);
                budget--;
            }

            while (budget > 0 && _queue.Count > 0)
            {
                RegionKey next = _queue[0];
                _queue.RemoveAt(0);
                if (!_loaded.ContainsKey(next))
                {
                    Build(next);
                    budget--;
                }
            }
        }

        public void Clear()
        {
            if (_world != null)
            {
                _world.Deltas.RegionChanged -= OnRegionChanged;
            }

            foreach (RegionKey key in new List<RegionKey>(_loaded.Keys))
            {
                Release(key, false);
            }

            _queue.Clear();
            _dirty.Clear();
            _hasCenter = false;
            _world = null;
        }

        private void OnDestroy()
        {
            Clear();
        }

        public static RegionKey RegionAt(Vector3 position)
        {
            return WorldLayout.RegionOf(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.z));
        }

        private void Replan()
        {
            foreach (RegionKey key in new List<RegionKey>(_loaded.Keys))
            {
                if (_policy.ShouldRelease(key, _center))
                {
                    Release(key, true);
                }
            }

            _queue.Clear();
            foreach (RegionKey key in _policy.Wanted(_center))
            {
                if (!_loaded.ContainsKey(key))
                {
                    _queue.Add(key);
                }
            }
        }

        private void OnRegionChanged(RegionKey region)
        {
            _dirty.Add(region);

            // A column edit on a region border changes the neighbour's side walls too.
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    _dirty.Add(new RegionKey(region.X + dx, region.Z + dz));
                }
            }
        }

        private void Build(RegionKey key)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            RegionMesher.Result data = RegionMesher.Build(_world, key);

            var root = new GameObject($"Region_{key.X}_{key.Z}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(key.X * WorldLayout.RegionSize, 0f, key.Z * WorldLayout.RegionSize);

            Mesh terrain = UnityMeshFactory.Create(data.Terrain, $"RegionMesh_{key.X}_{key.Z}");
            root.AddComponent<MeshFilter>().sharedMesh = terrain;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = PilotMaterials.Terrain(_terrainMaterial);
            root.AddComponent<MeshCollider>().sharedMesh = terrain;

            Mesh water = null;
            if (data.Water.VertexCount > 0)
            {
                water = UnityMeshFactory.Create(data.Water, $"WaterMesh_{key.X}_{key.Z}");
                var waterObject = new GameObject("Water_PLACEHOLDER");
                waterObject.transform.SetParent(root.transform, false);
                waterObject.AddComponent<MeshFilter>().sharedMesh = water;
                waterObject.AddComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;
            }

            _loaded[key] = new RegionView
            {
                Root = root,
                Terrain = terrain,
                Water = water,
                Vertices = data.Terrain.VertexCount + data.Water.VertexCount,
            };
            TotalBuilt++;
            stopwatch.Stop();
            LastBuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        }

        private void Release(RegionKey key, bool countAsUnload)
        {
            if (!_loaded.TryGetValue(key, out RegionView view))
            {
                return;
            }

            _loaded.Remove(key);
            if (countAsUnload)
            {
                TotalReleased++;
                _world?.Deltas.MarkDiscovered(key);
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
