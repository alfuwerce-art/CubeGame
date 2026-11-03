using System;
using System.Collections.Generic;
using CubeGame.Core.Items;

namespace CubeGame.Core.Equipment
{
    /// <summary>Вычисленные характеристики одного надетого предмета.</summary>
    public readonly struct ItemComputedStats
    {
        public ItemComputedStats(ItemInstance item, EquipmentSlot slot, double weaponDamage, double armor, double blockValue)
        {
            Item = item;
            Slot = slot;
            WeaponDamage = weaponDamage;
            Armor = armor;
            BlockValue = blockValue;
        }

        public ItemInstance Item { get; }

        public EquipmentSlot Slot { get; }

        /// <summary>Урон оружия по <c>ItemStatsCalculator.WeaponDamage</c> (0 для не-оружия).</summary>
        public double WeaponDamage { get; }

        /// <summary><c>item_computeStat_armor</c> (F2): «броня» предмета как отдельная величина.</summary>
        public double Armor { get; }

        /// <summary><c>item_computeBlockValue</c> (F2/E3): вклад слота брони, который Alpha добавляет к броне существа.</summary>
        public double BlockValue { get; }

        public override string ToString()
        {
            return $"{Slot}:{Item.Type} L{Item.Level} r{Item.Rarity} dmg={WeaponDamage:F2} armor={Armor:F2} block={BlockValue:F2}";
        }
    }

    /// <summary>
    /// Суммарные характеристики экипировки.
    ///
    /// Величины намеренно **не смешаны**: урон оружия, «броня» предмета (<c>ItemStatsCalculator.Armor</c>)
    /// и <c>BlockValue</c> — разные поля. Связь с боевым расчётом задаётся адаптером
    /// (<see cref="EquipmentCombatAdapter"/>), а не этим резюме.
    /// </summary>
    public sealed class EquipmentStatSummary
    {
        public EquipmentStatSummary(
            IReadOnlyList<ItemComputedStats> perItem,
            double mainHandDamage,
            double offHandDamage,
            double weaponDamageBonus,
            double totalArmor,
            double totalBlockValue)
        {
            if (perItem == null)
            {
                throw new ArgumentNullException(nameof(perItem));
            }

            // Защитная копия: изменение исходной коллекции после создания резюме не должно менять
            // результат агрегирования, а запись через приведение к IList бросает NotSupportedException
            // (REVIEW-FIX-01).
            var copy = new List<ItemComputedStats>(perItem.Count);
            for (int i = 0; i < perItem.Count; i++)
            {
                copy.Add(perItem[i]);
            }

            PerItem = copy.AsReadOnly();
            MainHandDamage = mainHandDamage;
            OffHandDamage = offHandDamage;
            WeaponDamageBonus = weaponDamageBonus;
            TotalArmor = totalArmor;
            TotalBlockValue = totalBlockValue;
        }

        /// <summary>Характеристики каждого надетого предмета (неизменяемый снимок).</summary>
        public IReadOnlyList<ItemComputedStats> PerItem { get; }

        public double MainHandDamage { get; }

        public double OffHandDamage { get; }

        /// <summary>Σ урона оружия — аддитивный вклад в боевой расчёт (E2, <c>PARTIAL</c>).</summary>
        public double WeaponDamageBonus { get; }

        /// <summary>Σ <c>item_computeStat_armor</c> — отдельная величина, не подставляется автоматически.</summary>
        public double TotalArmor { get; }

        /// <summary>Σ <c>item_computeBlockValue</c> — вклад брони в защиту существа по E3.</summary>
        public double TotalBlockValue { get; }

        public override string ToString()
        {
            return $"items={PerItem.Count} main={MainHandDamage:F2} off={OffHandDamage:F2} " +
                   $"weaponBonus={WeaponDamageBonus:F2} armorStat={TotalArmor:F2} block={TotalBlockValue:F2}";
        }
    }

    /// <summary>
    /// Агрегатор характеристик надетых предметов. Никаких собственных формул: только вызовы
    /// `ItemStatsCalculator` (S02) и раскладка результата по отдельным полям.
    /// </summary>
    public static class EquipmentStats
    {
        public static ItemComputedStats ComputeItem(EquipmentSlot slot, ItemInstance item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            double weaponDamage = item.Type == ItemType.Weapon
                ? ItemStatsCalculator.WeaponDamage(item.Level, item.Rarity, (WeaponSubtype)item.Subtype, item.PowerBonus)
                : 0.0;

            double armor = ItemStatsCalculator.Armor(
                item.Level, item.Rarity, item.Type, item.Modifier, item.Seed, item.PowerBonus);

            double blockValue = ItemStatsCalculator.BlockValue(
                item.Level, item.Rarity, item.Type, item.Modifier, item.PowerBonus);

            return new ItemComputedStats(item, slot, weaponDamage, armor, blockValue);
        }

        public static EquipmentStatSummary Compute(EquipmentLoadout loadout)
        {
            if (loadout == null)
            {
                throw new ArgumentNullException(nameof(loadout));
            }

            var perItem = new List<ItemComputedStats>(loadout.EquippedCount);
            double mainHandDamage = 0.0;
            double offHandDamage = 0.0;
            double weaponBonus = 0.0;
            double totalArmor = 0.0;
            double totalBlock = 0.0;

            foreach (EquipmentSlot slot in EquipmentLoadout.Slots)
            {
                ItemInstance item = loadout.GetEquipped(slot);
                if (item == null)
                {
                    continue;
                }

                ItemComputedStats stats = ComputeItem(slot, item);
                perItem.Add(stats);
                totalArmor += stats.Armor;
                totalBlock += stats.BlockValue;

                if (item.Type == ItemType.Weapon)
                {
                    weaponBonus += stats.WeaponDamage;

                    if (slot == EquipmentSlot.MainHand)
                    {
                        mainHandDamage = stats.WeaponDamage;
                    }
                    else if (slot == EquipmentSlot.OffHand)
                    {
                        offHandDamage = stats.WeaponDamage;
                    }
                }
            }

            return new EquipmentStatSummary(perItem, mainHandDamage, offHandDamage, weaponBonus, totalArmor, totalBlock);
        }
    }
}
