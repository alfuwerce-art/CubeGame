using System.Collections;
using CubeGame.Core.Combat;
using CubeGame.Presentation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.PlayMode
{
    /// <summary>
    /// PlayMode-тесты минимальной Unity-интеграции боя S03: тестовая цель получает урон,
    /// HP уменьшается на рассчитанную величину, смерть фиксируется, повторный удар игнорируется.
    ///
    /// Регрессия существующей сцены и S01 остаётся в <see cref="PilotPlayModeTests"/>
    /// и запускается в том же прогоне.
    /// </summary>
    public sealed class CombatCorePlayModeTests
    {
        [UnityTest]
        public IEnumerator CombatDemoStrikeReducesTargetHealth()
        {
            var go = new GameObject("CombatDemoTest");
            var demo = go.AddComponent<CombatDemo>();
            demo.Configure(
                newWeaponBase: 1.0,
                newAttackerLevel: 1,
                newHitChance: 1.0,
                newTargetMaxHealth: 100.0,
                newTargetArmor: 0.0,
                newBlockFraction: 0.0,
                newSeed: 5);
            yield return null;

            double before = demo.TargetHealth.CurrentHealth;
            CombatOutcome outcome = demo.Strike();

            Assert.AreEqual(CombatStatus.Hit, outcome.Status, outcome.ToLogLine());
            Assert.Greater(outcome.AppliedDamage, 0.0);
            Assert.AreEqual(before - outcome.AppliedDamage, demo.TargetHealth.CurrentHealth, 1e-9,
                "HP цели должно уменьшиться ровно на рассчитанный урон");
            Assert.IsFalse(outcome.IsDead);
            StringAssert.Contains("status=Hit", demo.LastOutcomeLine);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CombatDemoKillIsRecordedAndSecondStrikeIgnored()
        {
            var go = new GameObject("CombatDemoKillTest");
            var demo = go.AddComponent<CombatDemo>();
            demo.Configure(
                newWeaponBase: 1.0,
                newAttackerLevel: 1,
                newHitChance: 1.0,
                newTargetMaxHealth: 0.5,
                newTargetArmor: 0.0,
                newBlockFraction: 0.0,
                newSeed: 7);
            yield return null;

            CombatOutcome killing = demo.Strike();

            Assert.AreEqual(CombatStatus.Hit, killing.Status, killing.ToLogLine());
            Assert.IsTrue(killing.KilledTarget, "цель должна умереть этим ударом");
            Assert.IsTrue(killing.IsDead);
            Assert.AreEqual(0.0, demo.TargetHealth.CurrentHealth, 1e-9);

            CombatOutcome afterDeath = demo.Strike();

            Assert.AreEqual(CombatStatus.TargetAlreadyDead, afterDeath.Status);
            Assert.AreEqual(0.0, afterDeath.AppliedDamage, 1e-9);
            Assert.AreEqual(0.0, demo.TargetHealth.CurrentHealth, 1e-9, "HP мёртвой цели не меняется");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CombatDemoIsDeterministicForSameSeed()
        {
            CombatOutcome first = null;
            CombatOutcome second = null;
            GameObject firstObject = null;
            GameObject secondObject = null;

            try
            {
                firstObject = new GameObject("CombatDemoSeedA");
                var demoA = firstObject.AddComponent<CombatDemo>();
                demoA.Configure(1.0, 1, 0.6, 1000.0, 5.0, 0.25, 424242);
                yield return null;
                first = demoA.Strike();

                secondObject = new GameObject("CombatDemoSeedB");
                var demoB = secondObject.AddComponent<CombatDemo>();
                demoB.Configure(1.0, 1, 0.6, 1000.0, 5.0, 0.25, 424242);
                yield return null;
                second = demoB.Strike();

                Assert.AreEqual(first.Status, second.Status);
                Assert.AreEqual(first.AppliedDamage, second.AppliedDamage, 1e-12,
                    "одинаковый seed и входные данные дают одинаковый урон");
                Assert.AreEqual(first.HitRoll, second.HitRoll);
            }
            finally
            {
                if (firstObject != null)
                {
                    Object.Destroy(firstObject);
                }

                if (secondObject != null)
                {
                    Object.Destroy(secondObject);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CombatDemoCreatesSimpleTargetVisual()
        {
            var go = new GameObject("CombatDemoVisualTest");
            var demo = go.AddComponent<CombatDemo>();
            yield return null;

            GameObject visual = demo.EnsureTargetVisual();

            Assert.IsNotNull(visual);
            Assert.AreEqual(go.transform, visual.transform.parent);
            Assert.IsNotNull(visual.GetComponent<Renderer>(), "цель должна быть видимым примитивом");

            Object.Destroy(go);
            yield return null;
        }
    }
}
