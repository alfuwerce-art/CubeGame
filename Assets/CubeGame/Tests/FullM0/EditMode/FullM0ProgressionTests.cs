using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Progression;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>C3: kill experience (Cube.exe 0x595A60) and level-up healing (0x447B00).</summary>
    public sealed class FullM0ProgressionTests
    {
        [TestCase(1, 0, false, 1L)]     // curve 0 → max(1, 0) = 1
        [TestCase(3, 0, false, 1L)]     // 0.0909·20 = 1.82 → 1
        [TestCase(10, 0, false, 6L)]    // (1 − 1/1.45)·20 = 6.2 → 6
        [TestCase(10, 2, false, 7L)]    // stars add to the level: L 12 → 1 − 1/1.55 → 7.1 → 7
        [TestCase(10, 0, true, 124L)]   // boss ×20 before truncation: 6.2·20 = 124.1 → 124
        public void KillExperienceFollowsAlpha(int level, int stars, bool boss, long expected)
        {
            Assert.AreEqual(expected, GameSession.KillExperience(level, stars, boss));
        }

        [Test]
        public void KillingACreatureGivesExperienceAndLevelUpHeals()
        {
            var session = new GameSession(new ActionTimingTable());
            session.EnterMainMenu();
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { Seed = 3, GeneratorId = "test" }, 0));
            Assert.IsTrue(session.CompleteLoading());
            var gained = new List<ExperienceGained>();
            session.Events.Subscribe<ExperienceGained>(gained.Add);

            HeroState hero = session.Hero;
            hero.Sheet.AddExperience(AlphaMath.ExperienceCostForNextLevel(1) - 1);   // one point before level 2
            hero.Health.ApplyDamage(50.0);
            double maxBefore = hero.Health.MaxHealth;

            var wolf = Combatant.CreateCreature(StableEntityId.ForRuntime(77), new KindId(0x13), "Wolf", 1, 0);
            session.Combat.Register(wolf);
            wolf.Health.ApplyDamage(wolf.Health.MaxHealth);
            session.Events.Publish(new EntityDied { TimeMs = 10, Entity = wolf.Id, Killer = hero.Id });

            Assert.AreEqual(1, gained.Count);
            Assert.AreEqual(1L, gained[0].Amount);
            Assert.AreEqual(1, gained[0].LevelsGained);
            Assert.AreEqual(2, hero.Sheet.Progression.Level);

            session.Clock.StepTicks(1);
            Assert.Greater(hero.Health.MaxHealth, maxBefore, "level factor raises max HP");
            Assert.AreEqual(hero.Health.MaxHealth, hero.Health.CurrentHealth, 1e-9, "level-up sets HP to the maximum (0x447B00)");
            Assert.IsTrue(session.Combat.TryGet(hero.Id, out Combatant c));
            Assert.AreSame(hero.Health, c.Health, "hero combatant uses the new HP pool");
        }

        [Test]
        public void NoExperienceForFriendlyOrForeignKills()
        {
            var session = new GameSession(new ActionTimingTable());
            session.EnterMainMenu();
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { Seed = 3, GeneratorId = "test" }, 0));
            Assert.IsTrue(session.CompleteLoading());
            var gained = new List<ExperienceGained>();
            session.Events.Subscribe<ExperienceGained>(gained.Add);

            var npc = Combatant.CreateCreature(StableEntityId.ForRuntime(1), new KindId(0x02), "Npc", 5, 0);
            npc.Hostile = false;
            session.Combat.Register(npc);
            session.Events.Publish(new EntityDied { TimeMs = 1, Entity = npc.Id, Killer = session.Hero.Id });

            var wolf = Combatant.CreateCreature(StableEntityId.ForRuntime(2), new KindId(0x13), "Wolf", 5, 0);
            session.Combat.Register(wolf);
            session.Events.Publish(new EntityDied { TimeMs = 2, Entity = wolf.Id, Killer = StableEntityId.ForRuntime(9) });

            Assert.IsEmpty(gained);
        }
    }
}
