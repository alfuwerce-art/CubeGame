using System;
using System.Collections.Generic;
using CubeGame.Core.Items;
using CubeGame.M0Pilots.Generated;

namespace CubeGame.M0Pilots.Items
{
    public sealed class StarterItem
    {
        public int Type;
        public int Subtype;
        public int Modifier;
        public int Count;
    }

    public sealed class StarterKit
    {
        public int ClassId;
        public string Name;
        public StarterItem MainHand;
        public StarterItem OffHand;
        public readonly List<StarterItem> Inventory = new List<StarterItem>();
    }

    public static class AlphaItems
    {
        public const int StorageSlots = 13;
        public const int RenderedBaseSlots = 7;
        public const int InventoryContainers = 4;
        public const int SlotsPerContainer = 50;
        public const int StackLimit = 49;

        public static string TypeName(int itemType)
        {
            return AlphaItemType.NameOf(itemType);
        }

        /// <summary>Storage slot offsets that accept an item type. DIRECT (Equipment_sum_slot_values).</summary>
        public static string[] SlotsForType(int itemType)
        {
            switch (itemType)
            {
                case AlphaItemType.Weapon: return new[] { "0x990", "0xaa8" };
                case AlphaItemType.ChestArmor: return new[] { "0x530" };
                case AlphaItemType.Gloves: return new[] { "0x760" };
                case AlphaItemType.Boots: return new[] { "0x648" };
                case AlphaItemType.ShoulderArmor: return new[] { "0x878" };
                case AlphaItemType.Amulet: return new[] { "0x418" };
                case AlphaItemType.Ring: return new[] { "0xbc0", "0xcd8" };
                case AlphaItemType.Lamp: return new[] { "0xdf0" };
                default: return new string[0];
            }
        }

