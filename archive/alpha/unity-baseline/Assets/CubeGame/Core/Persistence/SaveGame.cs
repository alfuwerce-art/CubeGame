using System;
using System.Collections.Generic;
using CubeGame.Core.Items;
using CubeGame.Core.Progression;

namespace CubeGame.Core.Persistence
{
    /// <summary>
    /// Версия формата сохранения CubeGame. Это НЕ формат Alpha v11: у нас своя версия и своя
    /// схема (D42/ADR-0001). Версия увеличивается при любом несовместимом изменении полей.
    /// </summary>
    public static class SaveFormat
    {
        /// <summary>Текущая версия формата, которую пишет и читает CubeGame.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Заголовок файла сохранения (первая строка).</summary>
        public const string Header = "CUBEGAME_SAVE";

        /// <summary>Максимальное число предметов в сохранении первой партии (защита от мусора).</summary>
        public const int MaxItems = 512;
    }

    /// <summary>Состояние персонажа в сохранении (плоские поля — формат стабилен и переносим).</summary>
    [Serializable]
    public sealed class CharacterSaveData
    {
        public int level = 1;
        public long experience;
        public int classId;
        public int specializationId;
        public float baseHealth = 100f;
        public float weaponBase = 1f;
        public int weaponUpgrades;
        public float equipmentHealthBonus;
    }

    /// <summary>Предмет в сохранении: ровно те поля, которые составляют идентичность предмета.</summary>
    [Serializable]
    public sealed class ItemSaveData
    {
        public int type;
        public int subtype;
        public int level = 1;
        public int rarity;
        public int seed;
        public int modifier;
        public int powerBonus;
        public int stackCount = 1;
    }

    /// <summary>
    /// Корневой объект сохранения первой партии S02: версия формата, персонаж, список предметов.
    /// </summary>
    [Serializable]
    public sealed class SaveGame
    {
        public int formatVersion = SaveFormat.CurrentVersion;
        public CharacterSaveData character = new CharacterSaveData();
        public List<ItemSaveData> items = new List<ItemSaveData>();
    }

    /// <summary>
    /// Проверка целостности данных сохранения до записи и после чтения.
    ///
    /// Правило: любой файл, прошедший <see cref="SaveService.Load"/>, должен безопасно
    /// восстанавливаться через <see cref="CharacterSaveMapper"/> — поэтому здесь проверяются
    /// не только числа персонажа, но и полный набор идентификаторов предметов
    /// (тип, подтип, модификатор), флаги оружия и конечность дробных величин.
    ///
    /// Границы разделены на два вида:
    /// 1) достоверные правила Alpha (D42): редкость 0…4, уровень с 1, подтип только у оружия;
    /// 2) ограничения текущей модели/формата CubeGame: известный набор типов и модификаторов,
    ///    подтип оружия 1…0x14 (имена вне таблицы Alpha не восстановлены, OQ-09), маска флагов
    ///    <c>0x7e</c> только 0x8|0x10, не более <see cref="SaveFormat.MaxItems"/> предметов.
    /// </summary>
    public static class SaveValidator
    {
        /// <summary>Разрешённые биты флагов апгрейдов оружия (Alpha <c>0x7e &amp; 0x8</c> / <c>0x7e &amp; 0x10</c>).</summary>
        public const int SupportedWeaponUpgradeMask = 0x8 | 0x10;

        public static bool TryValidate(SaveGame data, out string error)
        {
            try
            {
                return TryValidateCore(data, out error);
            }
            catch (Exception exception)
            {
                // Валидатор обязан вернуть статус, а не исключение: повреждённый файл должен давать
                // понятную причину, а не необработанное исключение в загрузке.
                error = $"Внутренняя ошибка проверки сохранения: {exception.Message}";
                return false;
            }
        }

        private static bool TryValidateCore(SaveGame data, out string error)
        {
            if (data == null)
            {
                error = "Данные сохранения отсутствуют (null).";
                return false;
            }

            if (data.formatVersion != SaveFormat.CurrentVersion)
            {
                error = $"Неожидаемая версия формата в данных: {data.formatVersion}, ожидается {SaveFormat.CurrentVersion}.";
                return false;
            }

            if (data.character == null)
            {
                error = "Отсутствует блок персонажа.";
                return false;
            }

            CharacterSaveData character = data.character;
            if (character.level < 1)
            {
                error = $"Некорректный уровень персонажа: {character.level}.";
                return false;
            }

            if (character.experience < 0)
            {
                error = $"Отрицательный опыт персонажа: {character.experience}.";
                return false;
            }

            // Инвариант модели прогрессии: накопленный опыт всегда меньше стоимости следующего
            // уровня, иначе уровень был бы повышен. Нарушение означает противоречивое состояние.
            int nextLevelCost = AlphaMath.ExperienceCostForNextLevel(character.level);
            if (character.experience >= nextLevelCost)
            {
                error = $"Противоречивое состояние: опыт {character.experience} не меньше стоимости уровня {nextLevelCost}.";
                return false;
            }

            if (character.classId < 0 || character.classId >= 4)
            {
                error = $"Некорректный id класса: {character.classId}.";
                return false;
            }

            if (character.specializationId != 0 && character.specializationId != 1)
            {
                error = $"Некорректный id специализации: {character.specializationId}.";
                return false;
            }

            if (!IsFinitePositive(character.baseHealth))
            {
                error = $"Некорректная база HP: {character.baseHealth} (ожидается конечное положительное число).";
                return false;
            }

            if (!IsFinitePositive(character.weaponBase))
            {
                error = $"Некорректная база урона: {character.weaponBase} (ожидается конечное положительное число).";
                return false;
            }

            if (!IsFiniteNonNegative(character.equipmentHealthBonus))
            {
                error = $"Некорректный бонус экипировки: {character.equipmentHealthBonus} (ожидается конечное неотрицательное число).";
                return false;
            }

            if (character.weaponUpgrades < 0
                || (character.weaponUpgrades & ~SupportedWeaponUpgradeMask) != 0)
            {
                error = $"Некорректные флаги оружия: 0x{character.weaponUpgrades:X} (разрешены только биты 0x8 и 0x10).";
                return false;
            }

            if (data.items == null)
            {
                error = "Отсутствует список предметов.";
                return false;
            }

            if (data.items.Count > SaveFormat.MaxItems)
            {
                error = $"Слишком много предметов: {data.items.Count} (максимум {SaveFormat.MaxItems}).";
                return false;
            }

            for (int i = 0; i < data.items.Count; i++)
            {
                ItemSaveData item = data.items[i];
                if (item == null)
                {
                    error = $"Предмет #{i} отсутствует (null).";
                    return false;
                }

                if (!ItemInstance.TryCreateFromIdentifiers(
                        item.type,
                        item.subtype,
                        item.level,
                        item.rarity,
                        item.seed,
                        item.modifier,
                        item.powerBonus,
                        item.stackCount,
                        out _,
                        out string itemError))
                {
                    error = $"Предмет #{i}: {itemError}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
