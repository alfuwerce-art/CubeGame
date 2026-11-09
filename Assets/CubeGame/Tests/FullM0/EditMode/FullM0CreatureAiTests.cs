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
        private static readonly ActionId Bite = new ActionId(37);

        private GameSession _session;
        private CreatureCombatDriver _driver;
        private Combatant _wolf;

        [SetUp]
        public void SetUp()
        {
            // Wolf action 37 from the S05 clip provenance: windup 1185, action end 1381, recovery 1392.
            var timings = new ActionTimingTable().Add(Wolf, Bite, ActionTiming.FromAlpha(1185, 1381, 1392));
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

        [TestCase(null, false)]
        [TestCase(1, false)]
        [TestCase(3, true)]
        public void OnlyAWeaponInMainHandAllowsWeaponActions(int? itemType, bool expectsAttack)
        {
            var action = new ActionId(AlphaNpcWeaponActions.WeaponOnly[0]);
            ((ActionTimingTable)_session.ActionTimings).Add(Wolf, action, ActionTiming.FromAlpha(100, 200, 300));
            _session.Clock.Ticked -= _driver.Tick;
            _driver = new CreatureCombatDriver(_session, (x, z) => 5.0, kind => new[] { action.Value }, 11u);
            _session.Clock.Ticked += _driver.Tick;
            _wolf.Position = new WorldPosition(1, 5, 0);
            RawAlphaItemId? weapon = itemType.HasValue ? new RawAlphaItemId(itemType.Value, 0) : (RawAlphaItemId?)null;
            _driver.Add(_wolf, 1, 0, (int)CreatureFaction.Monster, -1, -1, weapon);

            _session.Clock.StepTicks(1);

            Assert.IsTrue(_driver.TryGetAi(_wolf.Id, out var ai));
            Assert.AreEqual(expectsAttack, _session.Combat.IsActing(_wolf.Id), ai.LastReason);
            if (!expectsAttack)
            {
                StringAssert.Contains("ACTION_NEEDS_WEAPON", ai.LastReason);
            }
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
