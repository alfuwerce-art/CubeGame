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

        /// <summary>Защита от бесконечного цикла при аномальных входных данных.</summary>
        private const int MaxLevelUpsPerCall = 1_000_000;

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

            _experience += amount;
            int gained = 0;

            while (_experience >= AlphaMath.ExperienceCostForNextLevel(_level))
            {
                _experience -= AlphaMath.ExperienceCostForNextLevel(_level);
                _level++;
                gained++;

                if (gained >= MaxLevelUpsPerCall)
                {
                    throw new InvalidOperationException("Слишком много повышений уровня за один вызов — проверьте входные данные.");
                }
            }

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
