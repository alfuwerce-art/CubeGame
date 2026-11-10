// CubeGame.Features.Creatures — the landmark gate on Alpha's spawner-object pass (D1).
//
// Decoded from Cube.exe: `WorldInfo_generateBiomeContent` (0x5E4850) calls `Chunk_generateObjects` (0x5AF170)
// when the 0x68-byte landmark column's type at +0x18 is Village (1) or Ruins (5).
// Cube.exe 0x5E4AAB–0x5E4AC0 selects the column; 0x5E9655–0x5E9672 tests it and calls the pass.
//
// Spawn-point locations, density and the contents of this pass are tracked separately in D1.
using System.Collections.Generic;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>Landmark gate for the Alpha spawner-object pass.</summary>
    public static class AlphaPopulationRules
    {
        public const string Status =
            "ALPHA_CODE_STATIC: Chunk_generateObjects 0x5AF170 is called for Village (1) and Ruins (5) at 0x5E9672";

        public static bool CarriesSpawnerObjects(LandmarkType landmarkType) =>
            landmarkType == LandmarkType.Village || landmarkType == LandmarkType.Ruins;

        /// <summary>Landmark types tested at the Alpha call site, in order.</summary>
        public static IReadOnlyList<LandmarkType> SpawnerLandmarkTypes { get; } =
            new[] { LandmarkType.Village, LandmarkType.Ruins };

        public static List<string> Validate()
        {
            var problems = new List<string>();
            if (!CarriesSpawnerObjects(LandmarkType.Village)) problems.Add("VILLAGE_RULE_MISSING");
            if (!CarriesSpawnerObjects(LandmarkType.Ruins)) problems.Add("RUINS_RULE_MISSING");
            if (CarriesSpawnerObjects(LandmarkType.Valley)) problems.Add("VALLEY_MUST_NOT_CARRY_SPAWNERS");
            if (CarriesSpawnerObjects(LandmarkType.None)) problems.Add("NONE_MUST_NOT_CARRY_SPAWNERS");
            return problems;
        }

        /// <summary>Why a zone has no population from Alpha spawners (diagnostics for the game).</summary>
        public static string ExplainEmptyZone(LandmarkType landmarkType) =>
            CarriesSpawnerObjects(landmarkType)
                ? string.Empty
                : $"NO_ALPHA_SPAWNERS landmark {landmarkType} (only Village and Ruins run this pass)";
    }
}
