using System;
using CubeGame.Core.Characters;
using CubeGame.Core.Combat;
using CubeGame.Core.Items;

namespace CubeGame.Core.Equipment
{
    /// <summary>
    /// Изолированный адаптер: <c>CharacterSheet + надетые предметы → CombatAttack</c>.
    ///
    /// <para>
    /// Формула адаптера (явно, `PARTIAL`):
    /// <c>WeaponBase = CharacterSheet.WeaponBase (base 0x180)</c>,
    /// <c>ItemWeaponDamageBonus = Σ ItemStatsCalculator.WeaponDamage(надетое оружие)</c>,
    /// итог в бою: <c>(WeaponBase · levelFactor(уровень персонажа) + флаги) + ItemWeaponDamageBonus</c>.
    /// </para>
    /// <para>
    /// Почему так: E_combat §E2 описывает <c>base(0x180)·levelFactor + флаги + список item_weaponDamage</c>,
    /// то есть вклад предмета аддитивен и **не** масштабируется уровнем персонажа повторно.
    /// <c>ItemStatsCalculator.WeaponDamage</c> уже содержит множитель уровня предмета и редкость,
    /// поэтому передавать его как <c>WeaponBase</c> запрещено (двойное масштабирование).
    /// Полный порядок применения в Alpha не доказан — статус `PARTIAL`.
    /// </para>
    /// <para>
    /// Защита цели: <c>TargetArmor = Σ ItemStatsCalculator.BlockValue</c> по слотам брони
    /// (E_combat §E3: слоты брони добавляют <c>item_computeBlockValue</c>). <c>ItemStatsCalculator.Armor</c>
    /// (<c>item_computeStat_armor</c>) — отдельная величина и здесь не используется.
    /// </para>
    /// <para>
    /// Экспериментальные механики выключены по умолчанию: модификатор шанса по подтипу оружия и
    /// множители ID атак включаются только явной настройкой. <c>HitChance</c> (OQ-01 `UNKNOWN`) и
    /// <c>BlockFraction</c> (OQ-24 `PARTIAL`) по-прежнему передаёт вызывающий код.
    /// </para>
    /// </summary>
    public sealed class EquipmentCombatAdapter
    {
        public EquipmentCombatAdapter(bool enableWeaponSubtypeHitModifier = false)
        {
            EnableWeaponSubtypeHitModifier = enableWeaponSubtypeHitModifier;
        }

        /// <summary>Включать ли частичный модификатор шанса по подтипу оружия (по умолчанию <c>false</c>).</summary>
        public bool EnableWeaponSubtypeHitModifier { get; }

        /// <summary>
        /// Собирает вход боя из листа персонажа и экипировки.
        /// <c>hitChance</c>, <c>targetArmor</c> и <c>blockFraction</c> задаются вызывающим кодом.
        /// </summary>
        public CombatAttack BuildAttack(
            CharacterSheet sheet,
            EquipmentLoadout loadout,
            double hitChance,
            double targetArmor,
            double blockFraction)
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            EquipmentStatSummary summary = EquipmentStats.Compute(loadout);

            var attack = new CombatAttack(sheet.WeaponBase, sheet.Progression.Level, hitChance)
            {
                Upgrades = sheet.WeaponUpgrades,
                ItemWeaponDamageBonus = summary.WeaponDamageBonus,
                TargetArmor = targetArmor,
                BlockFraction = blockFraction,

                // Частичные механики не включаются автоматически (D42/D44/D45).
                UseAttackIdMultiplier = false,
            };

            if (EnableWeaponSubtypeHitModifier)
            {
                ItemInstance mainHand = loadout.GetEquipped(EquipmentSlot.MainHand);
                if (mainHand != null)
                {
                    attack.EquippedItemType = (int)mainHand.Type;
                    attack.WeaponSubtype = mainHand.Subtype;
                }
            }

            return attack;
        }

        /// <summary>
        /// Защита цели из её экипировки: <c>Σ item_computeBlockValue</c> (E3, `PARTIAL`).
        /// Значение передаётся в <c>CombatAttack.TargetArmor</c> только явно.
        /// </summary>
        public static double ComputeTargetArmor(EquipmentLoadout targetLoadout)
        {
            if (targetLoadout == null)
            {
                throw new ArgumentNullException(nameof(targetLoadout));
            }

            return EquipmentStats.Compute(targetLoadout).TotalBlockValue;
        }

        /// <summary>
        /// Максимум HP по существующей формуле S02.
        ///
        /// OQ-25 `OPEN`: источник бонуса `+8 HP` из экипировки в сохранении Alpha не установлен,
        /// поэтому предметы **не** добавляют HP автоматически — используется только явный
        /// <see cref="CharacterSheet.EquipmentHealthBonus"/>. Смена экипировки в этой партии
        /// может не менять MaxHP — это ожидаемо и зафиксировано в S04_REPORT.
        /// </summary>
        public static double ComputeMaxHealth(CharacterSheet sheet)
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            return CharacterStats.CalcMaxHealth(
                sheet.BaseHealth,
                sheet.Progression.Level,
                sheet.Class,
                sheet.Specialization,
                sheet.EquipmentHealthBonus);
        }
    }
}
