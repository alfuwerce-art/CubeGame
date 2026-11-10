using System.Collections;
using System.Collections.Generic;
using System.IO;
using CubeGame.Core.Characters;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Gameplay;
using CubeGame.Presentation.CameraRig;
using CubeGame.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// M0-F2 integration checkpoint: F1 world + F2A character + F2B combat in one runtime, one scenario.
    /// new world → hero appearance → movement → melee kill → loot drop (one visual per item) → bow projectile →
    /// hitscan ray (Alpha 0x1C, started through the scene's <see cref="CombatSystem"/>: hitscan has no hero input yet,
    /// F2B PARTIAL) → floating-origin shift → streaming travel → save → exit → load → continue fighting without
    /// duplicate views or lost state.
    /// </summary>
    public sealed class FullM0F2IntegrationPlayModeTests
    {
        private string _saveDir;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "cubegame_f2int_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
            foreach (string name in new[] { "Main Camera", "Sun", "WorldRoot", "Player" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null) Object.Destroy(leftover);
            }

            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        private static int CountNamed(string name)
        {
            int count = 0;
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name == name) count++;
            }

            return count;
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private static IEnumerator Grounded(GameBootstrap game)
        {
            PlayerMotor motor = game.PlayerTransform.GetComponent<PlayerMotor>();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!motor.IsGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(motor.IsGrounded, "hero stands on generated terrain");
        }

        private static void LevelAim()
        {
            OrbitCameraController orbit = Camera.main.GetComponent<OrbitCameraController>();
            orbit.SetAngles(orbit.Yaw, 0f);
        }

        private static void AssertGroundLootConsistent(GameBootstrap game, string where)
        {
            Assert.AreEqual(game.Session.WorldItems.Count, game.Visuals.GroundItemCount, $"one visual per ground item ({where})");
        }

        private sealed class Log
        {
            public readonly List<HitResolved> Hits = new List<HitResolved>();
            public readonly List<ProjectileSpawned> Spawned = new List<ProjectileSpawned>();
            public readonly List<ProjectileRemoved> Removed = new List<ProjectileRemoved>();
            public readonly List<HitscanFired> Rays = new List<HitscanFired>();
            public readonly List<EntityDied> Deaths = new List<EntityDied>();
            public readonly List<ExperienceGained> Experience = new List<ExperienceGained>();

            public Log(GameEventBus bus)
            {
                bus.Subscribe<HitResolved>(Hits.Add);
                bus.Subscribe<ProjectileSpawned>(Spawned.Add);
                bus.Subscribe<ProjectileRemoved>(Removed.Add);
                bus.Subscribe<HitscanFired>(Rays.Add);
                bus.Subscribe<EntityDied>(Deaths.Add);
                bus.Subscribe<ExperienceGained>(Experience.Add);
            }
        }

        [UnityTest]
        public IEnumerator WorldCharacterAndCombatRunTogetherThroughSaveExitLoad()
        {
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            yield return null;

            // 1. New world (F1 /6 generator, recovered population) with a non-default hero appearance (F2A).
            var look = new HeroAppearance(2, 1, 2, 1, 0x20, 0x80, 0xFF);
            Assert.IsTrue(game.StartNewWorld("slot0", "F2Int", 424242u, "Brena", CharacterClass.Warrior, look), game.Status);
            yield return Grounded(game);
            float wait = Time.realtimeSinceStartup + 8f;
            while (game.TrainingTarget == null && Time.realtimeSinceStartup < wait) yield return null;
            Assert.IsTrue(game.Visuals.TryGetView(StableEntityId.Hero, out _), string.Join("; ", game.Visuals.Problems));
            Assert.AreEqual(look.Race, game.Session.Hero.Appearance.Race);
            string signature = game.Visuals.HeroSpec.Signature;
            Assert.AreEqual(1, CountNamed("Player"));
            Assert.AreEqual(1, CountNamed("WorldRoot"));
            var log = new Log(game.Session.Events);
            HeroState hero = game.Session.Hero;

            // 2. Movement: the character controller moves the hero and the session follows.
            WorldPosition before = hero.Position;
            CharacterController controller = game.PlayerTransform.GetComponent<CharacterController>();
            for (int i = 0; i < 20; i++)
            {
                controller.Move(new Vector3(0.1f, -0.05f, 0.05f));
                yield return null;
            }

            yield return Grounded(game);
            Assert.Greater(System.Math.Abs(hero.Position.X - before.X) + System.Math.Abs(hero.Position.Z - before.Z), 0.5,
                "the hero moved in the world");
            LevelAim();
            yield return null;

            // 3. Melee kill (F2B melee route) → one death, experience, loot on the ground with one visual per item.
            Combatant wolf = game.SpawnTrainingTargetAt(2.0, 0.0);
            Assert.IsNotNull(wolf, game.Status);
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!wolf.IsDead && Time.realtimeSinceStartup < deadline)
            {
                if (!game.Session.Combat.IsActing(hero.Id))
                {
                    Assert.IsTrue(game.HeroAttack(), game.CombatLog);
                    Assert.AreEqual(AttackPath.Melee, game.LastAttackPlan.Path, "melee weapon → melee route");
                }

                yield return null;
            }

            Assert.IsTrue(wolf.IsDead, game.CombatLog);
            Assert.AreEqual(1, log.Deaths.FindAll(d => d.Entity.Equals(wolf.Id)).Count, "exactly one death");
            Assert.IsEmpty(log.Spawned, "melee never releases a projectile");
            Assert.AreEqual(1, log.Experience.Count, "kill experience once");
            yield return Frames(2);
            Assert.Greater(game.Session.WorldItems.Count, 0, "the kill dropped loot");
            AssertGroundLootConsistent(game, "after the kill");
            int lootCount = game.Session.WorldItems.Count;

            // 4. Ranged: a bow from the bags → an Alpha arrow flies, hits once, disappears with its visual.
            HeroInventory inventory = hero.Inventory;
            var bow = new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Bow, 1, 0, 0, ItemModifier.M02);
            Assert.IsTrue(inventory.TryAdd(InventoryEntry.FromCore(bow)), inventory.LastRejection);
            int bag = -1;
            for (int slot = 0; slot < HeroInventory.Capacity; slot++)
            {
                InventoryEntry entry = inventory[slot];
                if (entry != null && entry.IsCore && entry.Core.Type == ItemType.Weapon && entry.Core.Subtype == (byte)WeaponSubtype.Bow)
                {
                    bag = slot;
                }
            }

            Assert.GreaterOrEqual(bag, 0, "bow in the bags");
            Assert.IsTrue(inventory.TryEquip(bag, out EquipmentSlot to), inventory.LastRejection);
            Assert.AreEqual(EquipmentSlot.MainHand, to);
            yield return Frames(2);
            Assert.AreNotEqual(signature, game.Visuals.HeroSpec.Signature, "the equipped bow changes the hero visual");
            LevelAim();
            yield return null;

            Combatant archeryWolf = game.SpawnTrainingTargetAt(14.0, 0.0);
            Assert.IsNotNull(archeryWolf, game.Status);
            double hp = archeryWolf.Health.CurrentHealth;
            deadline = Time.realtimeSinceStartup + 10f;
            while (game.Session.Combat.IsActing(hero.Id) && Time.realtimeSinceStartup < deadline) yield return null; // last melee swing ends
            Assert.IsTrue(game.HeroAttack(), game.CombatLog);
            Assert.AreEqual(AttackPath.Projectile, game.LastAttackPlan.Path);
            Assert.AreEqual(ProjectileKind.Arrow, game.LastAttackPlan.Projectile);
            deadline = Time.realtimeSinceStartup + 10f;
            bool seen = false;
            while (log.Removed.Count == 0 && Time.realtimeSinceStartup < deadline)
            {
                seen |= log.Spawned.Count > 0 && game.ProjectileVisuals.TryGet(log.Spawned[0].Projectile, out _, out _);
                yield return null;
            }

            Assert.AreEqual(1, log.Spawned.Count, "one arrow");
            Assert.IsTrue(seen, "the arrow had a scene visual");
            Assert.AreEqual(1, log.Removed.Count);
            Assert.AreEqual((int)ProjectileEndReason.HitTarget, log.Removed[0].Reason,
                $"arrow ended by {(ProjectileEndReason)log.Removed[0].Reason}");
            Assert.AreEqual(1, log.Hits.FindAll(h => h.Target.Equals(archeryWolf.Id)).Count, "arrow hits once");
            Assert.Less(archeryWolf.Health.CurrentHealth, hp, "arrow damage through CombatSystem.ResolveHit");
            yield return null;
            Assert.AreEqual(0, game.ProjectileVisuals.Count, "arrow visual removed");

            // 5. Hitscan (Alpha 0x1C ray group) in the same runtime: every target on the ray is hit once, the owner never.
            yield return Frames(30);
            Combatant rayWolf = game.SpawnTrainingTargetAt(8.0, 0.0);
            Assert.IsNotNull(rayWolf, game.Status);
            double rayHp = rayWolf.Health.CurrentHealth;
            deadline = Time.realtimeSinceStartup + 10f;
            while (game.Session.Combat.IsActing(hero.Id) && Time.realtimeSinceStartup < deadline) yield return null;
            AttackPlan ray = AlphaAttackRoute.ForAction(0x1C);
            Assert.AreEqual(AttackPath.Hitscan, ray.Path);
            Assert.IsTrue(game.Session.Combat.TryStartAttack(hero.Id, ray, game.Session.Clock.TimeMs), game.Session.Combat.LastRejection);
            while (log.Rays.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, log.Rays.Count, "one ray");
            Assert.AreEqual(1, log.Hits.FindAll(h => h.Target.Equals(rayWolf.Id)).Count, "ray hits the target once");
            Assert.AreEqual(0, log.Hits.FindAll(h => h.Attacker.Equals(hero.Id) && h.Target.Equals(hero.Id)).Count,
                "the owner is never hit by its own attacks");
            Assert.Less(rayWolf.Health.CurrentHealth, rayHp, "ray damage through CombatSystem.ResolveHit");
            Assert.AreEqual(0, game.Session.Combat.Projectiles.Count, "a ray leaves no flying projectile");

            // 6. Floating origin: loot, hero and target follow a shift without drift.
            Vector2Int origin = game.WorldView.Origin;
            game.ShiftOrigin(64, 64);
            yield return Frames(2);
            Assert.AreNotEqual(origin, game.WorldView.Origin, "the origin moved");
            Assert.IsTrue(game.Visuals.TryGetRoot(rayWolf.Id, out Transform wolfRoot));
            Assert.Less(Vector3.Distance(game.WorldView.ToScene(rayWolf.Position.X, rayWolf.Position.Y, rayWolf.Position.Z), wolfRoot.position), 0.05f,
                "creature visual follows the origin");
            Assert.Less(Vector3.Distance(game.WorldView.ToScene(hero.Position.X, hero.Position.Y, hero.Position.Z), game.PlayerTransform.position), 0.6f,
                "hero follows the origin");
            AssertGroundLootConsistent(game, "after the origin shift");

            // 7. Streaming travel: far away and back; chunks stream, the hero lands on terrain, nothing is duplicated.
            int startX = (int)System.Math.Floor(hero.Position.X), startZ = (int)System.Math.Floor(hero.Position.Z);
            long released = game.WorldView.TotalReleased;
            game.TeleportHero(startX + 1500, startZ);
            yield return Frames(3);
            game.WorldView.CompletePending();
            yield return Grounded(game);
            Assert.Greater(game.WorldView.TotalReleased, released, "far chunks were released");
            Assert.IsTrue(game.WorldView.IsLoaded(WorldLayout.ChunkOf(startX + 1500, startZ)), "destination streamed in");
            game.TeleportHero(startX, startZ);
            yield return Frames(3);
            game.WorldView.CompletePending();
            yield return Grounded(game);
            Assert.AreEqual(1, CountNamed("Player"));
            Assert.AreEqual(1, CountNamed("WorldRoot"));
            AssertGroundLootConsistent(game, "after streaming travel");

            // 8. Save → exit → load.
            long xp = hero.Sheet.Progression.Experience;
            int level = hero.Sheet.Progression.Level;
            string savedSignature = game.Visuals.HeroSpec.Signature;
            int savedBags = 0;
            for (int slot = 0; slot < HeroInventory.Capacity; slot++) if (inventory[slot] != null) savedBags++;
            Vector3 savedPosition = game.PlayerTransform.position;
            WorldPosition savedWorld = hero.Position;
            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(game.ExitToMenu(false), game.Status);
            yield return Frames(2);
            Assert.AreEqual(0, CountNamed("WorldRoot"), "world view destroyed on exit");
            Assert.AreEqual(0, CountNamed("Player"), "player destroyed on exit");
            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            yield return Frames(3);

            // 9. Continue: same hero, same gear, same progress, one of everything, combat works again.
            hero = game.Session.Hero;
            Assert.AreEqual(1, CountNamed("WorldRoot"));
            Assert.AreEqual(1, CountNamed("Player"));
            Assert.AreEqual(look.Race, hero.Appearance.Race);
            Assert.AreEqual(look.Gender, hero.Appearance.Gender);
            Assert.AreEqual(savedSignature, game.Visuals.HeroSpec.Signature, "appearance + bow → the same visual after load");
            Assert.AreEqual((byte)WeaponSubtype.Bow, hero.Loadout.GetEquipped(EquipmentSlot.MainHand).Subtype, "bow restored");
            Assert.AreEqual(xp, hero.Sheet.Progression.Experience, "experience restored");
            Assert.AreEqual(level, hero.Sheet.Progression.Level);
            int loadedBags = 0;
            for (int slot = 0; slot < HeroInventory.Capacity; slot++) if (hero.Inventory[slot] != null) loadedBags++;
            Assert.AreEqual(savedBags, loadedBags, "bags restored without duplicates");
            Assert.AreEqual(savedWorld.X, hero.Position.X, 0.05, "world position restored");
            Assert.AreEqual(savedWorld.Z, hero.Position.Z, 0.05);
            Assert.Less(Vector3.Distance(savedPosition, game.PlayerTransform.position), 0.6f, "scene position restored");
            Assert.AreEqual(0, game.Session.Combat.Projectiles.Count, "projectiles are not saved");
            Assert.AreEqual(0, game.ProjectileVisuals.Count);
            AssertGroundLootConsistent(game, "after load");
            Assert.LessOrEqual(game.Session.WorldItems.Count, lootCount, "no loot duplicated by the reload");

            yield return Grounded(game);
            LevelAim();
            yield return null;
            log = new Log(game.Session.Events); // the session clears its subscribers when a world is dropped
            Combatant again = game.SpawnTrainingTargetAt(14.0, 0.0);
            Assert.IsNotNull(again, game.Status);
            double againHp = again.Health.CurrentHealth;
            deadline = Time.realtimeSinceStartup + 10f;
            while (game.Session.Combat.IsActing(hero.Id) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(game.HeroAttack(), game.CombatLog);
            Assert.AreEqual(AttackPath.Projectile, game.LastAttackPlan.Path);
            deadline = Time.realtimeSinceStartup + 10f;
            while (log.Removed.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, log.Spawned.Count, "the reloaded hero shoots one arrow");
            Assert.AreEqual((int)ProjectileEndReason.HitTarget, log.Removed[0].Reason, $"ended by {(ProjectileEndReason)log.Removed[0].Reason}");
            Assert.Less(again.Health.CurrentHealth, againHp, "combat continues after load");
            Debug.Log($"[F2INT] loot {lootCount} xp {xp} level {level} rays {1} visual {savedSignature} problems [{string.Join("; ", game.Visuals.Problems)}]");
        }
    }
}
