using System;
using System.Collections.Generic;
using CubeGame.Features.World;

namespace CubeGame.Features.Session
{
    /// <summary>Alpha mission type, landmark column +0x34 (table of target names at _library/crt_stl.cpp:5404).</summary>
    public enum AlphaMissionType
    {
        None = 0,
        Monster = 1,
        RareBoss = 2,
        VillageMonster = 3,
        VillageRareBoss = 4,
        Dungeon = 5,
        NoTarget = 6,
        Invasion = 7,
        SceneryInvasion = 8,
        VillageInvasion = 9,
        CaveInvasion = 10,
        RiverInvasion = 11,
        Gang = 12,
        GangBoss = 13,
    }

    /// <summary>Column +0x41: 0 sleeping, 1 active (0x60CCA6), 2 complete (0x596517).</summary>
    public enum AlphaMissionState
    {
        Sleeping = 0,
        Active = 1,
        Complete = 2,
    }

    /// <summary>
    /// Mission and boss block of one landmark column (Alpha record 0x68, fields +0x24 and +0x30…+0x64;
    /// docs/research/alpha2013/P7_MISSIONS_TRACE.md). Column coordinates are global: voxel &gt;&gt; 11.
    /// </summary>
    public sealed class AlphaMission
    {
        public AlphaMission(int columnX, int columnZ)
        {
            ColumnX = columnX;
            ColumnZ = columnZ;
        }

        public int ColumnX { get; }

        public int ColumnZ { get; }

        public int RegionX => ColumnX >> 3;

        public int RegionZ => ColumnZ >> 3;

        /// <summary>+0x24: landmark level of the column (display and level gate).</summary>
        public int Level { get; internal set; } = 1;

        /// <summary>+0x30: rand() of the generator.</summary>
        public int Seed { get; internal set; }

        /// <summary>+0x34.</summary>
        public AlphaMissionType Type { get; internal set; }

        /// <summary>+0x38: kind of the mission target (boss, invasion or zone creature).</summary>
        public int TargetKind { get; internal set; }

        /// <summary>+0x3C: highest player level when the mission was generated.</summary>
        public int MaxLevel { get; internal set; }

        /// <summary>+0x40: column level class byte (+0x28).</summary>
        public int LevelClass { get; internal set; }

        /// <summary>+0x41.</summary>
        public AlphaMissionState State { get; internal set; }

        /// <summary>+0x44.</summary>
        public int Progress { get; internal set; }

        /// <summary>+0x48.</summary>
        public int Required { get; internal set; }

        /// <summary>+0x4C / +0x50: target zone, −1 when the mission has no zone.</summary>
        public int TargetZoneX { get; internal set; } = -1;

        public int TargetZoneZ { get; internal set; } = -1;

        /// <summary>+0x54: boss kind of a column without landmark (0 = no boss).</summary>
        public int BossKind { get; internal set; }

        /// <summary>+0x58.</summary>
        public int BossLevel { get; internal set; }

        /// <summary>+0x5C.</summary>
        public int BossQuality { get; internal set; }

        /// <summary>+0x60 / +0x64: zone where 0x5D9460 places the boss.</summary>
        public int BossZoneX { get; internal set; }

        public int BossZoneZ { get; internal set; }

        public bool HasMission => Type != AlphaMissionType.None;

        public bool HasBoss => BossKind != 0;

        /// <summary>0x5D9730: the boss carries flag 0x2000 (mission target) only when the column mission is type 1.</summary>
        public bool BossIsMissionTarget => HasBoss && Type == AlphaMissionType.Monster;

        /// <summary>Completion table 0x596C84: types 1–5 and 13 finish with one kill, 7–12 count to <see cref="Required"/>.</summary>
        public bool CountsKills => Type >= AlphaMissionType.Invasion && Type <= AlphaMissionType.Gang;

        internal void ResetMission()
        {
            Type = AlphaMissionType.None;
            State = AlphaMissionState.Sleeping;
            Progress = 0;
            Required = 0;
        }

