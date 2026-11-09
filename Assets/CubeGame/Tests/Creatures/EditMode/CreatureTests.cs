// D86 M0-06 — EditMode checks of the proven creature contracts, the Alpha AI and the spawn schema.
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Features.AI;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class CreatureContractTests
    {
        static CreatureState Monster(double hp = 100, int kind = 0x00)
        {
            return new CreatureState
            {
                StableId = "spawn:1:2:3:4:0", Kind = kind, Faction = (int)CreatureFaction.Monster,
                Level = 3, Stars = 0, MaxHealth = hp, Health = hp,
            };
        }

        [Test]
        public void AiClockAccumulates31Milliseconds()
        {
            var clock = new AiClock();
            Assert.AreEqual(0, clock.Add(30));
            Assert.AreEqual(1, clock.Add(1));
            Assert.AreEqual(2, clock.Add(62));
            Assert.AreEqual(0, clock.AccumulatorMs);
        }

        [Test]
        public void ThreatDecaysResetsAndPausesTargeting()
        {
            var threat = new ThreatTable();
            threat.Add("player", 10.0);
            for (int i = 0; i < 5; i++) threat.Tick();
            Assert.Less(threat.Get("player"), 10.0);
            Assert.AreEqual("player", threat.TopTarget());

            threat.MarkUnreachable("player", 21000);
            Assert.AreEqual(0, threat.Count, "unreachable longer than 20 s clears the table");
            Assert.AreEqual(ThreatTable.RescanMs, threat.RescanRemainingMs);
            for (int i = 0; i < 97; i++) threat.Tick();
            Assert.AreEqual(0, threat.RescanRemainingMs, "rescan pause is 3 s of 31 ms ticks");
        }

        [Test]
        public void PotionBranchesFollowAlphaOrder()
        {
            var state = Monster(100);
            state.Health = 20;                       // 20 % < 25 %
            state.ActionTimerMs = 500;
            Assert.AreEqual(PotionDecision.InventoryPotion,
                CreatureRules.EvaluatePotion(state, false, true, 400, false));
            Assert.AreEqual(PotionDecision.None,
                CreatureRules.EvaluatePotion(state, false, false, 400, false));
            state.ActionTimerMs = 100;
            Assert.AreEqual(PotionDecision.None,
                CreatureRules.EvaluatePotion(state, false, true, 400, false), "the action is not over yet");

            var ally = Monster(100, 0x03);
            ally.Faction = (int)CreatureFaction.Ally;
            ally.Health = 49;
            Assert.IsTrue(CreatureRules.ShouldDrinkPotion(ally.Health, ally.MaxHealth, ally.Faction, false));
            Assert.IsFalse(CreatureRules.ShouldDrinkPotion(ally.Health, ally.MaxHealth, ally.Faction, true), "only out of combat");
            Assert.AreEqual(PotionDecision.TickBlocked, CreatureRules.EvaluatePotion(ally, true, false, 0, false));
        }

        [Test]
        public void DodgeNeedsRollZeroMod10NonBossAndCloseRange()
        {
            var state = Monster();
            Assert.IsTrue(CreatureRules.TryDodge(10, false, 1.5, state));
            Assert.AreEqual(600, state.ActionLockMs);
            Assert.IsFalse(CreatureRules.TryDodge(11, false, 1.5, state));
            Assert.IsFalse(CreatureRules.TryDodge(10, true, 1.5, state), "bosses never dodge");
            Assert.IsFalse(CreatureRules.TryDodge(10, false, 2.0, state));
        }

        [Test]
        public void FriendlyFactionRegeneratesTenPercentPerSecond()
        {
            var npc = Monster(100, 0x03);
            npc.Faction = (int)CreatureFaction.Npc;
            npc.Health = 10;
            CreatureRules.RegenerateFriendly(npc, 1.0);
            Assert.AreEqual(20.0, npc.Health, 1e-9);
            CreatureRules.RegenerateFriendly(npc, 100.0);
            Assert.AreEqual(npc.MaxHealth, npc.Health);
            var monster = Monster(100);
            monster.Health = 10;
            CreatureRules.RegenerateFriendly(monster, 1.0);
            Assert.AreEqual(10.0, monster.Health, 1e-9, "only faction 5 regenerates");
        }

        [Test]
        public void StableSpawnIdAndBounds()
        {
            Assert.AreEqual("spawn:1:2:3:4:5", SpawnTable.SpawnId(1, 2, 3, 4, 5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SpawnTable.SpawnId(AlphaContentTables.WorldRegionGrid, 0, 0, 0, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SpawnTable.SpawnId(0, 0, AlphaContentTables.RegionCells, 0, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SpawnTable.SpawnId(0, 0, 0, 0, -1));
        }

        [Test]
        public void StateStoreRoundTripsThreatAndRejectsDamage()
        {
            var state = Monster(120);
            state.Health = 77.5;
            state.Threat["player"] = 4.25;
            var blob = CreatureStateStore.SerializeState(state);
            Assert.IsTrue(CreatureStateStore.TryParseState(blob, out var restored, out var error), error);
            Assert.AreEqual(77.5, restored.Health, 1e-9);
            Assert.AreEqual(4.25, restored.Threat["player"], 1e-9);
            Assert.AreEqual(state.StableId, restored.StableId);

            var store = new CreatureStateStore();
            store.Save(state);
            Assert.IsTrue(store.TryRestore(state.StableId, out var fromStore, out error), error);
            Assert.AreEqual(77.5, fromStore.Health, 1e-9);

            Assert.IsFalse(CreatureStateStore.TryParseState("M0NPC|2|x", out _, out var badVersion));
            StringAssert.Contains("header", badVersion);
            var damaged = blob.Replace("|77.5|", "|999.0|");
            Assert.IsFalse(CreatureStateStore.TryParseState(damaged, out _, out var badHealth));
            StringAssert.Contains("health", badHealth);
        }

        [Test]
        public void PassiveObjectsAreRecognisedAndHaveNoActions()
        {
            Assert.IsTrue(CreatureKinds.IsPassiveObject(0x78));
            Assert.AreEqual((int)CreatureFaction.Passive, CreatureKinds.FactionOf(0x78));
            Assert.AreEqual(0, CreatureKinds.ActionsOf(0x78).Length);
            Assert.IsTrue(CreatureKinds.IsBoss(0x65));
            Assert.IsFalse(CreatureKinds.IsBoss(0x02));
            Assert.IsTrue(CreatureKinds.TryActionClipName(0x00, 3, out var clip));
            Assert.AreEqual("action_003", clip);
        }
    }

    public class CreatureAiTests
    {
        sealed class FakeWorld : ICreatureAiWorld
        {
            public double TargetX, TargetZ;
            public bool HasTarget = true;
            public bool AcceptActions = true;
            public int ActionDurationMs;
            public bool Potion;
            public readonly List<int> Requested = new List<int>();
            public int Moves;
            public uint Roll = 10;

            public bool TryGetTarget(string id, out CreatureTarget target)
            {
                // Existence and reachability are different facts: a target may exist but be unreachable.
                target = new CreatureTarget { Id = id, X = TargetX, Z = TargetZ, Reachable = HasTarget };
                return HasTarget || ExistsWhenUnreachable;
            }

            public bool ExistsWhenUnreachable = true;

            public string FindNearestHostile(CreatureState self, double radius)
            {
                if (!HasTarget) return null;
                double dx = TargetX - self.X, dz = TargetZ - self.Z;
                return System.Math.Sqrt(dx * dx + dz * dz) <= radius ? "player" : null;
            }

            public bool TryBeginAction(CreatureState self, int actionCode, out string reason)
            {
                Requested.Add(actionCode);
                reason = AcceptActions ? "ACCEPTED" : "REFUSED_FOR_TEST";
                return AcceptActions;
            }

            public void MoveTowards(CreatureState self, double x, double z, double speed, int elapsedMs)
            {
                Moves++;
                self.X = x;
                self.Z = z;
            }

            public bool TryConsumeLifePotion(CreatureState self)
            {
                if (!Potion) return false;
                Potion = false;
                return true;
            }

            public bool HasLifePotion(CreatureState self) => Potion;
            public int CurrentActionDurationMs(CreatureState self) => ActionDurationMs;
            public int[] AvailableActions(CreatureState self) => new[] { 3, 4, 6, 54 };
            public double AggroRadius => 16.0;
            public double AttackRange => 2.0;
            public double WalkSpeed => 3.7;
            public uint NextRoll() => Roll;
        }

        static CreatureState Creature(int kind = 0x00)
        {
            return new CreatureState
            {
                StableId = "spawn:0:0:0:0:0", Kind = kind, Faction = (int)CreatureFaction.Monster,
                Level = 1, MaxHealth = 100, Health = 100,
            };
        }

        [Test]
        public void ChasesUntilInRangeThenRequestsAProvenAction()
        {
            var ai = new CreatureAi();
            var world = new FakeWorld { TargetX = 10, TargetZ = 0 };
            var self = Creature();
            ai.Tick(self, 100, world);
            Assert.AreEqual(CreatureAiState.Chase, ai.State);
            Assert.Greater(world.Moves, 0);
            Assert.AreEqual("player", ai.TargetId);

            world.TargetX = self.X;
            world.TargetZ = self.Z;
            ai.Tick(self, 100, world);
            Assert.AreEqual(CreatureAiState.Attack, ai.State);
            CollectionAssert.Contains(world.Requested, 3, "ElfMale 0x00 launches actions 3/4/6/54");
        }

        [Test]
        public void PassiveObjectNeverMovesOrActs()
        {
            var ai = new CreatureAi();
            var world = new FakeWorld { TargetX = 1, TargetZ = 0 };
            var plant = Creature(0x78);
            plant.Faction = (int)CreatureFaction.Passive;
            for (int i = 0; i < 10; i++) ai.Tick(plant, 100, world);
            Assert.AreEqual(CreatureAiState.Idle, ai.State);
            Assert.AreEqual("PASSIVE_OBJECT", ai.LastReason);
            Assert.AreEqual(0, world.Moves);
            Assert.IsEmpty(world.Requested);
        }

        [Test]
        public void StunnedCreatureDoesNotThink()
        {
            var ai = new CreatureAi();
            var world = new FakeWorld { TargetX = 1, TargetZ = 0 };
            var self = Creature();
            self.StunMs = 500;
            ai.Tick(self, 100, world);
            Assert.AreEqual("STUNNED", ai.LastReason);
            Assert.AreEqual(0, world.Moves);
        }

        [Test]
        public void InventoryPotionIsRequestedBeforeTargeting()
        {
            var ai = new CreatureAi();
            var world = new FakeWorld { TargetX = 10, TargetZ = 0, Potion = true };
            var self = Creature();
            self.Health = 10;
            self.ActionTimerMs = 1000;
            world.ActionDurationMs = 500;
            ai.Tick(self, 100, world);
            CollectionAssert.Contains(world.Requested, CreatureRules.PotionActionId);
            Assert.AreEqual(CreatureRules.PotionActionId, self.ActionId);
            Assert.AreEqual(CreatureAiState.Attack, ai.State);
        }

        [Test]
        public void DodgeUsesTheGameRollAndBossImmunity()
        {
            var ai = new CreatureAi();
            var world = new FakeWorld { Roll = 20 };
            var self = Creature();
            Assert.IsTrue(ai.TryDodge(self, 1.0, world));
            Assert.AreEqual(600, self.ActionLockMs);

            var boss = Creature(0x65);
            var world2 = new FakeWorld { Roll = 20 };
            Assert.IsFalse(ai.TryDodge(boss, 1.0, world2));
        }

        [Test]
        public void UnreachableTargetIsMarkedAndNeverChasedThroughWalls()
        {
            var ai = new CreatureAi();
            var world = new FakeWorld { TargetX = 5, TargetZ = 0 };
            var self = Creature();
            ai.Tick(self, 100, world);            // acquires target
            world.HasTarget = false;
            ai.Tick(self, 100, world);
            Assert.AreEqual("TARGET_UNREACHABLE", ai.LastReason);
            Assert.AreEqual(CreatureAiState.Chase, ai.State);
        }
    }

    public class CreatureSpawnTests
    {
        [Test]
        public void SpawnPlanIsDeterministicAndStable()
        {
            var kinds = new List<int> { 0x02, 0x20, 0x3f };
            var problems = new List<string>();
            var a = CreatureSpawnPlanner.Plan(1234u, 10, 20, 3, 4, 4, kinds, problems);
            var b = CreatureSpawnPlanner.Plan(1234u, 10, 20, 3, 4, 4, kinds, problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].StableId, b[i].StableId);
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].Level, b[i].Level);
                Assert.AreEqual(a[i].Stars, b[i].Stars);
                Assert.AreEqual(a[i].X, b[i].X, 1e-9);
                Assert.LessOrEqual(a[i].Stars, SpawnTable.StarClamp);
                Assert.GreaterOrEqual(a[i].Level, 1);
                StringAssert.StartsWith("spawn:", a[i].StableId);
            }
        }

        [Test]
        public void DifferentSeedMovesThePopulation()
        {
            var kinds = new List<int> { 0x02, 0x1c, 0x20, 0x3f, 0x65 };
            var a = CreatureSpawnPlanner.Plan(1u, 0, 0, 0, 0, 4, kinds);
            var b = CreatureSpawnPlanner.Plan(99991u, 0, 0, 0, 0, 4, kinds);
            Assert.AreNotEqual(string.Join(",", a.ConvertAll(r => r.Kind)), string.Join(",", b.ConvertAll(r => r.Kind)));
        }

        [Test]
        public void EmptyKindSetIsReportedNotInvented()
        {
            var problems = new List<string>();
            var plan = CreatureSpawnPlanner.Plan(1u, 0, 0, 0, 0, 4, new List<int>(), problems);
            Assert.IsEmpty(plan);
            CollectionAssert.Contains(problems, "SPAWN_NO_ELIGIBLE_KINDS");
        }

        [Test]
        public void VisualCatalogNeverSubstitutesAModel()
        {
            Assert.IsTrue(CreatureVisualCatalog.TryResolve(0x99, null, out var none, out var reason) == false);
            Assert.IsNull(none);
            StringAssert.Contains("MISSING_VISUAL_CONTENT", reason);
            Assert.AreEqual(AlphaStagedVisuals.All.Length, CreatureVisualCatalog.Staged.Count);
            Assert.GreaterOrEqual(CreatureVisualCatalog.Staged.Count, 28);
            Assert.IsTrue(CreatureVisualCatalog.IsStaged(0x00), "ElfMale 0x00 has a staged variant");
            Assert.IsTrue(CreatureVisualCatalog.IsStaged(0x02));
            Assert.IsTrue(CreatureVisualCatalog.IsStaged(0x4c));
            Assert.IsTrue(CreatureVisualCatalog.IsStaged(0x62));
            Assert.IsTrue(CreatureVisualCatalog.IsStaged(0x65));
            Assert.IsTrue(CreatureVisualCatalog.IsStaged(0x78), "plant objects are staged as static visuals");
            Assert.IsFalse(CreatureVisualCatalog.IsStaged(0x99));
            var plant = System.Array.Find(AlphaStagedVisuals.All, s => s.Kind == 0x78);
            Assert.AreEqual("STATIC_OBJECT", plant.ControllerMode);
            StringAssert.Contains("staged kinds", CreatureVisualCatalog.Describe());
        }

        [Test]
        public void BossAndPetFactsStayWithinEvidence()
        {
            Assert.AreEqual(10, CreatureKinds.BossKindsSorted.Count);
            CollectionAssert.Contains(CreatureKinds.BossKindsSorted, 0x65);
            Assert.IsTrue(CreatureKinds.IsBoss(0x65));
            Assert.IsFalse(CreatureKinds.IsBoss(0x02));
            StringAssert.Contains("only 0x65", CreatureKinds.BossEvidence);
            Assert.AreEqual(2, CreaturePets.SummonLimit);
            Assert.AreEqual(0x5C, CreaturePets.SummonAbilityId);
            StringAssert.Contains("UNKNOWN", CreaturePets.Status);
        }
        [Test]
        public void DecorSlotsAreDeterministicPassiveAndSeparated()
        {
            var combat = new List<int> { 0x02, 0x20 };
            var decor = new List<int> { 0x78, 0x79 };
            var problems = new List<string>();
            var a = CreatureSpawnPlanner.Plan(5u, 1, 2, 3, 4, 4, combat, decor, 3, problems);
            var b = CreatureSpawnPlanner.Plan(5u, 1, 2, 3, 4, 4, combat, decor, 3, new List<string>());
            Assert.IsEmpty(problems, string.Join("\n", problems));
            var decorA = a.FindAll(r => r.Kind == 0x78 || r.Kind == 0x79);
            Assert.Greater(decorA.Count, 0, "decor slots are planned from the staged passive kinds");
            foreach (var d in decorA)
            {
                Assert.AreEqual((int)CreatureFaction.Passive, d.Faction);
                var slot = int.Parse(d.StableId.Substring(d.StableId.LastIndexOf(':') + 1),
                    System.Globalization.CultureInfo.InvariantCulture);
                Assert.GreaterOrEqual(slot, CreatureSpawnPlanner.DecorSlotOffset, "decor ids never collide with creatures");
            }
            Assert.AreEqual(decorA.Count, b.FindAll(r => r.Kind == 0x78 || r.Kind == 0x79).Count);
        }

        [Test]
        public void AllStagedKindsResolveAgainstTheCache()
        {
            var source = AlphaContentSource.Resolve();
            if (source.State != AlphaContentState.Ready) Assert.Ignore(source.Diagnosis);
            var library = new AlphaActorLibrary(source);
            foreach (var profile in CreatureVisualCatalog.Staged)
                Assert.IsTrue(CreatureVisualCatalog.TryResolve(profile.Kind, library, out var id, out var reason), reason);
            Assert.GreaterOrEqual(CreatureVisualCatalog.AvailableKinds(library).Count, AlphaStagedVisuals.All.Length);
        }
    }
}
