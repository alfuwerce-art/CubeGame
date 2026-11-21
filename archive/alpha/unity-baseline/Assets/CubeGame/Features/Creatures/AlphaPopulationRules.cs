// CubeGame.Features.Creatures — what the Village/Ruins call of Cube.exe 0x5AF170 is (D1 REVIEW-FIX).
//
// Byte fact (kept): the zone generator 0x5E4850 calls 0x5AF170 when the 0x68-byte landmark column's type at
// +0x18 is Village (1) or Ruins (5) (0x5E9655–0x5E9672).
// Corrected meaning: 0x5AF170 is the village/ruins GENERATOR (houses, plots, their residents and monsters).
// It contains 13 of the 35 places where the client constructs a creature template (call 0x5ADB20); the other
// 22 are in six other generator functions, including the wild 3×3 grid of 0x5E4850 itself that runs for
// ordinary zones. So this gate does NOT decide whether a zone has creatures. The earlier statements
// "only zone types 1 and 7 carry spawners" and "all other zones have no Alpha population" are withdrawn.
using System.Collections.Generic;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>Landmark gate of the village/ruins generator call (not a population rule).</summary>
    public static class AlphaPopulationRules
    {
        public const string Status =
            "ALPHA_CODE_STATIC: 0x5AF170 (village/ruins generator) is called for landmark Village (1) and Ruins (5) at 0x5E9672; not a population gate";

        /// <summary>True when the village/ruins generator runs for a column of this landmark type.</summary>
        public static bool RunsVillageRuinsGenerator(LandmarkType landmarkType) =>
            landmarkType == LandmarkType.Village || landmarkType == LandmarkType.Ruins;

        /// <summary>Old name of <see cref="RunsVillageRuinsGenerator"/>; it never meant "has creatures".</summary>
        public static bool CarriesSpawnerObjects(LandmarkType landmarkType) => RunsVillageRuinsGenerator(landmarkType);

        /// <summary>Landmark types tested at the Alpha call site, in order.</summary>
        public static IReadOnlyList<LandmarkType> SpawnerLandmarkTypes { get; } =
            new[] { LandmarkType.Village, LandmarkType.Ruins };

        public static List<string> Validate()
        {
            var problems = new List<string>();
            if (!RunsVillageRuinsGenerator(LandmarkType.Village)) problems.Add("VILLAGE_RULE_MISSING");
            if (!RunsVillageRuinsGenerator(LandmarkType.Ruins)) problems.Add("RUINS_RULE_MISSING");
            if (RunsVillageRuinsGenerator(LandmarkType.Valley)) problems.Add("VALLEY_MUST_NOT_RUN_THE_GENERATOR");
            if (RunsVillageRuinsGenerator(LandmarkType.None)) problems.Add("NONE_MUST_NOT_RUN_THE_GENERATOR");
            return problems;
        }

        /// <summary>Diagnostics: why the village/ruins generator did not run. An empty zone is NOT implied.</summary>
        public static string ExplainEmptyZone(LandmarkType landmarkType) =>
            RunsVillageRuinsGenerator(landmarkType)
                ? string.Empty
                : $"NO_VILLAGE_RUINS_GENERATOR landmark {landmarkType} (wild creatures come from the zone grid, not from this call)";
    }
}
