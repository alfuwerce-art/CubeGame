using System;
using CubeGame.Core.Characters;
using CubeGame.Core.Combat;
using CubeGame.Core.Equipment;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Combat
{
    /// <summary>
    /// One participant of combat. Rules state lives in Core objects (<see cref="CharacterSheet"/>,
    /// <see cref="EquipmentLoadout"/>, <see cref="HealthPool"/>); this class only binds them to an entity id and
    /// a world position. The hero's combatant shares the very same Core objects as <c>HeroState</c>.
    /// </summary>
    public sealed class Combatant
    {
        public Combatant(StableEntityId id, KindId kind, string name, CharacterSheet sheet, EquipmentLoadout loadout, HealthPool health)
        {
            if (id.IsNone)
            {
                throw new ArgumentException("combatant needs an entity id", nameof(id));
            }

            Id = id;
            Kind = kind;
            Name = string.IsNullOrWhiteSpace(name) ? id.ToString() : name;
            Sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            Health = health ?? throw new ArgumentNullException(nameof(health));
        }

        public StableEntityId Id { get; }

        public KindId Kind { get; }

        public string Name { get; }

        public CharacterSheet Sheet { get; }

        public EquipmentLoadout Loadout { get; }

        public HealthPool Health { get; internal set; }

        /// <summary>Game-space position (blocks). Updated by the owner of movement every frame.</summary>
        public WorldPosition Position { get; set; }

        /// <summary>Facing in degrees (Unity convention: 0 = +Z, 90 = +X).</summary>
        public float Yaw { get; set; }

        /// <summary>Hostile combatants can be hit by the hero; the hero can be hit by hostile ones.</summary>
        public bool Hostile { get; set; }

        /// <summary>Alpha star count of a creature (+0x1A8); 0 for the hero.</summary>
        public int Stars { get; set; }

        /// <summary>Alpha boss species (creature_isBossType): kill experience ×20 (C3).</summary>
        public bool IsBoss { get; set; }

        public bool IsDead => Health.IsDead;

        // 0x447700 reads the non-player star term. Item haste, buffs and +0x17C updates remain partial.
        internal float TimingRate => Id.Equals(StableEntityId.Hero) ? 1f : 0.75f + Stars * 0.0625f;

        public int? AlphaMainHandSubtype { get; set; }

        /// <summary>
        /// Hit box in blocks (Unity axes: width X, height Y, depth Z). The game sets the Alpha species size
        /// (<c>creature_generateAppearance</c> appearance +0x08, emulated — <c>AlphaCreatureSizes</c>); the default is the
        /// HumanMale 0x02 size 0.96 × 2.16 × 0.96.
        /// </summary>
        public double HitWidth { get; set; } = 0.96;

        public double HitHeight { get; set; } = 2.16;

        public double HitDepth { get; set; } = 0.96;

        /// <summary>Aim direction (Alpha creature +0x160); unset means "horizontal along <see cref="Yaw"/>".</summary>
        public bool HasAim { get; private set; }

        private double _aimX, _aimY, _aimZ;

        public void SetAim(double x, double y, double z)
        {
            double length = Math.Sqrt(x * x + y * y + z * z);
            if (length < 1e-9)
            {
                HasAim = false;
                return;
            }

            _aimX = x / length;
            _aimY = y / length;
            _aimZ = z / length;
            HasAim = true;
        }

        public void ClearAim() => HasAim = false;

        /// <summary>Unit aim vector: the set aim, else the horizontal facing of <see cref="Yaw"/>.</summary>
        public void GetAim(out double x, out double y, out double z)
        {
            if (HasAim)
            {
                x = _aimX;
                y = _aimY;
                z = _aimZ;
                return;
            }

            double yaw = Yaw * Math.PI / 180.0;
            x = Math.Sin(yaw);
            y = 0.0;
            z = Math.Cos(yaw);
        }

        /// <summary>Off-hand subtype 0x0C (bracelet): Alpha doubles the wand projectile speed (0x617C88).</summary>
        public bool BraceletOffHand
        {
            get
            {
                CubeGame.Core.Items.ItemInstance off = Loadout.GetEquipped(EquipmentSlot.OffHand);
                return off != null && off.Type == CubeGame.Core.Items.ItemType.Weapon
                    && off.Subtype == (byte)CubeGame.Core.Items.WeaponSubtype.Bracelet;
            }
        }

        /// <summary>Creates a combatant with a fresh Core sheet at the given level (used for training targets and spawned enemies).</summary>
        public static Combatant Create(StableEntityId id, KindId kind, string name, CharacterClass characterClass, int level)
        {
            var sheet = new CharacterSheet(characterClass);
            if (level > 1)
            {
                sheet.RestoreProgression(level, 0);
            }

            return new Combatant(id, kind, name, sheet, new EquipmentLoadout(), new HealthPool(EquipmentCombatAdapter.ComputeMaxHealth(sheet)));
        }

        /// <summary>
        /// Non-player creature: max HP by the Alpha creature rule (Core <see cref="CharacterStats.CalcCreatureMaxHealth"/>:
        /// base · levelFactor · 2^(0.25·stars), no class). Attack inputs use a default Core sheet at that level —
        /// the creature weapon base is PLACEHOLDER_NOT_ALPHA until M0-06B.
        /// </summary>
        public static Combatant CreateCreature(StableEntityId id, KindId kind, string name, int level, int stars)
        {
            var sheet = new CharacterSheet();
            if (level > 1)
            {
                sheet.RestoreProgression(level, 0);
            }

            double maxHealth = CharacterStats.CalcCreatureMaxHealth(CharacterStats.DefaultBaseHealth, Math.Max(1, level), Math.Max(0, stars));
            return new Combatant(id, kind, name, sheet, new EquipmentLoadout(), new HealthPool(maxHealth)) { Hostile = true };
        }

        public override string ToString() => $"{Name} {Id} hp {Health.CurrentHealth:F1}/{Health.MaxHealth:F1}";
    }
}
