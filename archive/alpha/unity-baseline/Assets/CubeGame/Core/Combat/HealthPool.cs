using System;

namespace CubeGame.Core.Combat
{
    /// <summary>
    /// Пул здоровья бойца: текущее и максимальное HP, получение урона, лечение, смерть.
    ///
    /// Правила (собственная модель CubeGame по D42, подтверждённая часть Alpha):
    /// <list type="bullet">
    /// <item>HP не уходит в отрицательные значения и не превышает максимум (Alpha: кламп в 0 при смерти, кламп сверху при лечении);</item>
    /// <item>HP = 0 означает смерть; повторный урон по мёртвой цели HP не меняет;</item>
    /// <item>некорректные числа (<c>NaN</c>, <c>Infinity</c>, отрицательная величина) отвергаются;</item>
    /// <item>смерть с анимациями, дропом, опытом и респавном — вне этой партии.</item>
    /// </list>
    /// Чистая логика: без UnityEngine.
    /// </summary>
    public sealed class HealthPool
    {
        public HealthPool(double maxHealth)
        {
            if (double.IsNaN(maxHealth) || double.IsInfinity(maxHealth) || maxHealth <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "Максимум HP должен быть конечным положительным числом.");
            }

            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
        }

        public double MaxHealth { get; }

        public double CurrentHealth { get; private set; }

        public bool IsDead => CurrentHealth <= 0.0;

        /// <summary>
        /// Применяет урон. Возвращает фактически снятое количество HP (0 для мёртвой цели).
        /// </summary>
        public double ApplyDamage(double amount)
        {
            ValidateAmount(amount, nameof(amount));

            if (IsDead)
            {
                return 0.0;
            }

            double applied = Math.Min(amount, CurrentHealth);
            CurrentHealth -= applied;

            if (CurrentHealth < 0.0)
            {
                CurrentHealth = 0.0;
            }

            return applied;
        }

        /// <summary>
        /// Лечит, не превышая максимум. Возвращает фактически восстановленное HP (0 для мёртвой цели).
        /// </summary>
        public double Heal(double amount)
        {
            ValidateAmount(amount, nameof(amount));

            if (IsDead)
            {
                return 0.0;
            }

            double missing = MaxHealth - CurrentHealth;
            double healed = Math.Min(amount, missing);
            CurrentHealth += healed;
            return healed;
        }

        public override string ToString()
        {
            return $"{CurrentHealth:F2}/{MaxHealth:F2}{(IsDead ? " (dead)" : string.Empty)}";
        }

        private static void ValidateAmount(double amount, string parameterName)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, amount, "Величина должна быть конечной и неотрицательной.");
            }
        }
    }
}