        internal static AlphaMission Restore(int columnX, int columnZ, int level, int seed, int type, int targetKind,
            int maxLevel, int levelClass, int state, int progress, int required, int targetZoneX, int targetZoneZ,
            int bossKind, int bossLevel, int bossQuality, int bossZoneX, int bossZoneZ)
        {
            if (type < 0 || type > (int)AlphaMissionType.GangBoss || state < 0 || state > (int)AlphaMissionState.Complete)
            {
                return null;
            }

            return new AlphaMission(columnX, columnZ)
            {
                Level = level,
                Seed = seed,
                Type = (AlphaMissionType)type,
                TargetKind = targetKind,
                MaxLevel = maxLevel,
                LevelClass = levelClass,
                State = (AlphaMissionState)state,
                Progress = progress,
                Required = required,
                TargetZoneX = targetZoneX,
                TargetZoneZ = targetZoneZ,
                BossKind = bossKind,
                BossLevel = bossLevel,
                BossQuality = bossQuality,
                BossZoneX = bossZoneX,
                BossZoneZ = bossZoneZ,
            };
        }
    }

    /// <summary>
    /// Alpha 0x5F56C0 (creature kind at a voxel column): x, z, height, mode → kind, drawing from the stream.
    /// Lives in Features.Creatures; the game passes it in.
    /// </summary>
    public delegate int AlphaMissionKindSelector(int x, int z, int height, int mode, AlphaCrtRandom random);

    /// <summary>
    /// Alpha mission generation, ported from Cube.exe bytes (ALPHA_CODE_STATIC):
    /// region pass 0x5D9AD0, column generator 0x5D8750, boss kind 0x5F7820, invasion kind 0x5F7400.
    /// Alpha draws from the global CRT stream; CubeGame seeds one stream per region (<see cref="RegionSeed"/>,
    /// CUBEGAME_CONTRACT) so a region always gives the same missions for the same player level.
    /// </summary>
    public static class AlphaMissionGenerator
    {
        public const string Provenance =
            "ALPHA_CODE_STATIC: 0x5D9AD0/0x5D8750/0x5F7820/0x5F7400; random stream per region = CUBEGAME_CONTRACT";

        /// <summary>0x5F7400: kinds pushed before rand() % 9.</summary>
        public static readonly int[] InvasionKinds = { 0x30, 0x33, 0x60, 0x50, 0x4C, 0x28, 0x2B, 0x2D, 0x34 };

        /// <summary>0x5F7820 returns this for a grid cell outside 0…0x3FF or not generated, without rand().</summary>
        public const int DefaultBossKind = 0x6C;

        public static int RegionSeed(int worldSeed, int rx, int rz) =>
            unchecked(worldSeed * 3 + rz * 0x400 + rx + 0x4D495353);

        /// <summary>
        /// 0x5D9AD0: one pass over the 64 landmark columns of region (rx, rz) that the region owns
        /// (0x5CB0D0 = <see cref="AlphaTerrainGenerator.NearestCellRegion"/>). Returns every column that got a boss
        /// or a mission, in column order (x outer, z inner).
        /// </summary>
        public static List<AlphaMission> GenerateRegion(AlphaTerrainGenerator terrain, int rx, int rz, int minLevel,
            int maxLevel, AlphaCrtRandom random, AlphaMissionKindSelector kindAt)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var result = new List<AlphaMission>();
            AlphaRegion region = terrain.RegionRecord(rx, rz);
            if (region == null)
            {
                return result;
            }

            AlphaClimateCell climate = terrain.Climate.Get(rx, rz);
            var columns = new Dictionary<AlphaMission, LandmarkColumn>();
            var plain = new List<AlphaMission>();
            var current = new List<AlphaMission>();
            var lower = new List<AlphaMission>();
            for (int lx = 0; lx < 8; lx++)
            {
                for (int lz = 0; lz < 8; lz++)
                {
                    LandmarkColumn column = region.Column(lx, lz);
                    if (column == null || !terrain.NearestCellRegion((int)column.X, (int)column.Z, out int ox, out int oz)
                        || ox != rx || oz != rz)
                    {
                        continue;
                    }

                    var mission = new AlphaMission(rx * 8 + lx, rz * 8 + lz) { Level = column.Level };
                    columns.Add(mission, column);
                    if (column.Type == LandmarkType.None)
                    {
                        AssignBoss(mission, terrain, climate, rx, rz, random);
                        plain.Add(mission);
                    }

                    int type = (int)column.Type;
                    if (type != 0 && type != (int)LandmarkType.Unnamed10 && column.Level <= maxLevel + 2
                        && (column.Type != LandmarkType.Village || random.Next() % 50 == 0))
                    {
                        (column.Level >= maxLevel - 2 ? current : lower).Add(mission);
                    }
                }
            }

