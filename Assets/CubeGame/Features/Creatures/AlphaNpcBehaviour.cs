// CubeGame.Features.Creatures — runtime composition of the recovered Alpha NPC movement behaviours (M0-F1A).
//
// Alpha behaviour trees (LIVE_ONE_CAPTURE, D1/D2 report §4): village residents (class 0x88) run
// Combat, LookAt, SpawnLocation, RandomInteraction, RandomWalk; classes 0x80–0x87 / 0x89 run Combat, LookAt,
// WalkPath. Combat/LookAt stay in CreatureAi (target first); this driver answers "where to walk without a target":
//
//   game time → SpawnLocationBehavior active entry (AlphaSpawnLocationSchedule)
//             → travelling: walk to the entry
//             → arrived: entry position becomes home (+0x1C0) → RandomWalkBehavior around home
//   or        → WalkPathBehavior (AlphaWalkPath): point, arrive, pause rand()%4000+2000, next point
//
// RandomWalkBehavior (Cube.exe 0x4DBF20, bytes checked in creature_kind_evidence.py):
//   timer -= dt; at 0: horizontal (dx²+dz²)·65536 ≤ 0x640000 (inside 10 units of home, 0x4DBFFE/0x4DC004)
//   → direction = ((rand·2/32767 − 1)·10, (rand·2/32767 − 1)·10); else direction = unit(home − pos)·10;
//   then timer = rand()%5000 + 3000 (0x4DC1B2/0x4DC1BD). The vector is the creature's walk direction
//   (+0x40/+0x44); its magnitude 10 is not a CubeGame speed — the game keeps its own walk speed (PARTIAL).
//
// CUBEGAME_CONTRACT here: the arrival radius of SpawnLocationBehavior (Alpha uses its path follower's arrival
// flag; 2.0 = the WalkPath village radius is used), and the home of a creature without a schedule = its template
// position. RandomInteractionBehavior spots are PARTIAL and not driven (only the day window is known).
using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Creatures
{
    /// <summary>RandomWalkBehavior 0x4DBF20: wander inside a 10-unit leash around the home point.</summary>
    public sealed class AlphaRandomWalk
    {
        public const string Status =
            "ALPHA_DECOMPILED 0x4DBF20; leash 0x640000 and timer rand()%5000+3000 byte-verified";

        /// <summary>Leash radius (units = voxels): (dx²+dz²)·65536 ≤ 0x640000.</summary>
        public const double LeashRadius = 10.0;

        /// <summary>Length of the direction vector written to +0x40/+0x44.</summary>
        public const double DirectionLength = 10.0;

        public const int TimerSpanMs = 5000;
        public const int TimerBaseMs = 3000;

        public int TimerMs { get; private set; }

        public double DirectionX { get; private set; }

        public double DirectionZ { get; private set; }

        /// <summary>True once a direction was chosen.</summary>
        public bool HasDirection { get; private set; }
        public bool ReturningHome { get; private set; }

        /// <summary>Whether a position lies inside the leash (Alpha's integer test, written in units).</summary>
        public static bool InsideLeash(double x, double z, WorldPosition home)
        {
            double dx = x - home.X, dz = z - home.Z;
            return (dx * dx + dz * dz) * 65536.0 <= 0x640000;
        }

        /// <summary>One tick; returns true when a new direction was chosen this tick.</summary>
        public bool Tick(double x, double z, WorldPosition home, int elapsedMs, Func<int> rand)
        {
            if (rand == null) throw new ArgumentNullException(nameof(rand));
            TimerMs -= elapsedMs;
            if (TimerMs < 0) TimerMs = 0;
            if (TimerMs != 0) return false;
            ReturningHome = !InsideLeash(x, z, home);
            if (!ReturningHome)
            {
                DirectionX = ((float)rand() * 2.0f / 32767.0f - 1.0f) * 10.0f;
                DirectionZ = ((float)rand() * 2.0f / 32767.0f - 1.0f) * 10.0f;
            }
            else
            {
                double dx = home.X - x, dz = home.Z - z;
                double len = Math.Sqrt(dx * dx + dz * dz);
                DirectionX = dx / len * DirectionLength;
                DirectionZ = dz / len * DirectionLength;
            }

            TimerMs = rand() % TimerSpanMs + TimerBaseMs;
            HasDirection = true;
            return true;
        }
    }

    public enum AlphaNpcMovementMode
    {
        /// <summary>RandomWalkBehavior around the home point only (wild creatures, residents at home).</summary>
        RandomWalk = 0,

        /// <summary>SpawnLocationBehavior + RandomWalkBehavior (village residents).</summary>
        Schedule = 1,

        /// <summary>WalkPathBehavior (merchants/service NPCs).</summary>
        WalkPath = 2,
    }

    /// <summary>What the NPC wants this tick.</summary>
    public struct AlphaNpcMovementStep
    {
        public bool Move;
        public bool ReturningHome;
        public WorldPosition Target;
        public string Reason;

        /// <summary>Target x/z as plain numbers (for modules that do not reference the contracts assembly).</summary>
        public double TargetX => Target.X;

        public double TargetZ => Target.Z;
    }

    /// <summary>Recovered Alpha movement of one creature without a combat target.</summary>
    public sealed class AlphaNpcMovementDriver
    {
        public const string Provenance =
            "SpawnLocation/WalkPath/RandomWalk rules ALPHA_DECOMPILED (+byte-verified constants); " +
            "schedule arrival radius and default home CUBEGAME_CONTRACT";

        /// <summary>CUBEGAME_CONTRACT: arrival radius of SpawnLocationBehavior (see header).</summary>
        public const double ScheduleArrivalRadius = AlphaWalkPath.VillageNpcRadius;

        readonly Func<int> _worldTimeMs;

        /// <summary>CompanionBehavior 0x43ACE0: follow when the leader is at least 4 voxels away (16 = 4²).</summary>
        public const double CompanionFollowDistance2 = 16.0;

        /// <summary>CompanionBehavior: beyond 8 voxels (fixed 0x400000 = 64 = 8²) the creature runs (+0x124 |= 0x40).</summary>
        public const double CompanionRunDistance2 = 64.0;

        readonly Func<WorldPosition?> _companionOf;

        public AlphaNpcMovementDriver(WorldPosition home, Func<int> worldTimeMs,
            AlphaSpawnLocationSchedule schedule = null, AlphaWalkPath walkPath = null, Func<WorldPosition?> companionOf = null)
        {
            _worldTimeMs = worldTimeMs ?? throw new ArgumentNullException(nameof(worldTimeMs));
            _companionOf = companionOf;
            Home = home;
            Schedule = schedule != null && schedule.Count > 0 ? schedule : null;
            WalkPath = walkPath != null && walkPath.Count > 0 ? walkPath : null;
            Mode = WalkPath != null ? AlphaNpcMovementMode.WalkPath
                : Schedule != null ? AlphaNpcMovementMode.Schedule : AlphaNpcMovementMode.RandomWalk;
        }

        /// <summary>Driver for a runtime template: schedule/walk path from the template, home = template position.</summary>
        public static AlphaNpcMovementDriver ForTemplate(AlphaSpawnTemplateData data, Func<int> worldTimeMs)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            AlphaSpawnLocationSchedule schedule = data.Schedule.Count > 0 ? new AlphaSpawnLocationSchedule(data.Schedule) : null;
            AlphaWalkPath path = data.WalkPath.Count > 0 ? new AlphaWalkPath(data.WalkPath) : null;
            return new AlphaNpcMovementDriver(data.Definition.Position, worldTimeMs, schedule, path);
        }

        public AlphaNpcMovementMode Mode { get; }

        /// <summary>Home point (+0x1C0): the RandomWalk leash centre.</summary>
        public WorldPosition Home { get; private set; }

        public AlphaSpawnLocationSchedule Schedule { get; }

        public AlphaWalkPath WalkPath { get; }

        public AlphaRandomWalk RandomWalk { get; } = new AlphaRandomWalk();

        /// <summary>World time of day used by the last tick.</summary>
        public int LastWorldTimeMs { get; private set; }

        /// <summary>One movement tick at the creature position.</summary>
        public AlphaNpcMovementStep Tick(double x, double y, double z, int elapsedMs, Func<int> rand)
        {
            if (rand == null) throw new ArgumentNullException(nameof(rand));
            LastWorldTimeMs = _worldTimeMs();
            // SequentialBehavior 0x4DC390: the first child that handles the tick wins — Companion before WalkPath.
            if (_companionOf != null && _companionOf() is WorldPosition leader)
            {
                double dx = leader.X - x, dy = leader.Y - y, dz = leader.Z - z;
                double d2 = dx * dx + dy * dy + dz * dz;
                if (d2 >= CompanionFollowDistance2)
                {
                    return new AlphaNpcMovementStep
                    {
                        Move = true, Target = leader,
                        Reason = d2 > CompanionRunDistance2 ? "COMPANION_RUN" : "COMPANION",
                    };
                }
            }

            if (WalkPath != null)
            {
                bool walk = WalkPath.Tick(x, y, z, elapsedMs, rand, out WorldPosition point);
                return new AlphaNpcMovementStep
                {
                    Move = walk, Target = point,
                    Reason = walk ? "WALK_PATH " + WalkPath.Index : "WALK_PATH_PAUSE " + WalkPath.Index,
                };
            }

            if (Schedule != null)
            {
                Schedule.Update(LastWorldTimeMs);
                if (Schedule.Travelling)
                {
                    WorldPosition target = Schedule.Target.Position;
                    double dx = target.X - x, dz = target.Z - z;
                    if (dx * dx + dz * dz > ScheduleArrivalRadius * ScheduleArrivalRadius)
                    {
                        return new AlphaNpcMovementStep
                        {
                            Move = true, ReturningHome = true, Target = target, Reason = "SCHEDULE_TRAVEL " + Schedule.TargetIndex,
                        };
                    }

                    Home = Schedule.MarkArrived();
                }
            }

            RandomWalk.Tick(x, z, Home, elapsedMs, rand);
            if (!RandomWalk.HasDirection) return new AlphaNpcMovementStep { Move = false, Reason = "RANDOM_WALK_WAIT" };
            return new AlphaNpcMovementStep
            {
                Move = true,
                ReturningHome = RandomWalk.ReturningHome,
                Target = new WorldPosition(x + RandomWalk.DirectionX, y, z + RandomWalk.DirectionZ),
                Reason = "RANDOM_WALK",
            };
        }
    }
}
