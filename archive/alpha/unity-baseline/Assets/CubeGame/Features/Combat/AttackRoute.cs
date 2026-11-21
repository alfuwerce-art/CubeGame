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

        /// <summary>Frontal hit sector at the strike moment (<see cref="CombatSystem.InMeleeReach"/>).</summary>
        Melee = 1,

        /// <summary>A projectile is released at the strike moment and hits through <see cref="ProjectileSystem"/>.</summary>
        Projectile = 2,

        /// <summary>A short-lived hit volume in front of the attacker, also resolved by <see cref="ProjectileSystem"/>.</summary>
        AreaEffect = 3,

        /// <summary>
        /// Instant ray from the attacker up to 200 blocks, stopped by the first solid voxel (Alpha 0x615F62…0x615FDD,
        /// actions 0x1C/0x5E/0x5F; bomb 0x68 uses a fixed range of 10 instead of the voxel ray).
        /// </summary>
        Hitscan = 4,
    }

    /// <summary>
    /// Alpha projectile type byte (projectile struct +0x60, written by the action handlers of
    /// <c>GameController_updateSubsystems</c> 0x60C510): 0 arrow (actions 0x16/0x17), 1 magic (0x25…0x2E),
    /// 2 boomerang (0x1A). <see cref="StaffBurst"/> is CubeGame's: Alpha resolves the staff action 0x1E as a hit volume
    /// (the melee case group at 0x6142A0), not as a projectile.
    /// </summary>
    public enum ProjectileKind
    {
        Arrow = 0,
        Magic = 1,
        Boomerang = 2,
        StaffBurst = 100,

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

        /// <summary>Meaningful for <see cref="AttackPath.Projectile"/> and <see cref="AttackPath.AreaEffect"/>.</summary>
        public ProjectileKind Projectile { get; }

        public string Provenance { get; }

        /// <summary>Why the plan is <see cref="AttackPath.Unsupported"/> (empty otherwise).</summary>
        public string Reason { get; }

        public bool IsSupported => Path != AttackPath.Unsupported;

        public static AttackPlan Melee(ActionId action) =>
            new AttackPlan(action, AttackPath.Melee, ProjectileKind.Arrow, "ALPHA_CODE_STATIC (C4 0x445020)");

        public static AttackPlan Refused(string reason) =>
            new AttackPlan(default, AttackPath.Unsupported, ProjectileKind.Arrow, "UNKNOWN", reason);

        public override string ToString() =>
            IsSupported ? $"action {Action.Value} {Path}{(Path == AttackPath.Melee ? string.Empty : " " + Projectile)}" : $"refused: {Reason}";
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
        public const int BraceletAction = 39;
        public const int BombAction = 0x68;

        public static AttackPlan Select(CharacterClass characterClass, ItemInstance mainHand, ItemInstance offHand)
        {
            ActionId? melee = AlphaPrimaryAttack.SelectMelee(characterClass, mainHand, offHand);
            if (melee.HasValue)
            {
                return AttackPlan.Melee(melee.Value);
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
                        return ForAction(WandAction);
                    case WeaponSubtype.Staff:
                        return ForAction(StaffAction);
                    case WeaponSubtype.Bracelet:
                        // 0x445020 read from the decompilation (DeepSeek evidence 08ae4e3 §3): bracelet → 0x27 (spec 0);
                        // not covered by the C4 emulation → PARTIAL.
                        AttackPlan bracelet = ForAction(BraceletAction);
                        return new AttackPlan(bracelet.Action, bracelet.Path, bracelet.Projectile,
                            "PARTIAL: action ALPHA_DECOMPILED (0x445020), release ALPHA_CODE_STATIC (0x617C88 group 0x25..0x2E)");
                }
            }

            string hand = mainHand == null ? "empty main hand" : $"main hand type {(int)mainHand.Type} subtype {mainHand.Subtype}";
            return AttackPlan.Refused($"UNKNOWN Alpha primary attack for class {characterClass} with {hand}");
        }

        /// <summary>
        /// Path of an Alpha action id by the case group of <c>GameController_updateSubsystems</c> that consumes it
        /// (bytes, see M0_F2B report): 0x16/0x17/0x1A push an arrow/boomerang (0x617184), 0x25…0x2E and 0x6C push a magic
        /// projectile (0x617C88), 0x1E…0x21 resolve a forward volume in the melee group (0x6142A0), 0x1C/0x5E/0x5F cast
        /// the 200-block voxel ray (0x615F62) and 0x68 (bomb) uses the fixed range [0x745E74] = 10. Everything else
        /// is the melee group.
        /// </summary>
        public static AttackPlan ForAction(int action)
        {
            var id = new ActionId(action);
            const string release = "ALPHA_CODE_STATIC (0x60C510 action group)";
            switch (action)
            {
                case 0x16:
                case 0x17:
                    return new AttackPlan(id, AttackPath.Projectile, ProjectileKind.Arrow, release);
                case 0x1A:
                    return new AttackPlan(id, AttackPath.Projectile, ProjectileKind.Boomerang, release);
                case 0x1E:
                case 0x1F:
                case 0x20:
                case 0x21:
                    return new AttackPlan(id, AttackPath.AreaEffect, ProjectileKind.StaffBurst,
                        "ALPHA_DECOMPILED path (melee group, forward volume 0x6142A0); volume size/offset CUBEGAME_CONTRACT");
                case 0x1C:
                case 0x5E:
                case 0x5F:
                case BombAction:
                    return new AttackPlan(id, AttackPath.Hitscan, ProjectileKind.Beam, release);
                default:
                    if ((action >= 0x25 && action <= 0x2E) || action == 0x6C)
                    {
                        return new AttackPlan(id, AttackPath.Projectile, ProjectileKind.Magic, release);
                    }

                    return new AttackPlan(id, AttackPath.Melee, ProjectileKind.Arrow, "ALPHA_CODE_STATIC (melee group)");
            }
        }

        private static bool IsWeapon(ItemInstance item, WeaponSubtype subtype) =>
            item != null && item.Type == ItemType.Weapon && item.Subtype == (byte)subtype;
    }
}
