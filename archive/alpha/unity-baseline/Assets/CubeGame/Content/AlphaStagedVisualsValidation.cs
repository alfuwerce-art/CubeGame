// CubeGame.Content — integrity checks of the prepared-visual table (D86 M0-06A/M0-09).
//
// AlphaStagedVisuals is generated from the local cache, so it must stay consistent with the catalog:
// no unknown kinds, no duplicate variants, passive objects only as STATIC_OBJECT, combat kinds never
// frozen as static, every entry with parts and at least one clip.
using System.Collections.Generic;
using System.Text;

namespace CubeGame.Content
{
    public static class AlphaStagedVisualsValidation
    {
        public static List<string> Validate()
        {
            var p = new List<string>();
            var kinds = new HashSet<int>();
            var variants = new HashSet<string>();
            int passive = 0, combat = 0, staticObjects = 0;
            foreach (var s in AlphaStagedVisuals.All)
            {
                if (!AlphaContentDatabase.TryCreatureKind(s.Kind, out var def))
                {
                    p.Add($"STAGED_UNKNOWN_KIND 0x{s.Kind:x2}");
                    continue;
                }
                if (string.IsNullOrEmpty(s.Variant)) p.Add($"STAGED_EMPTY_VARIANT 0x{s.Kind:x2}");
                if (!kinds.Add(s.Kind)) p.Add($"STAGED_DUPLICATE_KIND 0x{s.Kind:x2}");
                if (!string.IsNullOrEmpty(s.Variant) && !variants.Add(s.Variant)) p.Add($"STAGED_DUPLICATE_VARIANT {s.Variant}");
                if (s.Parts <= 0) p.Add($"STAGED_NO_PARTS {s.Variant}");
                if (s.Clips <= 0) p.Add($"STAGED_NO_CLIPS {s.Variant}");
                if (def.Passive)
                {
                    passive++;
                    if (s.ControllerMode != "STATIC_OBJECT") p.Add($"STAGED_PASSIVE_NOT_STATIC 0x{s.Kind:x2} {s.ControllerMode}");
                }
                else
                {
                    combat++;
                    if (s.ControllerMode == "STATIC_OBJECT") p.Add($"STAGED_COMBAT_STATIC 0x{s.Kind:x2}");
                }
                if (s.ControllerMode == "STATIC_OBJECT") staticObjects++;
                if (s.ControllerMode != "ACTIONS" && s.ControllerMode != "LOCOMOTION_ONLY" && s.ControllerMode != "STATIC_OBJECT")
                    p.Add($"STAGED_MODE_UNKNOWN 0x{s.Kind:x2} {s.ControllerMode}");
            }
            if (passive != AlphaCreatureCatalog.PassiveKinds.Count)
                p.Add($"STAGED_PASSIVE_INCOMPLETE {passive}/{AlphaCreatureCatalog.PassiveKinds.Count}");
            if (staticObjects != passive) p.Add($"STAGED_STATIC_COUNT {staticObjects} != passive {passive}");
            if (combat == 0) p.Add("STAGED_NO_COMBAT_KINDS");
            return p;
        }

        public static string Summary()
        {
            int passive = 0, combat = 0, actions = 0;
            foreach (var s in AlphaStagedVisuals.All)
            {
                if (AlphaContentDatabase.TryCreatureKind(s.Kind, out var def) && def.Passive) passive++;
                else combat++;
                if (s.ControllerMode == "ACTIONS") actions++;
            }
            var sb = new StringBuilder();
            sb.Append("staged=").Append(AlphaStagedVisuals.All.Length)
              .Append(" combat=").Append(combat).Append("/").Append(AlphaCreatureCatalog.CombatKinds.Count)
              .Append(" passive=").Append(passive).Append("/").Append(AlphaCreatureCatalog.PassiveKinds.Count)
              .Append(" actionLibraries=").Append(actions);
            return sb.ToString();
        }
    }
}
