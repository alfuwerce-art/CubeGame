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
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.character == null)
            {
                throw new ArgumentException("В сохранении отсутствует блок персонажа.", nameof(data));
            }

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
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var items = new List<ItemInstance>(data.items?.Count ?? 0);
            if (data.items == null)
            {
                return items;
            }

            for (int i = 0; i < data.items.Count; i++)
            {
                ItemSaveData item = data.items[i];
                if (item == null)
                {
                    continue;
                }

                items.Add(new ItemInstance(
                    (ItemType)item.type,
                    (byte)item.subtype,
                    item.level,
                    item.rarity,
                    item.seed,
                    (ItemModifier)item.modifier,
                    item.powerBonus,
                    item.stackCount));
            }

            return items;
        }
    }
}
