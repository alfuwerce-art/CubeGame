using System;
using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;

namespace CubeGame.Core.Persistence
{
    /// <summary>
    /// Преобразование между чистой игровой моделью (лист персонажа, предметы) и плоскими
    /// структурами сохранения. Отдельный слой, чтобы модель не знала о формате файла.
    /// </summary>
    public static class CharacterSaveMapper
    {
        public static SaveGame ToSaveGame(CharacterSheet sheet, IEnumerable<ItemInstance> items = null)
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            var save = new SaveGame
            {
                formatVersion = SaveFormat.CurrentVersion,
                character = new CharacterSaveData
                {
                    level = sheet.Progression.Level,
                    experience = sheet.Progression.Experience,
                    classId = CharacterClasses.IdOf(sheet.Class),
                    specializationId = CharacterClasses.IdOf(sheet.Specialization),
                    baseHealth = (float)sheet.BaseHealth,
                    weaponBase = (float)sheet.WeaponBase,
                    weaponUpgrades = (int)sheet.WeaponUpgrades,
                    equipmentHealthBonus = (float)sheet.EquipmentHealthBonus,
                },
            };

            if (items != null)
            {
                foreach (ItemInstance item in items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    save.items.Add(new ItemSaveData
                    {
                        type = (int)item.Type,
                        subtype = item.Subtype,
                        level = item.Level,
                        rarity = item.Rarity,
                        seed = item.Seed,
                        modifier = (int)item.Modifier,
                        powerBonus = item.PowerBonus,
                        stackCount = item.StackCount,
                    });
                }
            }
            else
            {
                save.items = new List<ItemSaveData>();
            }

            return save;
        }

        public static CharacterSheet ToCharacterSheet(SaveGame data)
        {
            RequireValid(data, nameof(data));

            CharacterSaveData character = data.character;
            var sheet = new CharacterSheet(
                CharacterClasses.FromId(character.classId),
                CharacterClasses.SpecializationFromId(character.specializationId),
                character.baseHealth,
                character.weaponBase)
            {
                WeaponUpgrades = (WeaponUpgradeFlags)character.weaponUpgrades,
                EquipmentHealthBonus = character.equipmentHealthBonus,
            };

            sheet.RestoreProgression(character.level, character.experience);
            return sheet;
        }

        public static List<ItemInstance> ToItems(SaveGame data)
        {
            RequireValid(data, nameof(data));

            var items = new List<ItemInstance>(data.items.Count);

            for (int i = 0; i < data.items.Count; i++)
            {
                ItemSaveData item = data.items[i];
                if (item == null)
                {
                    // Валидатор не пропускает null-элементы; здесь защита от прямого вызова API.
                    throw new InvalidOperationException($"Предмет #{i} отсутствует (null).");
                }

                // Проверенные данные не должны терять точность при приведении int → byte;
                // если это всё же произошло, лучше явная ошибка, чем молча испорченный предмет.
                if (!ItemInstance.TryCreateFromIdentifiers(
                        item.type,
                        item.subtype,
                        item.level,
                        item.rarity,
                        item.seed,
                        item.modifier,
                        item.powerBonus,
                        item.stackCount,
                        out ItemInstance created,
                        out string itemError))
                {
                    throw new InvalidOperationException($"Предмет #{i} не может быть восстановлен: {itemError}.");
                }

                items.Add(created);
            }

            return items;
        }

        /// <summary>
        /// Маппер отказывается работать с непроверенными данными: это гарантирует, что успешная
        /// загрузка (<see cref="LoadStatus.Success"/>) всегда безопасно преобразуется в модель.
        /// </summary>
        private static void RequireValid(SaveGame data, string parameterName)
        {
            if (data == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!SaveValidator.TryValidate(data, out string error))
            {
                throw new ArgumentException($"Данные сохранения не прошли проверку: {error}", parameterName);
            }
        }
    }
}
