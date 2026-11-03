using System;
using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Core.Persistence;
using CubeGame.Persistence;
using UnityEngine;

namespace CubeGame.Presentation.Character
{
    /// <summary>
    /// Минимальное Unity-представление базового состояния персонажа (S02).
    ///
    /// Задача компонента — связать чистую модель (<see cref="CharacterSheet"/>), предметы и
    /// сохранение с игровой сценой, не превращаясь в боевую систему. Здесь нет урона по цели,
    /// попаданий, блока и способностей: только данные, характеристики и сохранение/загрузка.
    /// </summary>
    public sealed class CharacterSheetPresenter : MonoBehaviour
    {
        [SerializeField] private CharacterClass characterClass = CharacterClass.Warrior;
        [SerializeField] private CharacterSpecialization specialization = CharacterSpecialization.Primary;
        [SerializeField] private double baseHealth = CharacterStats.DefaultBaseHealth;

        private readonly List<ItemInstance> _items = new List<ItemInstance>();

        private CharacterSheet _sheet;
        private SaveService _saveService;
        private ISaveStore _store;

        public CharacterSheet Sheet
        {
            get
            {
                EnsureSheet();
                return _sheet;
            }
        }

        public IReadOnlyList<ItemInstance> Items => _items;

        public int Level => Sheet.Progression.Level;

        public long Experience => Sheet.Progression.Experience;

        public double MaxHealth => Sheet.MaxHealth;

        public double WeaponDamage => Sheet.WeaponDamage;

        /// <summary>Каталог файловых сохранений или строка-заглушка, если хранилище не файловое.</summary>
        public string SaveDirectory => (_store as FileSaveStore)?.DirectoryPath ?? "<non-file store>";

        private void Awake()
        {
            EnsureSheet();
        }

        /// <summary>Настраивает хранилище явно (PlayMode-тесты, будущее меню сохранений).</summary>
        public void Configure(ISaveStore store, ISaveSerializer serializer, string key = SaveService.DefaultKey)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _saveService = new SaveService(store, serializer, key);
        }

        /// <summary>Хранилище по умолчанию: <c>Application.persistentDataPath</c> + <c>JsonUtility</c>.</summary>
        public void ConfigureDefaultStorage(string key = SaveService.DefaultKey)
        {
            Configure(new FileSaveStore(), new JsonSaveSerializer(), key);
        }

        public int AddExperience(long amount)
        {
            int gained = Sheet.AddExperience(amount);
            Debug.Log($"[CubeGame] level up x{gained}: level={Level} xp={Experience} " +
                      $"hp={MaxHealth:F1} weapon={WeaponDamage:F2}");
            return gained;
        }

        public void AddItem(ItemInstance item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            _items.Add(item);
        }

        /// <summary>Собирает данные сохранения из текущего состояния.</summary>
        public SaveGame ToSaveGame()
        {
            return CharacterSaveMapper.ToSaveGame(Sheet, _items);
        }

        /// <summary>Применяет данные сохранения к компоненту (персонаж и предметы).</summary>
        public void ApplySaveGame(SaveGame data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // Сначала полностью восстанавливаем модель в локальных переменных. Если преобразование
            // не удалось, состояние компонента остаётся прежним (REVIEW-FIX-01, п.7): иначе при
            // ошибке на предметах персонаж уже был бы подменён новыми данными.
            CharacterSheet restoredSheet = CharacterSaveMapper.ToCharacterSheet(data);
            List<ItemInstance> restoredItems = CharacterSaveMapper.ToItems(data);

            _sheet = restoredSheet;
            _items.Clear();
            _items.AddRange(restoredItems);
        }

        public SaveResult SaveToDisk()
        {
            EnsureSaveService();
            SaveResult result = _saveService.Save(ToSaveGame());
            LogResult("save", result.IsSuccess, result.ToString());
            return result;
        }

        public LoadResult LoadFromDisk()
        {
            EnsureSaveService();
            LoadResult result = _saveService.Load();
            if (result.IsSuccess)
            {
                ApplySaveGame(result.Data);
            }

            LogResult("load", result.IsSuccess, result.ToString());
            return result;
        }

        private void EnsureSaveService()
        {
            if (_saveService == null)
            {
                ConfigureDefaultStorage();
            }
        }

        private void EnsureSheet()
        {
            if (_sheet == null)
            {
                double health = baseHealth <= 0d ? CharacterStats.DefaultBaseHealth : baseHealth;
                _sheet = new CharacterSheet(characterClass, specialization, health);
            }
        }

        private static void LogResult(string operation, bool success, string details)
        {
            if (success)
            {
                Debug.Log($"[CubeGame] {operation} ok: {details}");
            }
            else
            {
                Debug.LogWarning($"[CubeGame] {operation} failed: {details}");
            }
        }
    }
}
