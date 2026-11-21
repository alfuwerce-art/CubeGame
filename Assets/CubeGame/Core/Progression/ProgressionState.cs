using System;

namespace CubeGame.Core.Progression
{
    /// <summary>
    /// Состояние прогрессии персонажа: уровень и накопленный опыт.
    ///
    /// Модель Alpha (D_character §D4.1, DIRECT): <c>while XP &gt;= cost(L): XP -= cost(L); L += 1</c>.
    /// Верхнего капа уровня в коде Alpha не найдено (D7, UNKNOWN) — здесь он тоже не вводится.
    /// Чистая логика: без Unity, без рендера, без сохранений (это отдельный слой).
    /// </summary>
    public sealed class ProgressionState
    {
        /// <summary>Минимальный уровень (Alpha: уровень начинается с 1).</summary>
        public const int MinLevel = 1;

        /// <summary>
        /// Максимум опыта за один вызов <see cref="AddExperience"/>.
        ///
        /// Это НЕ игровой кап уровня (в Alpha верхнего капа не найдено, D7): ограничение защищает
        /// от переполнения и от зависания на абсурдных входах. Опыт можно начислять повторно.
        /// </summary>
        public const long MaxExperiencePerCall = 1_000_000_000L;

        /// <summary>Защита от бесконечного цикла: не больше миллиона повышений за один вызов.</summary>
        private const int MaxLevelUpsPerCall = 1_000_000;

        /// <summary>Верхняя граница <c>cost(L)</c>: предел кривой 1050, поэтому <c>cost(L) &lt; 1050</c>.</summary>
        private const int ExperienceCostUpperBound = 1050;

        private int _level;
        private long _experience;

        public ProgressionState()
            : this(MinLevel, 0L)
        {
        }

        public ProgressionState(int level, long experience)
        {
            if (level < MinLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Уровень начинается с 1 (Alpha).");
            }

            if (experience < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(experience), experience, "Опыт не может быть отрицательным.");
            }

            _level = level;
            _experience = experience;
        }

        /// <summary>Текущий уровень персонажа.</summary>
        public int Level => _level;

        /// <summary>Накопленный, но ещё не потраченный на уровни опыт.</summary>
        public long Experience => _experience;

        /// <summary>Сколько опыта нужно до следующего уровня (REFERENCE_VERIFIED, Alpha 00444d60).</summary>
        public int ExperienceToNextLevel => AlphaMath.ExperienceCostForNextLevel(_level);

        /// <summary>Множитель уровня <c>levelFactor</c>, общий для персонажа и предметов.</summary>
        public double LevelFactor => AlphaMath.LevelFactor(_level);

        /// <summary>
        /// Добавляет опыт и повышает уровень, пока накопленного опыта хватает.
        /// Возвращает число полученных уровней (0, если уровня не было).
        ///
        /// Все проверки выполняются до изменения состояния, а сами изменения применяются в конце:
        /// при любой ошибке уровень и опыт остаются прежними (никаких частичных начислений).
        /// </summary>
        public int AddExperience(long amount)
        {
            if (amount < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Нельзя отнимать опыт через AddExperience.");
            }

            if (amount == 0L)
            {
                return 0;
            }

            if (amount > MaxExperiencePerCall)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    $"Начисление {amount} превышает предел одного вызова {MaxExperiencePerCall}; состояние не изменено.");
            }

            if (amount > long.MaxValue - _experience)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "Начисление переполнит накопленный опыт (long); состояние не изменено.");
            }

            long totalExperience = _experience + amount;

            // Стоимость уровня всегда меньше 1050, значит повышений будет не меньше
            // totalExperience / 1050. Проверка до цикла исключает зависание на абсурдных данных.
            long minimumLevelUps = totalExperience / ExperienceCostUpperBound;
            if (minimumLevelUps > MaxLevelUpsPerCall)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    $"Ожидается не менее {minimumLevelUps} повышений уровня (предел {MaxLevelUpsPerCall} за вызов); состояние не изменено.");
            }

            int level = _level;
            long experience = totalExperience;
            int gained = 0;

            while (experience >= AlphaMath.ExperienceCostForNextLevel(level))
            {
                if (level == int.MaxValue)
                {
                    throw new InvalidOperationException(
                        "Уровень достиг предельного значения int; состояние прогресса не изменено.");
                }

                experience -= AlphaMath.ExperienceCostForNextLevel(level);
                level++;
                gained++;
            }

            _level = level;
            _experience = experience;
            return gained;
        }

        /// <summary>Восстанавливает состояние из сохранения без пересчёта уровней.</summary>
        public static ProgressionState FromSave(int level, long experience)
        {
            return new ProgressionState(level, experience);
        }

        public override string ToString()
        {
            return $"level={_level} xp={_experience}/{ExperienceToNextLevel}";
        }
    }
}
