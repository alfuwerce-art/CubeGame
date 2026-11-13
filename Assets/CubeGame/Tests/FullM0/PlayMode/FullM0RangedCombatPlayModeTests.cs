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
using CubeGame.Gameplay;
using CubeGame.Presentation.CameraRig;
using CubeGame.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// M0-F2B in the real game scene (<see cref="GameBootstrap"/>): melee kill without projectiles; bow → arrow appears,
    /// flies, collides, hits, disappears; staff volume and wand magic damage a target; a floating-origin shift during
    /// a flight keeps projectile and target consistent; a save with a projectile in flight reloads into an idle
    /// combat without projectiles.
    /// </summary>
    public sealed class FullM0RangedCombatPlayModeTests
    {
        private string _saveDir;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "cubegame_f2b_" + System.Guid.NewGuid().ToString("N"));
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

        private IEnumerator Start(CharacterClass heroClass, System.Action<GameBootstrap> ready)
        {
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            yield return null;
            Assert.IsTrue(game.StartNewWorld("slot0", "F2B", 424242u, "Shooter", heroClass), game.Status);
            PlayerMotor motor = game.PlayerTransform.GetComponent<PlayerMotor>();
            float deadline = Time.realtimeSinceStartup + 8f;
            while ((!motor.IsGrounded || game.TrainingTarget == null) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(motor.IsGrounded, "hero stands on generated terrain");
            // Level aim: the camera looks horizontally along the hero's facing (aim = camera look, CUBEGAME_CONTRACT).
            OrbitCameraController orbit = Camera.main.GetComponent<OrbitCameraController>();
            orbit.SetAngles(orbit.Yaw, 0f);
            yield return null;
            ready(game);
        }

        private static Combatant Target(GameBootstrap game, double distance, double bearing = 0.0)
        {
            Combatant target = game.SpawnTrainingTargetAt(distance, bearing);
            Assert.IsNotNull(target, game.Status);
            return target;
        }

        private static void EquipFromBags(GameBootstrap game, WeaponSubtype subtype)
        {
            HeroInventory inventory = game.Session.Hero.Inventory;
            if (game.Session.Hero.Loadout.GetEquipped(EquipmentSlot.MainHand) != null)
            {
                Assert.IsTrue(inventory.TryUnequip(EquipmentSlot.MainHand), inventory.LastRejection);
            }

            for (int slot = 0; slot < HeroInventory.Capacity; slot++)
            {
                InventoryEntry entry = inventory[slot];
                if (entry != null && entry.IsCore && entry.Core.Type == ItemType.Weapon && entry.Core.Subtype == (byte)subtype)
                {
                    Assert.IsTrue(inventory.TryEquip(slot, out EquipmentSlot to), inventory.LastRejection);
                    Assert.AreEqual(EquipmentSlot.MainHand, to);
                    return;
                }
            }

            Assert.Fail($"no {subtype} in the starting bags");
        }

        private sealed class Log
        {
            public readonly List<HitResolved> Hits = new List<HitResolved>();
            public readonly List<ProjectileSpawned> Spawned = new List<ProjectileSpawned>();
            public readonly List<ProjectileRemoved> Removed = new List<ProjectileRemoved>();
            public readonly List<EntityDied> Deaths = new List<EntityDied>();

            public Log(GameEventBus bus)
            {
                bus.Subscribe<HitResolved>(Hits.Add);
                bus.Subscribe<ProjectileSpawned>(Spawned.Add);
                bus.Subscribe<ProjectileRemoved>(Removed.Add);
                bus.Subscribe<EntityDied>(Deaths.Add);
            }
        }

        [UnityTest]
        public IEnumerator MeleeSpawnAttackHitDamageDeath()
        {
            GameBootstrap game = null;
            yield return Start(CharacterClass.Warrior, g => game = g);
            Combatant wolf = Target(game, 2.0);
            var log = new Log(game.Session.Events);
            HeroState hero = game.Session.Hero;
            var serials = new HashSet<long>();
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!wolf.IsDead && Time.realtimeSinceStartup < deadline)
            {
                if (!game.Session.Combat.IsActing(hero.Id))
                {
                    Assert.IsTrue(game.HeroAttack(), game.CombatLog);
                    Assert.AreEqual(AttackPath.Melee, game.LastAttackPlan.Path);
                    Assert.IsTrue(serials.Add(game.Session.Combat.ActionSerialOf(hero.Id)), "new attack id per swing");
                }

                yield return null;
            }

            Assert.IsTrue(wolf.IsDead, game.CombatLog);
            List<HitResolved> mine = log.Hits.FindAll(h => h.Target.Equals(wolf.Id));
            Assert.Greater(mine.Count, 0);
            Assert.AreEqual(mine.Count, new HashSet<long>(mine.ConvertAll(h => h.AttackSerial)).Count, "one hit per swing");
            Assert.AreEqual(1, log.Deaths.FindAll(d => d.Entity.Equals(wolf.Id)).Count, "exactly one death");
            Assert.IsEmpty(log.Spawned, "melee never releases a projectile");
        }

        [UnityTest]
        public IEnumerator BowArrowAppearsFliesCollidesHitsAndDisappears()
        {
            GameBootstrap game = null;
            yield return Start(CharacterClass.Ranger, g => game = g);
            Assert.AreEqual((byte)WeaponSubtype.Bow, game.Session.Hero.Loadout.GetEquipped(EquipmentSlot.MainHand).Subtype, "starting bow");
            Combatant wolf = Target(game, 14.0);
            double hp = wolf.Health.CurrentHealth;
            var log = new Log(game.Session.Events);

            Assert.IsTrue(game.HeroAttack(), game.CombatLog);
            Assert.AreEqual(AttackPath.Projectile, game.LastAttackPlan.Path);
            Assert.AreEqual(ProjectileKind.Arrow, game.LastAttackPlan.Projectile);

            var sceneZ = new List<Vector3>();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (log.Removed.Count == 0 && Time.realtimeSinceStartup < deadline)
            {
                if (log.Spawned.Count > 0 && game.ProjectileVisuals.TryGet(log.Spawned[0].Projectile, out Transform root, out _))
                {
                    sceneZ.Add(root.position);
                    Assert.IsTrue(game.Session.Combat.Projectiles.TryGet(log.Spawned[0].Projectile, out Projectile p));
                    Assert.Less(Vector3.Distance(game.WorldView.ToScene(p.X, p.Y, p.Z), root.position), 0.05f, "visual at the gameplay position");
                }

                yield return null;
            }

            Assert.AreEqual(1, log.Spawned.Count, "one arrow");
            Assert.Greater(sceneZ.Count, 0, "the arrow is visible in the scene");
            Assert.AreEqual(1, log.Removed.Count);
            Assert.AreEqual((int)ProjectileEndReason.HitTarget, log.Removed[0].Reason,
                $"arrow ended by {(ProjectileEndReason)log.Removed[0].Reason} at {log.Removed[0].Position}; wolf at {wolf.Position}");
            Assert.AreEqual(wolf.Id, log.Removed[0].Target);
            Assert.AreEqual(1, log.Hits.FindAll(h => h.Target.Equals(wolf.Id)).Count, "hit once");
            Assert.Less(wolf.Health.CurrentHealth, hp, "damage through CombatSystem.ResolveHit");
            yield return null;
            Assert.AreEqual(0, game.Session.Combat.Projectiles.Count);
            Assert.AreEqual(0, game.ProjectileVisuals.Count, "visual removed with the projectile");
            Assert.AreEqual(1, game.ProjectileVisuals.Created);
            Debug.Log($"[F2B] arrow frames {sceneZ.Count} problems [{string.Join("; ", game.ProjectileVisuals.Problems)}] meshes destroyed {game.ProjectileVisuals.MeshesDestroyed}");
        }

        [UnityTest]
        public IEnumerator StaffVolumeAndWandMagicDamageTargets()
        {
            GameBootstrap game = null;
            yield return Start(CharacterClass.Mage, g => game = g);
            var log = new Log(game.Session.Events);
            HeroState hero = game.Session.Hero;

            // Staff (starting weapon): Alpha 0x1E hit volume in front of the mage.
            Combatant near = Target(game, 1.8);
            double nearHp = near.Health.CurrentHealth;
            Assert.IsTrue(game.HeroAttack(), game.CombatLog);
            Assert.AreEqual(AttackPath.AreaEffect, game.LastAttackPlan.Path);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (game.Session.Combat.IsActing(hero.Id) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Less(near.Health.CurrentHealth, nearHp, "the staff volume damaged the target");
            Assert.IsEmpty(log.Spawned, "staff windows use the action volume directly");
            Assert.Greater(log.Hits.FindAll(h => h.Target.Equals(near.Id)).Count, 1);

            // Wand: Alpha 0x26 magic projectile.
            EquipFromBags(game, WeaponSubtype.Wand);
            yield return null;
            Combatant far = Target(game, 10.0);
            double farHp = far.Health.CurrentHealth;
            Assert.IsTrue(game.HeroAttack(), game.CombatLog);
            Assert.AreEqual(ProjectileKind.Magic, game.LastAttackPlan.Projectile);
            deadline = Time.realtimeSinceStartup + 10f;
            while (far.Health.CurrentHealth >= farHp && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Less(far.Health.CurrentHealth, farHp, "the magic projectile damaged the target");
            ProjectileRemoved magic = log.Removed.FindLast(r => r.Kind == (int)ProjectileKind.Magic);
            Assert.IsNotNull(magic);
            Assert.AreEqual((int)ProjectileEndReason.HitTarget, magic.Reason);
        }

        [UnityTest]
        public IEnumerator OriginShiftDuringFlightKeepsProjectileAndTargetConsistent()
        {
            GameBootstrap game = null;
            yield return Start(CharacterClass.Ranger, g => game = g);
            EquipFromBags(game, WeaponSubtype.Wand); // 50 blocks/s: the flight lasts several frames
            Combatant wolf = Target(game, 16.0);
            double hp = wolf.Health.CurrentHealth;
            var log = new Log(game.Session.Events);
            Assert.IsTrue(game.HeroAttack(), game.CombatLog);

            float deadline = Time.realtimeSinceStartup + 10f;
            while (log.Spawned.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, log.Spawned.Count);
            Vector2Int origin = game.WorldView.Origin;
            game.ShiftOrigin(64, 64);
            Assert.AreNotEqual(origin, game.WorldView.Origin, "the floating origin moved during the flight");

            int checkedFrames = 0;
            while (log.Removed.Count == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (game.Session.Combat.Projectiles.TryGet(log.Spawned[0].Projectile, out Projectile p)
                    && game.ProjectileVisuals.TryGet(p.Id, out Transform root, out _))
                {
                    Assert.Less(Vector3.Distance(game.WorldView.ToScene(p.X, p.Y, p.Z), root.position), 0.05f, "projectile visual follows the new origin");
                    Assert.IsTrue(game.Visuals.TryGetRoot(wolf.Id, out Transform wolfRoot));
                    Assert.Less(Vector3.Distance(game.WorldView.ToScene(wolf.Position.X, wolf.Position.Y, wolf.Position.Z), wolfRoot.position), 0.05f,
                        "target visual follows the new origin");
                    checkedFrames++;
                }
            }

            Assert.AreEqual(1, log.Removed.Count);
            Assert.AreEqual((int)ProjectileEndReason.HitTarget, log.Removed[0].Reason, $"ended by {(ProjectileEndReason)log.Removed[0].Reason}");
            Assert.Less(wolf.Health.CurrentHealth, hp, "the shifted flight still hits its target");
            Debug.Log($"[F2B] origin shift checked over {checkedFrames} frames");
        }

        /// <summary>
        /// Species scale: one voxel → block factor fitted on HumanMale 0x02, the same for every world whatever the hero's
        /// race; a race-4 (kind 0x04) hero is drawn smaller by its own Alpha models, like a 0x04 NPC would be.
        /// </summary>
        [UnityTest]
        public IEnumerator SpeciesScaleIsOneRuleForHeroAndCreatures()
        {
            if (CubeGame.AlphaModels.AlphaActorLibrary.Shared.State != CubeGame.AlphaModels.AlphaContentState.Ready)
            {
                Assert.Ignore("Alpha content cache is not staged");
            }

            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            yield return null;
            var heights = new float[2];
            var scales = new float[2];
            var wolfScales = new float[2];
            HeroAppearance[] looks = { AlphaHeroCreation.Default, new HeroAppearance(4, 0, 0, 0, 0xFF, 0xC8, 0x32) };
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(game.StartNewWorld("slot" + i, "Scale", 424242u, "Hero", CharacterClass.Warrior, looks[i]), game.Status);
                float deadline = Time.realtimeSinceStartup + 8f;
                while (game.TrainingTarget == null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(game.Visuals.TryGetView(StableEntityId.Hero, out CubeGame.AlphaModels.AlphaActorView hero));
                Assert.IsTrue(game.Visuals.TryGetView(game.TrainingTarget.Id, out CubeGame.AlphaModels.AlphaActorView wolf));
                scales[i] = game.Visuals.ModelScale;
                wolfScales[i] = wolf.transform.localScale.y;
                Transform weapon = hero.Rig.meshes[hero.Rig.IndexOf("weapon")];
                Bounds b = default;
                bool first = true;
                foreach (Renderer r in hero.GetComponentsInChildren<Renderer>())
                {
                    if (weapon != null && r.transform.IsChildOf(weapon)) continue;
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }

                heights[i] = b.size.y;
                Assert.IsTrue(game.ExitToMenu(false), game.Status);
                yield return null;
            }

            Assert.AreEqual(scales[0], scales[1], 1e-4f, "the factor does not depend on the hero's race");
            Assert.AreEqual(wolfScales[0], wolfScales[1], 1e-4f, "creatures keep their size whatever the hero's race");
            Assert.AreEqual(GameVisualSink.HeroVisualHeight, heights[0], 0.25f, "HumanMale reference = 1.8");
            // Emulated Creature_drawModel placement: 0x04 0.0714286 / 0x02 0.0857143 = 0.8333; wolf 0x1C 0.1357143 → 1.5833.
            Assert.AreEqual(heights[0] * 0.8333f, heights[1], 0.03f, "the kind-0x04 hero gets its Alpha species scale");
            Assert.AreEqual(scales[0] * GameVisualSink.SpeciesScale(0x1C), wolfScales[0], 1e-4f, "the wolf gets the same rule");
            Assert.AreEqual(1.5833f, GameVisualSink.SpeciesScale(0x1C), 1e-3f);
            Debug.Log($"[F2B] species scale {scales[0]:F4} human {heights[0]:F3} kind04 {heights[1]:F3} wolf {wolfScales[0]:F4}");
        }

        [UnityTest]
        public IEnumerator SaveWithProjectileInFlightReloadsIdle()
        {
            GameBootstrap game = null;
            yield return Start(CharacterClass.Ranger, g => game = g);
            EquipFromBags(game, WeaponSubtype.Wand);
            var log = new Log(game.Session.Events);
            Target(game, 30.0, 180.0); // the training wolf goes behind the hero
            Assert.IsTrue(game.HeroAttack(), game.CombatLog); // level aim, no target on the line: the magic flies on
            float deadline = Time.realtimeSinceStartup + 10f;
            while (game.Session.Combat.Projectiles.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, game.Session.Combat.Projectiles.Count, "a projectile is in flight; ended: " +
                string.Join(",", log.Removed.ConvertAll(r => $"{(ProjectileEndReason)r.Reason}@{r.Position}")));

            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(game.ExitToMenu(false), game.Status);
            Assert.AreEqual((int)ProjectileEndReason.Cleared, log.Removed[log.Removed.Count - 1].Reason, "cleared once on exit");
            yield return null;
            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            yield return null;

            Assert.AreEqual(0, game.Session.Combat.Projectiles.Count, "projectiles are not saved");
            Assert.AreEqual(0, game.ProjectileVisuals.Count);
            Assert.IsFalse(game.Session.Combat.IsActing(game.Session.Hero.Id), "combat back in idle");
            Assert.AreEqual((byte)WeaponSubtype.Wand, game.Session.Hero.Loadout.GetEquipped(EquipmentSlot.MainHand).Subtype, "wand restored");
            deadline = Time.realtimeSinceStartup + 8f;
            while (!game.PlayerTransform.GetComponent<PlayerMotor>().IsGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            log = new Log(game.Session.Events); // the session clears its subscribers when a world is dropped
            int before = log.Spawned.Count;
            Assert.IsTrue(game.HeroAttack(), game.CombatLog);
            deadline = Time.realtimeSinceStartup + 10f;
            while (log.Spawned.Count == before && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(before + 1, log.Spawned.Count, "the reloaded hero attacks again");
        }
    }
}
