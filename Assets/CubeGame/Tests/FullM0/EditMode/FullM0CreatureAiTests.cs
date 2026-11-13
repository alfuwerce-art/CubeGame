using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// M0-06C in the game: the Alpha creature AI chases the hero, starts an Alpha attack through the combat
    /// system and damages the hero only via Core; pause stops it; a dead hero is no longer a target; revive.
    /// </summary>
    public sealed class FullM0CreatureAiTests
    {
        private static readonly KindId Wolf = new KindId(0x1c);
        private static readonly ActionId Bite = new ActionId(3);

        private GameSession _session;
        private CreatureCombatDriver _driver;
        private Combatant _wolf;

        [SetUp]
        public void SetUp()
        {
            // Fixture for the legacy AI C-29 unarmed decision.
            var timings = new ActionTimingTable().Add(Wolf, Bite, new ActionTiming(100, 200, 300, "test"));
            _session = new GameSession(timings);
            _session.EnterMainMenu();
            Assert.IsTrue(_session.BeginNewWorld("slot0", new NewWorldRequest { Seed = 11, GeneratorId = "test" }, 0));
            Assert.IsTrue(_session.CompleteLoading());
            _session.Hero.Position = new WorldPosition(0, 5, 0);

            _driver = new CreatureCombatDriver(_session, (x, z) => 5.0, kind => new[] { Bite.Value }, 11u);
            _session.Clock.Ticked += _driver.Tick;
            _wolf = Combatant.CreateCreature(StableEntityId.ForRuntime(1), Wolf, "Wolf", 1, 0);
            _wolf.Position = new WorldPosition(8, 5, 0);
            _session.Combat.Register(_wolf);
            _driver.Add(_wolf, 1, 0);
        }

        [Test]
        public void CreatureHealthUsesTheAlphaCreatureRule()
        {
            Assert.AreEqual(Core.Characters.CharacterStats.CalcCreatureMaxHealth(100.0, 1, 0), _wolf.Health.MaxHealth, 1e-9);
            Assert.IsTrue(_wolf.Hostile);
        }

        [Test]
        public void WolfChasesBitesAndOnlyCoreChangesHeroHealth()
        {
            double heroMax = _session.Hero.Health.MaxHealth;
            int hits = 0;
            _session.Events.Subscribe<HitResolved>(h =>
            {
                if (h.Target.Equals(StableEntityId.Hero))
                {
                    hits++;
                }
            });

            double startDistance = Distance();
            Run(1.0);
            Assert.Less(Distance(), startDistance, "the wolf chases the hero");
            Assert.IsTrue(_driver.TryGetAi(_wolf.Id, out var ai));

            Run(6.0);
            Assert.LessOrEqual(Distance(), CreatureCombatDriverRange() + 1e-6, "the wolf stops at its attack range");
            Assert.Greater(hits, 0, $"the wolf bit the hero (ai {ai.State} {ai.LastReason})");
            Assert.Less(_session.Hero.Health.CurrentHealth, heroMax);
        }

        [TestCase(0, 6, 0x17, ProjectileKind.Arrow)]
        [TestCase(3, -1, 0x25, ProjectileKind.Magic)]
        public void C29SelectedAttackReleasesAlphaProjectile(int classId, int subtype, int code, ProjectileKind kind)
        {
            var action = new ActionId(code);
            ((ActionTimingTable)_session.ActionTimings).Add(Wolf, action, ActionTiming.FromAlpha(100, 200, 300));
            _session.Clock.Ticked -= _driver.Tick;
            _driver = new CreatureCombatDriver(_session, null, _ => new[] { code }, 11u);
            _session.Clock.Ticked += _driver.Tick;
            _wolf.Position = new WorldPosition(1.8, 5, 0);
            RawAlphaItemId? weapon = subtype < 0 ? (RawAlphaItemId?)null : new RawAlphaItemId(3, subtype);
            _driver.Add(_wolf, 1, 0, (int)CreatureFaction.Monster, classId, 0, weapon);
            ProjectileSpawned released = null;
            _session.Events.Subscribe<ProjectileSpawned>(e => released = e);

            _session.Clock.StepTicks(6);

            Assert.IsTrue(_driver.TryGetAi(_wolf.Id, out var ai));
            Assert.AreEqual(code, ai.LastSelectedAttack);
            Assert.IsNotNull(released, ai.LastReason);
            Assert.AreEqual(_wolf.Id, released.Owner);
            Assert.AreEqual((int)kind, released.Kind);
            Assert.Less(released.VelocityX, 0, "the creature aims toward the hero");
            Assert.Less(_session.Hero.Health.CurrentHealth, _session.Hero.Health.MaxHealth);
        }

        [Test]
        public void RecoveredCatNoticesChasesRequestsItsAlphaActionAndRecoversInCombat()
        {
            var kind = new KindId(0x1E);
            var pounce = new ActionId(75);
            ((ActionTimingTable)_session.ActionTimings).Add(kind, pounce, ActionTiming.FromAlpha(100, 250, 500));
            _driver.Remove(_wolf.Id);
            var cat = Combatant.CreateCreature(StableEntityId.ForRuntime(2), kind, "Cat", 1, 0);
            cat.Position = new WorldPosition(19, 5, 0);
            _session.Combat.Register(cat);
            _driver.Add(cat, 1, 0, (int)CreatureFaction.Monster, 0, 0, recovered: true);
            Assert.IsTrue(_driver.TryGetAi(cat.Id, out var ai));

            int launches = 0;
            _session.Events.Subscribe<ActionStarted>(action =>
            {
                if (!action.Entity.Equals(cat.Id)) return;
                Assert.AreEqual(pounce, action.Action);
                launches++;
            });
            _session.Clock.StepTicks(1);
            Assert.AreEqual(CreatureAiState.Notice, ai.State);
            _session.Clock.StepTicks(1);
            Assert.AreEqual(CreatureAiState.Chase, ai.State);
            for (int tick = 0; tick < 100 && launches == 0; tick++) _session.Clock.StepTicks(1);
            Assert.AreEqual(1, launches, ai.LastReason);
            Assert.AreEqual(75, ai.LastRequestedAction);
            Assert.IsTrue(_session.Combat.IsActing(cat.Id));
            Assert.AreEqual(_session.Hero.Health.MaxHealth, _session.Hero.Health.CurrentHealth, "decision only requests the action");
            _session.Hero.Position = new WorldPosition(-10, 5, 0);
            for (int tick = 0; tick < 30 && ai.State != CreatureAiState.Recover; tick++) _session.Clock.StepTicks(1);
            Assert.AreEqual(CreatureAiState.Recover, ai.State);
            for (int tick = 0; tick < 30 && ai.State != CreatureAiState.Chase; tick++) _session.Clock.StepTicks(1);
            Assert.AreEqual(CreatureAiState.Chase, ai.State);
            Assert.AreEqual(1, launches);
        }

        [Test]
        public void FriendlyCatIgnoresHeroAsTargetAndLooksAtThem()
        {
            var cat = Combatant.CreateCreature(StableEntityId.ForRuntime(2), new KindId(0x1E), "Town cat", 1, 0);
            cat.Position = new WorldPosition(1, 5, 0);
            _session.Combat.Register(cat);
            _driver.Remove(_wolf.Id);
            _driver.Add(cat, 1, 0, (int)CreatureFaction.Npc, 0, 0, recovered: true);

            _session.Clock.StepTicks(30);

            Assert.IsTrue(_driver.TryGetAi(cat.Id, out var ai));
            Assert.IsNull(ai.TargetId);
            Assert.AreEqual("LOOK_AT_PLAYER", ai.LastReason);
            Assert.AreEqual(-90f, cat.Yaw, 0.01f);
            Assert.IsFalse(_session.Combat.IsActing(cat.Id));
        }

        [Test]
        public void RecoveredActorDoesNotSubstituteAClipForAnUnknownDispatcherBranch()
        {
            _wolf.Position = new WorldPosition(1, 5, 0);
            _driver.Add(_wolf, 1, 0, (int)CreatureFaction.Monster, 0, 0, recovered: true);

            _session.Clock.StepTicks(30);

            Assert.IsTrue(_driver.TryGetAi(_wolf.Id, out var ai));
            Assert.AreEqual("UNKNOWN_ALPHA_DISPATCH", ai.LastReason);
            Assert.AreEqual(-1, ai.LastRequestedAction);
            Assert.IsFalse(_session.Combat.IsActing(_wolf.Id));
            Assert.AreEqual(_session.Hero.Health.MaxHealth, _session.Hero.Health.CurrentHealth);
        }

        [Test]
        public void RecoveredNoticeRequiresStrictRadiusAndUnobstructedVoxels()
        {
            bool wall = true;
            _session.Clock.Ticked -= _driver.Tick;
            _driver = new CreatureCombatDriver(_session, null, null, 11u,
                (x, y, z) => wall && x == 4 ? Features.World.Voxel.DefaultSolid : Features.World.Voxel.Air);
            _session.Clock.Ticked += _driver.Tick;
            _driver.Add(_wolf, 1, 0, (int)CreatureFaction.Monster, 0, 0, recovered: true);
            Assert.IsTrue(_driver.TryGetAi(_wolf.Id, out var ai));
            _session.Clock.StepTicks(1);
            Assert.IsNull(ai.TargetId, "solid voxel blocks acquisition");

            wall = false;
            _session.Hero.Position = new WorldPosition(-12, 5, 0);
            _session.Clock.StepTicks(1);
            Assert.IsNull(ai.TargetId, "distance exactly 20 is outside the Alpha scan");
            _session.Hero.Position = new WorldPosition(-11, 5, 0);
            _session.Clock.StepTicks(1);
            Assert.AreEqual(CreatureAiState.Notice, ai.State);
        }

        [Test]
        public void RecoveredApproachTimeoutReleasesTargetAndRandomWalkReturnsHome()
        {
            WorldPosition home = _wolf.Position;
            _driver.Add(_wolf, 1, 0, (int)CreatureFaction.Monster, 0, 0, recovered: true);
            Assert.IsTrue(_driver.TryGetAi(_wolf.Id, out var ai));
            ai.AttachAlphaMovement(new AlphaNpcMovementDriver(home, () => _session.Clock.WorldTimeMs));
            _session.Clock.StepTicks(1);
            _session.Hero.Position = new WorldPosition(-250, 5, 0);
            for (int i = 0; i < 615 && ai.TargetId != null; i++) _session.Clock.StepTicks(1);
            Assert.AreEqual("APPROACH_TIMEOUT", ai.LastReason);
            Assert.IsNull(ai.TargetId);
            double before = _wolf.Position.X;
            _session.Clock.StepTicks(1);
            Assert.AreEqual(CreatureAiState.Return, ai.State);
            Assert.Greater(_wolf.Position.X, before);
        }

        [Test]
        public void PauseStopsCreatures()
        {
            WorldPosition before = _wolf.Position;
            _session.Pause();
            Run(3.0);
            Assert.AreEqual(before, _wolf.Position);
        }

        [Test]
        public void DeadHeroIsNoTargetAndReviveRestoresCombat()
        {
            while (!_session.Hero.Health.IsDead)
            {
                _session.Hero.Health.ApplyDamage(50.0);
            }

            int hitsOnDeadHero = 0;
            _session.Events.Subscribe<HitResolved>(h => hitsOnDeadHero++);
            Run(4.0);
            Assert.AreEqual(0, hitsOnDeadHero, "a dead hero is not attacked");
            Assert.AreEqual(0.0, _session.Hero.Health.CurrentHealth);

            Assert.IsTrue(_session.ReviveHero(), _session.LastRejection);
            Assert.AreEqual(_session.Hero.Health.MaxHealth, _session.Hero.Health.CurrentHealth);
            Assert.IsTrue(_session.Combat.TryGet(StableEntityId.Hero, out Combatant hero));
            Assert.AreSame(_session.Hero.Health, hero.Health, "the revived hero is the combat participant");
            Assert.IsFalse(_session.ReviveHero(), "a living hero cannot be revived");
        }

        private static double CreatureCombatDriverRange() => Features.AI.CreatureAi.DefaultAttackRange;

        private double Distance()
        {
            double dx = _wolf.Position.X - _session.Hero.Position.X;
            double dz = _wolf.Position.Z - _session.Hero.Position.Z;
            return System.Math.Sqrt(dx * dx + dz * dz);
        }

        private void Run(double seconds)
        {
            for (double t = 0.0; t < seconds - 1e-9; t += 0.1)
            {
                _session.AdvanceFrame(0.1);
            }
        }
    }
}