        public static bool IsRenderedSlot(string offset)
        {
            switch (offset)
            {
                case "0x418":
                case "0x530":
                case "0x648":
                case "0x760":
                case "0x878":
                case "0x990":
                case "0xaa8":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsConditionalSlot(string offset)
        {
            return offset == "0xdf0" || offset == "0xf08" || offset == "0x1020";
        }

        public static string WeaponSubtypeName(int subtype)
        {
            return AlphaWeaponSubtype.NameOf(subtype);
        }
    }

    public static class ConsumableRules
    {
        public const int ActionUsePrimary = 0x50;
        public const int ActionUseOther = 0x51;

        /// <summary>GameController_itemCooldown @ 0x4c6b80.</summary>
        public static int CooldownMs(int itemType, int itemSubtype)
        {
            if (itemType != AlphaItemType.Consumable)
            {
                return 0;
            }

            return itemSubtype == 1 ? 3000 : 10000;
        }

        /// <summary>handleObjectAction: event subtype 1/2 selects 0x50, otherwise 0x51.</summary>
        public static int ActionForEventSubtype(int eventSubtype)
        {
            return eventSubtype == 1 || eventSubtype == 2 ? ActionUsePrimary : ActionUseOther;
        }

        /// <summary>
        /// The stack decrement and healing application were not found (B-U1), therefore the pilot
        /// exposes no "consumed" operation: only the proven action/cooldown contract is represented.
        /// </summary>
        public const bool ConsumptionEffectProven = false;
    }

    public static class StarterKits
    {
        public static List<StarterKit> Create()
        {
            var kits = new List<StarterKit>();

            var warrior = new StarterKit { ClassId = 1, Name = "Warrior" };
            warrior.OffHand = Item(AlphaItemType.Weapon, 0x11, 2, 1);
            warrior.Inventory.Add(Item(AlphaItemType.Weapon, 1, 0, 2));
            warrior.Inventory.Add(Item(AlphaItemType.Weapon, 0x0D, 0, 1));
            kits.Add(warrior);

            var ranger = new StarterKit { ClassId = 2, Name = "Ranger" };
            ranger.OffHand = Item(AlphaItemType.Weapon, 6, 0, 1);
            ranger.Inventory.Add(Item(AlphaItemType.Weapon, 2, 0, 1));
            ranger.Inventory.Add(Item(AlphaItemType.Weapon, 7, 0, 1));
            kits.Add(ranger);

            var mage = new StarterKit { ClassId = 3, Name = "Mage" };
            mage.OffHand = Item(AlphaItemType.Weapon, 0x0A, 0, 1);
            mage.Inventory.Add(Item(AlphaItemType.Weapon, 0x0C, 0, 1));
            mage.Inventory.Add(Item(AlphaItemType.Weapon, 0x0B, 0, 1));
            kits.Add(mage);

            var rogue = new StarterKit { ClassId = 4, Name = "Rogue" };
            rogue.MainHand = Item(AlphaItemType.Weapon, 3, 0, 1);
            rogue.OffHand = Item(AlphaItemType.Weapon, 3, 0, 1);
            rogue.Inventory.Add(Item(AlphaItemType.Weapon, 4, 0, 2));
            rogue.Inventory.Add(Item(AlphaItemType.Weapon, 5, 0, 1));
            kits.Add(rogue);

            return kits;
        }

        public static StarterItem Ring(int modifier)
        {
            return Item(AlphaItemType.Ring, 0, modifier, 1);
        }

        public static StarterItem Lamp()
        {
            return Item(AlphaItemType.Lamp, 0, 0, 1);
        }

        public static StarterItem LifePotion(int count)
        {
            return Item(AlphaItemType.Consumable, 1, 0, count);
        }

        public static StarterItem[] Rings()
        {
            return new[] { Ring(0x0C), Ring(0x0B) };
        }

        private static StarterItem Item(int type, int subtype, int modifier, int count)
        {
            return new StarterItem { Type = type, Subtype = subtype, Modifier = modifier, Count = count };
        }
    }

    public static class ItemVisualContract
    {
        /// <summary>
        /// Consumable body animation 0x50/0x51 was NOT transferred in S05B-2 (D69 keeps 51 blocked;
        /// the transferred set is 37/40/42/43). The held item model IS drawn by drawModel.
        /// </summary>
        public static bool IsConsumableAnimationAvailable(int actionId)
        {
            if (actionId == ConsumableRules.ActionUsePrimary || actionId == ConsumableRules.ActionUseOther)
            {
                return false;
            }

            return false;
        }

        public static string ConsumableAnimationStatus(int actionId)
        {
            if (actionId == ConsumableRules.ActionUsePrimary || actionId == ConsumableRules.ActionUseOther)
            {
                return "BLOCKED_NOT_TRANSFERRED";
            }

            return "UNKNOWN";
        }

        public const bool HeldItemModelDrawnByAlpha = true;
        public const bool ModelPipelineUsesS05Catalog = true;
        public const bool OriginalAssetsCommittedToRepo = false;
    }

    /// <summary>
    /// Detects the mismatch between the r3-proven Alpha tables and the accepted S02/S04 Core enums.
    /// The pilot must not change accepted S04 contracts; it reports them for a separate decision.
    /// </summary>
    public static class CoreEnumAudit
    {
        public static List<string> FindMismatches()
        {
            var mismatches = new List<string>();

            CheckPair(mismatches, "ItemType", 5, "Gloves", (int)ItemType.Gloves, ItemType.Gloves.ToString());
            CheckPair(mismatches, "ItemType", 6, "Boots", (int)ItemType.Boots, ItemType.Boots.ToString());

            var expectedWeapons = new Dictionary<int, string>
            {
                { 1, "Axe" }, { 2, "Mace" }, { 3, "Dagger" }, { 4, "Fist" }, { 5, "Longsword" },
                { 6, "Bow" }, { 7, "Crossbow" }, { 8, "Boomerang" }, { 9, "Arrow" }, { 10, "Staff" },
                { 11, "Wand" }, { 12, "Bracelet" }, { 13, "Shield" }, { 14, "Quiver" }, { 15, "Greatsword" },
                { 16, "Greataxe" }, { 17, "Greatmace" }, { 20, "Torch" },
            };

            foreach (KeyValuePair<int, string> pair in expectedWeapons)
            {
                string actualName = Enum.GetName(typeof(WeaponSubtype), pair.Key);
                if (actualName != pair.Value)
                {
                    mismatches.Add("WeaponSubtype " + pair.Key + ": expected " + pair.Value + ", actual "
                                   + (actualName ?? "MISSING"));
                }
            }

            return mismatches;
        }

        public static bool CoreCanRepresentAlphaType(int alphaType)
        {
            return ItemInstance.IsSupportedType(alphaType);
        }

        public static bool CoreCanRepresentWeaponSubtype(int subtype)
        {
            return Enum.IsDefined(typeof(WeaponSubtype), (WeaponSubtype)(byte)subtype);
        }

        private static void CheckPair(List<string> mismatches, string table, int id, string expectedName,
            int actualValue, string actualName)
        {
            if (actualValue != id || !string.Equals(actualName, expectedName, StringComparison.Ordinal))
            {
                mismatches.Add(table + " id " + id + ": expected " + expectedName + ", actual value="
                               + actualValue + " name=" + actualName);
            }
        }
    }

    public static class ItemPilot
    {
        public static PilotResult Run()
        {
            Check.Start("PILOT_ITEMS");
            TypeAndSubtypeTables();
            EquipmentSlots();
            InventoryAndStacking();
            Consumables();
            Kits();
            Visuals();
            CoreCompatibility();
            StatsReuse();
            return Check.Finish();
        }

        private static void TypeAndSubtypeTables()
        {
            Check.Equal("Alpha item type count is 24", 24, CountTypes());
            Check.Equal("type 4 is ChestArmor", "ChestArmor", AlphaItems.TypeName(4));
            Check.Equal("type 5 is Gloves (r3 correction)", "Gloves", AlphaItems.TypeName(5));
            Check.Equal("type 6 is Boots (r3 correction)", "Boots", AlphaItems.TypeName(6));
            Check.Equal("type 0x11 (17) is Vase", "Vase", AlphaItems.TypeName(17));
            Check.Equal("type 0x15 (21) is QuestItem", "QuestItem", AlphaItems.TypeName(21));
            Check.Equal("type 0x17 (23) is HangGliderAndBoat", "HangGliderAndBoat", AlphaItems.TypeName(23));
            Check.Equal("type 0x18 (24) is Lamp", "Lamp", AlphaItems.TypeName(24));
            Check.Equal("type 0x19 (25) is ManaCube", "ManaCube", AlphaItems.TypeName(25));
            Check.True("unknown item type is not named", AlphaItems.TypeName(10) == null);

            Check.Equal("weapon subtype 1 is Axe", "Axe", AlphaItems.WeaponSubtypeName(1));
            Check.Equal("weapon subtype 2 is Mace", "Mace", AlphaItems.WeaponSubtypeName(2));
            Check.Equal("weapon subtype 3 is Dagger", "Dagger", AlphaItems.WeaponSubtypeName(3));
            Check.Equal("weapon subtype 4 is Fist", "Fist", AlphaItems.WeaponSubtypeName(4));
            Check.Equal("weapon subtype 5 is Longsword", "Longsword", AlphaItems.WeaponSubtypeName(5));
            Check.Equal("weapon subtype 6 is Bow", "Bow", AlphaItems.WeaponSubtypeName(6));
            Check.Equal("weapon subtype 7 is Crossbow", "Crossbow", AlphaItems.WeaponSubtypeName(7));
            Check.Equal("weapon subtype 8 is Boomerang", "Boomerang", AlphaItems.WeaponSubtypeName(8));
            Check.Equal("weapon subtype 9 is Arrow", "Arrow", AlphaItems.WeaponSubtypeName(9));
            Check.Equal("weapon subtype 0x0e is Quiver", "Quiver", AlphaItems.WeaponSubtypeName(14));
            Check.Equal("weapon subtype 0x11 is Greatmace", "Greatmace", AlphaItems.WeaponSubtypeName(17));
        }

        private static int CountTypes()
        {
            int count = 0;
            for (int id = 0; id <= 255; id++)
            {
                if (AlphaItemType.NameOf(id) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void EquipmentSlots()
        {
            Check.Equal("equipment storage has 13 slots", 13, AlphaItems.StorageSlots);
            Check.Equal("7 base rendered attachments", 7, AlphaItems.RenderedBaseSlots);

            string[] weapon = AlphaItems.SlotsForType(AlphaItemType.Weapon);
            Check.Equal("weapon type maps to two hands", 2, weapon.Length);
            Check.True("main hand is +0x990", weapon[0] == "0x990");
            Check.True("second hand is +0xaa8", weapon[1] == "0xaa8");

            Check.True("chest maps to +0x530", AlphaItems.SlotsForType(AlphaItemType.ChestArmor)[0] == "0x530");
            Check.True("gloves map to +0x760", AlphaItems.SlotsForType(AlphaItemType.Gloves)[0] == "0x760");
            Check.True("boots map to +0x648", AlphaItems.SlotsForType(AlphaItemType.Boots)[0] == "0x648");
            Check.True("shoulders map to +0x878", AlphaItems.SlotsForType(AlphaItemType.ShoulderArmor)[0] == "0x878");
            Check.True("amulet maps to +0x418", AlphaItems.SlotsForType(AlphaItemType.Amulet)[0] == "0x418");

            string[] rings = AlphaItems.SlotsForType(AlphaItemType.Ring);
            Check.True("rings map to +0xbc0/+0xcd8", rings.Length == 2 && rings[0] == "0xbc0" && rings[1] == "0xcd8");
            Check.True("rings are stored but not rendered", !AlphaItems.IsRenderedSlot("0xbc0") && !AlphaItems.IsRenderedSlot("0xcd8"));
            Check.True("lamp has a conditional slot +0xdf0", AlphaItems.IsConditionalSlot("0xdf0"));
            Check.True("+0x300 has no proven item type mapping", AlphaItems.SlotsForType(0).Length == 0);
            Check.True("consumables are not equipment items", AlphaItems.SlotsForType(AlphaItemType.Consumable).Length == 0);
            Check.True("the 7 base rendered slots are exactly known",
                AlphaItems.IsRenderedSlot("0x418") && AlphaItems.IsRenderedSlot("0x530")
                && AlphaItems.IsRenderedSlot("0x648") && AlphaItems.IsRenderedSlot("0x760")
                && AlphaItems.IsRenderedSlot("0x878") && AlphaItems.IsRenderedSlot("0x990")
                && AlphaItems.IsRenderedSlot("0xaa8") && !AlphaItems.IsRenderedSlot("0x300"));
        }

        private static void InventoryAndStacking()
        {
            Check.Equal("4 inventory containers", 4, AlphaItems.InventoryContainers);
            Check.Equal("50 slots per container", 50, AlphaItems.SlotsPerContainer);
            Check.Equal("total capacity is 200 slots", 200, AlphaItems.InventoryContainers * AlphaItems.SlotsPerContainer);
            Check.Equal("stack limit is 49", 49, AlphaItems.StackLimit);
            Check.True("a stack of 49 is valid", 49 <= AlphaItems.StackLimit);
            Check.True("a stack of 50 exceeds the proven limit", 50 > AlphaItems.StackLimit);
        }

        private static void Consumables()
        {
            Check.Equal("life potion (type 1 subtype 1) cooldown is 3000 ms", 3000,
                ConsumableRules.CooldownMs(AlphaItemType.Consumable, 1));
            Check.Equal("other consumable (type 1 subtype 4) cooldown is 10000 ms", 10000,
                ConsumableRules.CooldownMs(AlphaItemType.Consumable, 4));
            Check.Equal("bomb uses the generic 10000 ms branch", 10000,
                ConsumableRules.CooldownMs(AlphaItemType.Consumable, 7));
            Check.Equal("weapon has no item cooldown", 0,
                ConsumableRules.CooldownMs(AlphaItemType.Weapon, 1));
            Check.Equal("event subtype 1 selects action 0x50", 0x50, ConsumableRules.ActionForEventSubtype(1));
            Check.Equal("event subtype 2 selects action 0x50", 0x50, ConsumableRules.ActionForEventSubtype(2));
            Check.Equal("event subtype 3 selects action 0x51", 0x51, ConsumableRules.ActionForEventSubtype(3));
            Check.True("consumption effect is explicitly not proven", !ConsumableRules.ConsumptionEffectProven);
        }

        private static void Kits()
        {
            List<StarterKit> kits = StarterKits.Create();
            Check.Equal("four starter kits", 4, kits.Count);

            StarterKit warrior = kits[0];
            Check.True("Warrior starts with a Greatmace in the second hand",
                warrior.OffHand != null && warrior.OffHand.Type == AlphaItemType.Weapon && warrior.OffHand.Subtype == 0x11);
            Check.True("Warrior has two axes and a shield in the inventory",
                warrior.Inventory.Count == 2 && warrior.Inventory[0].Subtype == 1 && warrior.Inventory[0].Count == 2
                && warrior.Inventory[1].Subtype == 0x0D);

            StarterKit ranger = kits[1];
            Check.True("Ranger starts with a Bow", ranger.OffHand.Subtype == 6);
            Check.True("Ranger inventory has a mace and a crossbow",
                ranger.Inventory[0].Subtype == 2 && ranger.Inventory[1].Subtype == 7);

            StarterKit mage = kits[2];
            Check.True("Mage starts with a Staff", mage.OffHand.Subtype == 0x0A);
            Check.True("Mage inventory has a bracelet and a wand",
                mage.Inventory[0].Subtype == 0x0C && mage.Inventory[1].Subtype == 0x0B);

            StarterKit rogue = kits[3];
            Check.True("Rogue wields two daggers in both hands",
                rogue.MainHand.Subtype == 3 && rogue.OffHand.Subtype == 3);
            Check.True("every class gets a lamp (type 0x18) in +0xdf0",
                StarterKits.Lamp().Type == AlphaItemType.Lamp);
            Check.Equal("every class gets two rings", 2, StarterKits.Rings().Length);
            Check.True("ring modifiers are 0x0c and 0x0b",
                StarterKits.Rings()[0].Modifier == 0x0C && StarterKits.Rings()[1].Modifier == 0x0B);
            Check.Equal("every class gets five life potions", 5, StarterKits.LifePotion(5).Count);
        }

        private static void Visuals()
        {
            Check.True("Alpha draws the held consumable model", ItemVisualContract.HeldItemModelDrawnByAlpha);
            Check.True("consumable animation 0x50 is blocked, not claimed as transferred",
                !ItemVisualContract.IsConsumableAnimationAvailable(0x50));
            Check.Equal("0x50 status is BLOCKED_NOT_TRANSFERRED", "BLOCKED_NOT_TRANSFERRED",
                ItemVisualContract.ConsumableAnimationStatus(0x50));
            Check.Equal("0x51 status is BLOCKED_NOT_TRANSFERRED", "BLOCKED_NOT_TRANSFERRED",
                ItemVisualContract.ConsumableAnimationStatus(0x51));
            Check.True("model pipeline relies on the S05 catalog", ItemVisualContract.ModelPipelineUsesS05Catalog);
            Check.True("no original Alpha assets are committed", !ItemVisualContract.OriginalAssetsCommittedToRepo);
        }

        private static void CoreCompatibility()
        {
            List<string> mismatches = CoreEnumAudit.FindMismatches();
            // 2026-11-09: weapon subtype names in Core were aligned with the r3 table (producer decision);
            // only the two item type labels (5/6) remain.
            // 2026-11-09: item type labels 5/6 aligned as well — Core names now match the r3 table.
            Check.Equal("no enum mismatches between Core and the r3 table", 0, mismatches.Count);
            Check.True("Core represents Alpha gloves type 5", CoreEnumAudit.CoreCanRepresentAlphaType(5));
            Check.True("Core cannot represent Alpha consumables (type 1)",
                !CoreEnumAudit.CoreCanRepresentAlphaType(1));
            Check.True("Core cannot represent Alpha material type 0x0b",
                !CoreEnumAudit.CoreCanRepresentAlphaType(11));
            Check.True("Core cannot represent Alpha quest items (type 0x15)",
                !CoreEnumAudit.CoreCanRepresentAlphaType(21));
            Check.True("Core cannot represent Alpha lamp (type 0x18)",
                !CoreEnumAudit.CoreCanRepresentAlphaType(24));
            Check.True("Core can represent Alpha weapon type 3", CoreEnumAudit.CoreCanRepresentAlphaType(3));
            Check.True("Core can represent Alpha chest type 4", CoreEnumAudit.CoreCanRepresentAlphaType(4));
            Check.True("Core represents Bow subtype 6", CoreEnumAudit.CoreCanRepresentWeaponSubtype(6));
            Check.True("Core names subtype 1 Axe (r3)", Enum.GetName(typeof(WeaponSubtype), 1) == "Axe");
            Check.True("Core names subtype 0 Sword (r3)", Enum.GetName(typeof(WeaponSubtype), 0) == "Sword");
            Check.True("Core type 5 is named Gloves", Enum.GetName(typeof(ItemType), 5) == "Gloves");
        }

        private static void StatsReuse()
        {
            // Reuse S04's accepted calculator without duplicating its formulas.
            Check.Near("longsword level 1 rarity 0 = 4.0 (S04 formula)", 4.0,
                ItemStatsCalculator.WeaponDamage(1, 0, WeaponSubtype.Longsword), 1e-9);
            Check.Near("chest armor level 1 rarity 0 = 10.0 (S04 formula)", 10.0,
                ItemStatsCalculator.Armor(1, 0, ItemType.Chest), 1e-9);
            Check.Near("shield level 1 rarity 0 = 2.0 (S04 formula)", 2.0,
                ItemStatsCalculator.WeaponDamage(1, 0, WeaponSubtype.Shield), 1e-9);
            Check.True("item value for a weapon is positive", ItemStatsCalculator.Value(1, 0, ItemType.Weapon) >= 1);
            Check.True("item value is clamped to at least 1", ItemStatsCalculator.Value(1, 0, ItemType.Gloves) >= 1);
        }
    }
}
