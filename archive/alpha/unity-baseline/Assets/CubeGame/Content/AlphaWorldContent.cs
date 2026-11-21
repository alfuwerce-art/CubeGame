// CubeGame.Content — Alpha world content: house/city objects, item names and skills (D86 M0-02/M0-07).
//
// The name tables come from the executed client constructor (cube::World::ctor_1) via the committed
// evidence file; they are ids and names only. Category classification is a CubeGame contract built
// from those names (the game needs to know "this is a door" / "this is a crafting bench"), and every
// derived statement says so instead of pretending Alpha stored a category.
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    public enum AlphaWorldObjectCategory
    {
        Unknown = 0,
        StructurePart = 1,
        Furniture = 2,
        Container = 3,
        Trap = 4,
        Service = 5,
        Decor = 6,
    }

    public static class AlphaWorldContent
    {
        public const string CategorySource = "NAME_PREFIX_CUBEGAME_CONTRACT (Alpha object table stores names only)";

        static readonly Dictionary<int, AlphaWorldObjectDef> ObjectsById = new Dictionary<int, AlphaWorldObjectDef>();
        static readonly Dictionary<int, AlphaSkillDef> SkillsById = new Dictionary<int, AlphaSkillDef>();
        static readonly Dictionary<long, AlphaItemNameDef> ItemNamesByKey = new Dictionary<long, AlphaItemNameDef>();

        static AlphaWorldContent()
        {
            foreach (var o in AlphaContentTables.WorldObjects) ObjectsById[o.Id] = o;
            foreach (var s in AlphaContentTables.Skills) SkillsById[s.Id] = s;
            foreach (var n in AlphaContentTables.ItemNames) ItemNamesByKey[(long)n.Type * 1000L + n.Subtype] = n;
        }

        public static IReadOnlyList<AlphaWorldObjectDef> Objects => AlphaContentTables.WorldObjects;
        public static IReadOnlyList<AlphaItemNameDef> ItemNames => AlphaContentTables.ItemNames;
        public static IReadOnlyList<AlphaSkillDef> Skills => AlphaContentTables.Skills;

        public static bool TryObject(int id, out AlphaWorldObjectDef def) => ObjectsById.TryGetValue(id, out def);
        public static bool TrySkill(int id, out AlphaSkillDef def) => SkillsById.TryGetValue(id, out def);
        public static bool TryItemName(int type, int subtype, out AlphaItemNameDef def) =>
            ItemNamesByKey.TryGetValue((long)type * 1000L + subtype, out def);

        public static AlphaWorldObjectCategory CategoryOf(int id)
        {
            if (!ObjectsById.TryGetValue(id, out var def)) return AlphaWorldObjectCategory.Unknown;
            var name = def.Name ?? "";
            if (name == "Door" || name == "BigDoor" || name == "Window" || name == "CastleWindow" || name == "Gate")
                return AlphaWorldObjectCategory.StructurePart;
            if (name.EndsWith("Trap", StringComparison.Ordinal)) return AlphaWorldObjectCategory.Trap;
            if (name == "Chest" || name == "Barrel" || name == "Crate" || name == "OpenCrate" || name == "Sack" ||
                name == "Cupboard")
                return AlphaWorldObjectCategory.Container;
            if (name == "MarketStand1" || name == "MarketStand2" || name == "MarketStand3" || name == "Furnace" ||
                name == "Anvil" || name == "SpinningWheel" || name == "Loom" || name == "SawBench" ||
                name == "Workbench" || name == "CustomizationBench")
                return AlphaWorldObjectCategory.Service;
            if (name == "Table" || name == "Stool" || name == "Bench" || name == "BedTable" || name == "Desktop" ||
                name == "Counter" || name.StartsWith("Shelf", StringComparison.Ordinal))
                return AlphaWorldObjectCategory.Furniture;
            if (name == "None") return AlphaWorldObjectCategory.Unknown;
            return AlphaWorldObjectCategory.Decor;
        }

        /// <summary>Crafting/trade furniture the town layer can use once NPC service rules are recovered.</summary>
        public static List<AlphaWorldObjectDef> ServiceObjects()
        {
            var list = new List<AlphaWorldObjectDef>();
            foreach (var o in AlphaContentTables.WorldObjects)
                if (CategoryOf(o.Id) == AlphaWorldObjectCategory.Service) list.Add(o);
            return list;
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (AlphaContentTables.WorldObjects.Length != 67) p.Add($"WORLD_OBJECTS {AlphaContentTables.WorldObjects.Length} != 67");
            if (AlphaContentTables.ItemNames.Length != 133) p.Add($"WORLD_ITEM_NAMES {AlphaContentTables.ItemNames.Length} != 133");
            if (AlphaContentTables.Skills.Length != 27) p.Add($"WORLD_SKILLS {AlphaContentTables.Skills.Length} != 27");
            if (ObjectsById.Count != AlphaContentTables.WorldObjects.Length) p.Add("WORLD_DUPLICATE_OBJECT");
            if (SkillsById.Count != AlphaContentTables.Skills.Length) p.Add("WORLD_DUPLICATE_SKILL");
            foreach (var o in AlphaContentTables.WorldObjects)
                if (string.IsNullOrEmpty(o.Name) && o.Id != 19) p.Add($"WORLD_OBJECT_UNNAMED {o.Id}");            foreach (var s in AlphaContentTables.Skills)
                if (string.IsNullOrEmpty(s.Name)) p.Add($"WORLD_SKILL_UNNAMED {s.Id}");
            if (ServiceObjects().Count < 8) p.Add($"WORLD_SERVICES {ServiceObjects().Count}");
            return p;
        }

        public static string Describe() =>
            $"worldObjects={AlphaContentTables.WorldObjects.Length} itemNames={AlphaContentTables.ItemNames.Length} " +
            $"skills={AlphaContentTables.Skills.Length} services={ServiceObjects().Count} categories={CategorySource}";
    }
}
