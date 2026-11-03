using System;

namespace CubeGame.Core.Characters
{
    /// <summary>
    /// Классы персонажа (Alpha: 4 класса, D_character §D1, DIRECT).
    /// Значение соответствует 0-based индексу класса: в структуре существа Alpha пишется
    /// <c>+0x140 = classIdx + 1</c>, поэтому проверка формулы HP <c>class == 1</c> относится
    /// именно к <see cref="Warrior"/> — см. <see cref="CharacterStats.SpecializationHealthMultiplier"/>.
    /// </summary>
    public enum CharacterClass
    {
        /// <summary>Alpha classIdx 0. Множитель HP 1.3.</summary>
        Warrior = 0,

        /// <summary>Alpha classIdx 1. Множитель HP 1.1.</summary>
        Ranger = 1,

        /// <summary>Alpha classIdx 2. Множитель HP 1.0.</summary>
        Mage = 2,

        /// <summary>Alpha classIdx 3. Множитель HP 1.2.</summary>
        Rogue = 3,
    }

    /// <summary>
    /// Специализация внутри класса (Alpha: 0 — первая, 1 — вторая; D_character §D5.1, DIRECT).
    /// Для Warrior: 0 = Berserker, 1 = Guardian (проверено в игре: spec=0 → Berserker).
    /// </summary>
    public enum CharacterSpecialization
    {
        /// <summary>Первая специализация класса (Alpha spec = 0).</summary>
        Primary = 0,

        /// <summary>Вторая специализация класса (Alpha spec = 1).</summary>
        Secondary = 1,
    }

    /// <summary>Помощники преобразования идентификаторов класса для сохранений.</summary>
    public static class CharacterClasses
    {
        /// <summary>Число классов в Alpha (Warrior, Ranger, Mage, Rogue).</summary>
        public const int Count = 4;

        public static CharacterClass FromId(int classId)
        {
            if (classId < 0 || classId >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(classId), classId, "Класс должен быть в диапазоне 0..3.");
            }

            return (CharacterClass)classId;
        }

        public static CharacterSpecialization SpecializationFromId(int specializationId)
        {
            if (specializationId != 0 && specializationId != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(specializationId), specializationId, "Специализация 0 или 1.");
            }

            return (CharacterSpecialization)specializationId;
        }

        public static int IdOf(CharacterClass characterClass)
        {
            return (int)characterClass;
        }

        public static int IdOf(CharacterSpecialization specialization)
        {
            return (int)specialization;
        }
    }
}
