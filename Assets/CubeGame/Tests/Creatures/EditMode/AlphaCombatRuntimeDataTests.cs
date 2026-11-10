// M0-F2B — EditMode checks of the generated Alpha combat-runtime data (weapon -> attack -> numbers).
//
// These tests never touch the runtime combat path: they only prove that the committed
// AlphaCombatRuntimeData table still says what the byte-verified generator derived from Cube.exe
// (tools/alpha_content/gen_combat_runtime_data.py) and that nothing UNKNOWN is presented as a default.
using System.Linq;
using System.Reflection;
using CubeGame.Content;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaCombatRuntimeDataTests
    {
        static AlphaWeaponCombatDef Weapon(int subtype)
        {
            Assert.IsTrue(AlphaCombatRuntimeData.TryGetWeapon(subtype, out var def), $"weapon subtype {subtype} missing");
            return def;
        }

        static AlphaAttackCombatDef Attack(int id)
        {
            Assert.IsTrue(AlphaCombatRuntimeData.TryGetAttack(id, out var def), $"attack {id} missing");
            return def;
        }

        [Test]
        public void WeaponProjectileMappingMatchesAlphaItemLayer()
        {
            var bow = Weapon(6);
            Assert.AreEqual("bow.cub", bow.Model);
            Assert.AreEqual(0x17, bow.AttackEven);
            Assert.AreEqual(0x17, bow.AttackOdd);
            Assert.AreEqual("weapon_6_before_class", bow.Route);

            var crossbow = Weapon(7);
            Assert.AreEqual("wood-crossbow1.cub", crossbow.Model);
            Assert.AreEqual(0x42, crossbow.AttackEven);
            Assert.AreEqual(0x41, crossbow.AttackOdd);
            Assert.AreEqual("parity", crossbow.Route);

            var boomerang = Weapon(8);
            Assert.AreEqual("wood-boomerang1.cub", boomerang.Model);
            Assert.AreEqual("parity", boomerang.Route);

            var arrow = Weapon(9);
            Assert.AreEqual("arrow.cub", arrow.Model);

            var staff = Weapon(10);
            Assert.AreEqual("wood-staff1.cub", staff.Model);
            Assert.AreEqual(0x1F, staff.AttackEven);
            Assert.AreEqual("class_branch_subtype", staff.Route);

            var wand = Weapon(11);
            Assert.AreEqual("wood-wand1.cub", wand.Model);
            Assert.AreEqual(0x2D, wand.AttackEven);
            Assert.AreEqual("class_branch_subtype", wand.Route);

            var bracelet = Weapon(12);
            Assert.AreEqual("silver-bracelet1.cub", bracelet.Model);
            Assert.AreEqual(0x25, bracelet.AttackEven);
        }

        [Test]
        public void EveryWeaponRowCarriesItsAlphaModel()
        {
            Assert.IsNotEmpty(AlphaCombatRuntimeData.Weapons);
            foreach (var weapon in AlphaCombatRuntimeData.Weapons)
            {
                Assert.AreEqual(3, weapon.ItemType, "every weapon class is item type 3");
                Assert.IsFalse(string.IsNullOrEmpty(weapon.Model), $"subtype {weapon.Subtype} has no model");
                StringAssert.EndsWith(".cub", weapon.Model, $"subtype {weapon.Subtype}");
                Assert.IsNotEmpty(weapon.Route, $"subtype {weapon.Subtype} has no Alpha route");
            }
        }

        [Test]
        public void AttackRowsAreUniqueAndCoverTheProvenRange()
        {
            var attacks = AlphaCombatRuntimeData.Attacks;
            Assert.AreEqual(AlphaCombatRuntimeData.MaxAttackId + 1, attacks.Length, "every action id 0..max has a row");

            var duplicates = attacks.GroupBy(a => a.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Assert.IsEmpty(duplicates, "duplicate attack rows");

            for (int i = 1; i < attacks.Length; i++)
            {
                Assert.AreEqual(attacks[i - 1].Id + 1, attacks[i].Id, "rows are ordered and dense");
            }

            Assert.IsFalse(AlphaCombatRuntimeData.TryGetAttack(AlphaCombatRuntimeData.MaxAttackId + 1, out _),
                "an id outside the proven range must not resolve to a silent default");
            Assert.IsFalse(AlphaCombatRuntimeData.TryGetAttack(-1, out _));
        }

        [Test]
        public void UnknownWeaponSubtypeIsNotSilentlyUnarmed()
        {
            Assert.IsFalse(AlphaCombatRuntimeData.TryGetWeapon(99, out _), "no row means unknown, not 'unarmed'");
            Assert.IsFalse(AlphaCombatRuntimeData.TryGetWeapon(AlphaCombatRuntimeData.FirstUnknownWeaponSubtype, out _),
                "subtype 26 is past the item-layer table");

            // ...while subtypes Alpha really has keep their row (absence is not a blanket answer).
            Assert.IsTrue(AlphaCombatRuntimeData.TryGetWeapon(0, out var sword));
            Assert.AreEqual("iron-sword1.cub", sword.Model);
            Assert.IsTrue(AlphaCombatRuntimeData.TryGetWeapon(20, out var torch));
            Assert.AreEqual("torch.cub", torch.Model);
        }

        [Test]
        public void MuzzleTrailPathIsProvenForExactlyTheByteVerifiedActions()
        {
            var expected = new[] { 0x1C, 0x25, 0x26, 0x2B, 0x2C, 0x5E, 0x5F };
            CollectionAssert.AreEquivalent(expected, AlphaCombatRuntimeData.MuzzleAttackIds);

            foreach (var attack in AlphaCombatRuntimeData.Attacks)
            {
                bool expectedMuzzle = expected.Contains(attack.Id);
                Assert.AreEqual(expectedMuzzle, attack.MuzzlePathProven, $"attack 0x{attack.Id:X2}");
            }

            Assert.AreEqual(0x68, AlphaCombatRuntimeData.BombAttackId);
            var bombRows = AlphaCombatRuntimeData.Attacks.Where(a => a.Bomb).Select(a => a.Id).ToArray();
            CollectionAssert.AreEquivalent(new[] { AlphaCombatRuntimeData.BombAttackId }, bombRows,
                "only the bomb action takes the fixed-range branch");
        }

        [Test]
        public void MuzzlePathIsNotClaimedForWeaponsWithoutProof()
        {
            // Bow/crossbow/staff/wand have models and attack ids, but no byte anchor puts their action
            // ids into the muzzle / trail gate: the flag stays false instead of being guessed true.
            foreach (int subtype in new[] { 6, 7, 8, 10, 11 })
            {
                var weapon = Weapon(subtype);
                Assert.IsFalse(weapon.MuzzlePathProven, $"subtype {subtype} ({weapon.Label}) has no muzzle proof");
                Assert.AreEqual("PARTIAL", weapon.Status, $"subtype {subtype} must stay PARTIAL");
            }

            var bracelet = Weapon(12);
            Assert.IsTrue(bracelet.MuzzlePathProven, "bracelet attack ids 0x25/0x2B are inside the muzzle gate");
            Assert.AreEqual("ALPHA_CODE_STATIC", bracelet.Status);
        }

        [Test]
        public void AttackNumbersMatchTheDecodedSwitches()
        {
            // Bow attack 0x17: cast 50 ms, recovery 50 ms, stamina base 10 (all ScaledByHasteAndSpeed).
            var bowAttack = Attack(0x17);
            Assert.AreEqual(AlphaCombatValueMode.ScaledByHasteAndSpeed, bowAttack.CastMode);
            Assert.AreEqual(50.0f, bowAttack.Cast);
            Assert.AreEqual(50.0f, bowAttack.Recover);
            Assert.AreEqual(10.0f, bowAttack.Stamina);
            Assert.IsTrue(bowAttack.IsExplicit);

            // Staff attack 0x1F: 1600 ms cast, fixed 1200 ms recovery, stamina base 100.
            var staffAttack = Attack(0x1F);
            Assert.AreEqual(1600.0f, staffAttack.Cast);
            Assert.AreEqual(AlphaCombatValueMode.Fixed, staffAttack.RecoverMode);
            Assert.AreEqual(1200.0f, staffAttack.Recover);
            Assert.AreEqual(100.0f, staffAttack.Stamina);

            // Bracelet attack 0x25: 600 ms only for weapon subtype 0x0C, 1200 ms otherwise.
            var braceletAttack = Attack(0x25);
            Assert.AreEqual(12, braceletAttack.CastConditionWeaponSubtype);
            Assert.AreEqual(600.0f, braceletAttack.Cast);
            Assert.AreEqual(0x2B, Attack(0x2B).Id);

            // Bomb 0x68 has no cast time and the fixed-range branch.
            var bomb = Attack(AlphaCombatRuntimeData.BombAttackId);
            Assert.AreEqual(AlphaCombatValueMode.Fixed, bomb.CastMode);
            Assert.AreEqual(0.0f, bomb.Cast);
            Assert.IsTrue(bomb.Bomb);
        }

        [Test]
        public void DefaultBranchValuesAreFlaggedInsteadOfFabricated()
        {
            // 0x69 is the last id of the cast switch; the recovery and stamina switches stop at 0x68,
            // so their numbers come from `default:` and must be flagged as such.
            var last = Attack(AlphaCombatRuntimeData.MaxAttackId);
            Assert.AreEqual(5000.0f, last.Cast);
            Assert.IsTrue(last.RecoverFromDefault);
            Assert.IsTrue(last.StaminaFromDefault);
            Assert.IsFalse(last.IsExplicit);

            // Explicit rows are not flagged.
            Assert.IsTrue(Attack(0x5F).IsExplicit);
            Assert.IsTrue(Attack(0x26).IsExplicit);
        }

        [Test]
        public void EscapeArrowsAndQuiversAreNotClaimedAsProjectileAttackActions()
        {
            // arrow.cub and quiver.cub are held items; Alpha has no action that fires them.
            Assert.AreEqual("unarmed", Weapon(9).Route);
            Assert.AreEqual("unarmed", Weapon(14).Route);
            Assert.IsFalse(Weapon(9).MuzzlePathProven);
            Assert.IsFalse(Weapon(14).MuzzlePathProven);
        }

        [Test]
        public void GeneratedDataInventsNoFlightNumbers()
        {
            // No speed / gravity / lifetime constant exists in the table: Alpha resolves the ranged hit
            // by a raycast and draws a swept mesh, so those numbers are UNKNOWN (see the F2B report).
            var names = typeof(AlphaCombatRuntimeData)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => f.Name.ToLowerInvariant())
                .ToArray();
            Assert.IsFalse(names.Any(n => n.Contains("speed")), "no invented projectile speed");
            Assert.IsFalse(names.Any(n => n.Contains("gravity")), "no invented gravity");
            Assert.IsFalse(names.Any(n => n.Contains("lifetime")), "no invented lifetime");

            Assert.AreEqual("ALPHA_CODE_STATIC", AlphaCombatRuntimeData.Status);
            Assert.AreEqual("cubegame.alpha_combat_runtime/1", AlphaCombatRuntimeData.Format);
            Assert.AreEqual(0x446BB0, AlphaCombatRuntimeData.MuzzlePositionVa);
            Assert.AreEqual(0x4C04C0, AlphaCombatRuntimeData.ProjectileTrailMeshVa);
        }
    }
}
