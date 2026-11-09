using System.Collections;
using System.IO;
using CubeGame.AlphaModels;
using CubeGame.Core.Characters;
using CubeGame.Core.Equipment;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using CubeGame.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// First playable combat slice in the real game scene: new world → hero with the starting greatsword on
    /// generated ground → training wolf in front → hero attacks (game clock, Alpha phase timings when the content
    /// cache is present) → wolf HP falls by Core damage → wolf dies → hero takes a hit → save → exit → load →
    /// hero HP and weapon restored.
    /// </summary>
    public sealed class FullM0CombatPlayModeTests
    {
        private string _saveDir;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "cubegame_fullm0_combat_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.Destroy(_host);
            }

            foreach (string name in new[] { "Main Camera", "Sun", "WorldRoot", "Player", "TrainingTarget_PLACEHOLDER" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null)
                {
                    Object.Destroy(leftover);
                }
            }

            if (Directory.Exists(_saveDir))
            {
                Directory.Delete(_saveDir, true);
            }
        }

        [UnityTest]
        public IEnumerator HeroAndWolfUseAlphaModelsAndAttackPlaysAction57()
        {
            if (AlphaActorLibrary.Shared.State != AlphaContentState.Ready)
            {
                Assert.Ignore("Alpha content cache is not staged: " + AlphaActorLibrary.Shared.Diagnosis);
            }

            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            yield return null;
            Assert.IsTrue(game.StartNewWorld("slot0", "Visual", 424242u, "Fighter", CharacterClass.Warrior), game.Status);
            float deadline = Time.realtimeSinceStartup + 8f;
            while (game.TrainingTarget == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsNotNull(game.TrainingTarget, "the training wolf appears once the hero stands on the ground");

            GameVisualSink visuals = game.Visuals;
            Assert.IsNotNull(visuals);
            Assert.IsTrue(visuals.TryGetView(StableEntityId.Hero, out AlphaActorView hero), string.Join("; ", visuals.Problems));
            Assert.IsTrue(hero.Ready, hero.Status());
            Assert.Greater(visuals.ModelScale, 0f);
            Assert.IsTrue(hero.Rig.weaponSubtype >= 0, "the greatsword model is in the hero's hand");
            Assert.IsTrue(visuals.TryGetView(game.TrainingTarget.Id, out AlphaActorView wolf), string.Join("; ", visuals.Problems));
            Assert.IsTrue(wolf.Ready, wolf.Status());

            Bounds heroBounds = BoundsOf(hero.transform);
            Assert.AreEqual(GameVisualSink.HeroVisualHeight, heroBounds.size.y, 0.25f, "hero model fitted to the controller height");

            Assert.IsTrue(game.HeroAttack(), game.CombatLog);
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            StringAssert.Contains("action_057", hero.Status(), "the hero plays Alpha action 57 when gameplay starts the attack");
            foreach (string problem in visuals.Problems)
            {
                StringAssert.DoesNotStartWith("ANIM_MISSING", problem);
            }

            Debug.Log($"[FullM0Visual] scale {visuals.ModelScale:F4} hero {hero.Status()} wolf {wolf.Status()} problems [{string.Join("; ", visuals.Problems)}]");
        }

        private static Bounds BoundsOf(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        [UnityTest]
        public IEnumerator HeroKillsTrainingWolfAndHealthSurvivesSaveLoad()
        {
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            yield return null;

            Assert.IsTrue(game.StartNewWorld("slot0", "Combat", 424242u, "Fighter", CharacterClass.Warrior), game.Status);
            PlayerMotor motor = game.PlayerTransform.GetComponent<PlayerMotor>();
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!motor.IsGrounded && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(motor.IsGrounded, "hero stands on generated terrain");
            HeroState hero = game.Session.Hero;
            Assert.IsNotNull(hero.Loadout.GetEquipped(EquipmentSlot.MainHand), "starting greatsword equipped");

            // Re-place the wolf now that the hero has landed, so both stand on the same ground.
            Combatant wolf = game.SpawnTrainingTarget();
            Assert.IsNotNull(wolf, game.Status);
            Assert.IsTrue(wolf.Hostile);
            Assert.AreEqual(wolf.Health.MaxHealth, wolf.Health.CurrentHealth);

            int hits = 0;
            int deaths = 0;
            game.Session.Events.Subscribe<HitResolved>(_ => hits++);
            game.Session.Events.Subscribe<EntityDied>(e =>
            {
                if (e.Entity.Equals(wolf.Id))
                {
                    deaths++;
                }
            });

            double firstHp = wolf.Health.CurrentHealth;
            deadline = Time.realtimeSinceStartup + 60f;
            while (!wolf.IsDead && Time.realtimeSinceStartup < deadline)
            {
                if (!game.Session.Combat.IsActing(hero.Id))
                {
                    Assert.IsTrue(game.HeroAttack(), game.CombatLog);
                }

                yield return null;
            }

            Assert.IsTrue(wolf.IsDead, $"wolf must die from Core hits; hits={hits} log='{game.CombatLog}'");
            Assert.Greater(hits, 0);
            Assert.Less(wolf.Health.CurrentHealth, firstHp);
            Assert.AreEqual(1, deaths, "exactly one death event");
            Debug.Log($"[FullM0Combat] wolf killed with {hits} hits; timing source " +
                      (game.Session.ActionTimings.TryGet(GameSession.HeroKind, GameBootstrap.HeroAttackAction, out ActionTiming t) ? t.ToString() : "fallback"));

            // The hero takes one Core hit (the training wolf has no AI yet), then the game is saved and reloaded.
            Combatant attacker = Combatant.Create(StableEntityId.ForRuntime(999), GameBootstrap.TrainingTargetKind, "Wolf B", CharacterClass.Warrior, 1);
            attacker.Hostile = true;
            game.Session.Combat.Register(attacker);
            game.Session.Combat.ResolveHit(attacker.Id, hero.Id, game.Session.Clock.TimeMs, 1);
            game.Session.Combat.Unregister(attacker.Id);
            double heroHp = hero.Health.CurrentHealth;
            Assert.Less(heroHp, hero.Health.MaxHealth, "hero was damaged");

            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(game.ExitToMenu(false), game.Status);
            yield return null;
            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            yield return null;

            deadline = Time.realtimeSinceStartup + 8f;
            while (game.TrainingTarget == null && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            HeroState loaded = game.Session.Hero;
            Assert.AreEqual(heroHp, loaded.Health.CurrentHealth, 1e-6, "hero HP restored from the save");
            Assert.IsNotNull(loaded.Loadout.GetEquipped(EquipmentSlot.MainHand), "weapon re-equipped after load");
            Assert.IsNotNull(game.TrainingTarget, "a fresh training wolf is spawned in the loaded world");
            Assert.IsFalse(game.TrainingTarget.IsDead);
        }
    }
}
