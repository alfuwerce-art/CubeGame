// CubeGame.Features.Creatures — CUBEGAME_CONTRACT stand-in population (D1 REVIEW-FIX).
//
// NOT Alpha. This is the temporary placement that existed before the spawn pipeline was recovered: a fixed
// number of spawns per zone at hashed positions, with the zone level. It is kept only so a caller that has no
// recovered data for a zone can still show creatures, and it is isolated here so it can never be mistaken
// for AlphaRecoveredPopulation. Nothing in this file may carry an ALPHA_* / EMULATED_* status.
//
// The kind comes from the caller. The earlier version picked it from AlphaSpawnTable; that table turned out to
// hold vendor ITEM records (see AlphaSpawnTable.Status), so it is no longer used and without a kind selector
// this policy places nothing.
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>Deterministic non-Alpha placement: N hashed positions per zone.</summary>
    public sealed class FallbackPopulationPolicy : IWorldPopulationSource
    {
        /// <summary>CUBEGAME_CONTRACT: invented density.</summary>
        public const int SpawnsPerZone = 6;

        public const string Provenance = "CUBEGAME_CONTRACT: fallback placement (count, positions, yaw, zone level); not Alpha";

        public const string BehaviourKey = "creature_ai";

        readonly WorldSeed _seed;
        readonly Func<ZoneKey, int> _zoneLevel;
        readonly Func<ZoneKey, int, int, double> _groundHeight;
        readonly Func<ZoneKey, int, int, int> _kindAt;

        public FallbackPopulationPolicy(WorldSeed seed, Func<ZoneKey, int, int, int> kindAt,
            Func<ZoneKey, int> zoneLevel = null, Func<ZoneKey, int, int, double> groundHeight = null)
        {
            _seed = seed;
            _kindAt = kindAt;
            _zoneLevel = zoneLevel;
            _groundHeight = groundHeight;
        }

        public IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone)
        {
            if (_kindAt == null) return Array.Empty<SpawnDefinition>();
            int level = _zoneLevel != null ? _zoneLevel(zone) : 1;
            if (level < 1) level = 1;
            var list = new List<SpawnDefinition>(SpawnsPerZone);
            var region = new RegionKey((int)Math.Floor(zone.X / (double)AlphaWorldScale.ZonesPerRegion),
                (int)Math.Floor(zone.Z / (double)AlphaWorldScale.ZonesPerRegion));
            for (int index = 0; index < SpawnsPerZone; index++)
            {
                ulong h = Hash(zone, index);
                int localX = 16 + (int)((h >> 8) % (ulong)(AlphaWorldScale.ZoneVoxels - 32));
                int localZ = 16 + (int)((h >> 24) % (ulong)(AlphaWorldScale.ZoneVoxels - 32));
                int kind = _kindAt(zone, localX, localZ);
                if (kind < 0 || CreatureKinds.IsPassiveObject(kind)) continue;
                double y = _groundHeight != null ? _groundHeight(zone, localX, localZ) : 0.0;
                list.Add(new SpawnDefinition
                {
                    Region = region,
                    SpawnIndex = index,
                    Kind = new KindId(kind),
                    Position = new WorldPosition(zone.X * (double)AlphaWorldScale.ZoneVoxels + localX, y,
                        zone.Z * (double)AlphaWorldScale.ZoneVoxels + localZ),
                    Yaw = (float)((h >> 40) % 360UL),
                    Faction = CreatureKinds.FactionOf(kind),
                    Level = level,
                    ClassId = -1,
                    SpecializationId = 0,
                    Equipment = Array.Empty<RawAlphaItemId>(),
                    BehaviourKey = BehaviourKey,
                    Provenance = Provenance,
                });
            }

            return list;
        }

        ulong Hash(ZoneKey zone, int index)
        {
            ulong x = (ulong)(uint)zone.X;
            ulong z = (ulong)(uint)zone.Z;
            ulong h = (ulong)_seed.Value * 0x9E3779B97F4A7C15UL;
            h ^= (x + 0x9E3779B97F4A7C15UL) * 0xC2B2AE3D27D4EB4FUL;
            h ^= (z + 0x165667B19E3779F9UL) * 0x27D4EB2F165667C5UL;
            h ^= ((ulong)(uint)index + 0x9E3779B185EBCA87UL) * 0x94D049BB133111EBUL;
            h ^= h >> 33;
            h *= 0xFF51AFD7ED558CCDUL;
            h ^= h >> 29;
            return h == 0 ? 1UL : h;
        }
    }
}
