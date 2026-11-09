// CubeGame.Features.Creatures — Alpha world population (D1, M0-06B): which creature spawns where.
//
// Provenance of every number:
//   * kind / level / weight — ALPHA_CODE executed: Alpha's own World_generateMonsterSpawnList (0x5C9B30) and
//     World_generateNpcSpawnList (0x5CA0A0) were run outside the game by
//     tools/alpha_models/world/spawn_oracle.py and distilled into AlphaSpawnTable (committed, EMULATED_ALPHA_CODE);
//   * how many spawns a zone holds and where exactly they stand — CUBEGAME_CONTRACT for now: Alpha places
//     spawn points inside Chunk_generateObjects (0x5AF170), which is not executed yet. Nothing here claims
//     Alpha density or Alpha positions.
//
// The source is deterministic: the same world seed and zone always produce the same list, so unloading and
// reloading a zone never creates new entities (M0-00C §8).
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Creatures
{
    /// <summary>Zone → spawn definitions built from Alpha's executed spawn table.</summary>
    public sealed class AlphaWorldPopulation : IWorldPopulationSource
    {
        /// <summary>CUBEGAME_CONTRACT: Alpha's own population density is not recovered yet (0x5AF170 not executed).</summary>
        public const int SpawnsPerZone = 6;

        public const string Provenance =
            "kind/level/weight=EMULATED_ALPHA_CODE (Cube.exe 0x5C9B30/0x5CA0A0); positions/count=CUBEGAME_CONTRACT";

        /// <summary>Behaviour profile key of Features/AI used for every spawned creature.</summary>
        public const string BehaviourKey = "creature_ai";

        readonly WorldSeed _seed;
        readonly Func<ZoneKey, int> _zoneLevel;
        readonly Func<ZoneKey, int, int, double> _groundHeight;
        readonly bool _npcTable;

        /// <param name="seed">World seed; part of the deterministic pick.</param>
        /// <param name="zoneLevel">Region level of a zone (world generator); null → level 1.</param>
        /// <param name="groundHeight">zone + local x/z → world Y of the surface; null → 0.</param>
        /// <param name="npcTable">Use the executed NPC rows instead of the monster rows.</param>
        public AlphaWorldPopulation(WorldSeed seed, Func<ZoneKey, int> zoneLevel = null,
            Func<ZoneKey, int, int, double> groundHeight = null, bool npcTable = false)
        {
            _seed = seed;
            _zoneLevel = zoneLevel;
            _groundHeight = groundHeight;
            _npcTable = npcTable;
        }

        public IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone)
        {
            int level = 1;
            if (_zoneLevel != null)
            {
                level = _zoneLevel(zone);
            }

            if (level < 1) level = 1;
            var rows = Rows();
            var list = new List<SpawnDefinition>(SpawnsPerZone);
            var region = new RegionKey((int)Math.Floor(zone.X / (double)AlphaWorldScale.ZonesPerRegion),
                (int)Math.Floor(zone.Z / (double)AlphaWorldScale.ZonesPerRegion));
            for (int index = 0; index < SpawnsPerZone; index++)
            {
                ulong h = Hash(zone, index);
                int kind = PickKind(rows, level, h);
                if (kind < 0) continue;
                if (CreatureKinds.IsPassiveObject(kind)) continue;   // plants/ore belong to zone content, not creatures
                int localX = 16 + (int)((h >> 8) % (ulong)(AlphaWorldScale.ZoneVoxels - 32));
                int localZ = 16 + (int)((h >> 24) % (ulong)(AlphaWorldScale.ZoneVoxels - 32));
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

        /// <summary>Level rows of the executed table (monster or NPC).</summary>
        AlphaSpawnEntryDef[] Rows() => _npcTable ? AlphaSpawnTable.Npc : AlphaSpawnTable.Monster;

        /// <summary>
        /// Weighted pick at the requested level; when the executed table has no row for that level the nearest
        /// lower level is used (the table covers levels 1…MaxLevel), never an invented one.
        /// </summary>
        public static int PickKind(AlphaSpawnEntryDef[] rows, int level, ulong hash)
        {
            if (rows == null || rows.Length == 0) return -1;
            int chosen = -1;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Level > level) continue;
                if (rows[i].Level > chosen) chosen = rows[i].Level;   // widen the band to this level
            }

            if (chosen < 0) return -1;
            long total = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Level == chosen) total += rows[i].Weight;
            }

            if (total <= 0) return -1;
            long roll = (long)(hash % (ulong)total);
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Level != chosen) continue;
                roll -= rows[i].Weight;
                if (roll < 0) return rows[i].Kind;
            }

            return -1;
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
