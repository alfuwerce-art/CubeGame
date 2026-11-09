// CubeGame.Content — one place that reports what Alpha content is actually loaded (D86 M0-01/M0-09).
//
// The game bootstrap and the reports need the same counters: how many kinds exist, how many have an
// action library, how many item anchors/slots are measured, how much world content is available.
// This class only reads committed tables and the staged-visual table; it never touches the cache.
using System.Collections.Generic;
using System.Text;

namespace CubeGame.Content
{
    public static class AlphaContentSummary
    {
        public static string Report()
        {
            int actions = 0, locomotion = 0, staticObjects = 0, actionClips = 0;
            foreach (var s in AlphaStagedVisuals.All)
            {
                if (s.ControllerMode == "ACTIONS") { actions++; actionClips += s.Clips; }
                else if (s.ControllerMode == "STATIC_OBJECT") staticObjects++;
                else locomotion++;
            }
            var sb = new StringBuilder();
            sb.Append("kinds=").Append(AlphaContentTables.CreatureKinds.Length)
              .Append(" passive=").Append(AlphaCreatureCatalog.PassiveKinds.Count)
              .Append(" combat=").Append(AlphaCreatureCatalog.CombatKinds.Count)
              .Append(" staged=").Append(AlphaStagedVisuals.All.Length)
              .Append(" actions=").Append(actions).Append(" clips=").Append(actionClips)
              .Append(" locomotionOnly=").Append(locomotion)
              .Append(" staticObjects=").Append(staticObjects)
              .Append(" itemTypes=").Append(AlphaContentTables.ItemTypes.Length)
              .Append(" weaponSubtypes=").Append(AlphaContentTables.WeaponSubtypes.Length)
              .Append(" weaponAnchors=").Append(AlphaContentTables.WeaponAnchors.Length)
              .Append(" offhandAnchors=").Append(AlphaContentTables.OffhandAnchors.Length)
              .Append(" itemSlots=").Append(AlphaContentTables.ItemSlots.Length)
              .Append(" itemActions=").Append(AlphaContentTables.ItemActions.Length)
              .Append(" worldObjects=").Append(AlphaContentTables.WorldObjects.Length)
              .Append(" itemNames=").Append(AlphaContentTables.ItemNames.Length)
              .Append(" skills=").Append(AlphaContentTables.Skills.Length)
              .Append(" scatterGroups=").Append(AlphaScatterEvidence.Groups.Length)
              .Append(" heldItems=").Append(AlphaHeldItemModels.Items.Length)
              .Append(" equipmentItems=").Append(AlphaEquipmentModels.Items.Length)
              .Append(" structures=").Append(AlphaStructureTemplates.All.Count)
              .Append(" services=").Append(AlphaNpcServices.All().Count);
            return sb.ToString();
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (AlphaStagedVisuals.All.Length != AlphaContentTables.CreatureKinds.Length)
                p.Add($"SUMMARY_STAGED {AlphaStagedVisuals.All.Length} != kinds {AlphaContentTables.CreatureKinds.Length}");
            int actions = 0, staticObjects = 0;
            foreach (var s in AlphaStagedVisuals.All)
            {
                if (s.ControllerMode == "ACTIONS") actions++;
                if (s.ControllerMode == "STATIC_OBJECT") staticObjects++;
            }
            if (staticObjects != AlphaCreatureCatalog.PassiveKinds.Count) p.Add("SUMMARY_STATIC_OBJECTS");
            if (actions == 0 || actions >= AlphaStagedVisuals.All.Length) p.Add($"SUMMARY_ACTIONS {actions}");
            var report = Report();
            foreach (var key in new[] { "kinds=", "actions=", "clips=", "itemTypes=", "worldObjects=", "scatterGroups=" })
                if (!report.Contains(key)) p.Add("SUMMARY_MISSING_" + key);
            return p;
        }
    }
}
