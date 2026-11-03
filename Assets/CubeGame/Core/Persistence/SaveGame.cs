using System;
using System.Collections.Generic;

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
    /// Ошибки формулируются словами, чтобы запись в лог была диагностируемой.
    /// </summary>
    public static class SaveValidator
    {
        public static bool TryValidate(SaveGame data, out string error)
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

            if (character.baseHealth <= 0f)
            {
                error = $"Некорректная база HP: {character.baseHealth}.";
                return false;
            }

            if (character.weaponBase <= 0f)
            {
                error = $"Некорректная база урона: {character.weaponBase}.";
                return false;
            }

            if (character.equipmentHealthBonus < 0f)
            {
                error = $"Отрицательный бонус экипировки: {character.equipmentHealthBonus}.";
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

                if (item.level < 1)
                {
                    error = $"Предмет #{i}: некорректный уровень {item.level}.";
                    return false;
                }

                if (item.rarity < 0 || item.rarity > 4)
                {
                    error = $"Предмет #{i}: редкость {item.rarity} вне диапазона 0…4.";
                    return false;
                }

                if (item.stackCount < 1)
                {
                    error = $"Предмет #{i}: размер стека {item.stackCount} меньше 1.";
                    return false;
                }

                if (item.powerBonus < 0)
                {
                    error = $"Предмет #{i}: отрицательный powerBonus {item.powerBonus}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
