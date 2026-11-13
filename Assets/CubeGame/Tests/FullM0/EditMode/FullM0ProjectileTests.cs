using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Core.Characters;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using EquipmentSlot = CubeGame.Core.Equipment.EquipmentSlot;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// M0-F2B: weapon state → attack path without melee fallback, projectile state from the Alpha bytes, lifetime,
    /// one hit per projectile, owner/friend/dead exclusion, terrain, boomerang return, staff volume, tick-rate
    /// independence, the Alpha size table and the stable attack id.
    /// </summary>
    public sealed class FullM0ProjectileTests
    {
        private static readonly KindId Human = new KindId(0x02);
        private static readonly KindId Wolf = new KindId(0x1c);

        private GameEventBus _events;
        private CombatSystem _combat;
        private Combatant _hero;
        private Combatant _wolf;
        private List<HitResolved> _hits;
        private List<ProjectileSpawned> _spawned;
        private List<ProjectileRemoved> _removed;
        private List<ActionStarted> _started;
        private List<EntityDied> _deaths;

        private static ItemInstance Weapon(WeaponSubtype subtype, byte material = 2) =>
            new ItemInstance(ItemType.Weapon, (byte)subtype, 1, 0, 1, (ItemModifier)material);

        [SetUp]
        public void SetUp()
        {
            _events = new GameEventBus();
            _hits = new List<HitResolved>();
            _spawned = new List<ProjectileSpawned>();
            _removed = new List<ProjectileRemoved>();
            _started = new List<ActionStarted>();
            _deaths = new List<EntityDied>();
            _events.Subscribe<HitResolved>(_hits.Add);
            _events.Subscribe<ProjectileSpawned>(_spawned.Add);
            _events.Subscribe<ProjectileRemoved>(_removed.Add);
            _events.Subscribe<ActionStarted>(_started.Add);
            _events.Subscribe<EntityDied>(_deaths.Add);
            _combat = new CombatSystem(_events, new ActionTimingTable(), 666666);

            _hero = Combatant.Create(StableEntityId.Hero, Human, "Hero", CharacterClass.Ranger, 1);
            _hero.Position = new WorldPosition(100, 50, 100);
            _hero.Yaw = 0f; // facing +Z
            _wolf = Combatant.CreateCreature(StableEntityId.ForRuntime(1), Wolf, "Wolf", 1, 0);
            _wolf.Position = new WorldPosition(100, 50, 112);
            GameBootstrap.ApplyAlphaSize(_wolf, Wolf);
            _combat.Register(_hero);
            _combat.Register(_wolf);
        }

        private void Run(long fromMs, long toMs, long stepMs)
        {
            for (long t = fromMs; t <= toMs; t += stepMs)
            {
                _combat.Update(t);
            }
        }

        // ----- weapon state → attack path -----

        [TestCase(CharacterClass.Warrior, 17, -1, 57, AttackPath.Melee, ProjectileKind.Arrow)]
        [TestCase(CharacterClass.Rogue, 3, 3, 19, AttackPath.Melee, ProjectileKind.Arrow)]
        [TestCase(CharacterClass.Ranger, 6, -1, 22, AttackPath.Projectile, ProjectileKind.Arrow)]
        [TestCase(CharacterClass.Ranger, 7, -1, 22, AttackPath.Projectile, ProjectileKind.Arrow)]
        [TestCase(CharacterClass.Warrior, 0, 6, 22, AttackPath.Projectile, ProjectileKind.Arrow)]
        [TestCase(CharacterClass.Ranger, 8, -1, 26, AttackPath.Projectile, ProjectileKind.Boomerang)]
        [TestCase(CharacterClass.Ranger, 11, -1, 38, AttackPath.Projectile, ProjectileKind.Magic)]
        [TestCase(CharacterClass.Mage, 11, -1, 38, AttackPath.Projectile, ProjectileKind.Magic)]
        [TestCase(CharacterClass.Mage, 10, -1, 30, AttackPath.AreaEffect, ProjectileKind.Arrow)]
        [TestCase(CharacterClass.Mage, 12, -1, 40, AttackPath.Projectile, ProjectileKind.Magic)]
        public void WeaponStateSelectsOneAttackPath(CharacterClass characterClass, int main, int off, int action, AttackPath path, ProjectileKind kind)
        {
            ItemInstance mainHand = main < 0 ? null : Weapon((WeaponSubtype)main);
            ItemInstance offHand = off < 0 ? null : Weapon((WeaponSubtype)off);
            AttackPlan plan = AlphaAttackRoute.Select(characterClass, mainHand, offHand);
            Assert.AreEqual(action, plan.Action.Value, plan.ToString());
            Assert.AreEqual(path, plan.Path, plan.ToString());
            if (path != AttackPath.Melee)
            {
                Assert.AreEqual(kind, plan.Projectile, plan.ToString());
            }
        }

        [TestCase(CharacterClass.Mage, -1)]
        [TestCase(CharacterClass.Mage, 0)]
        [TestCase(CharacterClass.Mage, 3)]
        public void UnknownWeaponStateIsRefusedAndNeverBecomesMelee(CharacterClass characterClass, int main)
        {
            AttackPlan plan = AlphaAttackRoute.Select(characterClass, main < 0 ? null : Weapon((WeaponSubtype)main), null);
            Assert.IsFalse(plan.IsSupported);
            StringAssert.Contains("UNKNOWN", plan.Reason);

            _wolf.Position = new WorldPosition(100, 50, 101.5); // inside melee reach
            Assert.IsFalse(_combat.TryStartAttack(_hero.Id, plan, 0));
            StringAssert.Contains("UNKNOWN", _combat.LastRejection);
            Run(0, 3000, 33);
            Assert.IsEmpty(_started, "a refused attack starts no action");
            Assert.IsEmpty(_hits, "and never hits through a melee fallback");
            Assert.AreEqual(_wolf.Health.MaxHealth, _wolf.Health.CurrentHealth);
        }

        [Test]
        public void RangedAttackHitsOnlyThroughItsProjectile()
        {
            _hero.Loadout.Equip(EquipmentSlot.MainHand, Weapon(WeaponSubtype.Bow));
            // A second hostile inside the melee sector but off the arrow line: a melee swing would hit it.
            Combatant side = Combatant.CreateCreature(StableEntityId.ForRuntime(2), Wolf, "Side", 1, 0);
            side.HitWidth = side.HitDepth = 0.5;
            side.Position = new WorldPosition(101.8, 50, 101.2);
            _combat.Register(side);

            AttackPlan plan = AlphaAttackRoute.Select(CharacterClass.Ranger, _hero.Loadout.GetEquipped(EquipmentSlot.MainHand), null);
            Assert.IsTrue(_combat.TryStartAttack(_hero.Id, plan, 0), _combat.LastRejection);
            long serial = _combat.ActionSerialOf(_hero.Id);

            Run(0, 399, 33);
            Assert.IsEmpty(_spawned, "nothing is released before the strike moment");
            Run(400, 2000, 33);

            Assert.AreEqual(1, _spawned.Count, "one projectile per attack");
            Assert.AreEqual((int)ProjectileKind.Arrow, _spawned[0].Kind);
            Assert.AreEqual(serial, _spawned[0].AttackSerial, "stable attack id");
            Assert.AreEqual(1, _hits.Count);
            Assert.AreEqual(_wolf.Id, _hits[0].Target, "the arrow hit the wolf on its line");
            Assert.AreEqual(serial, _hits[0].AttackSerial);
            Assert.AreEqual(side.Health.MaxHealth, side.Health.CurrentHealth, "no melee resolution for a ranged attack");
            Assert.Less(_wolf.Health.CurrentHealth, _wolf.Health.MaxHealth);
            Assert.AreEqual(1, _removed.Count);
            Assert.AreEqual((int)ProjectileEndReason.HitTarget, _removed[0].Reason);
            Assert.AreEqual(0, _combat.Projectiles.Count);
            Assert.IsFalse(_combat.IsActing(_hero.Id), "back to idle after the action");
        }

        [TestCase(0x16, AttackPath.Projectile, ProjectileKind.Arrow)]
        [TestCase(0x17, AttackPath.Projectile, ProjectileKind.Arrow)]
        [TestCase(0x1A, AttackPath.Projectile, ProjectileKind.Boomerang)]
        [TestCase(0x25, AttackPath.Projectile, ProjectileKind.Magic)]
        [TestCase(0x2E, AttackPath.Projectile, ProjectileKind.Magic)]
        [TestCase(0x6C, AttackPath.Unsupported, ProjectileKind.Arrow)]
        [TestCase(0x1E, AttackPath.AreaEffect, ProjectileKind.Arrow)]
        [TestCase(0x21, AttackPath.AreaEffect, ProjectileKind.Arrow)]
        [TestCase(0x1C, AttackPath.Hitscan, ProjectileKind.Beam)]
        [TestCase(0x5E, AttackPath.Hitscan, ProjectileKind.Beam)]
        [TestCase(0x5F, AttackPath.Hitscan, ProjectileKind.Beam)]
        [TestCase(0x68, AttackPath.Unsupported, ProjectileKind.Arrow)]
        [TestCase(57, AttackPath.Melee, ProjectileKind.Arrow)]
        [TestCase(1, AttackPath.Melee, ProjectileKind.Arrow)]
        public void ActionIdSelectsTheAlphaHandlerGroup(int action, AttackPath path, ProjectileKind kind)
        {
            AttackPlan plan = AlphaAttackRoute.ForAction(action);
            Assert.AreEqual(path, plan.Path);
            if (plan.IsSupported && path != AttackPath.Melee) Assert.AreEqual(kind, plan.Projectile);
        }

        [Test]
        public void TrailActionsAreExactlyTheByteVerifiedMuzzleIds()
        {
            CollectionAssert.AreEquivalent(AlphaCombatRuntimeData.MuzzleAttackIds, ProjectileRules.TrailActionIds);
            Assert.IsFalse(ProjectileRules.HasTrail(0x16), "no proven trail for the bow");
        }

        // ----- hitscan -----

        [Test]
        public void HitscanHitsEveryHostileOnTheRayOnceAndNeverTheOwner()
        {
            Combatant second = Combatant.CreateCreature(StableEntityId.ForRuntime(6), Wolf, "Second", 1, 0);
            GameBootstrap.ApplyAlphaSize(second, Wolf);
            second.Position = new WorldPosition(100, 50, 150);
            _combat.Register(second);
            var rays = new List<HitscanFired>();
            _events.Subscribe<HitscanFired>(rays.Add);

            Assert.IsTrue(_combat.TryStartAttack(_hero.Id, AlphaAttackRoute.ForAction(0x5E), 0), _combat.LastRejection);
            Run(0, 3000, 33);
            Assert.AreEqual(1, rays.Count, "one ray per attack");
            Assert.AreEqual(2, _hits.Count, "both wolves on the line, once each");
            Assert.IsFalse(_hits.Exists(h => h.Target.Equals(_hero.Id)), "never the owner");
            Assert.IsTrue(rays[0].Trail, "0x5E has a byte-verified trail");
            Assert.AreEqual(200.0, rays[0].End.Z - rays[0].Origin.Z, 1e-6, "range 200 without a wall");
            Assert.IsEmpty(_spawned, "a hitscan releases no flying projectile");
        }

        [Test]
        public void HitscanStopsAtTheFirstWall()
        {
            _combat.Projectiles.TerrainAt = (x, y, z) => z >= 106 ? TerrainCell.Solid : TerrainCell.Air;
            int hits = _combat.Projectiles.FireRay(_hero, new ActionId(0x5F), 1, 0);
            Assert.AreEqual(0, hits, "the wolf behind the wall at z=112 is not hit");
            _combat.Projectiles.TerrainAt = (x, y, z) => z >= 120 ? TerrainCell.Solid : TerrainCell.Air;
            Assert.AreEqual(1, _combat.Projectiles.FireRay(_hero, new ActionId(0x5F), 2, 0));
        }

        [Test]
        public void HitscanBeyond200BlocksMisses()
        {
            _wolf.Position = new WorldPosition(100, 50, 100 + 205);
            Assert.AreEqual(0, _combat.Projectiles.FireRay(_hero, new ActionId(0x1C), 1, 0));
            _wolf.Position = new WorldPosition(100, 50, 100 + 195);
            Assert.AreEqual(1, _combat.Projectiles.FireRay(_hero, new ActionId(0x1C), 2, 0));
        }

        [Test]
        public void BombUsesTheFixedRange()
        {
            _combat.Projectiles.TerrainAt = (x, y, z) => TerrainCell.Solid; // the bomb does not cast the voxel ray
            _wolf.Position = new WorldPosition(100, 50, 109);
            Assert.AreEqual(1, _combat.Projectiles.FireRay(_hero, new ActionId(0x68), 1, 0));
            _wolf.Position = new WorldPosition(100, 50, 113);
            Assert.AreEqual(0, _combat.Projectiles.FireRay(_hero, new ActionId(0x68), 2, 0), "beyond 10 blocks");
        }

        // ----- projectile state -----

        [Test]
        public void ArrowStateFollowsAlphaBytes()
        {
            _hero.SetAim(0, 0, 1);
            Projectile p = _combat.Projectiles.Launch(_hero, ProjectileKind.Arrow, new ActionId(22), 7, 1000);
            Assert.AreEqual(100.0, p.Vz, 1e-9, "0x617200: aim · 100");
            Assert.AreEqual(0.0, p.Vx, 1e-9);
            Assert.AreEqual(7.5, p.Gravity, 1e-9, "0x620C6F: 30 · 0.25");
            Assert.AreEqual(0.5, p.Scale, 1e-9, "0x617231");
            Assert.AreEqual(5000, p.LifetimeMs, "0x620CD3");
            Assert.AreEqual(50 + _hero.HitHeight * 0.5, p.Y, 1e-9, "released from the body centre");
            Assert.AreEqual(_hero.Id, p.Owner);

            _wolf.Position = new WorldPosition(500, 50, 500); // out of the way
            _combat.Projectiles.Update(1100);
            Assert.AreEqual(-0.75, p.Vy, 1e-6, "vy after 100 ms of gravity");
            Assert.AreEqual(100 + 10.0, p.Z, 1e-6, "10 blocks in 100 ms");
            Assert.AreEqual(100, p.AgeMs);
        }

        [Test]
        public void MagicAndBoomerangConfigurationIsDeterministic()
        {
            _hero.SetAim(1, 0, 0);
            Projectile magic = _combat.Projectiles.Launch(_hero, ProjectileKind.Magic, new ActionId(38), 1, 0);
            Assert.AreEqual(50.0, magic.Vx, 1e-9, "0x617CC4: wand · 50");
            Assert.AreEqual(0.0, magic.Gravity, "magic has no gravity");
            _hero.Loadout.Equip(EquipmentSlot.OffHand, Weapon(WeaponSubtype.Bracelet, 0x0B));
            Projectile fast = _combat.Projectiles.Launch(_hero, ProjectileKind.Magic, new ActionId(38), 2, 0);
            Assert.AreEqual(100.0, fast.Vx, 1e-9, "0x617CA7: bracelet off-hand · 100");
            Projectile boomerang = _combat.Projectiles.Launch(_hero, ProjectileKind.Boomerang, new ActionId(26), 3, 0);
            Assert.AreEqual(100.0, boomerang.Vx, 1e-9);
            Assert.AreEqual(0.0, boomerang.Gravity);
            Assert.AreEqual(new[] { 1L, 2L, 3L }, new[] { magic.Id, fast.Id, boomerang.Id }, "ids in release order");
        }

        [Test]
        public void ProjectileExpiresAfterAlphaLifetimeExactlyOnce()
        {
            _hero.SetAim(0, 1, 0); // straight up, no terrain, no target
            Projectile p = _combat.Projectiles.Launch(_hero, ProjectileKind.Magic, new ActionId(38), 1, 0);
            Run(33, 4990, 33);
            Assert.AreEqual(1, _combat.Projectiles.Count, "alive before 5000 ms");
            Run(5000, 6000, 33);
            Assert.AreEqual(0, _combat.Projectiles.Count);
            Assert.AreEqual(1, _removed.Count, "removed exactly once");
            Assert.AreEqual((int)ProjectileEndReason.Expired, _removed[0].Reason);
            Assert.IsTrue(p.Removed);
        }

        // ----- hit rules -----

        [Test]
        public void ArrowHitsOnceAndIsRemovedOnHit()
        {
            _hero.SetAim(0, 0, 1);
            _combat.Projectiles.Launch(_hero, ProjectileKind.Arrow, new ActionId(22), 9, 0);
            Run(0, 3000, 33);
            Assert.AreEqual(1, _hits.Count, "one hit per arrow");
            Assert.AreEqual(1, _removed.Count);
            Assert.AreEqual(_wolf.Id, _removed[0].Target);
            Assert.AreEqual((int)ProjectileEndReason.HitTarget, _removed[0].Reason);
        }

        [Test]
        public void OwnerFriendsAndDeadAreNotHit()
        {
            Combatant friend = Combatant.Create(StableEntityId.ForRuntime(3), Human, "Friend", CharacterClass.Warrior, 1);
            friend.Position = new WorldPosition(100, 50, 104); // on the line, not hostile to the hero
            _combat.Register(friend);
            Combatant corpse = Combatant.CreateCreature(StableEntityId.ForRuntime(4), Wolf, "Corpse", 1, 0);
            corpse.Position = new WorldPosition(100, 50, 107);
            corpse.Health.ApplyDamage(corpse.Health.MaxHealth * 2);
            Assert.IsTrue(corpse.IsDead);
            _combat.Register(corpse);

            _hero.SetAim(0, 0, 1);
            _combat.Projectiles.Launch(_hero, ProjectileKind.Arrow, new ActionId(22), 1, 0);
            Run(0, 3000, 33);
            Assert.AreEqual(1, _hits.Count);
            Assert.AreEqual(_wolf.Id, _hits[0].Target, "owner (spawned inside its own box), friend and corpse are passed");
            Assert.AreEqual(_hero.Health.MaxHealth, _hero.Health.CurrentHealth);
            Assert.AreEqual(friend.Health.MaxHealth, friend.Health.CurrentHealth);
        }

        [Test]
        public void KilledTargetGetsNoFurtherHitsOrEvents()
        {
            _wolf.Health.ApplyDamage(_wolf.Health.CurrentHealth - 0.01);
            _hero.SetAim(0, 0, 1);
            _combat.Projectiles.Launch(_hero, ProjectileKind.Arrow, new ActionId(22), 1, 0);
            _combat.Projectiles.Launch(_hero, ProjectileKind.Arrow, new ActionId(22), 2, 0);
            Run(0, 6000, 33);
            Assert.AreEqual(1, _deaths.Count, "dies once");
            Assert.AreEqual(1, _hits.Count, "the second arrow flies through the corpse");
            Assert.AreEqual(2, _removed.Count);
            Assert.AreEqual((int)ProjectileEndReason.Expired, _removed[1].Reason);

            int before = _hits.Count;
            _combat.ResolveHit(_hero.Id, _wolf.Id, 7000, 3);
            Assert.AreEqual(before, _hits.Count, "ResolveHit on a dead target publishes nothing");
        }

        [Test]
        public void HitIsIndependentOfTheTickLength()
        {
            _wolf.HitWidth = _wolf.HitDepth = 0.1; // thin target: 100 blocks/s would tunnel through it in one 33 ms step
            _hero.SetAim(0, 0, 1);
            _combat.Projectiles.Launch(_hero, ProjectileKind.Arrow, new ActionId(22), 1, 0);
            Run(0, 1000, 100);
            Assert.AreEqual(1, _hits.Count, "sub-steps catch the box");
        }

        [Test]
        public void TerrainRemovesArrowAndStopsBoomerang()
        {
            _wolf.Position = new WorldPosition(500, 50, 500);
            _combat.Projectiles.TerrainAt = (x, y, z) => z >= 105 ? TerrainCell.Solid : TerrainCell.Air;
            _hero.SetAim(0, 0, 1);
            Projectile arrow = _combat.Projectiles.Launch(_hero, ProjectileKind.Arrow, new ActionId(22), 1, 0);
            Projectile boomerang = _combat.Projectiles.Launch(_hero, ProjectileKind.Boomerang, new ActionId(26), 2, 0);
            Run(0, 200, 33);
            Assert.IsTrue(arrow.Removed);
            Assert.AreEqual((int)ProjectileEndReason.HitTerrain, _removed[0].Reason);
            Assert.Less(arrow.Z, 105.5);
            Assert.IsFalse(boomerang.Removed, "a boomerang stops in the wall");
            Assert.AreEqual(0.0, boomerang.Vz, 1e-9);
            Assert.Less(boomerang.Z, 105.0);

            _combat.Projectiles.TerrainAt = (x, y, z) => TerrainCell.NotLoaded;
            Projectile lost = _combat.Projectiles.Launch(_hero, ProjectileKind.Arrow, new ActionId(22), 3, 300);
            _combat.Projectiles.Update(333);
            Assert.IsTrue(lost.Removed);
            Assert.AreEqual((int)ProjectileEndReason.NotLoaded, _removed[_removed.Count - 1].Reason);
        }

        [Test]
        public void BoomerangHitsOnceComesBackAndIsCaught()
        {
            _hero.SetAim(0, 0, 1);
            _wolf.Position = new WorldPosition(100, 50, 102);
            Projectile b = _combat.Projectiles.Launch(_hero, ProjectileKind.Boomerang, new ActionId(26), 1, 0);
            Run(0, 1300, 10);
            Assert.AreEqual(1, _hits.Count, "hit on the way out, not again on the way back");
            Assert.IsTrue(b.Removed);
            ProjectileRemoved last = _removed[_removed.Count - 1];
            Assert.AreEqual((int)ProjectileEndReason.Caught, last.Reason);
            Assert.AreEqual(100, b.X, 0.5, "back at the owner");
            Assert.AreEqual(100, b.Z, 0.5);
        }

        [Test]
        public void StaffVolumeRepeatsWithinTheActionAndLeavesNoProjectile()
        {
            _wolf.Position = new WorldPosition(100, 50, 101.5);
            Combatant behind = Combatant.CreateCreature(StableEntityId.ForRuntime(5), Wolf, "Behind", 1, 0);
            behind.HitWidth = behind.HitDepth = 0.5;
            behind.Position = new WorldPosition(100, 50, 95);
            _combat.Register(behind);
            _hero.Loadout.Equip(EquipmentSlot.MainHand, Weapon(WeaponSubtype.Staff));
            AttackPlan plan = AlphaAttackRoute.Select(CharacterClass.Mage, _hero.Loadout.GetEquipped(EquipmentSlot.MainHand), null);
            Assert.IsTrue(_combat.TryStartAttack(_hero.Id, plan, 0), _combat.LastRejection);
            Run(0, 2000, 33);
            Assert.IsEmpty(_spawned);
            Assert.AreEqual(6, _hits.Count);
            Assert.IsTrue(_hits.TrueForAll(hit => hit.Target.Equals(_wolf.Id)));
            Assert.AreEqual(800, _hits[0].TimeMs);
            Assert.AreEqual(1300, _hits[5].TimeMs);
            Assert.AreEqual(behind.Health.MaxHealth, behind.Health.CurrentHealth);
            Assert.IsFalse(_combat.IsActing(_hero.Id));
        }

        [Test]
        public void OwnerBoxTestFollowsTheAlphaRule()
        {
            _hero.SetAim(0, 0, 1);
            Projectile p = _combat.Projectiles.Launch(_hero, ProjectileKind.Arrow, new ActionId(22), 1, 0);
            // Wolf 1.52 × 3.23 × 1.52 (Alpha size) at feet y = 50: centre y = 51.615.
            _wolf.Position = new WorldPosition(p.X + 0.76 + 0.5 - 0.01, 50, p.Z);
            Assert.IsTrue(ProjectileSystem.Overlaps(p, _wolf), "inside size/2 + scale");
            _wolf.Position = new WorldPosition(p.X + 0.76 + 0.5 + 0.01, 50, p.Z);
            Assert.IsFalse(ProjectileSystem.Overlaps(p, _wolf), "outside size/2 + scale");
        }

        [Test]
        public void ClearRemovesEveryProjectileOnce()
        {
            _hero.SetAim(0, 1, 0);
            _combat.Projectiles.Launch(_hero, ProjectileKind.Arrow, new ActionId(22), 1, 0);
            _combat.Projectiles.Launch(_hero, ProjectileKind.Magic, new ActionId(38), 2, 0);
            _combat.Projectiles.Clear(10);
            _combat.Projectiles.Clear(20);
            Assert.AreEqual(0, _combat.Projectiles.Count);
            Assert.AreEqual(2, _removed.Count);
            Assert.IsTrue(_removed.TrueForAll(r => r.Reason == (int)ProjectileEndReason.Cleared));
        }

        [Test]
        public void OwnerGoneRemovesItsProjectiles()
        {
            Combatant archer = Combatant.CreateCreature(StableEntityId.ForRuntime(9), Human, "Archer", 1, 0);
            archer.Position = new WorldPosition(0, 50, 0);
            archer.SetAim(0, 1, 0);
            _combat.Register(archer);
            _combat.Projectiles.Launch(archer, ProjectileKind.Arrow, new ActionId(22), 1, 0);
            _combat.Unregister(archer.Id);
            _combat.Projectiles.Update(33);
            Assert.AreEqual(0, _combat.Projectiles.Count);
            Assert.AreEqual((int)ProjectileEndReason.OwnerGone, _removed[0].Reason);
        }

        // ----- melee -----

        [Test]
        public void MeleeSwingHitsOnceWithAStableAttackId()
        {
            _hero.Loadout.Equip(EquipmentSlot.MainHand, Weapon(WeaponSubtype.Greatsword, 1));
            _wolf.Position = new WorldPosition(100, 50, 102);
            AttackPlan plan = AlphaAttackRoute.Select(CharacterClass.Warrior, _hero.Loadout.GetEquipped(EquipmentSlot.MainHand), null);
            Assert.AreEqual(AttackPath.Melee, plan.Path);

            Assert.IsTrue(_combat.TryStartAttack(_hero.Id, plan, 0));
            long first = _combat.ActionSerialOf(_hero.Id);
            Assert.IsFalse(_combat.TryStartAttack(_hero.Id, plan, 10), "no second swing while acting");
            _combat.Update(1400);
            Assert.AreEqual(1, _hits.Count, "one hit per swing window");
            Assert.AreEqual(first, _hits[0].AttackSerial);
            Assert.IsEmpty(_spawned, "melee releases no projectile");
            Assert.IsFalse(_combat.IsActing(_hero.Id));

            Assert.IsTrue(_combat.TryStartAttack(_hero.Id, plan, 1400));
            long second = _combat.ActionSerialOf(_hero.Id);
            _combat.Update(2800);
            Assert.AreEqual(2, _hits.Count);
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(second, _hits[1].AttackSerial);
        }

        // ----- Alpha size table / species scale -----

        [TestCase(0x02, 0.96, 2.16)]
        [TestCase(0x04, 0.8, 1.8)]
        [TestCase(0x0C, 1.04, 2.34)]
        [TestCase(0x1C, 1.52, 3.23)]
        public void AlphaSizeTableGivesTheCombatBox(int kind, double width, double height)
        {
            Assert.AreEqual(156, AlphaCreatureSizes.Count, "every kind 0x00..0x9B emulated");
            Assert.IsTrue(AlphaCreatureSizes.TryGet(kind, out double w, out double h, out double d));
            Assert.AreEqual(width, w, 1e-5);
            Assert.AreEqual(width, d, 1e-5);
            Assert.AreEqual(height, h, 1e-5);

            Combatant c = Combatant.CreateCreature(StableEntityId.ForRuntime(20), new KindId(kind), "c", 1, 0);
            GameBootstrap.ApplyAlphaSize(c, new KindId(kind));
            Assert.AreEqual(height, c.HitHeight, 1e-5, "the same rule for the hero and for creatures");
        }

        [TestCase(0x02, 1.0f)]
        [TestCase(0x04, 0.8333333f)]
        [TestCase(0x0C, 1.0833333f)]
        [TestCase(0x1C, 1.5833333f)]
        public void SpeciesScaleIsThePlacementRatioOfTheEmulatedDrawModel(int kind, float expected)
        {
            Assert.IsTrue(AlphaCreatureSizes.TryGetSpeciesScale(kind, out float factor));
            Assert.AreEqual(expected, factor, 1e-4f);
            Assert.AreEqual(expected, GameVisualSink.SpeciesScale(kind), 1e-4f, "the visual uses the same rule for hero and NPC");
        }

        [Test]
        public void ScaleReferenceIsTheDefaultHumanMale()
        {
            Assert.AreEqual(0x02, AlphaHeroCreation.KindOf(AlphaHeroCreation.Default).Value);
            Assert.AreEqual(0x02, GameVisualSink.ScaleReferenceKind);
        }
    }
}
