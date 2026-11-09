// CubeGame.Features.Creatures — Alpha's attack-variant selection (C-29 / M0-06C).
//
// Alpha does not launch "the first action of the library". `combat_selectAttackVariant` (Cube.exe VA 0x4452A0)
// maps the creature's held weapon (raw item type [creature+0xaa8], subtype [+0xaa9]), its class [+0x140],
// its specialisation [+0x141] and one parity bit to an action id. The numbers come from the generated table
// AlphaAttackSelectionTable (tools/alpha_content/attack_selection.py, byte-verified against Cube.exe
// sha256 84a7a132…30bf, function bytes sha256 81822ebc…17bc); this file only holds the control flow.
//
// Status: ALPHA_CODE_STATIC. Two facts stay PARTIAL/UNKNOWN and are deliberately not invented:
//   * the parity bit is bit0 of field +0x70, the same field the attack gate tests as `== 0` (no target), so
//     it holds a target handle/pointer; what makes bit0 flip is NOT established — the game passes false
//     and therefore always gets the even branch (0x42 / 3), which matches every observed value;
//   * the world spawn record that assigns class/specialisation/weapon (R-1) is not executed yet, so a
//     creature without a weapon correctly resolves to the unarmed branch (action 3).
using CubeGame.Content;

namespace CubeGame.Features.Creatures
{
    /// <summary>Pure port of Alpha's attack-variant choice. Never decides damage, timing or resources.</summary>
    public static class AlphaAttackSelection
    {
        /// <summary>Status of the numbers used here (no live capture, static decode of the shipped code).</summary>
        public const string EvidenceStatus = AlphaAttackSelectionTable.Status;

        public const string Evidence =
            "Cube.exe 0x4452A0 combat_selectAttackVariant (ALPHA_CODE_STATIC): weapon (3,6)->0x17, (3,5)->5, " +
            "class 3 / kind 0x75,0x56 or weapon subtype 0x0A/0x0B/0x0C -> class/specialisation branch, " +
            "other weapon subtypes {0x0F,0x10,0x11,0x05,0x0A,0x0B,0x12,0x08,0x06,0x07} -> 0x42 - parity, " +
            "everything else -> 3 + parity";

        /// <summary>No item in the hand (Alpha stores 0 in a free hand; the game uses -1 for "unknown").</summary>
        public const int NoWeapon = -1;

        /// <summary>
        /// Alpha attack id for this creature state. <paramref name="parityBit"/> is the PARTIAL bit of field
        /// +0x70 (see the file header); pass false to reproduce the observed even branch.
        /// </summary>
        public static int SelectAttackId(int creatureKind, int classId, int specialisationId,
            int weaponItemType, int weaponItemSubtype, bool parityBit)
        {
            // 0x4452A0..0x4452B8: weapon (type 3, subtype 6) is decided before class and kind.
            if (weaponItemType == AlphaAttackSelectionTable.WeaponItemType &&
                weaponItemSubtype == AlphaAttackSelectionTable.TwoHandedSubtype)
            {
                return AlphaAttackSelectionTable.TwoHandedAttack;
            }

            // 0x4452B9..0x4452D5: class 3 or kind 0x75/0x56 always takes the class/specialisation branch.
            if (IsClassBranch(creatureKind, classId))
            {
                return ClassBranchAttack(weaponItemSubtype, specialisationId);
            }

            if (weaponItemType == AlphaAttackSelectionTable.WeaponItemType)
            {
                // 0x4452DF..0x4452F2: subtypes 0x0A/0x0B/0x0C jump to the class branch.
                if (Contains(AlphaAttackSelectionTable.ClassBranchSubtypes, weaponItemSubtype))
                {
                    return ClassBranchAttack(weaponItemSubtype, specialisationId);
                }

                // 0x4452F8..0x445302: subtype 5 is always attack 5.
                if (weaponItemSubtype == AlphaAttackSelectionTable.ModeSwitchSubtype)
                {
                    return AlphaAttackSelectionTable.ModeSwitchAttack;
                }

                // 0x445307..0x44534B: these subtypes differ only by the parity bit.
                if (Contains(AlphaAttackSelectionTable.ParitySubtypes, weaponItemSubtype))
                {
                    return parityBit ? AlphaAttackSelectionTable.ParityAttackOdd : AlphaAttackSelectionTable.ParityAttackEven;
                }
            }

            // 0x44534C..0x445364: no recognised weapon -> unarmed attack, 3 or 4 by parity.
            return parityBit ? AlphaAttackSelectionTable.UnarmedAttackOdd : AlphaAttackSelectionTable.UnarmedAttackEven;
        }

        /// <summary>True when Alpha ignores the weapon kind and decides by class/specialisation (see 0x4452B9).</summary>
        public static bool IsClassBranch(int creatureKind, int classId)
        {
            if (classId == AlphaAttackSelectionTable.SpecialClass) return true;
            return Contains(AlphaAttackSelectionTable.SpecialKinds, creatureKind);
        }

        /// <summary>0x445365..0x4453AB: the class/specialisation sub-branch.</summary>
        public static int ClassBranchAttack(int weaponItemSubtype, int specialisationId)
        {
            bool specialist = specialisationId == AlphaAttackSelectionTable.SpecialistSpec;
            if (weaponItemSubtype == AlphaAttackSelectionTable.BleedSubtype)
            {
                return specialist ? AlphaAttackSelectionTable.BleedAttackSpecialist : AlphaAttackSelectionTable.BleedAttackDefault;
            }

            if (weaponItemSubtype == AlphaAttackSelectionTable.CastSubtype)
            {
                return specialist ? AlphaAttackSelectionTable.CastAttackSpecialist : AlphaAttackSelectionTable.CastAttackDefault;
            }

            return specialist ? AlphaAttackSelectionTable.DefaultAttackSpecialist : AlphaAttackSelectionTable.DefaultAttackDefault;
        }

        static bool Contains(int[] values, int value)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value) return true;
            }

            return false;
        }
    }
}
