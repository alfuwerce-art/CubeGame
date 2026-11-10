// D1 REVIEW-FIX — EditMode checks of the population sources: recovered Alpha rules vs the CUBEGAME fallback.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.World;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaWorldPopulationTests
    {
        static readonly ZoneKey ZoneA = new ZoneKey(32800, 32800);
        static readonly ZoneKey ZoneB = new ZoneKey(32801, 32800);
        const int Wolf = 0x13;        // level range 3…6, pack 1…1
        const int PackKind = 0x64;    // level range 6…9, pack 1…5

        static AlphaRecoveredPopulation Recovered(uint seed, int kind = Wolf) =>
            new AlphaRecoveredPopulation(new WorldSeed(seed), (z, x, zz) => kind, (z, x, zz) => 64.0);

        static FallbackPopulationPolicy Fallback(uint seed, int level = 5) =>
            new FallbackPopulationPolicy(new WorldSeed(seed), (z, x, zz) => Wolf, z => level, (z, x, zz) => 64.0);

        // ---- recovered Alpha rules -------------------------------------------------------------------------

        [Test]
        public void WildGridConstantsAreTheOnesReadFromCubeExe()
        {
            Assert.AreEqual(3, AlphaWildSpawnRules.CellsPerAxis);
            Assert.AreEqual(85, AlphaWildSpawnRules.CellStep);
            Assert.AreEqual(24, AlphaWildSpawnRules.CellOffset);
            Assert.AreEqual(10, AlphaWildSpawnRules.JitterRange);
            Assert.IsTrue(AlphaWildSpawnRules.CellSkipped(4));      // rand()&3 == 0
            Assert.IsFalse(AlphaWildSpawnRules.CellSkipped(5));
            Assert.AreEqual(24, AlphaWildSpawnRules.CellCoordinate(0, 0));
            Assert.AreEqual(24 + 85 + 9, AlphaWildSpawnRules.CellCoordinate(1, 19));
            Assert.AreEqual(24 + 170 + 9, AlphaWildSpawnRules.CellCoordinate(2, 9), "the last cell stays inside the 256 zone");
            Assert.AreEqual(21600000, AlphaWildSpawnRules.HostileWindowStartMs);
            Assert.AreEqual(86400000, AlphaWildSpawnRules.HostileWindowEndMs);
        }

        [Test]
        public void LeadersStandOnTheAlphaGridCells()
        {
            int leaders = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                foreach (SpawnDefinition s in Recovered(seed).SpawnsForZone(ZoneA))
                {
                    Assert.AreEqual(0, s.SpawnIndex % AlphaRecoveredPopulation.IndicesPerCell, "kind 0x13 has no pack");
                    int cell = s.SpawnIndex / AlphaRecoveredPopulation.IndicesPerCell;
                    int cx = cell / 3, cz = cell % 3;
                    double lx = s.Position.X - ZoneA.X * 256.0, lz = s.Position.Z - ZoneA.Z * 256.0;
                    Assert.That(lx, Is.InRange(24 + 85 * cx, 24 + 85 * cx + 9));
                    Assert.That(lz, Is.InRange(24 + 85 * cz, 24 + 85 * cz + 9));
                    Assert.AreEqual(64.0, s.Position.Y);
                    leaders++;
                }
            }

            // 9 cells, each kept with probability 3/4: 20 zones give about 135 leaders, never more than 180.
            Assert.That(leaders, Is.InRange(100, 170));
        }

        [Test]
        public void LevelComesFromTheExecutedRangeOfTheKindNotFromTheZone()
        {
            foreach (SpawnDefinition s in Recovered(7).SpawnsForZone(ZoneA)) Assert.That(s.Level, Is.InRange(3, 6));
            foreach (SpawnDefinition s in Recovered(7, PackKind).SpawnsForZone(ZoneA)) Assert.That(s.Level, Is.InRange(6, 9));
            AlphaSpawnPipelineData.LevelRange(0x9A, out int min, out int max);
            Assert.AreEqual(47, min);
            Assert.AreEqual(81, max);
            AlphaSpawnPipelineData.LevelRange(0x05, out min, out max);
            Assert.AreEqual(2, min, "default range of 0x444020");
            Assert.AreEqual(4, max);
            Assert.AreEqual(3, AlphaWildSpawnRules.Level(Wolf, 0));
            Assert.AreEqual(6, AlphaWildSpawnRules.Level(Wolf, 3));
            Assert.AreEqual(3, AlphaWildSpawnRules.Level(Wolf, 4));
        }

        [Test]
        public void PacksFollowTheExecutedCountRangeAndStandOnTheRadius8Circle()
        {
            AlphaSpawnPipelineData.PackRange(PackKind, out int min, out int max);
            Assert.AreEqual(1, min);
            Assert.AreEqual(5, max);
            Assert.AreEqual(0, AlphaWildSpawnRules.ExtraPackMembers(PackKind, 0));
            Assert.AreEqual(4, AlphaWildSpawnRules.ExtraPackMembers(PackKind, 4));
            Assert.AreEqual(0, AlphaWildSpawnRules.ExtraPackMembers(Wolf, 12345));
            bool sawPack = false;
            for (uint seed = 1; seed <= 10; seed++)
            {
                var byCell = Recovered(seed, PackKind).SpawnsForZone(ZoneA)
                    .GroupBy(s => s.SpawnIndex / AlphaRecoveredPopulation.IndicesPerCell);
                foreach (var cell in byCell)
                {
                    SpawnDefinition leader = cell.Single(s => s.SpawnIndex % AlphaRecoveredPopulation.IndicesPerCell == 0);
                    Assert.LessOrEqual(cell.Count(), 5, "pack range max is 5 including the leader");
                    foreach (SpawnDefinition member in cell.Where(s => s != leader))
                    {
                        double dx = member.Position.X - leader.Position.X, dz = member.Position.Z - leader.Position.Z;
                        Assert.AreEqual(8.0, Math.Sqrt(dx * dx + dz * dz), 1e-9);
                        Assert.AreEqual(PackKind, member.Kind.Value, "kind 0x64 has no pair to re-roll into");
                        sawPack = true;
                    }
                }
            }

            Assert.IsTrue(sawPack);
        }

        [Test]
        public void CompanionKindRerollsOnlyInsideAlphaPairs()
        {
            Assert.AreEqual(0x02, AlphaWildSpawnRules.CompanionKind(0x03, 0));
            Assert.AreEqual(0x03, AlphaWildSpawnRules.CompanionKind(0x02, 1));
            Assert.AreEqual(0x28, AlphaWildSpawnRules.CompanionKind(0x25, 3));
            Assert.AreEqual(0x06, AlphaWildSpawnRules.CompanionKind(0x06, 1), "0x06 is not in the switch");
            Assert.AreEqual(Wolf, AlphaWildSpawnRules.CompanionKind(Wolf, 1));
        }

        [Test]
        public void RecoveredSourceIsDeterministicAndNeverInventsAKind()
        {
            var a = Recovered(666666).SpawnsForZone(ZoneA);
            var b = Recovered(666666).SpawnsForZone(ZoneA);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].SpawnIndex, b[i].SpawnIndex);
                Assert.AreEqual(a[i].Level, b[i].Level);
            }

            Assert.AreEqual(a.Count, a.Select(s => s.SpawnIndex).Distinct().Count(), "indices are unique inside a zone");
            var nothing = new AlphaRecoveredPopulation(new WorldSeed(1), (z, x, zz) => -1);
            Assert.IsEmpty(nothing.SpawnsForZone(ZoneA));
            Assert.Throws<ArgumentNullException>(() => new AlphaRecoveredPopulation(new WorldSeed(1), null));
        }

        [Test]
        public void EveryTemplateSiteOfTheClientIsListed()
        {
            Assert.AreEqual(35, AlphaSpawnPipelineData.Sites.Length, "E8 scan of .text for call 0x5ADB20");
            Assert.AreEqual(7, AlphaSpawnPipelineData.Sites.Select(s => s.Function).Distinct().Count());
            Assert.AreEqual(13, AlphaSpawnPipelineData.Sites.Count(s => s.Function == "village_ruins_generator_0x5AF170"),
                "the village/ruins generator is one of seven functions that create templates, not the only one");
            Assert.AreEqual(5, AlphaSpawnPipelineData.Sites.Count(s => s.Function == "zone_generator_0x5E4850"));
            foreach (int cls in new[] { 0x80, 0x81, 0x82, 0x83, 0x84 })
            {
                Assert.IsTrue(AlphaSpawnPipelineData.Sites.Any(s => s.NpcClasses.Contains(cls)), $"class 0x{cls:X}");
            }
        }

        // ---- life cycle ------------------------------------------------------------------------------------

        [Test]
        public void TemplateBecomesACreatureOnlyUnderTheAlphaCondition()
        {
            const int day = 7;
            Func<bool, int, int, int, double, bool> create = (live, timer, deathDay, time, dist) =>
                AlphaSpawnLifecycle.ShouldCreate(live, timer, deathDay, day, 0, 0, time, dist,
                    AlphaSpawnLifecycle.DefaultActivationRadius);
            Assert.IsTrue(create(false, 0, 0, 1000, 199.9));
            Assert.IsFalse(create(false, 0, 0, 1000, 200.0), "player must be inside the radius");
            Assert.IsFalse(create(true, 0, 0, 1000, 10.0), "one creature per template");
            Assert.IsFalse(create(false, 5000, day, 1000, 10.0), "killed today and the timer still runs");
            Assert.IsTrue(create(false, 5000, day - 1, 1000, 10.0), "a new day respawns regardless of the timer");
            Assert.IsTrue(create(false, 0, day, 1000, 10.0), "timer ran out on the same day");
            Assert.AreEqual(1200000, AlphaSpawnLifecycle.RespawnTimerMs);
            Assert.AreEqual(1199000, AlphaSpawnLifecycle.TickTimer(AlphaSpawnLifecycle.RespawnTimerMs, 1000));
            Assert.AreEqual(0, AlphaSpawnLifecycle.TickTimer(500, 1000));
            Assert.IsTrue(AlphaSpawnLifecycle.ShouldDespawn(512.0));
            Assert.IsFalse(AlphaSpawnLifecycle.ShouldDespawn(511.0));
        }

        [Test]
        public void HostileWildTemplatesExistOnlyInsideTheirTimeWindow()
        {
            int from = AlphaWildSpawnRules.HostileWindowStartMs, to = AlphaWildSpawnRules.HostileWindowEndMs;
            Assert.IsTrue(AlphaSpawnLifecycle.HasTimeWindow(from, to));
            Assert.IsFalse(AlphaSpawnLifecycle.InTimeWindow(from, to, from - 1), "05:59 — no new hostile creature");
            Assert.IsTrue(AlphaSpawnLifecycle.InTimeWindow(from, to, from));
            Assert.IsTrue(AlphaSpawnLifecycle.InTimeWindow(from, to, to));
            Assert.IsTrue(AlphaSpawnLifecycle.InTimeWindow(0, 0, 123), "start == end means no window");
            Assert.IsFalse(AlphaSpawnLifecycle.ShouldCreate(false, 0, 0, 1, from, to, 3600000, 10.0, 200f));
        }

        // ---- fallback --------------------------------------------------------------------------------------

        [Test]
        public void FallbackIsDeterministicUsesTheZoneLevelAndNeedsAKindSelector()
        {
            var a = Fallback(123, 9).SpawnsForZone(ZoneA);
            var b = Fallback(123, 9).SpawnsForZone(ZoneA);
            var c = Fallback(123, 9).SpawnsForZone(ZoneB);
            Assert.AreEqual(FallbackPopulationPolicy.SpawnsPerZone, a.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(9, a[i].Level);
                Assert.AreEqual(StableEntityId.ForSpawn(a[i].Region, a[i].SpawnIndex, 0),
                    StableEntityId.ForSpawn(b[i].Region, b[i].SpawnIndex, 0));
            }

            Assert.IsFalse(a.Select(s => s.Position).SequenceEqual(c.Select(s => s.Position)));
            Assert.IsEmpty(new FallbackPopulationPolicy(new WorldSeed(1), null).SpawnsForZone(ZoneA),
                "no kind selector -> nothing; the vendor item table is never used as a kind source");
        }

        [Test]
        public void FacadeSeparatesTheTwoSourcesAndIsEmptyWithoutAKind()
        {
            var seed = new WorldSeed(42);
            Assert.IsEmpty(new AlphaWorldPopulation(seed, z => 5, (z, x, zz) => 1.0).SpawnsForZone(ZoneA));
            var fallback = new AlphaWorldPopulation(seed, z => 5, kindSelector: (z, x, zz) => Wolf);
            var recovered = new AlphaWorldPopulation(seed, kindSelector: (z, x, zz) => Wolf, recovered: true);
            Assert.IsFalse(fallback.UsesRecoveredRules);
            Assert.IsTrue(recovered.UsesRecoveredRules);
            Assert.IsTrue(fallback.SpawnsForZone(ZoneA).All(s => s.Provenance == FallbackPopulationPolicy.Provenance));
            Assert.IsTrue(recovered.SpawnsForZone(ZoneA).All(s => s.Provenance == AlphaRecoveredPopulation.Provenance));
        }

        // ---- ALPHA_* and CUBEGAME_* must not be mixed ---------------------------------------------------------

        static IEnumerable<string> ConstStrings(Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue());

        [Test]
        public void FallbackCodeNeverCarriesAnAlphaStatusAndRecoveredRulesNeverHideTheContractPart()
        {
            foreach (Type type in new[] { typeof(FallbackPopulationPolicy), typeof(PatrolRoute) })
            {
                foreach (string text in ConstStrings(type))
                {
                    StringAssert.DoesNotContain("ALPHA_", text, type.Name);
                    StringAssert.DoesNotContain("EMULATED_", text, type.Name);
                    StringAssert.DoesNotContain("TRANSFERRED_", text, type.Name);
                }
            }

            StringAssert.StartsWith("CUBEGAME_CONTRACT", FallbackPopulationPolicy.Provenance);
            StringAssert.StartsWith("CUBEGAME_FALLBACK", PatrolRoute.Provenance);
            foreach (string status in new[] { AlphaSpawnLifecycle.Status, AlphaWildSpawnRules.Status, AlphaNpcDayWindow.Status })
            {
                StringAssert.StartsWith("ALPHA_CODE_STATIC", status);
                StringAssert.DoesNotContain("CUBEGAME", status);
            }

            // The recovered source mixes by necessity (Alpha rules, own random stream): both parts must be named.
            StringAssert.Contains("ALPHA_CODE_STATIC", AlphaRecoveredPopulation.Provenance);
            StringAssert.Contains("random stream/kind source=CUBEGAME_CONTRACT", AlphaRecoveredPopulation.Provenance);
            foreach (SpawnDefinition s in Fallback(5).SpawnsForZone(ZoneA))
            {
                StringAssert.DoesNotContain("ALPHA_", s.Provenance);
                StringAssert.DoesNotContain("EMULATED_", s.Provenance);
            }
        }

        [Test]
        public void TheOldSpawnTableIsLabelledAsVendorItemsAndIsNotAKindSource()
        {
            StringAssert.Contains("VENDOR ITEMS", AlphaSpawnTable.Status);
            StringAssert.Contains("not creature kinds", AlphaSpawnTable.Status);
            // Its "kinds" are item subtypes 0…0x17: as creature ids they would be the playable races.
            Assert.IsTrue(AlphaSpawnTable.Kinds.All(k => k <= 0x17));
            foreach (Type type in new[] { typeof(AlphaWorldPopulation), typeof(AlphaRecoveredPopulation), typeof(FallbackPopulationPolicy) })
            {
                bool refersToTable = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(f => f.FieldType == typeof(AlphaSpawnEntryDef[]));
                Assert.IsFalse(refersToTable, type.Name + " must not hold rows of the vendor item table");
            }
        }
    }
}
