using System;
using System.Collections.Generic;
using CubeGame.Core.Progression;

namespace CubeGame.M0Pilots.Combat
{
    /// <summary>
    /// Ability cost rules from ALPHA_STATIC_TABLES_ITEM_SKILL §4.
    /// All values are DIRECT reads of the Alpha decision tree; unit normalization is PARTIAL.
    /// </summary>
    public static class AbilityRules
    {
        public const int RoleAlly = 3;
        public const int RoleNpc = 5;

        /// <summary>
        /// Ability_get_cooldown @ 0x444860. Seconds. Ally/NPC roles are not counted (return 0).
        /// </summary>
        public static double CooldownSeconds(int abilityId, double haste, double powerFactor, int role)
        {
            if (role == RoleAlly || role == RoleNpc)
            {
                return 0.0;
            }

            if (haste <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(haste), "Haste must be positive.");
            }

            switch (abilityId)
            {
                case 1:
                case 2:
                case 9:
                    return 6.0 / haste;
                case 3:
                case 4:
                case 0x3E:
                    return 3.0 / haste;
                case 5:
                    return 4.0;
                case 6:
                case 7:
                case 0x12:
                case 0x13:
                    return 3.0 / haste;
                case 0x0B:
                case 0x57:
                    return 10.0;
                case 0x0D:
                case 0x0E:
                case 0x0F:
                case 0x3C:
                case 0x3D:
                    return 6.0 / haste;
                case 0x11:
                case 0x14:
                    return 6.0;
                case 0x15:
                    return powerFactor * 30.0 + 5.0;
                case 0x36:
                    return 5.0;
                default:
                    return 2.0 / haste;
            }
        }

        /// <summary>ability_getManaCost @ 0x43e6a0. Raw units are 1/100000.</summary>
        public static double ManaUnits(int abilityId, double powerFactor)
        {
            switch (abilityId)
            {
                case 0x15: return 8000.0 - powerFactor * 8000.0;
                case 0x30: return 20000.0 - powerFactor * 12000.0;
                case 0x31:
                case 0x32: return 16000.0 - powerFactor * 10000.0;
                case 0x36: return 20000.0 - powerFactor * 14000.0;
                case 0x48: return 15000.0;
                case 0x56: return 60000.0 - powerFactor * 40000.0;
                case 0x58: return 8000.0 - powerFactor * 8000.0;
                case 0x60: return 20000.0 - powerFactor * 14000.0;
                case 0x61: return 60000.0 - powerFactor * 30000.0;
                case 0x63: return 12000.0 - powerFactor * 10000.0;
                case 0x64:
                case 0x65:
                case 0x66: return 60000.0 - powerFactor * 30000.0;
                case 0x67: return 20000.0 - powerFactor * 10000.0;
                default: return 0.0;
            }
        }

        /// <summary>combat_getStaminaCost @ 0x444270, divided by haste.</summary>
        public static double StaminaUnits(int abilityId, double haste)
        {
            if (haste <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(haste), "Haste must be positive.");
            }

            double value;
            switch (abilityId)
            {
                case 0x00:
                case 0x32:
                case 0x60: value = 100.0; break;
                case 0x03:
                case 0x04:
                case 0x05:
                case 0x3E: value = 300.0; break;
                case 0x07:
                case 0x0E:
                case 0x12: value = 200.0; break;
                case 0x0B:
                case 0x3C:
                case 0x3D:
                case 0x68: value = 100.0; break;
                case 0x0F: value = 400.0; break;
                case 0x10: value = 600.0; break;
                default: value = 500.0; break;
            }

