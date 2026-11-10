// CubeGame.Features.Creatures — wild population built with the RECOVERED Alpha rules (D1 REVIEW-FIX).
//
// Alpha path: AlphaWildSpawnRules (3×3 grid, skip, jitter, level range, pack, companion kind) — see that file
// for addresses. What is NOT Alpha here and is labelled so in every definition:
//   * the random numbers: Alpha draws CRT rand() in the order of the whole zone generator, which is not
//     reproduced; this source draws from its own deterministic stream (CUBEGAME_CONTRACT);
//   * the kind: supplied by the caller (Alpha calls 0x5F56C0 with the climate of the spot);
//   * the generator conditions listed as NOT MODELLED in AlphaWildSpawnRules.
// So the STRUCTURE of the population follows Alpha; exact Alpha coordinates are not claimed.
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>Zone → wild creature templates by Alpha's grid rules.</summary>
    public sealed class AlphaRecoveredPopulation : IWorldPopulationSource
    {
        public const string Provenance =
            "grid/level/pack=ALPHA_CODE_STATIC (Cube.exe 0x5EAE6A-0x5EBBB1); random stream/kind source=CUBEGAME_CONTRACT";

        public const string BehaviourKey = "creature_ai";

        /// <summary>Spawn indices of one cell: leader + up to four members (pack range max 5).</summary>
        public const int IndicesPerCell = 8;

        readonly WorldSeed _seed;
        readonly Func<ZoneKey, int, int, int> _kindAt;
        readonly Func<ZoneKey, int, int, double> _groundHeight;

        /// <param name="kindAt">zone + local x/z → Alpha kind (the 0x5F56C0 choice), or −1 for "nothing here".</param>
        /// <param name="groundHeight">zone + local x/z → world Y of the surface; null → 0.</param>
        public AlphaRecoveredPopulation(WorldSeed seed, Func<ZoneKey, int, int, int> kindAt,
            Func<ZoneKey, int, int, double> groundHeight = null)
        {
            _seed = seed;
            _kindAt = kindAt ?? throw new ArgumentNullException(nameof(kindAt));
            _groundHeight = groundHeight;
        }

        public IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone)
        {
            var list = new List<SpawnDefinition>();
            var stream = new Stream(_seed.Value, zone);
            var region = new RegionKey((int)Math.Floor(zone.X / (double)AlphaWorldScale.ZonesPerRegion),
                (int)Math.Floor(zone.Z / (double)AlphaWorldScale.ZonesPerRegion));
            for (int cx = 0; cx < AlphaWildSpawnRules.CellsPerAxis; cx++)
            {
                for (int cz = 0; cz < AlphaWildSpawnRules.CellsPerAxis; cz++)
                {
                    int cell = cx * AlphaWildSpawnRules.CellsPerAxis + cz;
                    if (AlphaWildSpawnRules.CellSkipped(stream.Next())) continue;
                    int localX = AlphaWildSpawnRules.CellCoordinate(cx, stream.Next());
                    int localZ = AlphaWildSpawnRules.CellCoordinate(cz, stream.Next());
                    int kind = _kindAt(zone, localX, localZ);
                    if (kind < 0) continue;
                    double leaderX = zone.X * (double)AlphaWorldScale.ZoneVoxels + localX;
                    double leaderZ = zone.Z * (double)AlphaWorldScale.ZoneVoxels + localZ;
                    bool passive = CreatureKinds.IsPassiveObject(kind);
                    list.Add(Definition(zone, region, cell * IndicesPerCell, kind, passive, leaderX, leaderZ, stream));
                    if (passive) continue;
                    int extra = AlphaWildSpawnRules.ExtraPackMembers(kind, stream.Next());
                    for (int i = 0; i < extra && i < IndicesPerCell - 1; i++)
                    {
                        AlphaWildSpawnRules.PackOffset(i, extra, out double dx, out double dz);
                        int memberKind = AlphaWildSpawnRules.CompanionKind(kind, stream.Next());
                        list.Add(Definition(zone, region, cell * IndicesPerCell + 1 + i, memberKind, false,
                            leaderX + dx, leaderZ + dz, stream));
                    }
                }
            }

            return list;
        }

        SpawnDefinition Definition(ZoneKey zone, RegionKey region, int index, int kind, bool passive, double x, double z,
            Stream stream)
        {
            int level = passive ? 1 : AlphaWildSpawnRules.Level(kind, stream.Next());
            float yaw = stream.Next() * 360f / 32767f;   // Alpha: rand()·360/32767 (template +0x54)
            double y = 0.0;
            if (_groundHeight != null)
            {
                y = _groundHeight(zone, (int)Math.Floor(x - zone.X * (double)AlphaWorldScale.ZoneVoxels),
                    (int)Math.Floor(z - zone.Z * (double)AlphaWorldScale.ZoneVoxels));
            }

            return new SpawnDefinition
            {
                Region = region,
                SpawnIndex = index,
                Kind = new KindId(kind),
                Position = new WorldPosition(x, y, z),
                Yaw = yaw,
                Faction = passive ? AlphaWildSpawnRules.FactionPassive : AlphaWildSpawnRules.FactionHostile,
                Level = level,
                ClassId = -1,
                SpecializationId = 0,
                Equipment = Array.Empty<RawAlphaItemId>(),
                BehaviourKey = passive ? "static_object" : BehaviourKey,
                Provenance = Provenance,
            };
        }

        /// <summary>CUBEGAME_CONTRACT: deterministic stand-in for CRT rand() (15-bit values, like MSVC).</summary>
        sealed class Stream
        {
            ulong _state;

            public Stream(uint seed, ZoneKey zone)
            {
                _state = seed * 0x9E3779B97F4A7C15UL ^ ((ulong)(uint)zone.X << 32 | (uint)zone.Z) * 0xC2B2AE3D27D4EB4FUL;
                if (_state == 0) _state = 1;
            }

            public int Next()
            {
                _state ^= _state >> 33;
                _state *= 0xFF51AFD7ED558CCDUL;
                _state ^= _state >> 29;
                _state += 0x9E3779B97F4A7C15UL;
                return (int)((_state >> 20) & 0x7FFF);
            }
        }
    }
}
