using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Combat
{
    /// <summary>First primary melee attack from an idle player (Cube.exe 0x445020, C4).</summary>
    public static class AlphaPrimaryAttack
    {
        /// <summary>Null leaves ranged and casting input to their own action path.</summary>
        public static ActionId? SelectMelee(CharacterClass characterClass, ItemInstance mainHand, ItemInstance offHand)
        {
            if (characterClass == CharacterClass.Mage || HasBow(offHand))
            {
                return null;
            }

            if (mainHand == null)
            {
                return new ActionId(6);
            }

            if (mainHand.Type != ItemType.Weapon || mainHand.Subtype > (byte)WeaponSubtype.Torch)
            {
                return null;
            }

            switch ((WeaponSubtype)mainHand.Subtype)
            {
                case WeaponSubtype.Bow:
                case WeaponSubtype.Crossbow:
                case WeaponSubtype.Boomerang:
                case WeaponSubtype.Staff:
                case WeaponSubtype.Wand:
                case WeaponSubtype.Bracelet:
                    return null;
                case WeaponSubtype.Fist:
                    return new ActionId(6);
                case WeaponSubtype.Dagger:
                    return new ActionId(19);
                case WeaponSubtype.Longsword:
                    return new ActionId(14);
                case WeaponSubtype.Greatsword:
                case WeaponSubtype.Greataxe:
                case WeaponSubtype.Greatmace:
                case WeaponSubtype.Pitchfork:
                    return new ActionId(57);
                default:
                    return new ActionId(offHand?.Subtype == (byte)WeaponSubtype.Shield ? 10 : 1);
            }
        }

        private static bool HasBow(ItemInstance item) =>
            item?.Subtype == (byte)WeaponSubtype.Bow || item?.Subtype == (byte)WeaponSubtype.Crossbow;
    }
}
