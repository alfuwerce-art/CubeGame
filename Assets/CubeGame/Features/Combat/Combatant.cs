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

        public HealthPool Health { get; }

        /// <summary>Game-space position (blocks). Updated by the owner of movement every frame.</summary>
        public WorldPosition Position { get; set; }

        /// <summary>Facing in degrees (Unity convention: 0 = +Z, 90 = +X).</summary>
        public float Yaw { get; set; }

        /// <summary>Hostile combatants can be hit by the hero; the hero can be hit by hostile ones.</summary>
        public bool Hostile { get; set; }

        public bool IsDead => Health.IsDead;

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

        public override string ToString() => $"{Name} {Id} hp {Health.CurrentHealth:F1}/{Health.MaxHealth:F1}";
    }
}
