using System;
using CubeGame.Core.Combat;
using UnityEngine;

namespace CubeGame.Presentation.Combat
{
    /// <summary>
    /// Минимальная Unity-интеграция боевого ядра S03: техническая демонстрация одного удара.
    ///
    /// Это НЕ прототип боевой анимации: компонент только связывает чистые вычисления
    /// (<see cref="DamageResolver"/> + <see cref="HealthPool"/>) со сценой, даёт одну команду
    /// <see cref="Strike"/> и пишет результат в лог. Никаких Animator, Physics-хитбоксов,
    /// AI, VFX и неподтверждённых скоростей здесь нет.
    /// </summary>
    public sealed class CombatDemo : MonoBehaviour
    {
        [Header("Атакующий (входные данные удара)")]
        [SerializeField] private double weaponBase = 1.0;
        [SerializeField] private int attackerLevel = 1;
        [SerializeField] private double hitChance = 0.75;
        [SerializeField] private int attackId;
        [SerializeField] private bool useAttackIdMultiplier;

        [Header("Цель")]
        [SerializeField] private double targetMaxHealth = 100.0;
        [SerializeField] private double targetArmor = 10.0;
        [SerializeField] private double blockFraction = 0.5;

        [Header("Детерминизм")]
        [SerializeField] private int randomSeed = 12345;

        private HealthPool _targetHealth;
        private DamageResolver _resolver;
        private CombatAttack _attack;
        private GameObject _targetVisual;

        public HealthPool TargetHealth => _targetHealth;

        public CombatAttack Attack => _attack;

        public string LastOutcomeLine { get; private set; } = string.Empty;

        /// <summary>Настраивает демонстрацию явно (используется PlayMode-тестами).</summary>
        public void Configure(
            double newWeaponBase,
            int newAttackerLevel,
            double newHitChance,
            double newTargetMaxHealth,
            double newTargetArmor,
            double newBlockFraction,
            int newSeed,
            int newAttackId = 0,
            bool newUseAttackIdMultiplier = false)
        {
            weaponBase = newWeaponBase;
            attackerLevel = newAttackerLevel;
            hitChance = newHitChance;
            targetMaxHealth = newTargetMaxHealth;
            targetArmor = newTargetArmor;
            blockFraction = newBlockFraction;
            randomSeed = newSeed;
            attackId = newAttackId;
            useAttackIdMultiplier = newUseAttackIdMultiplier;
            Rebuild();
        }

        private void Awake()
        {
            Rebuild();
        }

        /// <summary>Одна команда проведения удара: возвращает воспроизводимый результат и пишет лог.</summary>
        public CombatOutcome Strike()
        {
            if (_resolver == null || _attack == null || _targetHealth == null)
            {
                Rebuild();
            }

            CombatOutcome outcome = _resolver.Resolve(_attack, _targetHealth);
            LastOutcomeLine = outcome.ToLogLine();

            if (outcome.IsDead)
            {
                Debug.LogWarning($"[Combat] {LastOutcomeLine}");
            }
            else
            {
                Debug.Log($"[Combat] {LastOutcomeLine}");
            }

            return outcome;
        }

        /// <summary>Создаёт простой визуальный маркер цели, если его ещё нет (куб, без геймплея).</summary>
        public GameObject EnsureTargetVisual()
        {
            if (_targetVisual != null)
            {
                return _targetVisual;
            }

            _targetVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _targetVisual.name = "CombatDemoTarget";
            _targetVisual.transform.SetParent(transform, false);
            _targetVisual.transform.localPosition = new Vector3(0f, 1f, 2f);
            _targetVisual.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            return _targetVisual;
        }

        private void Rebuild()
        {
            _targetHealth = new HealthPool(targetMaxHealth);
            _attack = new CombatAttack(weaponBase, attackerLevel, hitChance)
            {
                TargetArmor = targetArmor,
                BlockFraction = blockFraction,
                AttackId = attackId,
                UseAttackIdMultiplier = useAttackIdMultiplier,
            };
            _resolver = new DamageResolver(new SeededCombatRandom(randomSeed));
        }
    }
}