            for (int i = 0; i < 2; i++)
            {
                AlphaMission picked = null;
                if (random.Next() % 6 == 0 && lower.Count > 0) picked = Take(lower, random);
                else if (current.Count > 0) picked = Take(current, random);
                else if (lower.Count > 0) picked = Take(lower, random);
                if (picked != null) GenerateColumn(picked, columns[picked], terrain, maxLevel, random, kindAt);
            }

            for (int i = 0; i < 2 && plain.Count > 0; i++)
            {
                AlphaMission picked = Take(plain, random);
                GenerateColumn(picked, columns[picked], terrain, maxLevel, random, kindAt);
            }

            foreach (AlphaMission mission in columns.Keys)
            {
                if (mission.HasBoss || mission.HasMission) result.Add(mission);
            }

            result.Sort((a, b) => a.ColumnX != b.ColumnX ? a.ColumnX.CompareTo(b.ColumnX) : a.ColumnZ.CompareTo(b.ColumnZ));
            return result;
        }

        /// <summary>Boss of a column without landmark: level range from the region's climate cell (0x5D9C9F…).</summary>
        private static void AssignBoss(AlphaMission mission, AlphaTerrainGenerator terrain, AlphaClimateCell climate,
            int rx, int rz, AlphaCrtRandom random)
        {
            int low = 1;
            int high = 10;
            if (climate != null)
            {
                if (climate.Field4 < 0.2f)
                {
                    low = 10;
                    high = 20;
                }

                if (climate.Field3 < 0.2f && 0.8f < climate.Field4)
                {
                    low = 15;
                    high = 25;
                }

                if (0.8f < climate.Field3 && 0.8f < climate.Field4)
                {
                    low = 10;
                    high = 20;
                }

                if (climate.Flag)
                {
                    low = 20;
                    high = 30;
                }
            }

            mission.BossQuality = 0;
            mission.BossLevel = random.Next() % (high - low + 1) + low;
            mission.BossKind = BossKind(terrain.Climate.Get(rx, rz), mission.BossLevel, random);
            mission.BossZoneZ = mission.ColumnZ * 8 + 2 + (random.Next() & 3);
            mission.BossZoneX = mission.ColumnX * 8 + 2 + (random.Next() & 3);
        }

        /// <summary>0x5D8750: mission of one column.</summary>
        internal static void GenerateColumn(AlphaMission mission, LandmarkColumn column, AlphaTerrainGenerator terrain,
            int maxLevel, AlphaCrtRandom random, AlphaMissionKindSelector kindAt)
        {
            mission.ResetMission();
            if (column.Type == LandmarkType.Unnamed10)
            {
                return;
            }

            int[] candidates = column.Type == LandmarkType.None ? new[] { 1 }
                : column.Type == LandmarkType.Village ? new[] { 9, 3, 4 }
                : new[] { 5 };
            var type = (AlphaMissionType)candidates[(int)((uint)random.Next() % (uint)candidates.Length)];

            switch (type)
            {
                case AlphaMissionType.Monster:
                    mission.TargetKind = mission.BossKind;
                    mission.TargetZoneX = mission.BossZoneX;
                    mission.TargetZoneZ = mission.BossZoneZ;
                    break;
                case AlphaMissionType.Invasion:
                    mission.TargetKind = InvasionKind(random);
                    mission.Required = (random.Next() & 7) + 25;
                    mission.TargetZoneX = mission.TargetZoneZ = -1;
                    break;
                case AlphaMissionType.CaveInvasion:
                    mission.TargetKind = InvasionKind(random);
                    mission.Required = random.Next() % 5 + 10;
                    mission.TargetZoneX = mission.TargetZoneZ = -1;
                    break;
                case AlphaMissionType.RiverInvasion:
                    mission.TargetKind = InvasionKind(random);
                    mission.Required = random.Next() % 5 + 15;
                    mission.TargetZoneX = mission.TargetZoneZ = -1;
                    break;
                default:
                    if (!BestZone(mission, column, terrain, type, out int zoneX, out int zoneZ))
                    {
                        return;     // 0x5D91C4: no zone inside the landmark → no mission, no tail rand()
                    }

                    int x = zoneX * WorldLayout.ZoneSize + 0x80;
                    int z = zoneZ * WorldLayout.ZoneSize + 0x80;
                    switch (type)
                    {
                        case AlphaMissionType.VillageMonster:
                            // 0x5D9223 passes the column coordinates to a region-grid lookup (Alpha behaviour kept).
                            mission.TargetKind = BossKind(terrain.Climate.Get(mission.ColumnX, mission.ColumnZ), maxLevel, random);
                            break;
                        case AlphaMissionType.RareBoss:
                        case AlphaMissionType.VillageRareBoss:
                            mission.TargetKind = kindAt?.Invoke(x, z, (int)column.Base, 0, random) ?? 0;
                            break;
                        case AlphaMissionType.Gang:
                        case AlphaMissionType.GangBoss:
                            mission.TargetKind = kindAt?.Invoke(x, z, (int)column.Base, 1, random) ?? 0;
                            if (type == AlphaMissionType.Gang) mission.Required = random.Next() % 5 + 6;
                            break;
                        default:
                            mission.TargetKind = InvasionKind(random);
                            mission.Required = random.Next() % 10 + 10;
                            break;
                    }

                    mission.TargetZoneX = zoneX;
                    mission.TargetZoneZ = zoneZ;
                    break;
            }

            mission.Seed = random.Next();
            mission.LevelClass = column.LevelClass;
            mission.MaxLevel = maxLevel;
            mission.Type = type;
        }

