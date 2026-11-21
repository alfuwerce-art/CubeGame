using System;
using System.Globalization;

namespace CubeGame.Core.Combat
{
    /// <summary>Итог обработки одного удара.</summary>
    public enum CombatStatus
    {
        /// <summary>Попадание: урон рассчитан и применён.</summary>
        Hit = 0,

        /// <summary>Промах: HP цели не изменилось.</summary>
        Miss = 1,

        /// <summary>Цель уже мертва: удар не обрабатывается, HP не меняется.</summary>
        TargetAlreadyDead = 2,
    }

    /// <summary>
    /// Воспроизводимый результат удара с полной диагностикой:
    /// попадание/промах, входящий урон, применённая защита, итоговый урон, HP до/после, смерть.
    ///
    /// Объект неизменяемый: создаётся резолвером и не даёт частично изменять результат.
    /// </summary>
    public sealed class CombatOutcome
    {
        private CombatOutcome(
            CombatStatus status,
            double effectiveHitChance,
            int hitRoll,
            double baseDamage,
            int varianceRoll,
            double varianceMultiplier,
            bool attackIdMultiplierApplied,
            int attackId,
            double attackIdMultiplier,
            double rawDamage,
            double armor,
            double blockFraction,
            double defenseCapacity,
            double absorbedDamage,
            double appliedDamage,
            double healthBefore,
            double healthAfter)
        {
            Status = status;
            EffectiveHitChance = effectiveHitChance;
            HitRoll = hitRoll;
            BaseDamage = baseDamage;
            VarianceRoll = varianceRoll;
            VarianceMultiplier = varianceMultiplier;
            AttackIdMultiplierApplied = attackIdMultiplierApplied;
            AttackId = attackId;
            AttackIdMultiplier = attackIdMultiplier;
            RawDamage = rawDamage;
            Armor = armor;
            BlockFraction = blockFraction;
            DefenseCapacity = defenseCapacity;
            AbsorbedDamage = absorbedDamage;
            AppliedDamage = appliedDamage;
            HealthBefore = healthBefore;
            HealthAfter = healthAfter;
        }

        public CombatStatus Status { get; }

        /// <summary>Эффективный шанс попадания (после модификатора подтипа оружия).</summary>
        public double EffectiveHitChance { get; }

        /// <summary>Израсходованный бросок попадания 0…32767 (-1, если бросок не делался).</summary>
        public int HitRoll { get; }

        /// <summary>Урон оружия до разброса и множителей.</summary>
        public double BaseDamage { get; }

        /// <summary>Бросок разброса (-1, если удар не попал).</summary>
        public int VarianceRoll { get; }

        /// <summary>Множитель разброса 0.75…1.25 (1.0, если удар не попал).</summary>
        public double VarianceMultiplier { get; }

        /// <summary>Был ли применён множитель по ID атаки (частичная механика, включается явно).</summary>
        public bool AttackIdMultiplierApplied { get; }

        public int AttackId { get; }

        public double AttackIdMultiplier { get; }

        /// <summary>Урон до защиты (после разброса и, если включён, множителя ID атаки).</summary>
        public double RawDamage { get; }

        public double Armor { get; }

        public double BlockFraction { get; }

        /// <summary>
        /// Расчётная защита <c>blockFraction · armor</c> — сколько урона защита могла бы поглотить.
        /// Диагностическая величина: может быть больше входящего удара (см. <see cref="AbsorbedDamage"/>).
        /// </summary>
        public double DefenseCapacity { get; }

        /// <summary>
        /// Фактически поглощено бронёй/блоком: <c>min(RawDamage, DefenseCapacity)</c>.
        ///
        /// Инвариант относится к расчёту защиты, а не к HP:
        /// <c>MitigationResult.AbsorbedDamage + MitigationResult.FinalDamage = RawDamage</c>.
        /// Здесь <c>AbsorbedDamage + AppliedDamage</c> может быть меньше <c>RawDamage</c>, потому что
        /// <see cref="AppliedDamage"/> ограничен оставшимся HP цели (overkill обрезается клампом),
        /// например raw = 5 при HP = 1 → absorbed 0, final 5, applied 1.
        /// </summary>
        public double AbsorbedDamage { get; }

        /// <summary>Фактически снятые HP (0 при промахе или мёртвой цели).</summary>
        public double AppliedDamage { get; }

        public double HealthBefore { get; }

        public double HealthAfter { get; }

        public bool IsHit => Status == CombatStatus.Hit;

        public bool IsDead => HealthAfter <= 0.0;

        /// <summary>Смерть наступила именно этим ударом.</summary>
        public bool KilledTarget => IsHit && HealthBefore > 0.0 && HealthAfter <= 0.0;

        public static CombatOutcome Miss(double effectiveChance, int hitRoll, double healthBefore)
        {
            return new CombatOutcome(
                CombatStatus.Miss, effectiveChance, hitRoll, 0.0, -1, 1.0,
                false, 0, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, healthBefore, healthBefore);
        }

        public static CombatOutcome TargetAlreadyDead(double healthBefore)
        {
            return new CombatOutcome(
                CombatStatus.TargetAlreadyDead, 0.0, -1, 0.0, -1, 1.0,
                false, 0, 1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, healthBefore, healthBefore);
        }

        public static CombatOutcome Hit(
            double effectiveChance,
            int hitRoll,
            double baseDamage,
            int varianceRoll,
            double varianceMultiplier,
            bool attackIdMultiplierApplied,
            int attackId,
            double attackIdMultiplier,
            double rawDamage,
            double armor,
            double blockFraction,
            double defenseCapacity,
            double absorbedDamage,
            double appliedDamage,
            double healthBefore,
            double healthAfter)
        {
            return new CombatOutcome(
                CombatStatus.Hit, effectiveChance, hitRoll, baseDamage, varianceRoll, varianceMultiplier,
                attackIdMultiplierApplied, attackId, attackIdMultiplier, rawDamage, armor, blockFraction,
                defenseCapacity, absorbedDamage, appliedDamage, healthBefore, healthAfter);
        }

        /// <summary>Однострочное описание для лога и тестов.</summary>
        public string ToLogLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "status={0} chance={1:F3} hitRoll={2} base={3:F2} variance={4:F3} raw={5:F2} " +
                "armor={6:F2} blockFraction={7:F3} capacity={8:F2} absorbed={9:F2} applied={10:F2} " +
                "hp={11:F2}->{12:F2} dead={13}",
                Status, EffectiveHitChance, HitRoll, BaseDamage, VarianceMultiplier, RawDamage,
                Armor, BlockFraction, DefenseCapacity, AbsorbedDamage, AppliedDamage,
                HealthBefore, HealthAfter, IsDead);
        }
    }
}
