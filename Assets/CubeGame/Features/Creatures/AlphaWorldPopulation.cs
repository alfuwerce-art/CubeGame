// CubeGame.Features.Creatures — world population entry point kept for existing callers (D1 REVIEW-FIX).
//
// History: this class used to pick creature kinds from AlphaSpawnTable and call the result
// "kind/level/weight = EMULATED_ALPHA_CODE". The review-fix showed that table holds vendor ITEM records
// (0x118-byte items appended by Cube.exe 0x5C9B30 / 0x5CA0A0 for NPC classes 0x80 / 0x81 / 0x83), so that
// claim is withdrawn. The class is now a thin facade over two explicitly separated sources:
//
//   AlphaRecoveredPopulation   — Alpha's wild grid rules (when `recovered` is requested);
//   FallbackPopulationPolicy   — the old CUBEGAME_CONTRACT placement.
//
// Both need a kind selector from the caller; without one the source is empty (it never invents a kind).
// The former landmark filter is gone as a population rule: templates are created in 35 places of 7 generator
// functions, so "only Village/Ruins columns carry creatures" was never true (see AlphaPopulationRules).
using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>Zone → spawn definitions; delegates to the recovered Alpha rules or to the fallback policy.</summary>
    public sealed class AlphaWorldPopulation : IWorldPopulationSource
    {
        /// <summary>CUBEGAME_CONTRACT density of the fallback policy (kept for existing references).</summary>
        public const int SpawnsPerZone = FallbackPopulationPolicy.SpawnsPerZone;

        public const string Provenance = FallbackPopulationPolicy.Provenance;

        public const string BehaviourKey = "creature_ai";

        readonly IWorldPopulationSource _source;

        /// <param name="seed">World seed.</param>
        /// <param name="zoneLevel">Fallback only: level given to every spawn of a zone.</param>
        /// <param name="groundHeight">zone + local x/z → world Y of the surface; null → 0.</param>
        /// <param name="npcTable">Ignored. It used to select the "NPC rows", which are weapon-vendor stock.</param>
        /// <param name="landmarkType">Ignored. The Village/Ruins gate is not a population rule (see header).</param>
        /// <param name="kindSelector">zone + local x/z → Alpha kind, or −1. Null → the source is empty.</param>
        /// <param name="recovered">True → Alpha's wild grid rules; false → the CUBEGAME_CONTRACT fallback.</param>
        public AlphaWorldPopulation(WorldSeed seed, Func<ZoneKey, int> zoneLevel = null,
            Func<ZoneKey, int, int, double> groundHeight = null, bool npcTable = false,
            Func<ZoneKey, LandmarkType> landmarkType = null, Func<ZoneKey, int, int, int> kindSelector = null,
            bool recovered = false)
        {
            if (kindSelector == null)
            {
                _source = null;
            }
            else if (recovered)
            {
                _source = new AlphaRecoveredPopulation(seed, kindSelector, groundHeight);
            }
            else
            {
                _source = new FallbackPopulationPolicy(seed, kindSelector, zoneLevel, groundHeight);
            }
        }

        /// <summary>True when the definitions follow the recovered Alpha rules.</summary>
        public bool UsesRecoveredRules => _source is AlphaRecoveredPopulation;

        public IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone) =>
            _source == null ? Array.Empty<SpawnDefinition>() : _source.SpawnsForZone(zone);
    }
}