            return value / haste;
        }

        private static readonly HashSet<int> Channeled = new HashSet<int>
        {
            0x5C, 0x4A, 0x22, 0x19, 0x37, 0x35, 0x57, 0x44, 0x43, 0x5D, 0x6A, 0x6B, 0x23, 0x3A, 0x39, 0x93,
            0x91, 0x92, 0x16, 0x17, 0x62, 0x38, 0x1E, 0x1F, 0x20, 0x13, 0x1A, 0x1B, 0x21, 0x64, 0x14, 0x59,
        };

        private static readonly HashSet<int> Melee = new HashSet<int>();

        static AbilityRules()
        {
            for (int id = 0x13; id <= 0x28; id++) Melee.Add(id);
            for (int id = 0x3F; id <= 0x43; id++) Melee.Add(id);
            int[] extra = { 0x4A, 0x4B, 0x62, 0x63, 0x64, 0x66, 0x67, 0x68, 0x69, 0x97 };
            for (int i = 0; i < extra.Length; i++) Melee.Add(extra[i]);
        }

        /// <summary>ability_isChanneled @ 0x444680: flags gate 0x1a00 plus the 32-id set.</summary>
        public static bool IsChanneled(int abilityId, int creatureFlags)
        {
            return (creatureFlags & 0x1A00) == 0 && Channeled.Contains(abilityId);
        }

        public static int ChanneledCount => Channeled.Count;

        /// <summary>weapon_isMeleeType @ 0x444760 classifies the ability id, not the weapon subtype.</summary>
        public static bool IsMeleeAbility(int abilityId)
        {
            return Melee.Contains(abilityId);
        }

        /// <summary>
        /// No proven mapping between a weapon subtype and a concrete attack action id exists yet.
        /// Returning false is intentional: the pilot must not invent a combat action table.
        /// </summary>
        public static bool TryGetAttackAction(int weaponSubtype, out int actionId)
        {
            actionId = 0;
            return false;
        }
    }

    /// <summary>
    /// Resource/cooldown runtime contract for one ability activation.
    /// Units: mana raws are 1/100000 (DIRECT); stamina normalization is PARTIAL, therefore the
    /// caller supplies unitsPerResource explicitly and the default is documented as a pilot assumption.
    /// </summary>
    public sealed class AbilityRuntime
    {
        public const double DefaultUnitsPerResource = 100000.0;

        public double Mana { get; set; } = 1.0;

        public double Stamina { get; set; } = 1.0;

        public double CooldownRemainingMs { get; private set; }

        public bool TryActivate(int abilityId, double haste, double powerFactor, int role,
            double unitsPerResource = DefaultUnitsPerResource)
        {
            if (unitsPerResource <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitsPerResource), "Units per resource must be positive.");
            }

            if (CooldownRemainingMs > 0.0)
            {
                return false;
            }

            double manaCost = AbilityRules.ManaUnits(abilityId, powerFactor) / unitsPerResource;
            double staminaCost = AbilityRules.StaminaUnits(abilityId, haste) / unitsPerResource;
            if (Mana + 1e-12 < manaCost || Stamina + 1e-12 < staminaCost)
            {
                return false;
            }

            Mana -= manaCost;
            Stamina -= staminaCost;
            CooldownRemainingMs = AbilityRules.CooldownSeconds(abilityId, haste, powerFactor, role) * 1000.0;
            return true;
        }

        public void Tick(double elapsedMs)
        {
            if (elapsedMs < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            }

            CooldownRemainingMs -= elapsedMs;
            if (CooldownRemainingMs < 0.0)
            {
                CooldownRemainingMs = 0.0;
            }
        }
    }

    /// <summary>
    /// Reward rules: experience formula is PARTIAL per the matrix, multipliers are documented in J2/J7.
    /// Money is not granted for kills (only item sale), so the contract returns 0.
    /// </summary>
    public static class RewardRules
    {
        public static double KillExperience(int level, int qualityStars, bool boss, bool flag0x200, bool flag0x800)
        {
            if (flag0x800)
            {
                return 0.0;
            }

            double value = AlphaMath.KillExperience(level, qualityStars);
            if (boss)
            {
                value *= 20.0;
            }

            if (flag0x200)
            {
                value *= 10.0;
            }

            return value;
        }

        public static int MoneyFromKill()
        {
            return 0;
        }
    }

    public static class CombatEvidence
    {
        public const bool HitChanceBaseProven = false; // OQ-01
        public const bool BlockFractionProven = false; // OQ-24
        public const bool CriticalsExist = false;      // no critical-hit formula found
        public const bool WeaponToActionProven = false;
    }
}
