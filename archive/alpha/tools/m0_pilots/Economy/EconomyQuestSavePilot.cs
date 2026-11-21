using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Core.Persistence;
using CubeGame.M0Pilots.Combat;
using CubeGame.M0Pilots.Generated;
using CubeGame.M0Pilots.Items;

namespace CubeGame.M0Pilots.Economy
{
    public sealed class Wallet
    {
        public int Money { get; private set; }

        public void Add(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative.");
            }

            Money = checked(Money + amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative.");
            }

            if (Money < amount)
            {
                return false;
            }

            Money -= amount;
            return true;
        }
    }

    public static class EconomyRules
    {
        /// <summary>Alpha money changes only through selling items (J2/J7), at item_valueByType.</summary>
        public static int SellPrice(ItemInstance item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return ItemStatsCalculator.Value(item.Level, item.Rarity, item.Type, item.PowerBonus);
        }

        /// <summary>Buy/sell margins, vendor assortment and restock were not recovered (F-U5).</summary>
        public static bool TryGetBuyPrice(ItemInstance item, out int price, out string reason)
        {
            price = 0;
            reason = "F-U5: buy/sell margins and vendor rules are UNKNOWN";
            return false;
        }

        public static bool VendorMarkupProven = false;
    }

    public sealed class QuestObjective
    {
        public int Type;
        public int State; // 0 sleeping, 1 active, 2 completed
        public int Progress;
        public int Required;

        public QuestObjective(int type, int required)
        {
            if (type < 1 || type > 13)
            {
                throw new ArgumentOutOfRangeException(nameof(type), "Quest type must be 1..13.");
            }

            if (required < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(required), "Required progress cannot be negative.");
            }

            Type = type;
            Required = required;
        }

        public bool Activate()
        {
            if (State != 0)
            {
                return false;
            }

            State = 1;
            return true;
        }

        /// <summary>
        /// Completion model: types 1..5 and 13 complete immediately; 7..12 use a counter;
        /// type 6 has no objective. No re-issue/reset (J4).
        /// </summary>
        public bool Advance(int amount = 1)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Progress amount cannot be negative.");
            }

            if (State != 1)
            {
                return false;
            }

            if (Type == 6)
            {
                return false;
            }

            if (IsImmediate(Type))
            {
                Progress = Required;
                State = 2;
                return true;
            }

            Progress += amount;
            if (Progress >= Required)
            {
                Progress = Required;
                State = 2;
            }

            return true;
        }

        public static bool IsImmediate(int type)
        {
            return (type >= 1 && type <= 5) || type == 13;
        }

        public static bool IsCounter(int type)
        {
            return type >= 7 && type <= 12;
        }
    }

    public static class QuestRules
    {
        public const int LevelGate = 2;

        public static bool IsQuestAvailable(int cellLevel, int playerLevel)
        {
            return Math.Abs(cellLevel - playerLevel) <= LevelGate;
        }

        public static double RewardExperience(int level, int stars, bool boss)
        {
            return RewardRules.KillExperience(level, stars, boss, false, false);
        }

        public static int RewardMoney()
        {
            return 0;
        }
    }

    public sealed class MemorySaveStore : ISaveStore
    {
        private readonly Dictionary<string, string> _data = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool Exists(string key)
        {
            return _data.ContainsKey(key);
        }

        public string Read(string key)
        {
            string value;
            return _data.TryGetValue(key, out value) ? value : null;
        }

        public void Write(string key, string contents)
        {
            _data[key] = contents;
        }

        public void Delete(string key)
        {
            _data.Remove(key);
        }
    }

    /// <summary>
    /// Minimal deterministic serializer for the Core SaveGame model (no UnityEngine.JsonUtility).
    /// It mirrors the flat S02 schema; it is not the Alpha v11 format.
    /// </summary>
    public sealed class PlainSaveSerializer : ISaveSerializer
    {
        public string Serialize(SaveGame data)
        {
            var builder = new StringBuilder();
            builder.Append(data.formatVersion.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(data.character.level).Append('|')
                .Append(data.character.experience).Append('|')
                .Append(data.character.classId).Append('|')
                .Append(data.character.specializationId).Append('|')
                .Append(data.character.baseHealth.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(data.character.weaponBase.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(data.character.weaponUpgrades).Append('|')
                .Append(data.character.equipmentHealthBonus.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(data.items.Count);
            for (int i = 0; i < data.items.Count; i++)
            {
                ItemSaveData item = data.items[i];
                builder.Append('\n').Append(item.type).Append(',').Append(item.subtype).Append(',')
                    .Append(item.level).Append(',').Append(item.rarity).Append(',').Append(item.seed).Append(',')
                    .Append(item.modifier).Append(',').Append(item.powerBonus).Append(',').Append(item.stackCount);
            }

            return builder.ToString();
        }

        public SaveGame Deserialize(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            string[] lines = payload.Split(new[] { '\n' });
            string[] header = lines[0].Split(new[] { '|' });
            if (header.Length != 10)
            {
                return null;
            }

            try
            {
                var data = new SaveGame
                {
                    formatVersion = int.Parse(header[0], CultureInfo.InvariantCulture),
                    character = new CharacterSaveData
                    {
                        level = int.Parse(header[1], CultureInfo.InvariantCulture),
                        experience = long.Parse(header[2], CultureInfo.InvariantCulture),
                        classId = int.Parse(header[3], CultureInfo.InvariantCulture),
                        specializationId = int.Parse(header[4], CultureInfo.InvariantCulture),
                        baseHealth = float.Parse(header[5], CultureInfo.InvariantCulture),
                        weaponBase = float.Parse(header[6], CultureInfo.InvariantCulture),
                        weaponUpgrades = int.Parse(header[7], CultureInfo.InvariantCulture),
                        equipmentHealthBonus = float.Parse(header[8], CultureInfo.InvariantCulture),
                    },
                };

                int count = int.Parse(header[9], CultureInfo.InvariantCulture);
                if (count < 0 || lines.Length - 1 != count)
                {
                    return null;
                }

                for (int i = 1; i <= count; i++)
                {
                    string[] fields = lines[i].Split(new[] { ',' });
                    if (fields.Length != 8)
                    {
                        return null;
                    }

                    data.items.Add(new ItemSaveData
                    {
                        type = int.Parse(fields[0], CultureInfo.InvariantCulture),
                        subtype = int.Parse(fields[1], CultureInfo.InvariantCulture),
                        level = int.Parse(fields[2], CultureInfo.InvariantCulture),
                        rarity = int.Parse(fields[3], CultureInfo.InvariantCulture),
                        seed = int.Parse(fields[4], CultureInfo.InvariantCulture),
                        modifier = int.Parse(fields[5], CultureInfo.InvariantCulture),
                        powerBonus = int.Parse(fields[6], CultureInfo.InvariantCulture),
                        stackCount = int.Parse(fields[7], CultureInfo.InvariantCulture),
                    });
                }

                return data;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }
    }

    public static class EconomyQuestSavePilot
    {
        public static PilotResult Run()
        {
            Check.Start("CONTRACTS_ECONOMY_QUESTS_SAVES");
            Economy();
            Quests();
            Saves();
            EquipmentPersistenceGap();
            return Check.Finish();
        }

        private static void Economy()
        {
            var sword = new ItemInstance(ItemType.Weapon, 5, 10, 2, 7);
            int sell = EconomyRules.SellPrice(sword);
            Check.True("sell price uses the accepted S04 value formula", sell >= 1);
            Check.Equal("sell price equals ItemStatsCalculator.Value", ItemStatsCalculator.Value(10, 2, ItemType.Weapon), sell);

            int price;
            string reason;
            Check.True("buy price is not invented while margins are unknown", !EconomyRules.TryGetBuyPrice(sword, out price, out reason));
            Check.True("the buy-price blocker names F-U5", reason.Contains("F-U5"));
            Check.True("vendor markup is explicitly unproven", !EconomyRules.VendorMarkupProven);

            var wallet = new Wallet();
            wallet.Add(100);
            Check.Equal("wallet accepts money", 100, wallet.Money);
            Check.True("wallet can spend available money", wallet.TrySpend(40));
            Check.Equal("wallet balance after spending", 60, wallet.Money);
            Check.True("wallet refuses overspending", !wallet.TrySpend(61));
            Check.Equal("refused spend leaves the balance", 60, wallet.Money);
            Check.Throws<ArgumentOutOfRangeException>("negative money is rejected", () => wallet.Add(-1));
            Check.Equal("kills do not grant money", 0, QuestRules.RewardMoney());
        }

        private static void Quests()
        {
            Check.True("type 1 monster completes immediately", QuestObjective.IsImmediate(1));
            Check.True("type 5 dungeon completes immediately", QuestObjective.IsImmediate(5));
            Check.True("type 13 gang boss completes immediately", QuestObjective.IsImmediate(13));
            Check.True("type 7 invasion is a counter objective", QuestObjective.IsCounter(7));
            Check.True("type 12 gang is a counter objective", QuestObjective.IsCounter(12));
            Check.True("type 6 has no objective", !QuestObjective.IsImmediate(6) && !QuestObjective.IsCounter(6));

            var counter = new QuestObjective(7, 5);
            Check.True("sleeping objective cannot progress", !counter.Advance());
            Check.True("objective activates", counter.Activate());
            Check.True("second activation is refused (no re-issue)", !counter.Activate());
            Check.True("counter progress advances", counter.Advance(2));
            Check.Equal("counter progress is 2", 2, counter.Progress);
            Check.Equal("counter is still active", 1, counter.State);
            Check.True("counter completes at the required amount", counter.Advance(3));
            Check.Equal("completed state is 2", 2, counter.State);
            Check.Equal("progress is clamped to required", 5, counter.Progress);
            Check.True("completed objective cannot progress again", !counter.Advance());

            var immediate = new QuestObjective(1, 10);
            immediate.Activate();
            Check.True("immediate type completes on one event", immediate.Advance());
            Check.Equal("immediate completion sets progress to required", 10, immediate.Progress);

            var noTarget = new QuestObjective(6, 1);
            noTarget.Activate();
            Check.True("type 6 never completes", !noTarget.Advance());

            Check.True("level gate allows difference 2", QuestRules.IsQuestAvailable(10, 12));
            Check.True("level gate rejects difference 3", !QuestRules.IsQuestAvailable(10, 13));
            Check.Throws<ArgumentOutOfRangeException>("invalid quest type is rejected", () => new QuestObjective(14, 1));

            Check.True("quest reward experience uses the kill formula", QuestRules.RewardExperience(10, 0, false) > 0.0);
        }

        private static void Saves()
        {
            var sheet = new CharacterSheet(CharacterClass.Rogue, CharacterSpecialization.Secondary, 250.0, 7.5)
            {
                WeaponUpgrades = WeaponUpgradeFlags.Upgrade8,
                EquipmentHealthBonus = 3.0,
            };
            sheet.RestoreProgression(5, 12);

            var items = new List<ItemInstance>
            {
                new ItemInstance(ItemType.Weapon, 5, 10, 2, 7),
                new ItemInstance(ItemType.Chest, 0, 3, 1, 42),
            };

            SaveGame save = CharacterSaveMapper.ToSaveGame(sheet, items);
            string validationError;
            Check.True("accepted Core save passes its own validator", SaveValidator.TryValidate(save, out validationError), validationError);

            var store = new MemorySaveStore();
            var service = new SaveService(store, new PlainSaveSerializer());
            Check.True("save succeeds", service.Save(save).IsSuccess);

            LoadResult loaded = service.Load();
            Check.True("load succeeds", loaded.IsSuccess, loaded.Message);
            CharacterSheet restoredSheet = CharacterSaveMapper.ToCharacterSheet(loaded.Data);
            List<ItemInstance> restoredItems = CharacterSaveMapper.ToItems(loaded.Data);

            Check.Equal("class survives save/load", CharacterClass.Rogue, restoredSheet.Class);
            Check.Equal("specialization survives", CharacterSpecialization.Secondary, restoredSheet.Specialization);
            Check.Equal("level survives", 5, restoredSheet.Progression.Level);
            Check.Equal("experience survives", 12L, restoredSheet.Progression.Experience);
            Check.Near("base HP survives", 250.0, restoredSheet.BaseHealth, 1e-6);
            Check.Near("equipment HP bonus survives", 3.0, restoredSheet.EquipmentHealthBonus, 1e-6);
            Check.Equal("item count survives", 2, restoredItems.Count);
            Check.True("item identity survives", items[0].Equals(restoredItems[0]));

            string framed = store.Read(SaveService.DefaultKey);
            store.Write(SaveService.DefaultKey, framed.Replace("|5|", "|6|"));
            LoadResult corrupted = service.Load();
            Check.True("a corrupted frame is rejected (checksum/length)", corrupted.Status == LoadStatus.Corrupted, corrupted.Message);

            store.Write(SaveService.DefaultKey, SaveService.Frame(99, new PlainSaveSerializer().Serialize(save)));
            Check.True("a newer save version is rejected as unsupported", service.Load().Status == LoadStatus.UnsupportedVersion);

            store.Write(SaveService.DefaultKey, SaveService.Frame(0, new PlainSaveSerializer().Serialize(save)));
            Check.True("an older save version fails migration explicitly", service.Load().Status == LoadStatus.MigrationFailed);

            SaveGame withUnsupportedItem = CharacterSaveMapper.ToSaveGame(sheet);
            withUnsupportedItem.items.Add(new ItemSaveData { type = 1, level = 1, rarity = 0, stackCount = 1 });
            Check.True("a save with an Alpha consumable item is rejected by the current S02 validator",
                !SaveValidator.TryValidate(withUnsupportedItem, out validationError));
            Check.True("the rejection names the unknown item type", validationError.Contains("тип предмета") || validationError.Contains("type"));
        }

        private static void EquipmentPersistenceGap()
        {
            FieldInfo[] characterFields = typeof(CharacterSaveData).GetFields(BindingFlags.Public | BindingFlags.Instance);
            bool hasEquipmentSlots = false;
            for (int i = 0; i < characterFields.Length; i++)
            {
                string name = characterFields[i].Name.ToLowerInvariant();
                if (name.Contains("equipmentslot") || name == "equipment" || name == "slots")
                {
                    hasEquipmentSlots = true;
                }
            }

            Check.True("S02 save has no equipment-slot array (documented gap I-11/Q-12)", !hasEquipmentSlots);
            FieldInfo[] saveFields = typeof(SaveGame).GetFields(BindingFlags.Public | BindingFlags.Instance);
            bool hasEquipmentList = false;
            for (int i = 0; i < saveFields.Length; i++)
            {
                if (saveFields[i].Name.ToLowerInvariant().Contains("equipment"))
                {
                    hasEquipmentList = true;
                }
            }

            Check.True("S02 SaveGame has no equipment list", !hasEquipmentList);
            Check.Equal("Alpha stores 13 equipment slots, none of which are persisted by S02", 13,
                AlphaItems.StorageSlots);
        }
    }
}
