using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Features.Session
{
    /// <summary>
    /// Alpha missions of the hero's world (P7-A, docs/research/alpha2013/P7_MISSIONS_TRACE.md):
    /// <list type="bullet">
    /// <item>region pass 0x5D9AD0 once per region the hero stands in (<see cref="EnsureRegion"/>);</item>
    /// <item>activation 0x60CC3D: the column under the hero, mission sleeping, hero inside the landmark
    /// (0x5FA4C0 weight &gt; 0) → active;</item>
    /// <item>completion 0x59642A: a kill by the hero of a creature with flag 0x2000 in the column — in Alpha only
    /// the column boss of a type-1 mission carries it (0x5D9730). Types 1–5 and 13 complete at once, 7–12 count.</item>
    /// </list>
    /// Missions of type 3, 4 and 9 get no flagged target in Alpha and so never complete; type 5 completes on the
    /// dungeon master (+0x1D38), which CubeGame does not have yet (P7 dungeons) — both kept as in Alpha.
    /// CUBEGAME_CONTRACT: the journal lives in the session save (Alpha: world database blobs mission&lt;x&gt;_&lt;z&gt;),
    /// one random stream per region, the region is the one containing the hero (Alpha selects it through the
    /// nearest region lookup).
    /// </summary>
    public sealed class MissionJournal
    {
        /// <summary>Population channel of column bosses (0 creatures, 1 zone content, 2 town NPCs).</summary>
        public const int BossChannel = 3;

        private readonly SortedDictionary<long, AlphaMission> _columns = new SortedDictionary<long, AlphaMission>();
        private readonly HashSet<long> _regions = new HashSet<long>();
        private AlphaTerrainGenerator _terrain;
        private AlphaMissionKindSelector _kindAt;
        private int _worldSeed;

        /// <summary>Every column record with a boss or a mission.</summary>
        public IReadOnlyCollection<AlphaMission> Columns => _columns.Values;

        /// <summary>Columns that carry a mission (any state).</summary>
        public IEnumerable<AlphaMission> Missions
        {
            get
            {
                foreach (AlphaMission mission in _columns.Values)
                {
                    if (mission.HasMission) yield return mission;
                }
            }
        }

        public IEnumerable<(int Rx, int Rz)> GeneratedRegions
        {
            get
            {
                foreach (long key in _regions) yield return ((int)(key >> 32), (int)(key & 0xFFFFFFFF));
            }
        }

        public bool HasWorld => _terrain != null;

        /// <summary>Raised after a region pass created bosses and missions (region x, region z).</summary>
        public event Action<int, int> RegionGenerated;

        /// <summary>Connects the journal to the Alpha terrain of the running world (no world → missions stay idle).</summary>
        public void AttachWorld(AlphaTerrainGenerator terrain, int worldSeed, AlphaMissionKindSelector kindAt)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _worldSeed = worldSeed;
            _kindAt = kindAt;
        }

        public void DetachWorld()
        {
            _terrain = null;
            _kindAt = null;
        }

        public bool IsRegionGenerated(int rx, int rz) => _regions.Contains(RegionId(rx, rz));

        public bool TryGet(int columnX, int columnZ, out AlphaMission mission) =>
            _columns.TryGetValue(ColumnKey(columnX, columnZ), out mission);

        /// <summary>Stable id of the boss of a column (zone of the boss, boss channel, index 0).</summary>
        public static StableEntityId BossId(AlphaMission column) =>
            StableEntityId.ForZoneSpawn(new ZoneKey(column.BossZoneX, column.BossZoneZ), BossChannel, 0);

        /// <summary>Boss placed in this zone by 0x5D9460, if any.</summary>
        public bool TryBossAt(ZoneKey zone, out AlphaMission column)
        {
            if (_columns.TryGetValue(ColumnKey(zone.X >> 3, zone.Z >> 3), out column) && column.HasBoss
                && column.BossZoneX == zone.X && column.BossZoneZ == zone.Z)
            {
                return true;
            }

            column = null;
            return false;
        }

        /// <summary>Runs the region pass for the hero's region once. Returns true when it ran now.</summary>
        internal bool EnsureRegion(WorldPosition hero, int heroLevel)
        {
            if (_terrain == null || !Voxel(hero, out int x, out int z))
            {
                return false;
            }

            int rx = x >> 14;
            int rz = z >> 14;
            if (!_regions.Add(RegionId(rx, rz)))
            {
                return false;
            }

            var random = new AlphaCrtRandom(AlphaMissionGenerator.RegionSeed(_worldSeed, rx, rz));
            foreach (AlphaMission mission in AlphaMissionGenerator.GenerateRegion(_terrain, rx, rz, heroLevel, heroLevel,
                random, _kindAt))
            {
                _columns[ColumnKey(mission.ColumnX, mission.ColumnZ)] = mission;
            }

            RegionGenerated?.Invoke(rx, rz);
            return true;
        }

        /// <summary>0x60CC3D…0x60CCA6.</summary>
        internal bool TryActivate(WorldPosition hero, out AlphaMission activated)
        {
            activated = null;
            if (_terrain == null || !Voxel(hero, out int x, out int z)
                || !_columns.TryGetValue(ColumnKey(x >> 11, z >> 11), out AlphaMission mission)
                || !mission.HasMission || mission.State != AlphaMissionState.Sleeping)
            {
                return false;
            }

            LandmarkColumn column = _terrain.ColumnAt(x, z);
            if (column == null || !(AlphaTerrainGenerator.Falloff(_terrain.LandmarkDistance2(column, x, z)) > 0f))
            {
                return false;
            }

            mission.State = AlphaMissionState.Active;
            activated = mission;
            return true;
        }

        /// <summary>0x59642A…0x596517 for a creature killed by the hero. Returns true when the mission finished now.</summary>
        internal bool TryComplete(StableEntityId target, out AlphaMission completed)
        {
            completed = null;
            if (target.Kind != EntityIdKind.ZoneSpawn || (int)((target.Value >> 12) & 0xF) != BossChannel)
            {
                return false;
            }

            var zone = new ZoneKey((int)((target.Value >> 32) & 0xFFFF), (int)((target.Value >> 16) & 0xFFFF));
            if (!TryBossAt(zone, out AlphaMission mission) || !mission.BossIsMissionTarget
                || mission.State == AlphaMissionState.Complete)
            {
                return false;
            }

            if (mission.CountsKills)
            {
                mission.Progress++;
                mission.State = AlphaMissionState.Active;
                if (mission.Progress < mission.Required)
                {
                    return false;
                }
            }

            mission.State = AlphaMissionState.Complete;
            completed = mission;
            return true;
        }

        internal bool TryRestore(AlphaMission mission)
        {
            if (mission == null)
            {
                return false;
            }

            long key = ColumnKey(mission.ColumnX, mission.ColumnZ);
            if (_columns.ContainsKey(key))
            {
                return false;
            }

            _columns.Add(key, mission);
            return true;
        }

        internal bool TryRestoreRegion(int rx, int rz) =>
            rx >= 0 && rz >= 0 && rx < WorldLayout.RegionGrid && rz < WorldLayout.RegionGrid && _regions.Add(RegionId(rx, rz));

        private static bool Voxel(WorldPosition position, out int x, out int z)
        {
            x = (int)Math.Floor(position.X);
            z = (int)Math.Floor(position.Z);
            return x >= 0 && z >= 0 && x < WorldLayout.WorldVoxels && z < WorldLayout.WorldVoxels;
        }

        private static long ColumnKey(int columnX, int columnZ) => ((long)columnX << 16) | (uint)columnZ;

        private static long RegionId(int rx, int rz) => ((long)rx << 32) | (uint)rz;
    }
}