        /// <summary>
        /// 0x5D8EC0…0x5D91FC: zone of the column with the largest weight (1 − d²)², d² = 0x5F8E10 to the zone centre;
        /// gang types double the weight of zones with an odd index sum. Descending insertion/intro sort, first wins.
        /// Ties keep scan order (x outer, z inner) — PARTIAL: Alpha's introsort is not stable for 64 entries.
        /// </summary>
        private static bool BestZone(AlphaMission mission, LandmarkColumn column, AlphaTerrainGenerator terrain,
            AlphaMissionType type, out int bestX, out int bestZ)
        {
            bestX = bestZ = -1;
            float best = 0f;
            for (int ix = 0; ix < 8; ix++)
            {
                int zoneX = mission.ColumnX * 8 + ix;
                for (int iz = 0; iz < 8; iz++)
                {
                    int zoneZ = mission.ColumnZ * 8 + iz;
                    float d2 = terrain.LandmarkDistance2(column, zoneX * WorldLayout.ZoneSize + 0x80,
                        zoneZ * WorldLayout.ZoneSize + 0x80);
                    float f = 1.0f - d2;
                    if (!(0f < f))
                    {
                        continue;
                    }

                    float weight = f * f;
                    if (!(0f < weight))
                    {
                        continue;
                    }

                    if ((type == AlphaMissionType.Gang || type == AlphaMissionType.GangBoss) && ((zoneZ + zoneX) & 1) != 0)
                    {
                        weight *= 2.0f;
                    }

                    if (bestX < 0 || weight > best)
                    {
                        best = weight;
                        bestX = zoneX;
                        bestZ = zoneZ;
                    }
                }
            }

            return bestX >= 0;
        }

        /// <summary>0x5F7820: boss kind from the climate cell and level.</summary>
        public static int BossKind(AlphaClimateCell cell, int level, AlphaCrtRandom random)
        {
            if (cell == null)
            {
                return DefaultBossKind;
            }

            bool cold = cell.Field4 <= 0.2f;
            var kinds = new List<int>(8) { cold ? 0x72 : 0x6C, cold ? 0x74 : 0x77 };
            if (level > 10) kinds.Add(0x73);
            if (level > 20) kinds.Add(0x75);
            if (level > 50)
            {
                kinds.Add(cell.Field4 < 0.2f ? 0x71 : cell.Flag ? 0x70 : 0x6F);
                kinds.Add(0x6D);
            }

            if (level > 70) kinds.Add(0x6E);
            return kinds[(int)((uint)random.Next() % (uint)kinds.Count)];
        }

        /// <summary>0x5F7400.</summary>
        public static int InvasionKind(AlphaCrtRandom random) =>
            InvasionKinds[(int)((uint)random.Next() % (uint)InvasionKinds.Length)];

        private static AlphaMission Take(List<AlphaMission> list, AlphaCrtRandom random)
        {
            int index = (int)((uint)random.Next() % (uint)list.Count);
            AlphaMission picked = list[index];
            list.RemoveAt(index);
            return picked;
        }
    }
}
