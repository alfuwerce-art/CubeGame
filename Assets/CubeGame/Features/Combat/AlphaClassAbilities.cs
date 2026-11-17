using System;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;

namespace CubeGame.Features.Combat
{
    /// <summary>
    /// Alpha 2013 class/specialization action context of the player (ALPHA_CODE_STATIC, Cube.exe):
    /// the hotbar <c>GameController+0x800814</c> built at 0x48B8AF…0x48BD8A, the ability → rank slot table
    /// 0x43C9C0, the power factor 0x43ED60, ability cooldowns 0x43E6A0, mana costs 0x444AE0, the usability gate
    /// 0x43E5A0 and the mana (+0x170) regeneration of the creature update (0x618790…0x6187B0).
    /// P5 owns these data; executing an action is P4 (<see cref="CombatSystem"/>).
    /// </summary>
    public static class AlphaClassAbilities
    {
        /// <summary>Hotbar size: primary, secondary and three class abilities.</summary>
        public const int Slots = 5;

        /// <summary>Mana regained per millisecond (<c>dt_ms·0.0001</c>): an empty bar fills in 10 s.</summary>
        public const float ManaRegenPerMs = 0.0001f;

        public const int Kick = 0x15, HealingStream = 0x22, Intercept = 0x30, Teleport = 0x31, Retreat = 0x32,
            Smash = 0x36, Sneak = 0x4F, Cyclone = 0x56, FireExplosion = 0x58, Shuriken = 0x60, Camouflage = 0x61,
            Aim = 0x63, Swiftness = 0x64, Bulwark = 0x65, WarFrenzy = 0x66, ManaShield = 0x67;

        /// <summary>Alpha class byte <c>+0x140</c> = class index + 1.</summary>
        public static int AlphaClassByte(CharacterClass c) => (int)c + 1;

        /// <summary>
        /// The five hotbar actions. Slot 1/2 follow the weapon (staff, bracelet and wand pairs depend on the
        /// specialization, otherwise the primary from <see cref="AlphaAttackRoute"/> and a secondary by subtype);
        /// slots 3–5 are the class abilities, 0 while the rank of their slot (<c>+0x1138[6…8]</c>) is not positive.
        /// </summary>
        public static int[] Hotbar(CharacterClass characterClass, CharacterSpecialization specialization,
            ItemInstance mainHand, ItemInstance offHand, int[] ranks)
        {
            var bar = new int[Slots];
            bool secondSpec = specialization == CharacterSpecialization.Secondary;
            int main = Subtype(mainHand);
            int off = Subtype(offHand);
            switch (main)
            {
                case 0x0A:
                    bar[0] = secondSpec ? 0x20 : 0x1E;
                    bar[1] = secondSpec ? 0x21 : 0x1F;
                    break;
                case 0x0C:
                    bar[0] = secondSpec ? 0x2A : 0x28;
                    bar[1] = secondSpec ? 0x2B : 0x25;
                    break;
                case 0x0B:
                    bar[0] = secondSpec ? 0x2C : 0x26;
                    bar[1] = secondSpec ? 0x2D : 0x2E;
                    break;
                default:
                    AttackPlan primary = AlphaAttackRoute.Select(characterClass, mainHand, offHand, specialization);
                    bar[0] = primary.Path == AttackPath.Unsupported ? 0 : primary.Action.Value;
                    bar[1] = Secondary(main, off);
                    break;
            }

            switch (characterClass)
            {
                case CharacterClass.Mage:
                    bar[2] = secondSpec ? HealingStream : FireExplosion;
                    bar[3] = ManaShield;
                    bar[4] = Teleport;
                    break;
                case CharacterClass.Rogue:
                    bar[2] = Intercept;
                    bar[3] = Sneak;
                    bar[4] = secondSpec ? Shuriken : Camouflage;
                    break;
                case CharacterClass.Ranger:
                    bar[2] = Kick;
                    bar[3] = Retreat;
                    bar[4] = secondSpec ? Swiftness : Aim;
                    break;
                case CharacterClass.Warrior:
                    bar[2] = Smash;
                    bar[3] = Cyclone;
                    bar[4] = secondSpec ? Bulwark : WarFrenzy;
                    break;
            }

            for (int slot = 2; slot < Slots; slot++)
            {
                int rank = ranks != null && slot + 4 < ranks.Length ? ranks[slot + 4] : 0;
                if (rank <= 0)
                {
                    bar[slot] = 0;
                }
            }

            return bar;
        }

        private static int Subtype(ItemInstance item) =>
            item != null && item.Type == ItemType.Weapon ? item.Subtype : -1;

