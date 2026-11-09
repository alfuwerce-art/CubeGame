// CubeGame.Content — NPC service/workstation mapping (D86 M0-07C data side).
//
// Alpha's object table names the town furniture (MarketStand1-3, Furnace, Anvil, Workbench,
// CustomizationBench, BedTable/SleepingMat, Loom, SpinningWheel, SawBench). The game needs to know
// which interaction each represents; that mapping is a CubeGame contract built from those names,
// because trading rules, assortments, prices and restock (F-U5) are UNKNOWN in Alpha evidence.
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    public enum AlphaServiceKind
    {
        Unmapped = 0,
        Trade = 1,
        Craft = 2,
        Customize = 3,
        Rest = 4,
        Smelt = 5,
    }

    public readonly struct AlphaNpcServiceDef
    {
        public readonly int ObjectId;
        public readonly string ObjectName;
        public readonly AlphaServiceKind Kind;
        public AlphaNpcServiceDef(int objectId, string objectName, AlphaServiceKind kind)
        { ObjectId = objectId; ObjectName = objectName; Kind = kind; }
    }

    public static class AlphaNpcServices
    {
        public const string Status =
            "CUBEGAME_CONTRACT: service categories inferred from Alpha object names; trade rules/prices/assortment UNKNOWN (F-U5)";

        public static AlphaServiceKind KindOfName(string name)
        {
            switch (name)
            {
                case "MarketStand1":
                case "MarketStand2":
                case "MarketStand3":
                    return AlphaServiceKind.Trade;
                case "Furnace":
                    return AlphaServiceKind.Smelt;
                case "Anvil":
                case "Workbench":
                case "SawBench":
                case "Loom":
                case "SpinningWheel":
                    return AlphaServiceKind.Craft;
                case "CustomizationBench":
                    return AlphaServiceKind.Customize;
                case "BedTable":
                case "SleepingMat":
                    return AlphaServiceKind.Rest;
                default:
                    return AlphaServiceKind.Unmapped;
            }
        }

        public static bool TryServiceFor(int objectId, out AlphaNpcServiceDef def)
        {
            def = default;
            if (!AlphaWorldContent.TryObject(objectId, out var obj)) return false;
            var category = AlphaWorldContent.CategoryOf(objectId);
            if (category != AlphaWorldObjectCategory.Service && category != AlphaWorldObjectCategory.Furniture) return false;
            def = new AlphaNpcServiceDef(objectId, obj.Name ?? "", KindOfName(obj.Name));
            return def.Kind != AlphaServiceKind.Unmapped;
        }

        public static List<AlphaNpcServiceDef> All()
        {
            var list = new List<AlphaNpcServiceDef>();
            foreach (var o in AlphaWorldContent.Objects)
            {
                var kind = KindOfName(o.Name);
                if (kind != AlphaServiceKind.Unmapped) list.Add(new AlphaNpcServiceDef(o.Id, o.Name ?? "", kind));
            }
            return list;
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            var services = All();
            if (services.Count < 8) p.Add($"NPC_SERVICES {services.Count} < 8");
            int mapped = 0;
            foreach (var s in services)
            {
                if (s.Kind == AlphaServiceKind.Unmapped) p.Add($"NPC_SERVICE_UNMAPPED {s.ObjectId} {s.ObjectName}");
                else mapped++;
            }
            if (mapped == 0) p.Add("NPC_SERVICES_NONE_MAPPED");
            return p;
        }

        public static string Describe() =>
            string.Join(", ", All().ConvertAll(s => $"{s.ObjectName}->{s.Kind}")) + " | " + Status;
    }
}
