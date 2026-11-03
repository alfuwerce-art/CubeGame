using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Core.Persistence;
using CubeGame.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// Тесты сохранения S02: round-trip, «перезапуск», повреждённые данные, версии.
    ///
    /// Используется реальное хранилище Unity (<see cref="FileSaveStore"/> в
    /// <c>Application.persistentDataPath</c>) и реальный сериализатор (<c>JsonUtility</c>),
    /// поэтому проверяется фактический путь записи, а не заглушка.
    /// Каждый тест пишет в отдельный временный подкаталог и удаляет его за собой.
    /// </summary>
    public sealed class PersistenceTests
    {
        private string _directory;
        private SaveService _service;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Application.persistentDataPath, "CubeGameTests", Guid.NewGuid().ToString("N"));
            _service = new SaveService(new FileSaveStore(_directory), new JsonSaveSerializer());
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_directory) && Directory.Exists(_directory))
            {
                try
                {
                    Directory.Delete(_directory, true);
                }
                catch (IOException)
                {
                    // Временный каталог не критичен для результата теста.
                }
            }
        }

        [Test]
        public void SaveWritesIntoPersistentDataPath()
        {
            var store = new FileSaveStore();
            StringAssert.StartsWith(Application.persistentDataPath, store.DirectoryPath,
                "штатное хранилище должно находиться в Application.persistentDataPath");
        }

        [Test]
        public void SaveAndLoadRoundTripPreservesState()
        {
            SaveGame original = CreateSampleSave();

            SaveResult save = _service.Save(original);
            Assert.IsTrue(save.IsSuccess, $"save должен пройти: {save}");

            LoadResult load = _service.Load();
            Assert.IsTrue(load.IsSuccess, $"load должен пройти: {load}");

            SaveGame restored = load.Data;
            Assert.AreEqual(SaveFormat.CurrentVersion, restored.formatVersion);
            Assert.AreEqual(original.character.level, restored.character.level);
            Assert.AreEqual(original.character.experience, restored.character.experience);
            Assert.AreEqual(original.character.classId, restored.character.classId);
            Assert.AreEqual(original.character.specializationId, restored.character.specializationId);
            Assert.AreEqual(original.character.baseHealth, restored.character.baseHealth, 1e-4);
            Assert.AreEqual(original.character.weaponBase, restored.character.weaponBase, 1e-4);
            Assert.AreEqual(original.character.weaponUpgrades, restored.character.weaponUpgrades);
            Assert.AreEqual(original.character.equipmentHealthBonus, restored.character.equipmentHealthBonus, 1e-4);

            Assert.AreEqual(original.items.Count, restored.items.Count);
            for (int i = 0; i < original.items.Count; i++)
            {
                Assert.AreEqual(original.items[i].type, restored.items[i].type, $"items[{i}].type");
                Assert.AreEqual(original.items[i].subtype, restored.items[i].subtype, $"items[{i}].subtype");
                Assert.AreEqual(original.items[i].level, restored.items[i].level, $"items[{i}].level");
                Assert.AreEqual(original.items[i].rarity, restored.items[i].rarity, $"items[{i}].rarity");
                Assert.AreEqual(original.items[i].seed, restored.items[i].seed, $"items[{i}].seed");
                Assert.AreEqual(original.items[i].modifier, restored.items[i].modifier, $"items[{i}].modifier");
                Assert.AreEqual(original.items[i].powerBonus, restored.items[i].powerBonus, $"items[{i}].powerBonus");
                Assert.AreEqual(original.items[i].stackCount, restored.items[i].stackCount, $"items[{i}].stackCount");
            }
        }

        [Test]
        public void SaveSurvivesRestartWithNewServiceInstance()
        {
            Assert.IsTrue(_service.Save(CreateSampleSave()).IsSuccess);

            // Имитация перезапуска: новые объекты хранилища, сериализатора и сервиса.
            var restarted = new SaveService(new FileSaveStore(_directory), new JsonSaveSerializer());
            LoadResult load = restarted.Load();

            Assert.IsTrue(load.IsSuccess, $"после перезапуска сохранение должно читаться: {load}");
            Assert.AreEqual(5, load.Data.character.level);
            Assert.AreEqual(123, load.Data.character.experience);
            Assert.AreEqual(2, load.Data.items.Count);
        }

        [Test]
        public void CharacterSheetRoundTripKeepsComputedStats()
        {
            var sheet = new CharacterSheet(CharacterClass.Rogue, CharacterSpecialization.Secondary, 100.0, 1.0)
            {
                EquipmentHealthBonus = 8.0,
            };
            sheet.AddExperience(467);
            double expectedHealth = sheet.MaxHealth;

            SaveGame save = CharacterSaveMapper.ToSaveGame(sheet, new[]
            {
                new ItemInstance(ItemType.Boots, 0, 10, 2, seed: 42, modifier: ItemModifier.M1A),
            });

            Assert.IsTrue(_service.Save(save).IsSuccess);
            LoadResult load = _service.Load();
            Assert.IsTrue(load.IsSuccess);

            CharacterSheet restored = CharacterSaveMapper.ToCharacterSheet(load.Data);
            List<ItemInstance> items = CharacterSaveMapper.ToItems(load.Data);

            Assert.AreEqual(sheet.Class, restored.Class);
            Assert.AreEqual(sheet.Specialization, restored.Specialization);
            Assert.AreEqual(sheet.Progression.Level, restored.Progression.Level);
            Assert.AreEqual(sheet.Progression.Experience, restored.Progression.Experience);
            Assert.AreEqual(expectedHealth, restored.MaxHealth, 1e-4);
            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(ItemType.Boots, items[0].Type);
            Assert.AreEqual(42, items[0].Seed);
        }

        [Test]
        public void MissingSaveReportsNotFound()
        {
            LoadResult load = _service.Load();
            Assert.AreEqual(LoadStatus.NotFound, load.Status, load.ToString());
            Assert.IsFalse(_service.Exists());
        }

        [Test]
        public void CorruptedTextIsReportedWithoutException()
        {
            var store = new FileSaveStore(_directory);
            store.Write(SaveService.DefaultKey, "это не сохранение CubeGame");

            LoadResult load = _service.Load();

            Assert.AreEqual(LoadStatus.Corrupted, load.Status, load.ToString());
            Assert.IsNull(load.Data);
        }

        [Test]
        public void TruncatedSaveIsDetected()
        {
            Assert.IsTrue(_service.Save(CreateSampleSave()).IsSuccess);
            var store = new FileSaveStore(_directory);
            string valid = store.Read(SaveService.DefaultKey);

            store.Write(SaveService.DefaultKey, valid.Substring(0, valid.Length - 20));

            LoadResult load = _service.Load();
            Assert.AreEqual(LoadStatus.Corrupted, load.Status, "обрезанный файл должен отвергаться");
        }

        [Test]
        public void TamperedPayloadIsDetectedByChecksum()
        {
            SaveGame save = CreateSampleSave();
            Assert.IsTrue(_service.Save(save).IsSuccess);

            var store = new FileSaveStore(_directory);
            string valid = store.Read(SaveService.DefaultKey);
            string tampered = valid.Replace("\"level\":5", "\"level\":9");

            Assert.AreNotEqual(valid, tampered, "подмена должна изменить файл");
            store.Write(SaveService.DefaultKey, tampered);

            LoadResult load = _service.Load();
            Assert.AreEqual(LoadStatus.Corrupted, load.Status, "изменение полезной нагрузки ломает контрольную сумму");
        }

        [Test]
        public void NewerFormatVersionIsRejected()
        {
            string payload = new JsonSaveSerializer().Serialize(CreateSampleSave());
            string framed = SaveService.Frame(SaveFormat.CurrentVersion + 1, payload);
            new FileSaveStore(_directory).Write(SaveService.DefaultKey, framed);

            LoadResult load = _service.Load();

            Assert.AreEqual(LoadStatus.UnsupportedVersion, load.Status, "сохранение из будущей версии не должно читаться молча");
            Assert.IsNull(load.Data);
        }

        [Test]
        public void InvalidDataIsRejectedBeforeWriting()
        {
            SaveGame valid = CreateSampleSave();
            Assert.IsTrue(_service.Save(valid).IsSuccess);

            SaveGame invalid = CreateSampleSave();
            invalid.character.level = 0;

            SaveResult result = _service.Save(invalid);
            Assert.AreEqual(SaveStatus.ValidationFailed, result.Status, result.ToString());

            LoadResult load = _service.Load();
            Assert.IsTrue(load.IsSuccess, "неудачная проверка не должна портить прежнее сохранение");
            Assert.AreEqual(5, load.Data.character.level);
        }

        [Test]
        public void ValidatorRejectsOutOfRangeItems()
        {
            SaveGame data = CreateSampleSave();
            data.items[0].rarity = 7;
            Assert.IsFalse(SaveValidator.TryValidate(data, out string rarityError));
            StringAssert.Contains("редкость", rarityError);

            data = CreateSampleSave();
            data.items[0].stackCount = 0;
            Assert.IsFalse(SaveValidator.TryValidate(data, out string stackError));
            StringAssert.Contains("стека", stackError);

            data = CreateSampleSave();
            data.character.specializationId = 2;
            Assert.IsFalse(SaveValidator.TryValidate(data, out string specError));
            StringAssert.Contains("специализации", specError);
        }

        [Test]
        public void FrameRejectsPayloadChanges()
        {
            string framed = SaveService.Frame(SaveFormat.CurrentVersion, "{\"a\":1}");

            Assert.IsTrue(SaveService.TryUnframe(framed, out int version, out string payload, out string error),
                $"корректный кадр должен читаться: {error}");
            Assert.AreEqual(SaveFormat.CurrentVersion, version);
            Assert.AreEqual("{\"a\":1}", payload);

            Assert.IsFalse(SaveService.TryUnframe(framed + " ", out _, out _, out string lengthError),
                "изменение длины полезной нагрузки должно отвергаться");
            StringAssert.Contains("Длина", lengthError);
        }

        [Test]
        public void DeleteRemovesSave()
        {
            Assert.IsTrue(_service.Save(CreateSampleSave()).IsSuccess);
            Assert.IsTrue(_service.Exists());

            Assert.IsTrue(_service.Delete().IsSuccess);
            Assert.IsFalse(_service.Exists());
            Assert.AreEqual(LoadStatus.NotFound, _service.Load().Status);
        }

        private static SaveGame CreateSampleSave()
        {
            return new SaveGame
            {
                formatVersion = SaveFormat.CurrentVersion,
                character = new CharacterSaveData
                {
                    level = 5,
                    experience = 123,
                    classId = 0,
                    specializationId = 0,
                    baseHealth = 100f,
                    weaponBase = 1f,
                    weaponUpgrades = 0,
                    equipmentHealthBonus = 8f,
                },
                items = new List<ItemSaveData>
                {
                    new ItemSaveData
                    {
                        type = (int)ItemType.Chest,
                        subtype = 0,
                        level = 10,
                        rarity = 2,
                        seed = 777,
                        modifier = (int)ItemModifier.M01,
                        powerBonus = 5,
                        stackCount = 1,
                    },
                    new ItemSaveData
                    {
                        type = (int)ItemType.Weapon,
                        subtype = (int)WeaponSubtype.Sword,
                        level = 1,
                        rarity = 0,
                        seed = 12345,
                        modifier = 0,
                        powerBonus = 0,
                        stackCount = 1,
                    },
                },
            };
        }
    }
}
