// CubeGame.Features.Creatures — NPC schedule and waypoint patrol recovered from Alpha (D2 REVIEW-FIX).
//
// Two behaviours of the Alpha behaviour tree, re-read against the decompilation and checked by bytes where
// stated (tools/alpha_models/world/spawn_pipeline_evidence.py):
//
// SpawnLocationBehavior (tick 0x4DFAC0…0x4E0050):
//   * the creature holds a list of {position, time-of-day ms} (stride 0x20, time at +0x18), copied from the
//     template's list at +0x10A0;
//   * active entry = the LAST entry whose time <= world time of day (+0x80015C); none qualifies → entry 0;
//   * when the active entry differs from the target, the creature paths to that entry;
//   * on arrival the entry becomes "arrived" and its position is written to the creature home point
//     (+0x1C0…+0x1D4), which RandomWalkBehavior uses as its leash.
// WalkPathBehavior (tick up to 0x58D630):
//   * points with stride 0x18; a pause timer counts down by the frame time and the behaviour does nothing
//     while it is above 0;
//   * the current point is tried at up to 21 heights to find a walkable spot (not modelled: needs voxels);
//   * closer than the arrival radius (constructor argument: 2.0 for village NPCs) → stop, pause =
//     rand()%4000 + 2000 ms (bytes verified), index = (index + 1) % count; otherwise path to the point.
// RandomInteractionBehavior: the day set of spots is used for 06:00 <= time < 23:00 (0x4DB404/0x4DB411,
//   bytes verified); the content of the spot lists is PARTIAL and is not implemented here.
//
// Status: ALPHA_DECOMPILED for the control flow, byte-verified constants as listed. What the entries/points
// ARE in a generated village (which house, which room) is PARTIAL: the lists are filled by the village
// generator 0x5AF170, not reproduced. These classes run the rule on whatever list the game supplies.
using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Creatures
{
    /// <summary>One entry of an NPC's daily schedule: where to be from <see cref="TimeMs"/> on.</summary>
    public readonly struct AlphaScheduleEntry
    {
        public readonly WorldPosition Position;
        public readonly int TimeMs;

        public AlphaScheduleEntry(WorldPosition position, int timeMs)
        {
            Position = position;
            TimeMs = timeMs;
        }
    }

    /// <summary>SpawnLocationBehavior rule: pick the schedule entry by world time, go there, make it home.</summary>
    public sealed class AlphaSpawnLocationSchedule
    {
        public const string Status = "ALPHA_DECOMPILED: SpawnLocationBehavior 0x4DFAC0 (entry rule and home write); entry lists PARTIAL";

        readonly AlphaScheduleEntry[] _entries;

        public AlphaSpawnLocationSchedule(IReadOnlyList<AlphaScheduleEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            _entries = new AlphaScheduleEntry[entries.Count];
            for (int i = 0; i < _entries.Length; i++) _entries[i] = entries[i];
        }

        public int Count => _entries.Length;

        /// <summary>Entry the NPC is heading to (creature +0x1488).</summary>
        public int TargetIndex { get; private set; }

        /// <summary>Entry the NPC last arrived at (creature +0x1484).</summary>
        public int ArrivedIndex { get; private set; }

        public bool Travelling => _entries.Length > 0 && TargetIndex != ArrivedIndex;

        public AlphaScheduleEntry Target => _entries[TargetIndex];

        /// <summary>Last entry with time &lt;= world time; 0 when none qualifies (Alpha's loop starts from 0).</summary>
        public static int ActiveIndex(IReadOnlyList<AlphaScheduleEntry> entries, int worldTimeMs)
        {
            int best = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].TimeMs <= worldTimeMs) best = i;
            }

            return best;
        }

        /// <summary>Re-evaluates the schedule; true when the NPC got a new destination this call.</summary>
        public bool Update(int worldTimeMs)
        {
            if (_entries.Length == 0) return false;
            int best = ActiveIndex(_entries, worldTimeMs);
            if (best == TargetIndex) return false;
            TargetIndex = best;
            return true;
        }

        /// <summary>Call when the NPC reached <see cref="Target"/>; returns the new home point (leash centre).</summary>
        public WorldPosition MarkArrived()
        {
            ArrivedIndex = TargetIndex;
            return _entries[TargetIndex].Position;
        }
    }

    /// <summary>WalkPathBehavior rule: visit the points in order with a random pause at each.</summary>
    public sealed class AlphaWalkPath
    {
        public const string Status = "ALPHA_DECOMPILED: WalkPathBehavior tick; pause rand()%4000+2000 byte-verified; point lists PARTIAL";

        /// <summary>Pause after reaching a point: rand() % PauseSpanMs + PauseBaseMs.</summary>
        public const int PauseSpanMs = 4000;
        public const int PauseBaseMs = 2000;

        /// <summary>Heights tried per direction when looking for a walkable spot (not modelled here).</summary>
        public const int GroundSearchSteps = 21;

        /// <summary>Arrival radius passed to the constructor for village NPCs (0x40000000).</summary>
        public const float VillageNpcRadius = 2f;

        readonly WorldPosition[] _points;
        readonly float _radius;

        public AlphaWalkPath(IReadOnlyList<WorldPosition> points, float radius = VillageNpcRadius)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            _points = new WorldPosition[points.Count];
            for (int i = 0; i < _points.Length; i++) _points[i] = points[i];
            _radius = radius;
        }

        public int Count => _points.Length;

        public int Index { get; private set; }

        public int PauseMs { get; private set; }

        public WorldPosition Current => _points[Index];

        /// <summary>
        /// One tick. Returns true and the point to walk to, or false while pausing / with no points / on the tick
        /// the NPC arrived (it then stops, starts the pause and advances the index).
        /// </summary>
        /// <param name="rand">Source of rand() values (0…32767).</param>
        public bool Tick(double x, double y, double z, int elapsedMs, Func<int> rand, out WorldPosition walkTo)
        {
            walkTo = default;
            if (_points.Length == 0) return false;
            PauseMs -= elapsedMs;
            if (PauseMs < 0) PauseMs = 0;
            if (PauseMs != 0) return false;
            if (Index < 0) Index = 0;
            Index %= _points.Length;
            WorldPosition p = _points[Index];
            double dx = p.X - x, dy = p.Y - y, dz = p.Z - z;
            if (dx * dx + dy * dy + dz * dz >= (double)_radius * _radius)
            {
                walkTo = p;
                return true;
            }

            PauseMs = rand() % PauseSpanMs + PauseBaseMs;
            Index = (Index + 1) % _points.Length;
            return false;
        }
    }

    /// <summary>RandomInteractionBehavior day window (byte-verified compare constants).</summary>
    public static class AlphaNpcDayWindow
    {
        public const string Status = "ALPHA_CODE_STATIC: Cube.exe 0x4DB404 / 0x4DB411";
        public const int DayStartMs = 21600000;
        public const int DayEndMs = 82800000;

        /// <summary>True when Alpha uses the day set of interaction spots.</summary>
        public static bool IsDay(int worldTimeMs) => worldTimeMs >= DayStartMs && worldTimeMs < DayEndMs;
    }
}
