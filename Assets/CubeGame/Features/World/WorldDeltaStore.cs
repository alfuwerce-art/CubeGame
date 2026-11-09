using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Persistent world changes relative to generator output (M0-00C §8), owned by the running session.
    /// Alpha marks player-changed voxels with byte-3 flag 0x80 (W-13) and writes the "discovered" blob when a
    /// zone unloads (W-12); CubeGame keeps both here and saves them with the slot.
    /// The terrain is still a height field in this package, so the only block edit is
    /// <see cref="WorldDeltaKind.ColumnSurfaceSet"/> (new top-block Y of one column); volumetric edits come with caves.
    /// </summary>
    public sealed class WorldDeltaStore
    {
        public const int MaxSurface = 1024;
        public const int MinSurface = -256;

        private readonly Dictionary<RegionKey, Dictionary<long, WorldDelta>> _byRegion = new Dictionary<RegionKey, Dictionary<long, WorldDelta>>();
        private readonly HashSet<RegionKey> _discovered = new HashSet<RegionKey>();

        /// <summary>Raised after a change was recorded (presentation rebuilds the region view).</summary>
        public event Action<RegionKey> RegionChanged;

        public int Count
        {
            get
            {
                int n = 0;
                foreach (Dictionary<long, WorldDelta> region in _byRegion.Values)
                {
                    n += region.Count;
                }

                return n;
            }
        }

        public IReadOnlyCollection<RegionKey> Discovered => _discovered;

        public bool SetColumnSurface(int blockX, int blockZ, int surface, long timeMs)
        {
            if (!WorldLayout.IsInsideBlocks(blockX, blockZ) || surface < MinSurface || surface > MaxSurface)
            {
                return false;
            }

            RegionKey region = WorldLayout.RegionOf(blockX, blockZ);
            Record(new WorldDelta
            {
                Region = region,
                Kind = WorldDeltaKind.ColumnSurfaceSet,
                X = blockX,
                Y = surface,
                Z = blockZ,
                Value = surface,
                TimeMs = timeMs,
            });
            return true;
        }

        /// <summary>Adds a delta; a later delta for the same (kind, x, y, z) key replaces the earlier one.</summary>
        public void Record(WorldDelta delta)
        {
            if (delta == null)
            {
                throw new ArgumentNullException(nameof(delta));
            }

            if (!_byRegion.TryGetValue(delta.Region, out Dictionary<long, WorldDelta> map))
            {
                map = new Dictionary<long, WorldDelta>();
                _byRegion.Add(delta.Region, map);
            }

            map[KeyOf(delta)] = delta;
            RegionChanged?.Invoke(delta.Region);
        }

        public bool TryGetColumnSurface(int blockX, int blockZ, out int surface)
        {
            RegionKey region = WorldLayout.RegionOf(blockX, blockZ);
            if (_byRegion.TryGetValue(region, out Dictionary<long, WorldDelta> map)
                && map.TryGetValue(ColumnKey(blockX, blockZ), out WorldDelta delta))
            {
                surface = delta.Value;
                return true;
            }

            surface = 0;
            return false;
        }

        public IReadOnlyCollection<WorldDelta> DeltasOf(RegionKey region)
        {
            return _byRegion.TryGetValue(region, out Dictionary<long, WorldDelta> map)
                ? (IReadOnlyCollection<WorldDelta>)map.Values
                : Array.Empty<WorldDelta>();
        }

        public IEnumerable<WorldDelta> All()
        {
            foreach (Dictionary<long, WorldDelta> map in _byRegion.Values)
            {
                foreach (WorldDelta delta in map.Values)
                {
                    yield return delta;
                }
            }
        }

        /// <summary>Marks a region as explored (called when it unloads and when the hero enters it).</summary>
        public bool MarkDiscovered(RegionKey region)
        {
            return WorldLayout.IsInside(region) && _discovered.Add(region);
        }

        public bool IsDiscovered(RegionKey region) => _discovered.Contains(region);

        private static long KeyOf(WorldDelta delta)
        {
            if (delta.Kind == WorldDeltaKind.ColumnSurfaceSet)
            {
                return ColumnKey(delta.X, delta.Z);
            }

            unchecked
            {
                return ((long)(int)delta.Kind << 56) ^ ((long)(delta.X & 0xFFFFF) << 36) ^ ((long)(delta.Y & 0xFFF) << 24) ^ (delta.Z & 0xFFFFFF);
            }
        }

        private static long ColumnKey(int x, int z)
        {
            return ((long)WorldDeltaKind.ColumnSurfaceSet << 56) | ((long)(x & 0xFFFFFF) << 24) | (long)(z & 0xFFFFFF);
        }
    }

    /// <summary>
    /// Pure streaming decision (C_streaming §C2: Alpha keeps zone data within 8 zones and frees view tiles past 10).
    /// CubeGame separates the same two levels: <see cref="ViewRadius"/> (meshes/colliders) and a larger
    /// <see cref="KeepRadius"/> before a view is released (hysteresis). Radii are CubeGame decisions
    /// (PLACEHOLDER until profiling, C6.4), measured in regions (Chebyshev distance).
    /// </summary>
    public sealed class WorldStreamingPolicy
    {
        public WorldStreamingPolicy(int viewRadius = 2, int keepRadius = 3)
        {
            if (viewRadius < 0 || keepRadius < viewRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(keepRadius));
            }

            ViewRadius = viewRadius;
            KeepRadius = keepRadius;
        }

        public int ViewRadius { get; }

        public int KeepRadius { get; }

        /// <summary>Regions that must be shown around <paramref name="center"/>, nearest first.</summary>
        public List<RegionKey> Wanted(RegionKey center)
        {
            var result = new List<RegionKey>();
            for (int ring = 0; ring <= ViewRadius; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring)
                        {
                            continue;
                        }

                        var key = new RegionKey(center.X + dx, center.Z + dz);
                        if (WorldLayout.IsInside(key))
                        {
                            result.Add(key);
                        }
                    }
                }
            }

            return result;
        }

        public bool ShouldRelease(RegionKey loaded, RegionKey center)
        {
            return WorldLayout.Chebyshev(loaded, center) > KeepRadius;
        }
    }
}
