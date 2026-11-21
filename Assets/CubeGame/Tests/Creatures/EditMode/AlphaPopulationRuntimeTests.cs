// M0-F1A — EditMode checks of the recovered creature kind selector (0x5F56C0), the template runtime
// (window / radius 200 / despawn 512 / timer 1 200 000 ms / day change / stable id) and the NPC movement driver.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CubeGame.Content;
using CubeGame.Features.AI;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.World;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaCreatureKindSelectorTests
    {
        static AlphaClimateSample Climate(double[] c) =>
            new AlphaClimateSample((float)c[0], (float)c[1], (float)c[2], (float)c[3]);

        [Test]
        public void SelectorMatchesEveryExecutedCaseOf0x5F56C0()
        {
            Assert.GreaterOrEqual(AlphaCreatureKindCases.Cases.Length, 3000);
            StringAssert.Contains("EMULATED_ALPHA_CODE", AlphaCreatureKindCases.Status);
            int mismatches = 0;
            foreach (double[] c in AlphaCreatureKindCases.Cases)
            {
                int[] expected = AlphaCreatureKindCases.Lists[(int)c[8]];
                int[] got = AlphaCreatureKindSelector.Candidates(Climate(c), (int)c[4], (int)c[5]);
                if (!expected.SequenceEqual(got)) mismatches++;
                Assert.AreEqual((int)c[7], AlphaCreatureKindSelector.Pick(got, (int)c[6]),
                    "the returned kind is list[rand() % n]");
            }

            Assert.AreEqual(0, mismatches, "candidate lists must equal the executed machine code");
        }

        [Test]
        public void EmptyListGives0x3CAndPickIsUnsignedModulo()
        {
            Assert.AreEqual(0x3C, AlphaCreatureKindSelector.Pick(new int[0], 5));
            Assert.AreEqual(0x3C, AlphaCreatureKindSelector.Pick(null, 5));
            var list = new[] { 1, 2, 3 };
            Assert.AreEqual(2, AlphaCreatureKindSelector.Pick(list, 4));
            Assert.AreEqual(1, AlphaCreatureKindSelector.Pick(list, 32766 - 32766 % 3));
        }

        [Test]
        public void TheExistingScatterTranslationIsNotEquivalentAndIsNotUsedForKinds()
        {
            // AlphaScatterTree names the same address but differs from the executed code; it must not be the kind source.
            int differing = 0;
            foreach (double[] c in AlphaCreatureKindCases.Cases)
            {
                int[] translated = AlphaScatterTree.Select(c[0], c[1], c[2], c[3], (int)c[4], (int)c[5]);
                if (!AlphaCreatureKindCases.Lists[(int)c[8]].SequenceEqual(translated)) differing++;
            }

            Assert.Greater(differing, 0, "if this fails the translation was fixed: re-check the handoff in the M0-F1A report");
            Assert.IsFalse(References(typeof(AlphaCreatureKindSelector), typeof(AlphaScatterTree)));
            Assert.IsFalse(References(typeof(AlphaRecoveredPopulation), typeof(AlphaScatterTree)));
        }

        [Test]
        public void RecoveredPopulationTakesKindsFromTheSelectorList()
        {
            var seen = new HashSet<int>();
            int[] list = { 0x13, 0x14, 0x1E };
            var population = AlphaRecoveredPopulation.WithKindSelector(new WorldSeed(7u), (x, z) => list, (zone, x, z) => 64.0);
            Assert.IsTrue(population.UsesRecoveredKindSelector);
            for (int i = 0; i < 20; i++)
            {
                foreach (SpawnDefinition d in population.SpawnsForZone(new ZoneKey(32800 + i, 32800)))
                {
                    if (d.SpawnIndex % AlphaRecoveredPopulation.IndicesPerCell == 0) seen.Add(d.Kind.Value);
                    StringAssert.Contains("0x5F56C0", d.Provenance);
                }
            }

            CollectionAssert.IsSubsetOf(seen, list, "leaders are picked from the candidate list");
            Assert.Greater(seen.Count, 1, "the pick uses rand(), not always the first entry");
            var empty = AlphaRecoveredPopulation.WithKindSelector(new WorldSeed(7u), (x, z) => new int[0]);
            Assert.IsTrue(empty.SpawnsForZone(new ZoneKey(32800, 32800)).Where(d => d.SpawnIndex % 8 == 0)
                .All(d => d.Kind.Value == 0x3C), "empty list -> 0x3C as in Alpha");
        }

        [Test]
        public void ClimateAdapterPassesVoxelCoordinatesAndColumnHeight()
        {
            int seenX = -1, seenZ = -1;
            Func<int, int, int[]> candidates = AlphaCreatureKindSelector.FromClimate(
                (x, z) => { seenX = x; seenZ = z; return new AlphaClimateSample(0.5f, 0.5f, 0.5f, 0.5f); },
                (x, z) => 10);
            CollectionAssert.AreEqual(new[] { 0x49, 0x52, 0x55, 0x70, 0x67, 0x3E, 0x6E }, candidates(8396824, 8396900));
            Assert.AreEqual(8396824, seenX);
            Assert.AreEqual(8396900, seenZ);
        }

        [Test]
        public void NoPopulationOrRuntimeTypeReadsTheVendorItemTable()
        {
            foreach (Type type in new[]
                     {
                         typeof(AlphaCreatureKindSelector), typeof(AlphaRecoveredPopulation), typeof(AlphaSpawnRuntime),
                         typeof(AlphaNpcMovementDriver), typeof(AlphaRandomWalk), typeof(AlphaWorldPopulation),
                     })
            {
                Assert.IsFalse(References(type, typeof(AlphaSpawnTable)), type.Name + " must not read AlphaSpawnTable");
            }
        }

        /// <summary>IL scan: does any method of <paramref name="type"/> (incl. nested/lambdas) touch a member of <paramref name="target"/>?</summary>
        internal static bool References(Type type, Type target)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                     BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var types = new List<Type> { type };
            types.AddRange(type.GetNestedTypes(all));
            types.AddRange(type.Assembly.GetTypes().Where(t => t.FullName != null &&
                t.FullName.StartsWith(type.FullName + "+", StringComparison.Ordinal)));
            foreach (Type t in types.Distinct())
            {
                foreach (MethodBase m in t.GetMethods(all).Cast<MethodBase>().Concat(t.GetConstructors(all)))
                {
                    byte[] il = m.GetMethodBody()?.GetILAsByteArray();
                    if (il == null) continue;
                    for (int i = 0; i + 4 < il.Length; i++)
                    {
                        byte op = il[i];
                        if (op != 0x28 && op != 0x6F && op != 0x73 && op != 0x7E && op != 0x7F && op != 0xD0) continue;
                        int token = BitConverter.ToInt32(il, i + 1);
                        try
                        {
                            MemberInfo member = m.Module.ResolveMember(token,
                                t.IsGenericType ? t.GetGenericArguments() : null,
                                m.IsGenericMethod ? m.GetGenericArguments() : null);
                            if (member == target || member?.DeclaringType == target) return true;
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            return false;
        }
    }

    public class AlphaSpawnRuntimeTests
    {
        const int Hour = 3600000;
        static readonly ZoneKey Zone = new ZoneKey(32800, 32800);
        static readonly WorldPosition Spawn = new WorldPosition(32800 * 256.0 + 100, 64, 32800 * 256.0 + 100);

        sealed class OneTemplate : IAlphaSpawnTemplateSource
        {
            public int Calls;
            public int Start = AlphaWildSpawnRules.HostileWindowStartMs;
            public int End = AlphaWildSpawnRules.HostileWindowEndMs;

            public IReadOnlyList<AlphaSpawnTemplateData> TemplatesForZone(ZoneKey zone)
            {
                Calls++;
                if (!zone.Equals(Zone)) return new AlphaSpawnTemplateData[0];
                var def = new SpawnDefinition
                {
                    Region = new RegionKey(zone.X >> 6, zone.Z >> 6), SpawnIndex = 17, Kind = new KindId(0x13),
                    Position = Spawn, Faction = AlphaWildSpawnRules.FactionHostile, Level = 4,
                };
                return new[] { new AlphaSpawnTemplateData(def, Start, End) };
            }
        }

        static WorldPosition Away(double distance) => new WorldPosition(Spawn.X + distance, Spawn.Y, Spawn.Z);

        static (AlphaSpawnRuntime Runtime, List<AlphaSpawnTemplate> Created, List<(AlphaSpawnTemplate, AlphaDespawnReason)> Gone)
            Make(OneTemplate source = null)
        {
            var runtime = new AlphaSpawnRuntime();
            runtime.AddSource(source ?? new OneTemplate());
            var created = new List<AlphaSpawnTemplate>();
            var gone = new List<(AlphaSpawnTemplate, AlphaDespawnReason)>();
            runtime.Created += t => created.Add(t);
            runtime.Despawned += (t, r) => gone.Add((t, r));
            runtime.LoadZone(Zone);
            return (runtime, created, gone);
        }

        static StableEntityId Id => AlphaSpawnRuntime.IdOf(Zone, AlphaSpawnRuntime.CreatureChannel, 17);

        [Test]
        public void StableIdIsAlphasZoneAndIndexInTheExistingIdSpace()
        {
            Assert.AreEqual(StableEntityId.ForZoneSpawn(Zone, 0, 17), Id);
            var (runtime, created, _) = Make();
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(10) });
            Assert.AreEqual(1, created.Count);
            Assert.AreEqual(Id, created[0].Id);
        }

        [Test]
        public void HostileTemplateRespectsTheTimeWindow()
        {
            var (runtime, created, _) = Make();
            runtime.Tick(16, 3 * Hour, 0, new[] { Away(10) });
            Assert.AreEqual(0, created.Count, "03:00 is outside 06:00…24:00");
            runtime.Tick(16, 6 * Hour, 0, new[] { Away(10) });
            Assert.AreEqual(1, created.Count, "window start is inclusive");

            var noWindow = new OneTemplate { Start = 0, End = 0 };
            var (r2, c2, _) = Make(noWindow);
            r2.Tick(16, 3 * Hour, 0, new[] { Away(10) });
            Assert.AreEqual(1, c2.Count, "start == end -> no window");
        }

        [Test]
        public void CreatureAppearsOnlyInsideRadius200AtTheTemplatePosition()
        {
            var (runtime, created, _) = Make();
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(200) });
            Assert.AreEqual(0, created.Count, "distance must be < 200");
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(500), Away(199.5) });
            Assert.AreEqual(1, created.Count, "the nearest player counts");
            Assert.AreEqual(Spawn, created[0].Definition.Position);
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(10) });
            Assert.AreEqual(1, created.Count, "never a second creature of a live template");
        }

        [Test]
        public void LiveCreatureFartherThan512IsRemovedAndReactivatedAtTheSamePlace()
        {
            var (runtime, created, gone) = Make();
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(10) });
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(511) }, id => Spawn);
            Assert.AreEqual(0, gone.Count, "511 < 512 keeps the creature");
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(512) }, id => Spawn);
            Assert.AreEqual(1, gone.Count);
            Assert.AreEqual(AlphaDespawnReason.OutOfRange, gone[0].Item2);
            Assert.IsTrue(runtime.TryGet(Id, out AlphaSpawnTemplate t), "the template stays");
            Assert.AreEqual(0, t.RespawnTimerMs, "leaving range is not a death");
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(50) });
            Assert.AreEqual(2, created.Count, "re-activation of the same template");
            Assert.AreEqual(Id, created[1].Id);
            Assert.AreEqual(Spawn, created[1].Definition.Position, "position is the template's, not re-rolled");
        }

        [Test]
        public void DeathStartsThe1200000MsTimer()
        {
            var (runtime, created, _) = Make();
            runtime.Tick(16, 8 * Hour, 3, new[] { Away(10) });
            Assert.IsTrue(runtime.NotifyDeath(Id, 3));
            Assert.IsFalse(runtime.NotifyDeath(Id, 3), "a dead template is not live");
            runtime.TryGet(Id, out AlphaSpawnTemplate t);
            Assert.AreEqual(1200000, t.RespawnTimerMs);
            Assert.AreEqual(3, t.DayOfDeath);
            runtime.Tick(1199999, 9 * Hour, 3, new[] { Away(10) });
            Assert.AreEqual(1, created.Count, "1 ms left on the same day");
            runtime.Tick(1, 9 * Hour, 3, new[] { Away(10) });
            Assert.AreEqual(2, created.Count, "timer ran out");
        }

        [Test]
        public void DayChangeRespawnsBeforeTheTimerRunsOut()
        {
            var (runtime, created, _) = Make();
            runtime.Tick(16, 23 * Hour, 0, new[] { Away(10) });
            runtime.NotifyDeath(Id, 0);
            runtime.Tick(16, 23 * Hour + 1000, 0, new[] { Away(10) });
            Assert.AreEqual(1, created.Count);
            runtime.Tick(16, 7 * Hour, 1, new[] { Away(10) });
            Assert.AreEqual(2, created.Count, "day of death != current day");
        }

        [Test]
        public void UnloadReloadKeepsIdentityAndDeathState()
        {
            var source = new OneTemplate();
            var (runtime, created, gone) = Make(source);
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(10) });
            runtime.UnloadZone(Zone);
            Assert.AreEqual(AlphaDespawnReason.ZoneUnloaded, gone.Single().Item2);
            Assert.AreEqual(0, runtime.TemplateCount);
            runtime.LoadZone(Zone);
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(10) });
            Assert.AreEqual(2, created.Count);
            Assert.AreEqual(created[0].Id, created[1].Id, "same stable id after reload");

            runtime.NotifyDeath(Id, 0);
            runtime.UnloadZone(Zone);
            runtime.Tick(600000, 8 * Hour, 0, new[] { Away(10) });   // nothing loaded: parked state does not tick
            runtime.LoadZone(Zone);
            runtime.TryGet(Id, out AlphaSpawnTemplate t);
            Assert.AreEqual(1200000, t.RespawnTimerMs, "death state survives the unload");
            runtime.Tick(16, 8 * Hour, 0, new[] { Away(10) });
            Assert.AreEqual(2, created.Count, "still dead after reload");
        }

        [Test]
        public void SnapshotRestoreSurvivesANewRuntime()
        {
            var (runtime, _, _) = Make();
            runtime.Tick(16, 8 * Hour, 2, new[] { Away(10) });
            runtime.NotifyDeath(Id, 2);
            runtime.Tick(200000, 8 * Hour, 2, new[] { Away(10) });
            List<AlphaSpawnTemplateState> saved = runtime.SnapshotStates();
            Assert.AreEqual(1, saved.Count);
            Assert.AreEqual(1000000, saved[0].RespawnTimerMs);

            var fresh = new AlphaSpawnRuntime();
            fresh.AddSource(new OneTemplate());
            var created = new List<AlphaSpawnTemplate>();
            fresh.Created += t => created.Add(t);
            fresh.RestoreStates(saved);
            fresh.LoadZone(Zone);
            fresh.Tick(16, 8 * Hour, 2, new[] { Away(10) });
            Assert.AreEqual(0, created.Count, "restored timer keeps it dead");
            fresh.Tick(16, 8 * Hour, 3, new[] { Away(10) });
            Assert.AreEqual(1, created.Count);
            Assert.AreEqual(Id, created[0].Id);
        }

        [Test]
        public void RecoveredPopulationFeedsTheRuntimeWithStableIds()
        {
            var population = AlphaRecoveredPopulation.WithKindSelector(new WorldSeed(11u), (x, z) => new[] { 0x13, 0x64 },
                (zone, x, z) => 64.0);
            var a = new AlphaSpawnRuntime();
            a.AddSource(population);
            a.LoadZone(Zone);
            var ids = a.Templates.Select(t => t.Id).OrderBy(i => i.Value).ToList();
            Assert.Greater(ids.Count, 0);
            Assert.IsEmpty(a.Problems);
            foreach (AlphaSpawnTemplate t in a.Templates)
            {
                bool hostile = t.Definition.Faction == AlphaWildSpawnRules.FactionHostile;
                Assert.AreEqual(hostile ? 21600000 : 0, t.Data.WindowStartMs);
                Assert.AreEqual(200f, t.Data.ActivationRadius);
            }

            a.UnloadZone(Zone);
            a.LoadZone(Zone);
            CollectionAssert.AreEqual(ids, a.Templates.Select(t => t.Id).OrderBy(i => i.Value).ToList(),
                "reload of the zone gives the same ids");
            var b = new AlphaSpawnRuntime();
            b.AddSource(AlphaRecoveredPopulation.WithKindSelector(new WorldSeed(11u), (x, z) => new[] { 0x13, 0x64 }, (zone, x, z) => 64.0));
            b.LoadZone(Zone);
            CollectionAssert.AreEqual(ids, b.Templates.Select(t => t.Id).OrderBy(i => i.Value).ToList(),
                "a new session (save/load) derives the same ids from the world");
        }

        [Test]
        public void GameDayFollowsTheClockWrap()
        {
            var clock = new CubeGame.Features.Session.GameClock();
            Assert.AreEqual(0, AlphaGameDay.Of(clock));
            int ticksToMidnight = (86400000 - 32400000) / 10 / 1000 * 30;
            clock.StepTicks(ticksToMidnight - 1);
            Assert.AreEqual(0, AlphaGameDay.Of(clock));
            clock.StepTicks(2);
            Assert.AreEqual(1, AlphaGameDay.Of(clock));
            Assert.Less(clock.WorldTimeMs, 1000, "the world time wrapped together with the day");
        }
    }

    public class AlphaNpcBehaviourTests
    {
        const int Hour = 3600000;

        sealed class Rand
        {
            int _i;
            readonly int[] _values;
            public Rand(params int[] values) { _values = values; }
            public int Next() => _values[_i++ % _values.Length];
        }

        [Test]
        public void RandomWalkInsideTheLeashPicksARandomDirectionAndTimer()
        {
            var walk = new AlphaRandomWalk();
            var home = new WorldPosition(0, 0, 0);
            var r = new Rand(32767, 0, 4999);
            Assert.IsTrue(walk.Tick(3, 4, home, 16, r.Next));
            Assert.AreEqual(10.0, walk.DirectionX, 1e-4);
            Assert.AreEqual(-10.0, walk.DirectionZ, 1e-4);
            Assert.AreEqual(4999 % 5000 + 3000, walk.TimerMs);
            Assert.IsFalse(walk.Tick(3, 4, home, 7000, r.Next), "timer 7999 still running");
            Assert.IsTrue(walk.Tick(3, 4, home, 999, r.Next));
            Assert.IsTrue(AlphaRandomWalk.InsideLeash(6, 8, home), "exactly 10 units is inside");
            Assert.IsFalse(AlphaRandomWalk.InsideLeash(6, 8.01, home));
        }

        [Test]
        public void RandomWalkOutsideTheLeashHeadsHome()
        {
            var walk = new AlphaRandomWalk();
            Assert.IsTrue(walk.Tick(30, 0, new WorldPosition(0, 0, 0), 16, () => 0));
            Assert.AreEqual(-10.0, walk.DirectionX, 1e-9);
            Assert.AreEqual(0.0, walk.DirectionZ, 1e-9);
            Assert.AreEqual(3000, walk.TimerMs);
        }

        [Test]
        public void ScheduleDriverGoesToTheActivePointThenWandersAroundIt()
        {
            int time = 7 * Hour;
            var entries = new[]
            {
                new AlphaScheduleEntry(new WorldPosition(0, 5, 0), 0),
                new AlphaScheduleEntry(new WorldPosition(100, 5, 0), 8 * Hour),
            };
            var driver = new AlphaNpcMovementDriver(new WorldPosition(0, 5, 0), () => time,
                new AlphaSpawnLocationSchedule(entries));
            Assert.AreEqual(AlphaNpcMovementMode.Schedule, driver.Mode);
            AlphaNpcMovementStep step = driver.Tick(0, 5, 0, 16, () => 100);
            StringAssert.StartsWith("RANDOM_WALK", step.Reason, "before 08:00 the first entry is home");

            time = 9 * Hour;
            step = driver.Tick(0, 5, 0, 16, () => 100);
            Assert.IsTrue(step.Move);
            Assert.AreEqual("SCHEDULE_TRAVEL 1", step.Reason);
            Assert.AreEqual(100.0, step.Target.X);

            step = driver.Tick(99, 5, 0, 16, () => 100);
            Assert.AreEqual(new WorldPosition(100, 5, 0), driver.Home, "arrival writes the home point");
            StringAssert.StartsWith("RANDOM_WALK", step.Reason);
        }

        [Test]
        public void WalkPathDriverProgressesAndPauses()
        {
            var points = new[] { new WorldPosition(0, 0, 0), new WorldPosition(20, 0, 0) };
            var driver = new AlphaNpcMovementDriver(points[0], () => 12 * Hour, null, new AlphaWalkPath(points));
            Assert.AreEqual(AlphaNpcMovementMode.WalkPath, driver.Mode);
            AlphaNpcMovementStep step = driver.Tick(0, 0, 0, 16, () => 1000);
            Assert.IsFalse(step.Move, "arrived at point 0 -> pause");
            Assert.AreEqual(3000, driver.WalkPath.PauseMs, "1000 % 4000 + 2000");
            Assert.IsFalse(driver.Tick(0, 0, 0, 2999, () => 0).Move);
            step = driver.Tick(0, 0, 0, 1, () => 0);
            Assert.IsTrue(step.Move);
            Assert.AreEqual(20.0, step.Target.X, "next point");
        }

        [Test]
        public void CreatureAiUsesTheAlphaDriverBeforeTheFallbackPatrol()
        {
            var ai = new CreatureAi();
            ai.AttachPatrol(new PatrolRoute(0, 0, 3, 4, 99UL));
            var points = new[] { new WorldPosition(40, 0, 0), new WorldPosition(0, 0, 40) };
            ai.AttachAlphaMovement(new AlphaNpcMovementDriver(points[0], () => 12 * Hour, null, new AlphaWalkPath(points)));
            var self = new CreatureState
            {
                StableId = "spawn:0:0:0:0:0", Kind = 0x1c, Faction = (int)CreatureFaction.Monster,
                Level = 1, MaxHealth = 50, Health = 50, X = 0, Z = 0,
            };
            var world = new Probe();
            ai.Tick(self, 100, world);
            Assert.AreEqual("WALK_PATH 0", ai.LastReason);
            Assert.AreEqual(40.0, world.LastX, 1e-9);
            StringAssert.Contains("CUBEGAME_FALLBACK", PatrolRoute.Provenance, "the ring stays a fallback");
        }

        sealed class Probe : ICreatureAiWorld
        {
            public double LastX, LastZ;
            public bool TryGetTarget(string id, out CreatureTarget target) { target = default; return false; }
            public string FindNearestHostile(CreatureState self, double radius) => null;
            public bool TryBeginAction(CreatureState self, int actionCode, out string reason) { reason = "ACCEPTED"; return true; }
            public void MoveTowards(CreatureState self, double x, double z, double speed, int elapsedMs) { LastX = x; LastZ = z; }
            public bool TryConsumeLifePotion(CreatureState self) => false;
            public bool HasLifePotion(CreatureState self) => false;
            public int CurrentActionDurationMs(CreatureState self) => 0;
            public double AggroRadius => 16.0;
            public double AttackRange => 2.0;
            public double WalkSpeed => 0;
            public int[] AvailableActions(CreatureState self) => new int[0];
            public uint NextRoll() => 5;
        }
    }
}
