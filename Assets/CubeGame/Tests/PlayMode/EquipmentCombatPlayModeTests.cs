using System.Collections;
using System.Collections.Generic;
using CubeGame.Core.Combat;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Presentation.Equipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.PlayMode
{
    /// <summary>
    /// PlayMode-тесты технической демонстрации S04: экипировка меняет боевой расчёт,
    /// смена оружия обновляет характеристики без пересоздания сцены, броня цели меняет защиту,
    /// смерть цели и HP работают как прежде.
    ///
    /// Регрессия сцены/генерации S01 остаётся в <see cref="PilotPlayModeTests"/>.
    /// </summary>
    public sealed class EquipmentCombatPlayModeTests
    {
        [UnityTest]
        public IEnumerator EquippingWeaponChangesStrikeResult()
        {
            var go = new GameObject("EquipmentDemoTest");
            var demo = go.AddComponent<EquipmentCombatDemo>();
            yield return null;

            demo.EquipMainHand(EquipmentCombatDemo.CreateSword(level: 1, rarity: 0));
            double swordStat = demo.ComputeEquippedStats().MainHandDamage;
            CombatOutcome swordHit = demo.Strike(seed: 4242);

            demo.EquipMainHand(EquipmentCombatDemo.CreateGreatmace(level: 1, rarity: 0));
            double greatmaceStat = demo.ComputeEquippedStats().MainHandDamage;
            CombatOutcome greatmaceHit = demo.Strike(seed: 4242);

            Assert.That(swordStat, Is.EqualTo(2.0).Within(1e-9), "sword L1 r0 → 2.00");
            Assert.That(greatmaceStat, Is.EqualTo(8.0).Within(1e-9), "greatmace L1 r0 → 8.00");
            Assert.AreEqual(swordHit.HitRoll, greatmaceHit.HitRoll, "seed одинаковый → броски одинаковые");
            Assert.Greater(greatmaceHit.RawDamage, swordHit.RawDamage, "оружие с большим статом даёт больший урон");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwappingWeaponUpdatesStatsWithoutSceneRebuild()
        {
            var go = new GameObject("EquipmentDemoSwapTest");
            var demo = go.AddComponent<EquipmentCombatDemo>();
            yield return null;

            demo.EquipMainHand(EquipmentCombatDemo.CreateSword(level: 1, rarity: 0));
            double first = demo.ComputeEquippedStats().WeaponDamageBonus;

            demo.EquipMainHand(EquipmentCombatDemo.CreateSword(level: 10, rarity: 2));
            double second = demo.ComputeEquippedStats().WeaponDamageBonus;

            Assert.AreEqual(1, demo.Loadout.EquippedCount, "замена не добавляет слот");
            Assert.Greater(second, first, "характеристика обновилась без пересоздания сцены");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnequippingWeaponReturnsBaseline()
        {
            var go = new GameObject("EquipmentDemoUnequipTest");
            var demo = go.AddComponent<EquipmentCombatDemo>();
            yield return null;

            demo.EquipMainHand(EquipmentCombatDemo.CreateGreatmace(level: 1, rarity: 0));
            double equippedBonus = demo.ComputeEquippedStats().WeaponDamageBonus;

            demo.UnequipMainHand();
            double emptyBonus = demo.ComputeEquippedStats().WeaponDamageBonus;
            CombatOutcome baselineHit = demo.Strike(seed: 4242);

            Assert.Greater(equippedBonus, 0.0);
            Assert.That(emptyBonus, Is.EqualTo(0.0).Within(1e-12), "снятие возвращает базовое состояние");
            Assert.IsTrue(demo.Loadout.IsSlotEmpty(EquipmentSlot.MainHand), "слот пуст после снятия");
            Assert.That(baselineHit.BaseDamage, Is.EqualTo(1.0).Within(1e-9), "остаётся только база персонажа");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TargetArmorFromEquipmentLowersAppliedDamage()
        {
            var go = new GameObject("EquipmentDemoArmorTest");
            var demo = go.AddComponent<EquipmentCombatDemo>();
            yield return null;
            demo.EquipMainHand(EquipmentCombatDemo.CreateSword(level: 1, rarity: 0));

            var bareTarget = new EquipmentLoadout();
            var armoredTarget = new EquipmentLoadout();
            armoredTarget.Equip(EquipmentSlot.Chest, EquipmentCombatDemo.CreateChest(level: 10, rarity: 2));

            double armorBare = EquipmentCombatDemo.ComputeTargetArmor(bareTarget);
            double armorArmored = EquipmentCombatDemo.ComputeTargetArmor(armoredTarget);

            CombatOutcome bareHit = demo.Strike(targetArmor: armorBare, seed: 4242);
            CombatOutcome armoredHit = demo.Strike(targetArmor: armorArmored, seed: 4242);

            Assert.That(armorBare, Is.EqualTo(0.0).Within(1e-12));
            Assert.Greater(armorArmored, 0.0, "броня даёт вычисляемую защиту");
            Assert.That(armoredHit.RawDamage, Is.EqualTo(bareHit.RawDamage).Within(1e-9));
            Assert.GreaterOrEqual(armoredHit.AbsorbedDamage, bareHit.AbsorbedDamage);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeathStillWorksWithEquipment()
        {
            var go = new GameObject("EquipmentDemoDeathTest");
            var demo = go.AddComponent<EquipmentCombatDemo>();
            yield return null;
            demo.EquipMainHand(EquipmentCombatDemo.CreateGreatmace(level: 1, rarity: 0));

            // Каждый Strike создаёт свежую цель (честное сравнение ударов), поэтому смерть
            // проверяется на одном ударе; повторный удар по мёртвой цели покрыт в S03
            // (CombatDemo) и в EditMode-тестах DamageResolver.
            CombatOutcome killing = demo.Strike(targetArmor: 0.0, seed: 7, health: 0.5);

            Assert.AreEqual(CombatStatus.Hit, killing.Status, killing.ToLogLine());
            Assert.IsTrue(killing.KilledTarget, "цель должна умереть этим ударом");
            Assert.IsTrue(killing.IsDead);
            Assert.That(killing.HealthAfter, Is.EqualTo(0.0).Within(1e-12), "HP не уходит в минус");
            Assert.That(killing.AppliedDamage, Is.LessThanOrEqualTo(0.5 + 1e-12), "снято не больше оставшегося HP");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScenarioReportsWeaponSwapAndArmorEffect()
        {
            var go = new GameObject("EquipmentDemoScenarioTest");
            var demo = go.AddComponent<EquipmentCombatDemo>();
            yield return null;

            IReadOnlyList<string> lines = demo.RunScenario();

            Assert.AreEqual(4, lines.Count, "сценарий: меч, булава, без оружия, броня цели");
            StringAssert.Contains("weapon=sword", lines[0]);
            StringAssert.Contains("weapon=greatmace", lines[1]);
            StringAssert.Contains("weapon=none", lines[2]);
            StringAssert.Contains("targetArmor=", lines[3]);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TargetVisualIsSimplePrimitive()
        {
            var go = new GameObject("EquipmentDemoVisualTest");
            var demo = go.AddComponent<EquipmentCombatDemo>();
            yield return null;

            GameObject visual = demo.EnsureTargetVisual();

            Assert.IsNotNull(visual);
            Assert.IsNotNull(visual.GetComponent<Renderer>(), "цель — видимый примитив");
            Assert.AreEqual(go.transform, visual.transform.parent);

            Object.Destroy(go);
            yield return null;
        }
    }
}
