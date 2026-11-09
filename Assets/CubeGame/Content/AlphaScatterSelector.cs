// CubeGame.Content — conservative climate -> object selector (D86 M0-02C).
//
// The full W-18 table lives in AlphaScatterEvidence (extracted from WorldInfo_scatterObjectsInArea),
// but its climate selection uses goto-based branching that is not ported yet. This selector therefore
// answers only the two branches that are unambiguous in the decompiled code (underground level and the
// dry branch) and reports UNRESOLVED for everything else — it must never invent vegetation.
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    public enum ScatterResolution
    {
        /// <summary>The branch was read from the decompiled function and is returned verbatim.</summary>
        Resolved = 0,
        /// <summary>The climate branch needs the goto port; no object list is produced.</summary>
        Unresolved = 1,
    }

    public readonly struct AlphaScatterSelection
    {
        public readonly ScatterResolution Resolution;
        public readonly string Rule;
        public readonly int[] ObjectKinds;
        public AlphaScatterSelection(ScatterResolution resolution, string rule, int[] kinds)
        { Resolution = resolution; Rule = rule; ObjectKinds = kinds ?? new int[0]; }
    }

    public static class AlphaScatterSelector
    {
        /// <summary>Alpha branch at 0x005f56c0: humidity &lt;= 0.1 (fVar5) is the dry branch.</summary>
        public const double DryHumidityThreshold = 0.1;
        /// <summary>Dry branch adds the third object when World_placeObjectWithSpacing &gt; 0.3.</summary>
        public const double SpacingThreshold = 0.3;

        public const string EvidenceSymbol = "WorldInfo_scatterObjectsInArea @ 0x005f56c0";

        // Exact lists from the decompiled branches (order preserved).
        static readonly int[] Underground = { 0x91, 0x92, 0x93, 0x96, 0x98, 0x99, 0x9b, 0x9a };
        static readonly int[] DryBase = { 0x15, 0x2e };
        static readonly int[] DryWithSpacing = { 0x15, 0x2e, 0x2f };

        public static AlphaScatterSelection Select(double humidity, double temperature, int level, double spacing)
        {
            if (double.IsNaN(humidity) || double.IsNaN(temperature) || double.IsNaN(spacing))
                throw new ArgumentException("climate inputs must be finite numbers");
            if (level < 0)
                return new AlphaScatterSelection(ScatterResolution.Resolved, "underground(level<0)", Underground);
            if (humidity <= DryHumidityThreshold)
                return new AlphaScatterSelection(ScatterResolution.Resolved, "dry(humidity<=0.1)",
                    spacing > SpacingThreshold ? DryWithSpacing : DryBase);
            // The remaining climate branches are goto-selected in the original; returning an empty
            // resolved list would silently turn "unknown" into "nothing grows here".
            return new AlphaScatterSelection(ScatterResolution.Unresolved,
                "climate branch not ported (goto-based): see AlphaScatterEvidence", new int[0]);
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (Underground.Length != 8) p.Add($"SCATTER_UNDERGROUND {Underground.Length}");
            if (DryBase.Length != 2 || DryWithSpacing.Length != 3) p.Add("SCATTER_DRY_LISTS");
            foreach (var kind in Underground)
                if (!AlphaContentDatabase.TryCreatureKind(kind, out _)) p.Add($"SCATTER_UNDERGROUND_KIND 0x{kind:x2}");
            foreach (var kind in DryWithSpacing)
                if (!AlphaContentDatabase.TryCreatureKind(kind, out _)) p.Add($"SCATTER_DRY_KIND 0x{kind:x2}");
            var dry = Select(0.05, 0.5, 5, 0.0);
            if (dry.Resolution != ScatterResolution.Resolved || dry.ObjectKinds.Length != 2) p.Add("SCATTER_DRY_SELECT");
            var unknown = Select(0.5, 0.5, 5, 0.0);
            if (unknown.Resolution != ScatterResolution.Unresolved || unknown.ObjectKinds.Length != 0)
                p.Add("SCATTER_UNKNOWN_MUST_NOT_INVENT");
            return p;
        }

        public static string Summary()
        {
            int resolved = 0;
            for (int i = 0; i <= 20; i++)
            {
                var s = Select(0.05 * i, 0.5, 1, 0.0);
                if (s.Resolution == ScatterResolution.Resolved) resolved++;
            }
            return $"selector resolvedCases={resolved}/21 evidence={EvidenceSymbol} unresolved=climate";
        }
    }
}
