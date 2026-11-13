using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Combat;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// M0-05A first slice: attack phases on game time, one hit per strike through Core S03/S04, death,
    /// determinism, refusals with reasons, pause, and the hero combatant sharing the hero's Core state.
    /// </summary>
    public sealed class FullM0CombatTests
    {
        /// <summary>Level-1 common greatsword used by the combat-number tests (before C1 it was the start weapon).</summary>
        private static ItemInstance Greatsword() => new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Greatsword, 1, 0, 1);

        private static readonly KindId Human = new KindId(0x02);
        private static readonly KindId Wolf = new KindId(0x1c);
        private static readonly ActionId Attack57 = new ActionId(57);

        // Alpha action 57 timings from the S05 clip provenance: windup 790, action end 1382, recovery 1392.
        private static readonly ActionTiming Alpha57 = ActionTiming.FromAlpha(790, 1382, 1392);

        private GameEventBus _events;
        private List<object> _log;
        private CombatSystem _combat;
        private Combatant _hero;
        private Combatant _wolf;

        [TestCase(CharacterClass.Warrior, -1, -1, 6)]
        [TestCase(CharacterClass.Ranger, 0, -1, 1)]
        [TestCase(CharacterClass.Warrior, 0, 13, 10)]
        [TestCase(CharacterClass.Rogue, 3, 3, 19)]
        [TestCase(CharacterClass.Warrior, 4, 13, 6)]
        [TestCase(CharacterClass.Warrior, 5, 13, 14)]
        [TestCase(CharacterClass.Warrior, 17, 13, 57)]
        [TestCase(CharacterClass.Warrior, 18, -1, 57)]
        [TestCase(CharacterClass.Warrior, 0, 6, -1)]
        [TestCase(CharacterClass.Ranger, 6, -1, -1)]
        [TestCase(CharacterClass.Ranger, 8, -1, -1)]
        [TestCase(CharacterClass.Warrior, 11, -1, -1)]
        [TestCase(CharacterClass.Mage, 0, -1, -1)]
        public void FirstMeleeActionFollowsAlphaPrimaryInput(CharacterClass characterClass, int main, int off, int expected)
        {
            ItemInstance mainHand = main < 0 ? null : new ItemInstance(ItemType.Weapon, (byte)main, 1, 0, 1);
            ItemInstance offHand = off < 0 ? null : new ItemInstance(ItemType.Weapon, (byte)off, 1, 0, 2);
            ActionId? action = AlphaPrimaryAttack.SelectMelee(characterClass, mainHand, offHand);
            Assert.AreEqual(expected, action?.Value ?? -1);
        }

        [SetUp]
        public void SetUp()
        {
            _events = new GameEventBus();
            _log = new List<object>();
            _events.Subscribe<ActionStarted>(e => _log.Add(e));
            _events.Subscribe<ActionPhaseChanged>(e => _log.Add(e));
            _events.Subscribe<ActionResolved>(e => _log.Add(e));
            _events.Subscribe<HitResolved>(e => _log.Add(e));
            _events.Subscribe<EntityDied>(e => _log.Add(e));
            _combat = new CombatSystem(_events, new ActionTimingTable().Add(Human, Attack57, Alpha57), 666666);

            _hero = Combatant.Create(StableEntityId.Hero, Human, "Hero", CharacterClass.Warrior, 1);
            _hero.Loadout.Equip(EquipmentSlot.MainHand, Greatsword());
            _hero.Position = new WorldPosition(100, 50, 100);
            _hero.Yaw = 0f; // facing +Z
            _wolf = Combatant.Create(StableEntityId.ForRuntime(1), Wolf, "Wolf", CharacterClass.Warrior, 1);
            _wolf.Hostile = true;
            _wolf.Position = new WorldPosition(100, 50, 102);
            _combat.Register(_hero);
            _combat.Register(_wolf);
        }

        [Test]
        public void AlphaTimingMapsToPhases()
        {
            Assert.AreEqual(790, Alpha57.WindupMs);
            Assert.AreEqual(592, Alpha57.StrikeMs);
            Assert.AreEqual(10, Alpha57.RecoverMs);
            Assert.AreEqual("EMULATED_ALPHA_CODE", Alpha57.Source);
        }

        [Test]
        public void HitResolvesExactlyAtStrikeStartWithCoreDamage()
        {
            double before = _wolf.Health.CurrentHealth;
            Assert.IsTrue(_combat.TryStartAttack(_hero.Id, Attack57, 1000));

            _combat.Update(1000 + 789);
            Assert.AreEqual(before, _wolf.Health.CurrentHealth, "no damage during windup");
            Assert.IsTrue(_combat.TryGetPhase(_hero.Id, out ActionPhase phase));
            Assert.AreEqual(ActionPhase.Windup, phase);

            _combat.Update(1000 + 790);
            Assert.IsTrue(_combat.TryGetPhase(_hero.Id, out phase));
            Assert.AreEqual(ActionPhase.Strike, phase);

            // Independent recomputation with Core only: same inputs and the same strike seed.
            var expectedTarget = new HealthPool(before);
            CombatAttack attack = new EquipmentCombatAdapter().BuildAttack(_hero.Sheet, _hero.Loadout, 1.0,
                EquipmentCombatAdapter.ComputeTargetArmor(_wolf.Loadout), 0.0);
            double weaponDamage = CombatFormulas.WeaponDamage(attack.WeaponBase, attack.Level, attack.Upgrades) + attack.ItemWeaponDamageBonus;
            var strike = new CombatAttack(2.0 * weaponDamage, 1, attack.HitChance) { TargetArmor = attack.TargetArmor };
            CombatOutcome expected = new DamageResolver(new SeededCombatRandom(_combat.StrikeSeed(_hero.Id, _wolf.Id, 1)))
                .Resolve(strike, expectedTarget);

            Assert.IsTrue(expected.IsHit);
            Assert.Greater(expected.AppliedDamage, 0.0);
            Assert.AreEqual(expectedTarget.CurrentHealth, _wolf.Health.CurrentHealth, 1e-9);
            HitResolved hit = Single<HitResolved>();
            Assert.AreEqual(expected.AppliedDamage, hit.Damage, 1e-9);
            Assert.AreEqual(1000 + 790, hit.TimeMs);

            _combat.Update(1000 + 5000);
            Assert.IsFalse(_combat.IsActing(_hero.Id));
            Assert.AreEqual(1, Count<HitResolved>(), "one strike = one hit");
            ActionResolved done = Single<ActionResolved>();
            Assert.AreEqual(ActionOutcome.Completed, done.Outcome);
            Assert.AreEqual(1000 + 1392, done.TimeMs);
        }

        [Test]
        public void PhaseEventsCarryAlphaDurations()
        {
            _combat.TryStartAttack(_hero.Id, Attack57, 0, "action_057");
            _combat.Update(10000);
            ActionStarted started = Single<ActionStarted>();
            CollectionAssert.AreEqual(new long[] { 790, 592, 10 }, started.PhaseDurationsMs);
            Assert.AreEqual("action_057", started.AnimationKey);

            var phases = new List<ActionPhase>();
            foreach (object e in _log)
            {
                if (e is ActionPhaseChanged changed)
                {
                    phases.Add(changed.Phase);
                }
            }

            CollectionAssert.AreEqual(new[] { ActionPhase.Windup, ActionPhase.Strike, ActionPhase.Recover, ActionPhase.Finished }, phases);
        }

        [Test]
        public void TargetOutsideReachOrBehindIsNotHit()
        {
            _wolf.Position = new WorldPosition(100, 50, 105);
            _combat.TryStartAttack(_hero.Id, Attack57, 0);
            _combat.Update(2000);
            Assert.AreEqual(0, Count<HitResolved>());

            _wolf.Position = new WorldPosition(100, 50, 98); // behind the hero
            _combat.TryStartAttack(_hero.Id, Attack57, 3000);
            _combat.Update(6000);
            Assert.AreEqual(0, Count<HitResolved>());
            Assert.AreEqual(_wolf.Health.MaxHealth, _wolf.Health.CurrentHealth);
        }

        [Test]
        public void SameSideIsNeverHit()
        {
            _wolf.Hostile = false;
            _combat.TryStartAttack(_hero.Id, Attack57, 0);
            _combat.Update(2000);
            Assert.AreEqual(0, Count<HitResolved>());
        }

        [Test]
        public void SecondAttackWhileActingIsRefusedWithReason()
        {
            Assert.IsTrue(_combat.TryStartAttack(_hero.Id, Attack57, 0));
            Assert.IsFalse(_combat.TryStartAttack(_hero.Id, Attack57, 100));
            StringAssert.Contains("still performing", _combat.LastRejection);
            Assert.AreEqual(1, Count<ActionStarted>());

            _combat.Update(1392);
            Assert.IsTrue(_combat.TryStartAttack(_hero.Id, Attack57, 1392), "free again after the action end");
        }

        [Test]
        public void KillPublishesDeathOnceAndDeadTargetTakesNoMoreDamage()
        {
            long now = 0;
            int guard = 0;
            while (!_wolf.IsDead && guard++ < 500)
            {
                Assert.IsTrue(_combat.TryStartAttack(_hero.Id, Attack57, now), _combat.LastRejection);
                now += 1392;
                _combat.Update(now);
            }

            Assert.IsTrue(_wolf.IsDead, "the wolf dies from repeated Core hits");
            Assert.AreEqual(1, Count<EntityDied>());
            Assert.AreEqual(_wolf.Id, Single<EntityDied>().Entity);

            int hits = Count<HitResolved>();
            _combat.TryStartAttack(_hero.Id, Attack57, now);
            _combat.Update(now + 2000);
            Assert.AreEqual(hits, Count<HitResolved>(), "dead targets are skipped");
            Assert.AreEqual(0.0, _wolf.Health.CurrentHealth);

            _wolf.Hostile = false;
            Assert.IsFalse(_combat.TryStartAttack(_wolf.Id, Attack57, now), "dead combatants cannot act");
        }

        [Test]
        public void SameSeedGivesSameDamageSequence()
        {
            List<double> first = DamageSequence(666666);
            List<double> second = DamageSequence(666666);
            CollectionAssert.AreEqual(first, second);
            Assert.Greater(first.Count, 1);
        }

        [Test]
        public void UnknownActionIsRefusedWithoutAnimationOrHit()
        {
            Assert.IsFalse(_combat.TryStartAttack(_hero.Id, new ActionId(0xFE), 0));
            StringAssert.Contains("UNKNOWN", _combat.LastRejection);
            _combat.Update(2000);
            Assert.IsEmpty(_log);
        }

        [Test]
        public void EarlierStrikeInterruptsLaterWindupEvenWhenRegisteredSecond()
        {
            var timing = new ActionTimingTable()
                .Add(Human, new ActionId(1), new ActionTiming(100, 150, 200, "test"))
                .Add(Wolf, new ActionId(1), new ActionTiming(300, 350, 400, "test"));
            _combat = new CombatSystem(_events, timing, 666666);
            _combat.Register(_hero);
            _combat.Register(_wolf);
            _wolf.Health.ApplyDamage(_wolf.Health.MaxHealth - 0.01);
            Assert.IsTrue(_combat.TryStartAttack(_wolf.Id, new ActionId(1), 0));
            Assert.IsTrue(_combat.TryGetAction(_wolf.Id, out AuthoritativeActionState interrupted));
            Assert.IsTrue(_combat.TryStartAttack(_hero.Id, new ActionId(1), 0));
            _combat.Update(500);
            Assert.AreEqual(ActionOutcome.Interrupted, interrupted.Outcome);
            Assert.AreEqual(0, interrupted.ReleaseCount);
            Assert.AreEqual(1, Count<HitResolved>());
            Assert.IsTrue(_wolf.IsDead);
        }

        [Test]
        public void SessionDrivesCombatByClockAndPauseStopsIt()
        {
            var session = new GameSession(new ActionTimingTable().Add(GameSession.HeroKind, Attack57, Alpha57));
            session.EnterMainMenu();
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { Seed = 7, GeneratorId = "test" }, 0));
            Assert.IsTrue(session.CompleteLoading());

            HeroState hero = session.Hero;
            int kitCore = 0;
            foreach (StarterKitItem item in AlphaHeroCreation.StarterKit(CharacterClass.Warrior))
            {
                kitCore += item.Core != null ? 1 : 0;
            }

            Assert.AreEqual(kitCore, hero.Items.Count, "new hero carries the Alpha start kit (C1)");
            Assert.AreEqual((byte)WeaponSubtype.Greatmace, hero.Loadout.GetEquipped(EquipmentSlot.MainHand).Subtype, "Warrior: greatmace in the hand");
            Assert.IsTrue(session.Combat.TryGet(hero.Id, out Combatant heroCombatant));
            Assert.AreSame(hero.Health, heroCombatant.Health, "hero combatant shares the hero's Core HP");

            hero.Position = new WorldPosition(10, 5, 10);
            hero.Yaw = 90f; // facing +X
            heroCombatant.Yaw = hero.Yaw;
            Combatant wolf = Combatant.Create(StableEntityId.ForRuntime(9), Wolf, "Wolf", CharacterClass.Warrior, 1);
            wolf.Hostile = true;
            wolf.Position = new WorldPosition(12, 5, 10);
            session.Combat.Register(wolf);

            Assert.IsTrue(session.Combat.TryStartAttack(hero.Id, Attack57, session.Clock.TimeMs));
            session.Pause();
            Run(session, 5.0);
            Assert.AreEqual(wolf.Health.MaxHealth, wolf.Health.CurrentHealth, "paused clock: no strike");

            session.Resume();
            Run(session, 0.5);
            Assert.AreEqual(wolf.Health.MaxHealth, wolf.Health.CurrentHealth, "0.5 s < 790 ms windup");
            Run(session, 0.4);
            Assert.Less(wolf.Health.CurrentHealth, wolf.Health.MaxHealth, "strike after 790 ms of game time");
        }

        /// <summary>Advances the session in 0.1 s frames (the clock caps ticks per frame).</summary>
        private static void Run(GameSession session, double seconds)
        {
            for (double t = 0.0; t < seconds - 1e-9; t += 0.1)
            {
                session.AdvanceFrame(0.1);
            }
        }

        private List<double> DamageSequence(int seed)
        {
            var events = new GameEventBus();
            var damages = new List<double>();
            events.Subscribe<HitResolved>(e => damages.Add(e.Damage));
            var combat = new CombatSystem(events, new ActionTimingTable().Add(Human, Attack57, Alpha57), seed);
            Combatant hero = Combatant.Create(StableEntityId.Hero, Human, "Hero", CharacterClass.Warrior, 1);
            hero.Loadout.Equip(EquipmentSlot.MainHand, Greatsword());
            Combatant wolf = Combatant.Create(StableEntityId.ForRuntime(1), Wolf, "Wolf", CharacterClass.Warrior, 1);
            wolf.Hostile = true;
            wolf.Position = new WorldPosition(0, 0, 2);
            combat.Register(hero);
            combat.Register(wolf);
            long now = 0;
            for (int i = 0; i < 5 && !wolf.IsDead; i++)
            {
                combat.TryStartAttack(hero.Id, Attack57, now);
                now += 1392;
                combat.Update(now);
            }

            return damages;
        }

        private T Single<T>()
        {
            T found = default;
            int count = 0;
            foreach (object e in _log)
            {
                if (e is T typed)
                {
                    found = typed;
                    count++;
                }
            }

            Assert.AreEqual(1, count, $"expected exactly one {typeof(T).Name}");
            return found;
        }

        private int Count<T>()
        {
            int count = 0;
            foreach (object e in _log)
            {
                if (e is T)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
