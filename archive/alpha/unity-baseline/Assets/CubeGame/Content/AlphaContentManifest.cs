// CubeGame.Content — machine-readable manifest of the transferred content (D86 M0-09C).
//
// Claude's world/session bootstrap can read one JSON document instead of probing every table: counts of
// kinds, how many have action libraries, item anchors/slots, held/equipment models, world objects, scatter
// groups and dungeon rules. Values come from committed tables and AlphaStagedVisuals only (no cache, no
// assets), so the manifest is stable across machines.
using System.Text;

namespace CubeGame.Content
{
    public static class AlphaContentManifest
    {
        public const string Format = "cubegame.alpha_content_manifest/1";

        public static string BuildJson()
        {
            int actionsKinds = 0, actionClips = 0, staticObjects = 0, locomotionOnly = 0;
            foreach (var staged in AlphaStagedVisuals.All)
            {
                if (staged.ControllerMode == "ACTIONS") { actionsKinds++; actionClips += staged.Clips; }
                else if (staged.ControllerMode == "STATIC_OBJECT") staticObjects++;
                else locomotionOnly++;
            }
            int mappedSlots = 0;
            foreach (var type in AlphaContentTables.ItemTypes) if (AlphaItemSlotRules.TrySlot(type.Id, out _)) mappedSlots++;
            var sb = new StringBuilder();
            sb.Append("{\"format\":\"").Append(Format).Append('"');
            sb.Append(",\"kinds\":").Append(AlphaContentTables.CreatureKinds.Length);
            sb.Append(",\"passiveKinds\":").Append(AlphaCreatureCatalog.PassiveKinds.Count);
            sb.Append(",\"combatKinds\":").Append(AlphaCreatureCatalog.CombatKinds.Count);
            sb.Append(",\"actionsKinds\":").Append(actionsKinds);
            sb.Append(",\"actionClips\":").Append(actionClips);
            sb.Append(",\"staticObjects\":").Append(staticObjects);
            sb.Append(",\"locomotionOnly\":").Append(locomotionOnly);
            sb.Append(",\"itemTypes\":").Append(AlphaContentTables.ItemTypes.Length);
            sb.Append(",\"weaponSubtypes\":").Append(AlphaContentTables.WeaponSubtypes.Length);
            sb.Append(",\"weaponAnchors\":").Append(AlphaContentTables.WeaponAnchors.Length);
            sb.Append(",\"offhandAnchors\":").Append(AlphaContentTables.OffhandAnchors.Length);
            sb.Append(",\"heldItems\":").Append(AlphaHeldItemModels.Items.Length);
            sb.Append(",\"equipmentItems\":").Append(AlphaEquipmentModels.Items.Length);
            sb.Append(",\"itemSlots\":").Append(mappedSlots);
            sb.Append(",\"itemActions\":").Append(AlphaContentTables.ItemActions.Length);
            sb.Append(",\"worldObjects\":").Append(AlphaContentTables.WorldObjects.Length);
            sb.Append(",\"itemNames\":").Append(AlphaContentTables.ItemNames.Length);
            sb.Append(",\"skills\":").Append(AlphaContentTables.Skills.Length);
            sb.Append(",\"regions\":").Append(AlphaContentTables.RegionTypes.Length);
            sb.Append(",\"abilities\":").Append(AlphaContentTables.Abilities.Length);
            sb.Append(",\"structures\":").Append(AlphaStructureTemplates.All.Count);
            sb.Append(",\"scatterGroups\":").Append(AlphaScatterEvidence.Groups.Length);
            sb.Append(",\"dungeonChestItems\":").Append(AlphaDungeonRules.ChestItemCount);
            sb.Append(",\"chestAnchor\":\"CUBEGAME_CONTRACT\"");
            sb.Append('}');
            return sb.ToString();
        }

        public static string Summary() =>
            $"manifest={Format} kinds={AlphaContentTables.CreatureKinds.Length} bytes={BuildJson().Length}";
    }
}
