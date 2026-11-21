using System;
using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Combat;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using UnityEngine;

namespace CubeGame.Presentation.Equipment
{
    /// <summary>
    /// Техническая демонстрация S04: экипировка влияет на боевой расчёт.
    ///
    /// Сценарий: персонаж экипирует оружие → показывается рассчитанная характеристика оружия →
    /// удар по тестовой цели → смена оружия → повторный удар по такой же цели с тем же seed →
    /// показывается разница. Отдельно показывается влияние брони цели на её вычисляемую защиту.
    ///
    /// Это НЕ игровая сцена: нет интерфейса инвентаря, drag-and-drop, моделей оружия, анимаций,
    /// AI, сложных хитбоксов и VFX.
    /// </summary>
    public sealed class EquipmentCombatDemo : MonoBehaviour
    {
        [Header("Демонстрационные параметры удара")]
        [SerializeField] private double targetMaxHealth = 1000.0;
        [SerializeField] private double hitChance = 1.0;
        [SerializeField] private double blockFraction;
        [SerializeField] private int strikeSeed = 4242;

        private CharacterSheet _sheet;
        private EquipmentLoadout _loadout;
        private EquipmentCombatAdapter _adapter;
        private GameObject _targetVisual;

        public CharacterSheet Sheet
        {
            get
            {
                EnsureInitialized();
                return _sheet;
            }
        }

        public EquipmentLoadout Loadout
        {
            get
            {
                EnsureInitialized();
                return _loadout;
            }
        }

        public EquipmentCombatAdapter Adapter
        {
            get
            {
                EnsureInitialized();
                return _adapter;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Тестовое лёгкое оружие S04 (подтип 3 — по таблице Alpha это кинжал; до 2026-11-09 Core называл его «Sword»).
        /// Числа и проверки S04 не менялись.
        /// </summary>
        public static ItemInstance CreateSword(int level, int rarity, int seed = 1, ItemModifier modifier = ItemModifier.None)
        {
            return new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Dagger, level, rarity, seed, modifier);
        }

        /// <summary>Тестовое оружие «двуручная булава» — другие подтверждённые характеристики.</summary>
        public static ItemInstance CreateGreatmace(int level, int rarity, int seed = 2, ItemModifier modifier = ItemModifier.None)
        {
            return new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Greatmace, level, rarity, seed, modifier);
        }

        /// <summary>Тестовый нагрудник для цели.</summary>
        public static ItemInstance CreateChest(int level, int rarity, int seed = 3, ItemModifier modifier = ItemModifier.None)
        {
            return new ItemInstance(ItemType.Chest, 0, level, rarity, seed, modifier);
        }

        public void EquipMainHand(ItemInstance item)
        {
            Loadout.Equip(EquipmentSlot.MainHand, item);
        }

        public void UnequipMainHand()
        {
            Loadout.Unequip(EquipmentSlot.MainHand);
        }

        /// <summary>Рассчитанные характеристики надетых предметов (урон, «броня», блок — раздельно).</summary>
        public EquipmentStatSummary ComputeEquippedStats()
        {
            return EquipmentStats.Compute(Loadout);
        }

        /// <summary>Защита цели из её экипировки (Σ BlockValue, E3 `PARTIAL`).</summary>
        public static double ComputeTargetArmor(EquipmentLoadout targetLoadout)
        {
            return EquipmentCombatAdapter.ComputeTargetArmor(targetLoadout);
        }

        /// <summary>
        /// Один удар по свежей тестовой цели с заданным seed.
        /// Одинаковые seed и параметры дают одинаковые броски — разница в уроне объясняется экипировкой.
        /// </summary>
        public CombatOutcome Strike(double targetArmor = 0.0, int seed = -1, double health = -1.0)
        {
            double armor = targetArmor;
            int actualSeed = seed >= 0 ? seed : strikeSeed;
            double targetHealth = health > 0.0 ? health : targetMaxHealth;

            CombatAttack attack = Adapter.BuildAttack(Sheet, Loadout, hitChance, armor, blockFraction);
            var resolver = new DamageResolver(new SeededCombatRandom(actualSeed));
            var target = new HealthPool(targetHealth);

            CombatOutcome outcome = resolver.Resolve(attack, target);
            Debug.Log($"[Equipment] {outcome.ToLogLine()} weaponBonus={attack.ItemWeaponDamageBonus:F2}");
            return outcome;
        }

        /// <summary>
        /// Полный технический сценарий: смена оружия и брони при одинаковом seed.
        /// Возвращает строки отчёта (они же пишутся в лог).
        /// </summary>
        public IReadOnlyList<string> RunScenario()
        {
            EnsureInitialized();

            var lines = new List<string>();
            ItemInstance sword = CreateSword(level: 1, rarity: 0, seed: 11);
            ItemInstance greatmace = CreateGreatmace(level: 1, rarity: 0, seed: 12);

            EquipMainHand(sword);
            EquipmentStatSummary withSword = ComputeEquippedStats();
            CombatOutcome swordHit = Strike(0.0, strikeSeed);
            lines.Add($"weapon=sword {withSword} => {swordHit.ToLogLine()}");

            EquipMainHand(greatmace);
            EquipmentStatSummary withGreatmace = ComputeEquippedStats();
            CombatOutcome greatmaceHit = Strike(0.0, strikeSeed);
            lines.Add($"weapon=greatmace {withGreatmace} => {greatmaceHit.ToLogLine()}");

            UnequipMainHand();
            EquipmentStatSummary unarmed = ComputeEquippedStats();
            CombatOutcome unarmedHit = Strike(0.0, strikeSeed);
            lines.Add($"weapon=none {unarmed} => {unarmedHit.ToLogLine()}");

            var targetLoadout = new EquipmentLoadout();
            double armorBefore = ComputeTargetArmor(targetLoadout);
            targetLoadout.Equip(EquipmentSlot.Chest, CreateChest(level: 10, rarity: 2, seed: 5));
            double armorAfter = ComputeTargetArmor(targetLoadout);
            CombatOutcome armoredHit = Strike(armorAfter, strikeSeed);
            lines.Add($"targetArmor={armorBefore:F2}->{armorAfter:F2} => {armoredHit.ToLogLine()}");

            foreach (string line in lines)
            {
                Debug.Log($"[Equipment] scenario: {line}");
            }

            return lines;
        }

        /// <summary>Простой визуальный маркер цели (куб), без геймплея.</summary>
        public GameObject EnsureTargetVisual()
        {
            if (_targetVisual != null)
            {
                return _targetVisual;
            }

            _targetVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _targetVisual.name = "EquipmentDemoTarget";
            _targetVisual.transform.SetParent(transform, false);
            _targetVisual.transform.localPosition = new Vector3(0f, 1f, 2f);
            _targetVisual.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            return _targetVisual;
        }

        private void EnsureInitialized()
        {
            if (_sheet == null)
            {
                _sheet = new CharacterSheet();
            }

            if (_loadout == null)
            {
                _loadout = new EquipmentLoadout();
            }

            if (_adapter == null)
            {
                _adapter = new EquipmentCombatAdapter();
            }
        }
    }
}
