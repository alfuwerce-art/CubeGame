// CubeGame.Features.Creatures — which zones carry Alpha's spawner objects (D1, static evidence).
//
// Decoded from Cube.exe: `WorldInfo_generateBiomeContent` (0x5E4850) calls `Chunk_generateObjects` (0x5AF170)
// only when the zone record type is 1 or 7 (the call site at WorldInfo.cpp:3266 tests `local_1304[6]` against
// 0x00000001 and 0x00000007). Chunk_generateObjects is the pass that reads the zone's spawn-point map and
// creates the spawner objects 0x80…0x84, so EVERY other zone type carries no spawners in Alpha.
//
// What this rule does NOT say: which creatures those spawners create (the executed spawn table gives kinds and
// levels, but the point positions come from the spawn-point map that is not dumped yet — D1 step 2 in progress).
using System.Collections.Generic;

namespace CubeGame.Features.Creatures
{
    /// <summary>Zone-type rule for Alpha spawner objects (static decode, no execution).</summary>
    public static class AlphaPopulationRules
    {
        public const string Status =
            "ALPHA_CODE_STATIC: Chunk_generateObjects 0x5AF170 runs only for zone types 1 and 7 (WorldInfo.cpp:3266)";

        /// <summary>Zone type 1 of the Alpha zone record (settlement/town).</summary>
        public const int SettlementZoneType = 1;

        /// <summary>Zone type 7 — the second type that reaches Chunk_generateObjects; its meaning is UNKNOWN.</summary>
        public const int UnknownSpawnerZoneType = 7;

        public static bool CarriesSpawnerObjects(int zoneType) =>
            zoneType == SettlementZoneType || zoneType == UnknownSpawnerZoneType;

        /// <summary>All zone types that carry spawners, in Alpha's order.</summary>
        public static IReadOnlyList<int> SpawnerZoneTypes { get; } =
            new[] { SettlementZoneType, UnknownSpawnerZoneType };

        public static List<string> Validate()
        {
            var problems = new List<string>();
            if (!CarriesSpawnerObjects(1)) problems.Add("SETTLEMENT_RULE_MISSING");
            if (!CarriesSpawnerObjects(7)) problems.Add("TYPE7_RULE_MISSING");
            if (CarriesSpawnerObjects(3)) problems.Add("TYPE3_MUST_NOT_CARRY_SPAWNERS");
            if (CarriesSpawnerObjects(0)) problems.Add("TYPE0_MUST_NOT_CARRY_SPAWNERS");
            return problems;
        }

        /// <summary>Why a zone has no population from Alpha spawners (diagnostics for the game).</summary>
        public static string ExplainEmptyZone(int zoneType) =>
            CarriesSpawnerObjects(zoneType)
                ? string.Empty
                : $"NO_ALPHA_SPAWNERS zone type {zoneType} (only 1 and 7 carry them)";
    }
}
