using System.Collections.Generic;
using System.Linq;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>P7-A: Alpha missions (docs/research/alpha2013/P7_MISSIONS_TRACE.md).</summary>
    public sealed class FullM0P7MissionTests
    {
        private const uint Seed = 666666;
        private static readonly int[] BossKinds = { 0x6C, 0x72, 0x74, 0x77, 0x73, 0x75, 0x71, 0x70, 0x6F, 0x6D, 0x6E };

        private static int StartRegion => WorldLayout.StartVoxel >> 14;

        private static List<AlphaMission> Generate(AlphaTerrainGenerator terrain, int rx, int rz, int level) =>
            AlphaMissionGenerator.GenerateRegion(terrain, rx, rz, level, level,
                new AlphaCrtRandom(AlphaMissionGenerator.RegionSeed((int)Seed, rx, rz)), null);

        [Test]
        public void RegionPassFollowsAlphaRules()
        {
            var terrain = new AlphaTerrainGenerator(Seed);
            int bosses = 0;
            int missions = 0;
            for (int rx = StartRegion - 1; rx <= StartRegion + 1; rx++)
            {
                for (int rz = StartRegion - 1; rz <= StartRegion + 1; rz++)
                {
                    List<AlphaMission> result = Generate(terrain, rx, rz, 1);
                    AlphaRegion region = terrain.RegionRecord(rx, rz);
                    Assert.LessOrEqual(result.Count(m => m.Type == AlphaMissionType.Monster), 2, "0x5D9AD0: two plain-column missions");
                    Assert.LessOrEqual(result.Count(m => m.HasMission && m.Type != AlphaMissionType.Monster), 2,
                        "0x5D9AD0: two landmark missions");
                    foreach (AlphaMission m in result)
                    {
                        Assert.AreEqual(rx, m.RegionX);
                        Assert.AreEqual(rz, m.RegionZ);
                        LandmarkColumn column = region.Column(m.ColumnX & 7, m.ColumnZ & 7);
                        Assert.IsTrue(terrain.NearestCellRegion((int)column.X, (int)column.Z, out int ox, out int oz));
                        Assert.AreEqual((rx, rz), (ox, oz), "only columns the region owns");
                        if (column.Type == LandmarkType.None)
                        {
                            bosses++;
                            Assert.IsTrue(m.HasBoss);
                            Assert.That(m.BossLevel, Is.InRange(1, 30));
                            CollectionAssert.Contains(BossKinds, m.BossKind);
                            Assert.That(m.BossZoneX - m.ColumnX * 8, Is.InRange(2, 5));
                            Assert.That(m.BossZoneZ - m.ColumnZ * 8, Is.InRange(2, 5));
                        }
                        else
                        {
                            Assert.IsFalse(m.HasBoss, "bosses only stand in columns without landmark");
                        }

                        if (!m.HasMission) continue;
                        missions++;
                        Assert.AreEqual(AlphaMissionState.Sleeping, m.State);
                        Assert.AreEqual(1, m.MaxLevel);
                        if (m.Type == AlphaMissionType.Monster)
                        {
                            Assert.AreEqual(LandmarkType.None, column.Type);
                            Assert.AreEqual(m.BossKind, m.TargetKind);
                            Assert.AreEqual((m.BossZoneX, m.BossZoneZ), (m.TargetZoneX, m.TargetZoneZ));
                            Assert.IsTrue(m.BossIsMissionTarget);
                        }
                        else
                        {
                            Assert.AreNotEqual(LandmarkType.Unnamed10, column.Type);
                            Assert.LessOrEqual(column.Level, 3, "landmark level ≤ max player level + 2");
                            var allowed = column.Type == LandmarkType.Village
                                ? new[] { AlphaMissionType.VillageInvasion, AlphaMissionType.VillageMonster, AlphaMissionType.VillageRareBoss }
                                : new[] { AlphaMissionType.Dungeon };
                            CollectionAssert.Contains(allowed, m.Type);
                            Assert.That(m.TargetZoneX - m.ColumnX * 8, Is.InRange(0, 7));
                            Assert.That(m.TargetZoneZ - m.ColumnZ * 8, Is.InRange(0, 7));
                        }
                    }
                }
            }

            Assert.Greater(bosses, 0, "the 3×3 start regions contain columns without landmark");
            Assert.Greater(missions, 0);
        }

        [Test]
        public void RegionPassIsDeterministicPerRegionStream()
        {
            var terrain = new AlphaTerrainGenerator(Seed);
            List<AlphaMission> first = Generate(terrain, StartRegion, StartRegion, 4);
            List<AlphaMission> second = Generate(new AlphaTerrainGenerator(Seed), StartRegion, StartRegion, 4);
            Assert.AreEqual(Describe(first), Describe(second));
        }

        [Test]
        public void BossKindFollowsLevelAndClimate()
        {
            // 0x5F7820: no grid cell → 0x6C without drawing from the stream.
            var a = new AlphaCrtRandom(7);
            var b = new AlphaCrtRandom(7);
            Assert.AreEqual(AlphaMissionGenerator.DefaultBossKind, AlphaMissionGenerator.BossKind(null, 80, a));
            Assert.AreEqual(b.Next(), a.Next());

            var terrain = new AlphaTerrainGenerator(Seed);
            AlphaClimateCell cell = terrain.Climate.Get(StartRegion, StartRegion);
            bool cold = cell.Field4 <= 0.2f;
            var low = new[] { cold ? 0x72 : 0x6C, cold ? 0x74 : 0x77 };
            for (int s = 0; s < 32; s++)
            {
                int kind = AlphaMissionGenerator.BossKind(cell, 5, new AlphaCrtRandom(s));
                Assert.AreEqual(low[(uint)new AlphaCrtRandom(s).Next() % 2u], kind);
            }

            var high = new List<int>(low) { 0x73, 0x75, cell.Field4 < 0.2f ? 0x71 : cell.Flag ? 0x70 : 0x6F, 0x6D, 0x6E };
            for (int s = 0; s < 32; s++)
            {
                int kind = AlphaMissionGenerator.BossKind(cell, 80, new AlphaCrtRandom(s));
                Assert.AreEqual(high[(int)((uint)new AlphaCrtRandom(s).Next() % (uint)high.Count)], kind);
            }
        }

        [Test]
        public void InvasionKindIsRandModNine()
        {
            for (int s = 0; s < 32; s++)
            {
                Assert.AreEqual(AlphaMissionGenerator.InvasionKinds[(uint)new AlphaCrtRandom(s).Next() % 9u],
                    AlphaMissionGenerator.InvasionKind(new AlphaCrtRandom(s)));
            }

            CollectionAssert.AreEqual(new[] { 0x30, 0x33, 0x60, 0x50, 0x4C, 0x28, 0x2B, 0x2D, 0x34 },
                AlphaMissionGenerator.InvasionKinds);
        }

        [Test]
        public void TypeOneMissionActivatesInItsColumnAndCompletesOnTheBossKill()
        {
            GameSession session = StartSession(out AlphaTerrainGenerator terrain);
            HeroState hero = session.Hero;
            AlphaMission mission = FindMission(session, AlphaMissionType.Monster);
            Assert.IsNotNull(mission, "a type-1 mission near the start");
            SetState(mission, AlphaMissionState.Sleeping);     // the search may have stood in the column already
            var events = new List<HuntChanged>();
            session.Events.Subscribe<HuntChanged>(events.Add);

            // A column without landmark has radius 0: 0x5FA4C0 gives 1 anywhere in it.
            hero.Position = new WorldPosition(mission.ColumnX * 2048 + 1024.5, hero.Position.Y, mission.ColumnZ * 2048 + 1024.5);
            session.Clock.StepTicks(1);
            Assert.AreEqual(AlphaMissionState.Active, mission.State);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(mission.TargetKind, events[0].Kind.Value);
            Assert.AreEqual((int)AlphaMissionType.Monster, events[0].MissionType);

            // A plain boss (no type-1 mission in its column) carries no 0x2000 flag.
            AlphaMission plain = hero.Missions.Columns.FirstOrDefault(c => c.HasBoss && !c.BossIsMissionTarget);
            if (plain != null)
            {
                Kill(session, Register(session, plain));
                Assert.AreEqual(0, hero.Platinum);
            }

            Combatant boss = Register(session, mission);
            Kill(session, boss);
            Assert.AreEqual(AlphaMissionState.Complete, mission.State);
            Assert.AreEqual(2, events.Count);
            Assert.IsTrue(events[1].Complete);
            Assert.Greater(hero.Platinum, 0, "0x596919 mission reward");
            long platinum = hero.Platinum;
            Kill(session, boss);
            Assert.AreEqual(platinum, hero.Platinum, "a finished mission does not pay twice");
        }

        [Test]
        public void MissionsAndGeneratedRegionsSurviveTheSessionSave()
        {
            GameSession session = StartSession(out _);
            AlphaMission mission = FindMission(session, AlphaMissionType.Monster);
            Assert.IsNotNull(mission);
            SetState(mission, AlphaMissionState.Active);

            var data = new SessionSaveData();
            GameSlotStore.WriteMissions(session.Hero.Missions, data);
            Assert.IsNull(data.huntMissions);
            var restored = JsonUtility.FromJson<SessionSaveData>(JsonUtility.ToJson(data));

            GameSession other = StartSession(out _, attach: false);
            Assert.IsTrue(GameSlotStore.RestoreMissions(other.Hero, restored));
            Assert.AreEqual(Describe(session.Hero.Missions.Columns), Describe(other.Hero.Missions.Columns));
            CollectionAssert.AreEquivalent(session.Hero.Missions.GeneratedRegions.ToList(),
                other.Hero.Missions.GeneratedRegions.ToList());
            Assert.IsTrue(other.Hero.Missions.TryGet(mission.ColumnX, mission.ColumnZ, out AlphaMission loaded));
            Assert.AreEqual(AlphaMissionState.Active, loaded.State);
        }

        [Test]
        public void LegacyHuntsAreIgnoredAndBrokenRecordsRejected()
        {
            GameSession session = StartSession(out _, attach: false);
            var legacy = new SessionSaveData
            {
                huntMissions = new[] { new HuntMissionSaveData { target = 123, kind = 0x1C, complete = true } },
            };
            Assert.IsTrue(GameSlotStore.RestoreMissions(session.Hero, legacy));
            Assert.IsEmpty(session.Hero.Missions.Columns);

            Assert.IsFalse(GameSlotStore.RestoreMissions(session.Hero, new SessionSaveData { alphaMissionRegions = new[] { 1 } }));
            Assert.IsFalse(GameSlotStore.RestoreMissions(session.Hero,
                new SessionSaveData { alphaMissions = new[] { new AlphaMissionSaveData { columnX = 10, columnZ = 10, type = 99 } } }));
        }

        private static AlphaMission FindMission(GameSession session, AlphaMissionType type)
        {
            HeroState hero = session.Hero;
            for (int dx = 0; dx <= 2; dx++)
            {
                for (int dz = 0; dz <= 2; dz++)
                {
                    hero.Position = new WorldPosition((StartRegion + dx) * 16384 + 8192.5, hero.Position.Y,
                        (StartRegion + dz) * 16384 + 8192.5);
                    session.Clock.StepTicks(1);
                    AlphaMission found = hero.Missions.Missions.FirstOrDefault(m => m.Type == type);
                    if (found != null) return found;
                }
            }

            return null;
        }

        private static void SetState(AlphaMission mission, AlphaMissionState state) =>
            typeof(AlphaMission).GetProperty(nameof(AlphaMission.State)).SetValue(mission, state);

        private static Combatant Register(GameSession session, AlphaMission column)
        {
            StableEntityId id = MissionJournal.BossId(column);
            if (session.Combat.TryGet(id, out Combatant existing)) return existing;
            Combatant boss = Combatant.CreateCreature(id, new KindId(column.BossKind), "Boss", column.BossLevel, 0);
            boss.Hostile = true;
            boss.Position = session.Hero.Position;
            session.Combat.Register(boss);
            return boss;
        }

        private static void Kill(GameSession session, Combatant fighter)
        {
            fighter.Health.ApplyDamage(fighter.Health.CurrentHealth);
            session.Events.Publish(new EntityDied { Entity = fighter.Id, Killer = session.Hero.Id, TimeMs = session.Clock.TimeMs });
        }

        private static GameSession StartSession(out AlphaTerrainGenerator terrain, bool attach = true)
        {
            var session = new GameSession();
            Assert.IsTrue(session.EnterMainMenu());
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { GeneratorId = "test" }, 0));
            Assert.IsTrue(session.CompleteLoading());
            terrain = new AlphaTerrainGenerator(Seed);
            if (attach) session.Hero.Missions.AttachWorld(terrain, (int)Seed, null);
            return session;
        }

        private static string Describe(IEnumerable<AlphaMission> missions) => string.Join("\n", missions.Select(m =>
            $"{m.ColumnX},{m.ColumnZ} L{m.Level} t{(int)m.Type} k{m.TargetKind:x} s{m.Seed} st{(int)m.State} " +
            $"{m.Progress}/{m.Required} z{m.TargetZoneX},{m.TargetZoneZ} b{m.BossKind:x}:{m.BossLevel}:{m.BossQuality}@{m.BossZoneX},{m.BossZoneZ} " +
            $"m{m.MaxLevel} c{m.LevelClass}"));
    }
}
