using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Features.Combat;
using CubeGame.Presentation.Visual;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    /// <summary>
    /// M0-F2B: the visual weapon grip of F2A (<see cref="HeroVisualSpec.WeaponGrip"/>) and the gameplay attack path agree
    /// for every weapon subtype — a bow is never drawn as a bow while attacking as a sword, and vice versa.
    /// </summary>
    public sealed class AttackRouteGripTests
    {
        [Test]
        public void GripAndAttackPathAgreeForEveryWeaponSubtype()
        {
            for (int subtype = 0; subtype <= 0x12; subtype++)
            {
                HeroWeaponGrip grip = HeroVisualComposer.GripOf(subtype);
                var item = new ItemInstance(ItemType.Weapon, (byte)subtype, 1, 0, 1, (ItemModifier)2);
                CharacterClass cls = grip == HeroWeaponGrip.Staff || grip == HeroWeaponGrip.Wand || grip == HeroWeaponGrip.Bracelet
                    ? CharacterClass.Mage
                    : CharacterClass.Ranger;
                AttackPlan plan = AlphaAttackRoute.Select(cls, item, null);
                string label = $"subtype {subtype} grip {grip} → {plan}";
                switch (grip)
                {
                    case HeroWeaponGrip.Bow:
                    case HeroWeaponGrip.Crossbow:
                        Assert.AreEqual(AttackPath.Projectile, plan.Path, label);
                        Assert.AreEqual(ProjectileKind.Arrow, plan.Projectile, label);
                        break;
                    case HeroWeaponGrip.Boomerang:
                        Assert.AreEqual(ProjectileKind.Boomerang, plan.Projectile, label);
                        break;
                    case HeroWeaponGrip.Wand:
                    case HeroWeaponGrip.Bracelet:
                        Assert.AreEqual(AttackPath.Projectile, plan.Path, label);
                        Assert.AreEqual(ProjectileKind.Magic, plan.Projectile, label);
                        break;
                    case HeroWeaponGrip.Staff:
                        Assert.AreEqual(AttackPath.AreaEffect, plan.Path, label);
                        break;
                    case HeroWeaponGrip.OneHanded:
                    case HeroWeaponGrip.TwoHanded:
                        Assert.AreEqual(AttackPath.Melee, plan.Path, label);
                        break;
                    default:
                        Assert.AreNotEqual(AttackPath.Projectile, plan.Path, label);
                        break;
                }
            }
        }
    }
}
