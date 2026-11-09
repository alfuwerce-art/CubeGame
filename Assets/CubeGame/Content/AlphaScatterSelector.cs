// CubeGame.Content — climate -> object selection for W-18 (D86 M0-02C).
//
// The selection itself is a mechanical translation of the decompiled WorldInfo_scatterObjectsInArea
// (AlphaScatterTree): conditions and id lists come from the code, not from a guess. The status is
// TRANSLATED_FROM_CODE, not VERIFIED_ALPHA — it has not been compared against a live Alpha scatter
// scene yet, so callers must keep the provenance visible.
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    public enum ScatterResolution
    {
        /// <summary>List produced by the translated Alpha decision tree (code-derived, not live-verified).</summary>
        TranslatedFromCode = 0,
        /// <summary>No selection available (should not happen while the translation is intact).</summary>
        Unresolved = 1,
    }

    public readonly struct AlphaScatterSelection
    {
        public readonly ScatterResolution Resolution;
        public readonly string Rule;
        public readonly int[] ObjectKinds;
        public AlphaScatterSelection(ScatterResolution resolution, string rule, int[] kinds)
        { Resolution = resolution; Rule = rule ?? ""; ObjectKinds = kinds ?? new int[0]; }
    }

    public static class AlphaScatterSelector
    {
        public const string EvidenceSymbol = AlphaScatterTree.Symbol;

        /// <param name="humidity">GameController_sampleHumidityGrid (local_80)</param>
        /// <param name="temperature">GameController_sampleTemperatureGrid (local_84)</param>
        /// <param name="cell">World_getRegionCellPtr value (fVar5 after the grids)</param>
        /// <param name="spacing">World_placeObjectWithSpacing (fVar6)</param>
        /// <param name="level">level parameter (&lt; 0 means the underground table)</param>
        /// <param name="mode">(char)param_4 flag written by the caller</param>
        public static AlphaScatterSelection Select(double humidity, double temperature, double cell,
            double spacing, int level, int mode)
        {
            if (double.IsNaN(humidity) || double.IsNaN(temperature) || double.IsNaN(cell) || double.IsNaN(spacing))
                throw new ArgumentException("climate inputs must be finite numbers");
            var ids = AlphaScatterTree.Select(humidity, temperature, cell, spacing, level, mode);
            return new AlphaScatterSelection(ScatterResolution.TranslatedFromCode,
                "W-18 " + AlphaScatterTree.Status, ids);
        }

        /// <summary>Overworld call shape: no (char)param_4 flag.</summary>
        public static AlphaScatterSelection Select(double humidity, double temperature, double cell, double spacing, int level)
            => Select(humidity, temperature, cell, spacing, level, 0);

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (string.IsNullOrEmpty(AlphaScatterTree.Symbol) || !AlphaScatterTree.Symbol.Contains("0x005f56c0"))
                p.Add("SCATTER_TREE_SYMBOL");
            if (!AlphaScatterTree.Status.Contains("TRANSLATED_FROM_CODE"))
                p.Add("SCATTER_TREE_STATUS: provenance must stay visible");
            var underground = Select(0.5, 0.5, 0.5, 0.0, -1, 0);
            if (underground.ObjectKinds.Length != 8) p.Add($"SCATTER_UNDERGROUND {underground.ObjectKinds.Length} != 8");
            var dry = Select(0.5, 0.5, 0.0, 0.0, 6, 0);
            if (Array.IndexOf(underground.ObjectKinds, 0x91) < 0) p.Add("SCATTER_UNDERGROUND_IDS");
            if (Array.IndexOf(dry.ObjectKinds, 0x15) < 0 || Array.IndexOf(dry.ObjectKinds, 0x2e) < 0)
                p.Add("SCATTER_DRY_IDS");
            var flagged = Select(0.5, 0.5, 0.5, 0.0, 6, 1);
            if (flagged.ObjectKinds.Length == 0) p.Add("SCATTER_FLAGGED_BRANCH_EMPTY");
            // A grid of inputs must never throw or return an unresolved selection.
            for (int hi = 0; hi <= 10; hi++)
            for (int ti = 0; ti <= 10; ti++)
            {
                var s = Select(hi / 10.0, ti / 10.0, 0.05, 0.5, 6, 0);
                if (s.Resolution != ScatterResolution.TranslatedFromCode) p.Add($"SCATTER_RESOLUTION {hi}/{ti}");
            }
            return p;
        }

        public static string Summary()
        {
            int[] samples = { 0, 0, 0, 0, 0 };
            samples[0] = Select(0.5, 0.5, 0.0, 0.0, 6, 0).ObjectKinds.Length;      // dry
            samples[1] = Select(0.5, 0.5, 0.5, 0.0, 6, 0).ObjectKinds.Length;      // wet climate tree
            samples[2] = Select(0.5, 0.5, 0.5, 0.0, -1, 0).ObjectKinds.Length;     // underground
            samples[3] = Select(0.5, 0.5, 0.5, 0.0, 6, 1).ObjectKinds.Length;      // flagged mode
            samples[4] = Select(0.1, 0.9, 0.05, 0.5, 6, 0).ObjectKinds.Length;
            return $"scatter dry={samples[0]} climate={samples[1]} underground={samples[2]} flagged={samples[3]} " +
                   $"sample={samples[4]} evidence={EvidenceSymbol} status=TRANSLATED_FROM_CODE";
        }
    }
}
