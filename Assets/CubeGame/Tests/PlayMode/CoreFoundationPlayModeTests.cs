using System;
using System.Collections;
using System.IO;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Core.Persistence;
using CubeGame.Persistence;
using CubeGame.Presentation.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.PlayMode
{
    /// <summary>
    /// PlayMode smoke-тесты S02: чистая модель действительно работает внутри запущенной сцены Unity,
    /// а сохранение переживает создание нового компонента (аналог перезапуска).
    ///
    /// Регрессия существующего пилота (сцена, чанки, персонаж на земле) остаётся в
    /// <see cref="PilotPlayModeTests"/> и запускается в том же прогоне.
    /// </summary>
    public sealed class CoreFoundationPlayModeTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Application.persistentDataPath, "CubeGameTests", Guid.NewGuid().ToString("N"));
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
                    // Временный каталог не влияет на результат.
                }
            }
        }

        [UnityTest]
        public IEnumerator PresenterAppliesAlphaHealthAndProgression()
        {
            var go = new GameObject("CharacterSheetTest");
            var presenter = go.AddComponent<CharacterSheetPresenter>();
            presenter.Configure(new FileSaveStore(_directory), new JsonSaveSerializer(), "playmode");
            yield return null;

            Assert.AreEqual(1, presenter.Level);
            Assert.That(presenter.MaxHealth, Is.EqualTo(260.0).Within(1e-4),
                "Warrior L1 без экипировки: 100 · 2.0 · 1.3 = 260");
            Assert.That(presenter.WeaponDamage, Is.EqualTo(1.0).Within(1e-9));

            int gained = presenter.AddExperience(467);

            Assert.AreEqual(4, gained);
            Assert.AreEqual(5, presenter.Level);
            Assert.That(presenter.MaxHealth, Is.EqualTo(100.0 * 1.4142135623730951 * 2.0 * 1.3).Within(1e-4),
                "levelFactor(5) = 1.414214");

            UnityEngine.Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PresenterSaveAndLoadSurviveNewComponent()
        {
            var first = new GameObject("CharacterSheetFirst");
            var presenter = first.AddComponent<CharacterSheetPresenter>();
            presenter.Configure(new FileSaveStore(_directory), new JsonSaveSerializer(), "playmode");
            yield return null;

            presenter.AddExperience(467);
            presenter.AddItem(new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 777, modifier: ItemModifier.M01));
            double savedHealth = presenter.MaxHealth;

            SaveResult save = presenter.SaveToDisk();
            Assert.IsTrue(save.IsSuccess, $"save: {save}");

            UnityEngine.Object.Destroy(first);
            yield return null;

            var second = new GameObject("CharacterSheetSecond");
            var restored = second.AddComponent<CharacterSheetPresenter>();
            restored.Configure(new FileSaveStore(_directory), new JsonSaveSerializer(), "playmode");
            yield return null;

            LoadResult load = restored.LoadFromDisk();
            Assert.IsTrue(load.IsSuccess, $"load: {load}");

            Assert.AreEqual(5, restored.Level);
            Assert.AreEqual(1, restored.Items.Count);
            Assert.That(restored.MaxHealth, Is.EqualTo(savedHealth).Within(1e-3));

            UnityEngine.Object.Destroy(second);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CorruptedSaveDoesNotBreakPresenter()
        {
            var store = new FileSaveStore(_directory);
            store.Write("playmode", "повреждённый файл");

            var go = new GameObject("CharacterSheetCorrupted");
            var presenter = go.AddComponent<CharacterSheetPresenter>();
            presenter.Configure(store, new JsonSaveSerializer(), "playmode");
            yield return null;

            LoadResult load = presenter.LoadFromDisk();

            Assert.AreEqual(LoadStatus.Corrupted, load.Status, load.ToString());
            Assert.AreEqual(1, presenter.Level, "после неудачной загрузки персонаж остаётся в исходном состоянии");

            UnityEngine.Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidSaveDataLeavesPresenterUnchanged()
        {
            // Файл разбирается как JSON, но содержит недопустимый тип предмета (REVIEW-FIX-01):
            // загрузка обязана вернуть Corrupted и не тронуть текущее состояние компонента.
            var data = new SaveGame
            {
                formatVersion = SaveFormat.CurrentVersion,
                character = new CharacterSaveData
                {
                    level = 9,
                    experience = 0,
                    classId = 3,
                    specializationId = 1,
                    baseHealth = 100f,
                    weaponBase = 1f,
                },
                items = new System.Collections.Generic.List<ItemSaveData>
                {
                    new ItemSaveData
                    {
                        type = 999,
                        subtype = 0,
                        level = 10,
                        rarity = 1,
                        seed = 1,
                        modifier = 0,
                        powerBonus = 0,
                        stackCount = 1,
                    },
                },
            };

            string payload = new JsonSaveSerializer().Serialize(data);
            new FileSaveStore(_directory).Write(
                "playmode",
                SaveService.Frame(SaveFormat.CurrentVersion, payload));

            var go = new GameObject("CharacterSheetInvalidSave");
            var presenter = go.AddComponent<CharacterSheetPresenter>();
            presenter.Configure(new FileSaveStore(_directory), new JsonSaveSerializer(), "playmode");
            yield return null;

            presenter.AddExperience(49);
            int levelBefore = presenter.Level;
            long experienceBefore = presenter.Experience;

            LoadResult load = presenter.LoadFromDisk();

            Assert.AreEqual(LoadStatus.Corrupted, load.Status, load.ToString());
            Assert.AreEqual(levelBefore, presenter.Level, "уровень не должен меняться при неудачной загрузке");
            Assert.AreEqual(experienceBefore, presenter.Experience, "опыт не должен меняться");
            Assert.AreEqual(0, presenter.Items.Count, "предметы не должны появляться из некорректного файла");

            UnityEngine.Object.Destroy(go);
            yield return null;
        }
    }
}
