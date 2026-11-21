// CubeGame.Content — integrity checks of the extracted climate->object scatter evidence (W-18).
//
// This is DATA extracted from the decompiled WorldInfo_scatterObjectsInArea, not a gameplay rule yet:
// the branch selection uses gotos, so the runtime selector is not implemented and no placement is
// derived from these groups. What can be checked now is that the extraction is complete enough to be
// reviewed and that every id is a real Alpha kind.
using System.Collections.Generic;
using System.Text;

namespace CubeGame.Content
{
    public static class AlphaScatterEvidenceValidation
    {
        public static List<string> Validate()
        {
            var p = new List<string>();
            var kinds = new HashSet<int>();
            if (AlphaScatterEvidence.Groups.Length < 4)
                p.Add($"SCATTER_GROUPS {AlphaScatterEvidence.Groups.Length} < 4");
            foreach (var g in AlphaScatterEvidence.Groups)
            {
                if ((g.ObjectKinds == null || g.ObjectKinds.Length == 0) && (g.Gotos == null || g.Gotos.Length == 0))
                    p.Add("SCATTER_EMPTY_GROUP");
                if (g.Conditions == null || g.Conditions.Length == 0)
                    p.Add("SCATTER_GROUP_WITHOUT_CONDITIONS");
                foreach (var kind in g.ObjectKinds ?? new int[0])
                {
                    if (!AlphaContentDatabase.TryCreatureKind(kind, out _))
                        p.Add($"SCATTER_UNKNOWN_KIND 0x{kind:x2}");
                    kinds.Add(kind);
                }
            }
            if (kinds.Count < 20) p.Add($"SCATTER_DISTINCT_KINDS {kinds.Count} < 20");
            if (!AlphaScatterEvidence.Status.Contains("PARTIAL"))
                p.Add("SCATTER_STATUS_NOT_PARTIAL: runtime evaluation must not be claimed");
            return p;
        }

        public static string Summary()
        {
            var kinds = new HashSet<int>();
            int ids = 0, gotos = 0;
            foreach (var g in AlphaScatterEvidence.Groups)
            {
                ids += g.ObjectKinds?.Length ?? 0;
                gotos += g.Gotos?.Length ?? 0;
                foreach (var kind in g.ObjectKinds ?? new int[0]) kinds.Add(kind);
            }
            var sb = new StringBuilder();
            sb.Append("scatterGroups=").Append(AlphaScatterEvidence.Groups.Length)
              .Append(" ids=").Append(ids).Append(" distinctKinds=").Append(kinds.Count)
              .Append(" gotoMarkers=").Append(gotos).Append(" status=PARTIAL");
            return sb.ToString();
        }
    }
}
