using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Combat
{
    /// <summary>How an attack reaches its target. There is no fallback between paths (M0-F2B).</summary>
    public enum AttackPath
    {
        /// <summary>The Alpha rule for this weapon state is not known: the attack is refused, never turned into melee.</summary>
        Unsupported = 0,

        /// <summary>Actor-relative volume evaluated in the action hit window.</summary>
        Melee = 1,

        /// <summary>A projectile is released at the strike moment and hits through <see cref="ProjectileSystem"/>.</summary>
        Projectile = 2,

        /// <summary>A short-lived hit volume in front of the attacker, resolved within the action hit windows.</summary>
        AreaEffect = 3,

        /// <summary>
        /// Instant ray from the attacker up to 200 blocks, stopped by the first solid voxel (Alpha 0x615F62…0x615FDD,
        /// actions 0x1C/0x5E/0x5F; bomb 0x68 uses a fixed range of 10 instead of the voxel ray).
        /// </summary>
        Hitscan = 4,

        ItemUse = 5,
    }

    /// <summary>
    /// Alpha projectile type byte (projectile struct +0x60, written by the action handlers of
    /// <c>GameController_updateSubsystems</c> 0x60C510): 0 arrow (actions 0x16/0x17), 1 magic (0x25…0x2E),
    /// 2 boomerang (0x1A).
    /// </summary>
    public enum ProjectileKind
    {
        Arrow = 0,
        Magic = 1,
        Boomerang = 2,

        /// <summary>Not a flying object: the trail of a hitscan attack (event <see cref="HitscanFired"/>).</summary>
        Beam = 101,
    }

    /// <summary>Result of the attack selection: one action, one path, and the provenance of the decision.</summary>
    public readonly struct AttackPlan
    {
        public AttackPlan(ActionId action, AttackPath path, ProjectileKind projectile, string provenance, string reason = "")
        {
            Action = action;
            Path = path;
            Projectile = projectile;
            Provenance = provenance ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public ActionId Action { get; }

        public AttackPath Path { get; }

        /// <summary>Projectile type for the launch route.</summary>
        public ProjectileKind Projectile { get; }

        public string Provenance { get; }

        /// <summary>Why the plan is <see cref="AttackPath.Unsupported"/> (empty otherwise).</summary>
        public string Reason { get; }

        public bool IsSupported => Path != AttackPath.Unsupported;

        public static AttackPlan Refused(string reason) =>
            new AttackPlan(default, AttackPath.Unsupported, ProjectileKind.Arrow, "UNKNOWN", reason);

        public override string ToString() =>
            IsSupported ? $"action {Action.Value} {Path}" : $"refused: {Reason}";
    }

    /// <summary>
    /// Weapon state → attack path of the player's primary attack (M0-F2B). Melee actions come from
    /// <see cref="AlphaPrimaryAttack"/> (C4). Ranged/magic actions are the first actions emulated from
    /// <c>Cube.exe 0x445020</c> (C4 report: 22 bow/crossbow, 26 boomerang, 30 staff, 38 wand); the path of each
    /// action is the action handler of <c>GameController_updateSubsystems</c> that consumes it:
    /// <list type="bullet">
    /// <item>0x16 (22), 0x1A (26): push a projectile (type 0 / 2) when the action time crosses the windup — 0x6171DB.</item>
    /// <item>0x26 (38): pushes a magic projectile (type 1) at the windup — 0x617C88.</item>
    /// <item>0x1E (30): is in the melee case group and hits through a volume moved in front of the attacker — 0x6142A0.</item>
    /// </list>
    /// A weapon state without a recovered rule is refused (never silently turned into melee).
    /// </summary>
    public static class AlphaAttackRoute
    {
        public const int BowAction = 22;
        public const int BoomerangAction = 26;
        public const int StaffAction = 30;
        public const int WandAction = 38;
        public const int BraceletAction = 0x28;
        public const int BombAction = 0x68;

        public static AttackPlan Select(CharacterClass characterClass, ItemInstance mainHand, ItemInstance offHand,
            CharacterSpecialization specialization = CharacterSpecialization.Primary)
        {
            ActionId? melee = AlphaPrimaryAttack.SelectMelee(characterClass, mainHand, offHand);
            if (melee.HasValue)
            {
                return ForAction(melee.Value.Value);
            }

            if (IsWeapon(offHand, WeaponSubtype.Bow) || IsWeapon(offHand, WeaponSubtype.Crossbow))
            {
                return ForAction(BowAction);
            }

            if (mainHand != null && mainHand.Type == ItemType.Weapon)
            {
                switch ((WeaponSubtype)mainHand.Subtype)
                {
                    case WeaponSubtype.Bow:
                    case WeaponSubtype.Crossbow:
                        return ForAction(BowAction);
                    case WeaponSubtype.Boomerang:
                        return ForAction(BoomerangAction);
                    case WeaponSubtype.Wand:
                        return ForAction(specialization == CharacterSpecialization.Secondary ? 0x2C : WandAction);
                    case WeaponSubtype.Staff:
                        return ForAction(specialization == CharacterSpecialization.Secondary ? 0x20 : StaffAction);
                    case WeaponSubtype.Bracelet:
                        return ForAction(specialization == CharacterSpecialization.Secondary ? 0x2A : BraceletAction);
                }
            }

            string hand = mainHand == null ? "empty main hand" : $"main hand type {(int)mainHand.Type} subtype {mainHand.Subtype}";
            return AttackPlan.Refused($"UNKNOWN Alpha primary attack for class {characterClass} with {hand}");
        }

        /// <summary>Routes decoded from the action jump table at 0x611E04 / 0x622CCC in Cube.exe.</summary>
        public static AttackPlan ForAction(int action)
        {
            var id = new ActionId(action);
            const string dispatcher = "ALPHA_CODE_STATIC (0x611E04)";
            switch (action)
            {
                case 0x16:
                case 0x17:
                    return new AttackPlan(id, AttackPath.Projectile, ProjectileKind.Arrow, dispatcher);
                case 0x1A:
                    return new AttackPlan(id, AttackPath.Projectile, ProjectileKind.Boomerang, dispatcher);
                case 0x1E:
                case 0x1F:
                case 0x20:
                case 0x21:
                    return new AttackPlan(id, AttackPath.AreaEffect, default, dispatcher);
                case 0x1C:
                case 0x5E:
                case 0x5F:
                    return new AttackPlan(id, AttackPath.Hitscan, ProjectileKind.Beam, dispatcher);
                case 0x50:
                    return new AttackPlan(id, AttackPath.ItemUse, default, "PARTIAL (0x50 consumable)");
                case 0x01: case 0x02: case 0x03: case 0x04: case 0x05: case 0x06: case 0x07:
                case 0x09: case 0x0A: case 0x0C: case 0x0D: case 0x0E: case 0x0F:
                case 0x10: case 0x12: case 0x13: case 0x39: case 0x3A: case 0x41:
                case 0x42: case 0x43: case 0x44: case 0x46: case 0x4B: case 0x4C:
                case 0x4D: case 0x4E:
                    return new AttackPlan(id, AttackPath.Melee, default, dispatcher);
                default:
                    if (action >= 0x25 && action <= 0x2E)
                    {
                        return new AttackPlan(id, AttackPath.Projectile, ProjectileKind.Magic, dispatcher);
                    }

                    return new AttackPlan(id, AttackPath.Unsupported, default, "UNKNOWN",
                        $"UNKNOWN or unsupported Alpha action 0x{action:X}");
            }
        }

        private static bool IsWeapon(ItemInstance item, WeaponSubtype subtype) =>
            item != null && item.Type == ItemType.Weapon && item.Subtype == (byte)subtype;
    }
}
