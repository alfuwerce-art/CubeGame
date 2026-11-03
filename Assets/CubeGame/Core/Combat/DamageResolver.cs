using System;

namespace CubeGame.Core.Combat
{
    /// <summary>
    /// Связывает модули боя в один процесс:
    /// <c>AttackInput → HitCheck → DamageCalculation → Mitigation → HealthPool → CombatOutcome</c>.
    ///
    /// Порядок и потребление бросков зафиксированы, чтобы результат был воспроизводимым:
    /// <list type="number">
    /// <item>мёртвая цель — броски не тратятся, HP не меняется;</item>
    /// <item>проверка попадания — один бросок;</item>
    /// <item>промах — второй бросок не тратится;</item>
    /// <item>попадание — второй бросок на разброс, затем расчёт урона и защита;</item>
    /// <item>HP меняется последним, после всех проверок и расчётов.</item>
    /// </list>
    ///
    /// Что остаётся <c>PARTIAL</c>: множитель по числовому ID атаки (только по явному флагу),
    /// модификатор шанса по подтипу оружия, порядок митигации. Что остаётся <c>UNKNOWN</c>:
    /// базовая формула шанса попадания (OQ-01) и автоматическая доля блока (OQ-24).
    /// </summary>
    public sealed class DamageResolver
    {
        private readonly ICombatRandom _random;

        public DamageResolver(ICombatRandom random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public CombatOutcome Resolve(CombatAttack attack, HealthPool target)
        {
            if (attack == null)
            {
                throw new ArgumentNullException(nameof(attack));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            double healthBefore = target.CurrentHealth;

            if (target.IsDead)
            {
                return CombatOutcome.TargetAlreadyDead(healthBefore);
            }

            double chance = CombatFormulas.ApplyWeaponSubtypeHitModifier(
                attack.HitChance,
                attack.EquippedItemType,
                attack.WeaponSubtype);

            int hitRoll = _random.NextRoll();
            if (!HitResolver.IsHit(chance, hitRoll))
            {
                return CombatOutcome.Miss(chance, hitRoll, healthBefore);
            }

            int varianceRoll = _random.NextRoll();
            double variance = CombatFormulas.DamageVariance(varianceRoll);
            double baseDamage = CombatFormulas.WeaponDamage(attack.WeaponBase, attack.Level, attack.Upgrades, attack.Stars)
                                + attack.ItemWeaponDamageBonus;
            double rawDamage = baseDamage * variance;

            // PARTIAL: множитель применяется только если механизм разрешён вызывающим кодом
            // (UseAttackIdMultiplier) И для конкретного ID найден в таблице. Неизвестный ID
            // не меняет урон и не выдаётся за поддерживаемую атаку (REVIEW-FIX-01).
            bool attackIdApplied = false;
            double attackIdMultiplier = 1.0;
            if (attack.UseAttackIdMultiplier
                && CombatFormulas.TryGetAttackIdMultiplier(attack.AttackId, out double foundMultiplier))
            {
                attackIdApplied = true;
                attackIdMultiplier = foundMultiplier;
                rawDamage *= foundMultiplier;
            }

            // Защита считается до изменения HP: исключение здесь не оставляет половинчатого состояния.
            MitigationResult mitigation = DamageMitigation.Apply(rawDamage, attack.TargetArmor, attack.BlockFraction);

            double applied = target.ApplyDamage(mitigation.FinalDamage);

            return CombatOutcome.Hit(
                chance,
                hitRoll,
                baseDamage,
                varianceRoll,
                variance,
                attackIdApplied,
                attack.AttackId,
                attackIdMultiplier,
                rawDamage,
                mitigation.Armor,
                mitigation.BlockFraction,
                mitigation.DefenseCapacity,
                mitigation.AbsorbedDamage,
                applied,
                healthBefore,
                target.CurrentHealth);
        }
    }
}
