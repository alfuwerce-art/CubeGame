// CubeGame.Features.World.Foundation — G2-1: population source of CubeGame content (structures, anchors, portals,
// creatures). It replaces the Alpha content provider as the owner of channel-1/channel-7 content for the CubeGame
// content world, keeps the existing WorldPopulation contract, and filters destroyed objects through the same world
// delta the rest of the world uses — so a destroyed structure never comes back after unload/reload or save/load.
using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>Deterministic spawns of the CubeGame content stage of a zone.</summary>
    public sealed class CubeGameStructurePopulation : IWorldPopulationSource
    {
        public const int Channel = CubeGameContentIds.StructureChannel;

        private readonly AlphaTerrainGenerator _terrain;
        private readonly WorldDeltaStore _deltas;

        public CubeGameStructurePopulation(AlphaTerrainGenerator terrain, WorldDeltaStore deltas = null)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _deltas = deltas;
        }

        public IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone)
        {
            var result = new List<SpawnDefinition>();
            if (_terrain.GeneratorId == null ||
                !AlphaTerrainGenerator.UsesCubeGameContent(_terrain.GeneratorId))
            {
                return result;
            }

            AlphaZone content = _terrain.Zone(zone.X, zone.Z);
            foreach (SpawnDefinition spawn in content.StructureSpawns)
            {
                if (IsRemoved(zone, spawn)) continue;
                result.Add(spawn);
            }

            return result;
        }

        public bool IsRemoved(ZoneKey zone, SpawnDefinition spawn) =>
            spawn != null && _deltas != null && _deltas.IsObjectRemoved(zone, Channel, spawn.SpawnIndex);

        /// <summary>Removal record used by the runtime (destroyed structure/portal/anchor).</summary>
        public bool MarkRemoved(ZoneKey zone, SpawnDefinition spawn, long timeMs)
        {
            if (spawn == null || _deltas == null) return false;
            _deltas.Record(new WorldDelta
            {
                Region = new RegionKey(zone.X >> 6, zone.Z >> 6),
                Kind = WorldDeltaKind.ObjectRemoved,
                X = zone.X,
                Y = Channel * 4096 + spawn.SpawnIndex,
                Z = zone.Z,
                Value = 1,
                TimeMs = timeMs,
            });
            return true;
        }

        /// <summary>All spawns of the zone, removed or not (debug overlay and tests).</summary>
        public IReadOnlyList<SpawnDefinition> AllSpawnsOfZone(ZoneKey zone)
        {
            var result = new List<SpawnDefinition>();
            if (_terrain.GeneratorId == null || !AlphaTerrainGenerator.UsesCubeGameContent(_terrain.GeneratorId))
            {
                return result;
            }

            result.AddRange(_terrain.Zone(zone.X, zone.Z).StructureSpawns);
            return result;
        }
    }
}