        /// <summary>Secondary action by weapon (0x48BA32…0x48BB3C); 0x444230 = two-handed main hand.</summary>
        private static int Secondary(int main, int off)
        {
            switch (main)
            {
                case 5: return 0x05;
                case 3: return 0x11;
                case 4: return 0x14;
            }

            if (main == 6 || off == 6) return 0x19;
            if (main == 7 || off == 7) return 0x18;
            if (main == 8) return 0x1B;
            if (IsTwoHanded(main)) return 0x3B;
            return off == 0x0D ? 0x08 : 0x3F;
        }

        /// <summary>0x444230.</summary>
        public static bool IsTwoHanded(int subtype)
        {
            switch (subtype)
            {
                case 0x0F: case 0x10: case 0x11: case 0x05: case 0x0A: case 0x0B: case 0x12: case 0x08: case 0x06: case 0x07:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>0x43C9C0: rank slot that powers an ability, −1 for other actions.</summary>
        public static int RankSlot(int action)
        {
            switch (action)
            {
                case 0x15: case 0x22: case 0x30: case 0x36: case 0x58: return 6;
                case 0x32: case 0x4F: case 0x56: case 0x67: return 7;
                case 0x31: case 0x60: case 0x61: case 0x63: case 0x64: case 0x65: case 0x66: return 8;
                default: return -1;
            }
        }

        /// <summary>
        /// 0x43ED60 <c>1 − 1/(0.1·rank + 1)</c> (1 for actions without a rank slot). A non-player creature with rank 0
        /// uses <c>level/2 + stars + 1</c>.
        /// </summary>
        public static float PowerFactor(int action, int[] ranks, bool player = true, int level = 1, int stars = 0)
        {
            int slot = RankSlot(action);
            if (slot < 0)
            {
                return 1f;
            }

            int rank = ranks != null && slot < ranks.Length ? ranks[slot] : 0;
            if (rank == 0 && !player)
            {
                rank = level / 2 + stars + 1;
            }

            return 1f - 1f / (rank * 0.1f + 1f);
        }

        /// <summary>0x43E6A0: cooldown in ms that starts when the ability is used (0 for other actions).</summary>
        public static int CooldownMs(int action, float p)
        {
            switch (action)
            {
                case 0x15: return (int)(8000f - p * 8000f);
                case 0x30: return (int)(20000f - p * 12000f);
                case 0x31: return (int)(16000f - p * 10000f);
                case 0x32: return (int)(16000f - p * 10000f);
                case 0x36: return (int)(20000f - p * 14000f);
                case 0x48: return 15000;
                case 0x56: return (int)(60000f - p * 40000f);
                case 0x58: return (int)(8000f - p * 8000f);
                case 0x60: return (int)(20000f - p * 14000f);
                case 0x61: return (int)(60000f - p * 30000f);
                case 0x63: return (int)(12000f - p * 10000f);
                case 0x64: return (int)(60000f - p * 30000f);
                case 0x65: return (int)(60000f - p * 30000f);
                case 0x66: return (int)(60000f - p * 30000f);
                case 0x67: return (int)(20000f - p * 10000f);
                default: return 0;
            }
        }

        /// <summary>
        /// 0x444AE0: mana fraction an action needs. Shield bash actions 3/4 cost 0.1; the magic secondaries cost 0.3
        /// unless buff type 9 is active; Healing Stream costs <c>(1 − 0.75p)·0.125</c>; everything else 0.
        /// </summary>
        public static float ManaCost(int action, float p, bool buff9Active = false)
        {
            switch (action)
            {
                case 3:
                case 4:
                    return 0.1f;
                case 0x1F: case 0x21: case 0x25: case 0x2B: case 0x2D: case 0x2E: case 0x5F:
                    return buff9Active ? 0f : 0.3f;
                case 0x22:
                    return (1f - p * 0.75f) * 0.125f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// 0x43E5A0: the action may start when the stun-like counter <c>+0x12C</c> is below 1 (Bulwark ignores it), the
        /// mana covers its cost and its cooldown is over; 0x1C additionally needs some mana.
        /// </summary>
        public static bool IsUsable(int action, float p, float mana, int counter12C, int cooldownLeftMs, bool buff9Active = false)
        {
            if (counter12C >= 1 && action != Bulwark)
            {
                return false;
            }

            if (ManaCost(action, p, buff9Active) > mana || cooldownLeftMs > 0)
            {
                return false;
            }

            return action != 0x1C || mana > 0f;
        }

        /// <summary>Actions that stop the mana regeneration until their attack timing (0x6187A7 condition).</summary>
        public static bool BlocksManaRegen(int action) =>
            action == 0x1C || action == 0x5F || action == 0x25 || action == 0x2E || action == 0x2D
            || action == 0x1F || action == 0x21 || action == 0x2B || action == 0x22;
    }
}
