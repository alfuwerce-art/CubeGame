using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Persistent world changes (voxel coordinates) relative to generator output (M0-00C §8), owned by the running session.
    /// Alpha marks player-changed voxels with byte-3 flag 0x80 (W-13) and writes the "discovered" blob when a
    /// zone unloads (W-12); CubeGame keeps both here and saves them with the slot.
    /// Terrain edits: <see cref="WorldDeltaKind.BlockSet"/> (one voxel, Value = packed Alpha voxel uint) and
    /// <see cref="WorldDeltaKind.ColumnSurfaceSet"/> (new top solid Y of one column). Block edits are applied after the
    /// column edit of the same column.
    /// </summary>
    public sealed class WorldDeltaStore
    {
        public const int MaxSurface = 1024;
        public const int MinSurface = -256;

        private readonly Dictionary<RegionKey, Dictionary<(WorldDeltaKind, int, int, int), WorldDelta>> _byRegion =
            new Dictionary<RegionKey, Dictionary<(WorldDeltaKind, int, int, int), WorldDelta>>();

        /// <summary>Block edits per column (x, z) → (y → delta), for building effective voxel columns.</summary>
        private readonly Dictionary<(int, int), Dictionary<int, WorldDelta>> _blocksByColumn = new Dictionary<(int, int), Dictionary<int, WorldDelta>>();
        private readonly HashSet<ZoneKey> _discovered = new HashSet<ZoneKey>();

        /// <summary>Raised after a change was recorded (presentation rebuilds the region view).</summary>
        public event Action<RegionKey> RegionChanged;

        /// <summary>Raised for the column of a newly recorded terrain edit.</summary>
        public event Action<int, int> TerrainColumnChanged;

        public int Count
        {
            get
            {
                int n = 0;
                foreach (Dictionary<(WorldDeltaKind, int, int, int), WorldDelta> region in _byRegion.Values)
                {
                    n += region.Count;
                }

                return n;
            }
        }

        /// <summary>Explored Alpha zones (256×256 voxels).</summary>
        public IReadOnlyCollection<ZoneKey> Discovered => _discovered;

        public bool SetColumnSurface(int x, int z, int surface, long timeMs)
        {
            if (!WorldLayout.IsInsideVoxels(x, z) || surface < MinSurface || surface > MaxSurface)
            {
                return false;
            }

            RegionKey region = WorldLayout.RegionOf(x, z);
            Record(new WorldDelta
            {
                Region = region,
                Kind = WorldDeltaKind.ColumnSurfaceSet,
                X = x,
                Y = surface,
                Z = z,
                Value = surface,
                TimeMs = timeMs,
            });
            return true;
        }

        /// <summary>Sets one voxel (packed Alpha voxel: RGB + flags; 0 = air).</summary>
        public bool SetVoxel(int x, int y, int z, uint voxel, long timeMs)
        {
            if (!WorldLayout.IsInsideVoxels(x, z) || y < MinSurface || y > MaxSurface)
            {
                return false;
            }

            Record(new WorldDelta
            {
                Region = WorldLayout.RegionOf(x, z),
                Kind = WorldDeltaKind.BlockSet,
                X = x,
                Y = y,
                Z = z,
                Value = unchecked((int)voxel),
                TimeMs = timeMs,
            });
            return true;
        }

        public bool TryGetVoxel(int x, int y, int z, out uint voxel)
        {
            if (_blocksByColumn.TryGetValue((x, z), out Dictionary<int, WorldDelta> column) && column.TryGetValue(y, out WorldDelta delta))
            {
                voxel = unchecked((uint)delta.Value);
                return true;
            }

            voxel = 0u;
            return false;
        }

        /// <summary>True when column (x, z) has any terrain edit (surface or block).</summary>
        public bool HasColumnEdits(int x, int z)
        {
            return _blocksByColumn.ContainsKey((x, z)) || TryGetColumnSurface(x, z, out _);
        }

        /// <summary>Block edits of column (x, z) (y → packed voxel); empty when none.</summary>
        public IEnumerable<KeyValuePair<int, uint>> BlocksOf(int x, int z)
        {
            if (!_blocksByColumn.TryGetValue((x, z), out Dictionary<int, WorldDelta> column))
            {
                yield break;
            }

            foreach (KeyValuePair<int, WorldDelta> pair in column)
            {
                yield return new KeyValuePair<int, uint>(pair.Key, unchecked((uint)pair.Value.Value));
            }
        }

        /// <summary>Adds a delta; a later delta for the same (kind, x, y, z) key replaces the earlier one.</summary>
        public void Record(WorldDelta delta)
        {
            if (delta == null)
            {
                throw new ArgumentNullException(nameof(delta));
            }

            if (!_byRegion.TryGetValue(delta.Region, out Dictionary<(WorldDeltaKind, int, int, int), WorldDelta> map))
            {
                map = new Dictionary<(WorldDeltaKind, int, int, int), WorldDelta>();
                _byRegion.Add(delta.Region, map);
            }

            map[KeyOf(delta)] = delta;
            if (delta.Kind == WorldDeltaKind.BlockSet)
            {
                if (!_blocksByColumn.TryGetValue((delta.X, delta.Z), out Dictionary<int, WorldDelta> column))
                {
                    column = new Dictionary<int, WorldDelta>();
                    _blocksByColumn.Add((delta.X, delta.Z), column);
                }

                column[delta.Y] = delta;
            }
            if (delta.Kind == WorldDeltaKind.BlockSet || delta.Kind == WorldDeltaKind.ColumnSurfaceSet)
            {
                TerrainColumnChanged?.Invoke(delta.X, delta.Z);
            }
            RegionChanged?.Invoke(delta.Region);
        }

        public bool TryGetColumnSurface(int x, int z, out int surface)
        {
            RegionKey region = WorldLayout.RegionOf(x, z);
            if (_byRegion.TryGetValue(region, out Dictionary<(WorldDeltaKind, int, int, int), WorldDelta> map)
                && map.TryGetValue((WorldDeltaKind.ColumnSurfaceSet, x, 0, z), out WorldDelta delta))
            {
                surface = delta.Value;
                return true;
            }

            surface = 0;
            return false;
        }

        public IReadOnlyCollection<WorldDelta> DeltasOf(RegionKey region)
        {
            return _byRegion.TryGetValue(region, out Dictionary<(WorldDeltaKind, int, int, int), WorldDelta> map)
                ? (IReadOnlyCollection<WorldDelta>)map.Values
                : Array.Empty<WorldDelta>();
        }

        /// <summary>World-object removals use the same zone/channel/index coordinates as entity state.</summary>
        public bool IsObjectRemoved(ZoneKey zone, int channel, int spawnIndex)
        {
            var region = new RegionKey(zone.X >> 6, zone.Z >> 6);
            return _byRegion.TryGetValue(region, out var map)
                && map.ContainsKey((WorldDeltaKind.ObjectRemoved, zone.X, channel * 4096 + spawnIndex, zone.Z));
        }

        /// <summary>Discards a completed entity state without touching terrain or object removals.</summary>
        public void RemoveEntityState(WorldDelta delta)
        {
            if (delta.Kind == WorldDeltaKind.EntityStateChanged && _byRegion.TryGetValue(delta.Region, out var map))
            {
                map.Remove(KeyOf(delta));
            }
        }

        public IEnumerable<WorldDelta> All()
        {
            foreach (Dictionary<(WorldDeltaKind, int, int, int), WorldDelta> map in _byRegion.Values)
            {
                foreach (WorldDelta delta in map.Values)
                {
                    yield return delta;
                }
            }
        }

        /// <summary>Marks a zone as explored (Alpha writes "discovered" when a zone unloads; CubeGame also on entry).</summary>
        public bool MarkDiscovered(ZoneKey zone)
        {
            const int zones = WorldLayout.RegionGrid * WorldLayout.ZonesPerRegion;
            return zone.X >= 0 && zone.Z >= 0 && zone.X < zones && zone.Z < zones && _discovered.Add(zone);
        }

        public bool IsDiscovered(ZoneKey zone) => _discovered.Contains(zone);

        /// <summary>Delta identity: a column edit is keyed by its column only (its Y is the new surface).</summary>
        private static (WorldDeltaKind, int, int, int) KeyOf(WorldDelta delta)
        {
            return delta.Kind == WorldDeltaKind.ColumnSurfaceSet
                ? (delta.Kind, delta.X, 0, delta.Z)
                : (delta.Kind, delta.X, delta.Y, delta.Z);
        }
    }

    /// <summary>
    /// Pure streaming decision (C_streaming §C2: Alpha keeps zone data within 8 zones and frees view tiles past 10).
    /// CubeGame separates the same two levels: <see cref="ViewRadius"/> (meshes/colliders) and a larger
    /// <see cref="KeepRadius"/> before a view is released (hysteresis). Radii are CubeGame decisions
    /// (PLACEHOLDER until profiling, C6.4), measured in 32-voxel chunks (Chebyshev distance).
    /// </summary>
    public sealed class WorldStreamingPolicy
    {
        public WorldStreamingPolicy(int viewRadius = 6, int keepRadius = 8)
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

        /// <summary>Chunks that must be shown around <paramref name="center"/>, nearest first.</summary>
        public List<ChunkKey> Wanted(ChunkKey center)
        {
            var result = new List<ChunkKey>();
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

                        var key = new ChunkKey(center.X + dx, center.Z + dz);
                        if (WorldLayout.IsInside(key))
                        {
                            result.Add(key);
                        }
                    }
                }
            }

            return result;
        }

        public bool ShouldRelease(ChunkKey loaded, ChunkKey center)
        {
            return WorldLayout.Chebyshev(loaded, center) > KeepRadius;
        }
    }
}
