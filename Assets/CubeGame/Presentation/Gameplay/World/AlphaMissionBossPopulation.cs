using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Session;
using CubeGame.Features.World;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>
    /// Column bosses of the Alpha region pass (0x5D9460, P7-A): a column without landmark gets a boss of kind
    /// +0x54, level +0x58 at the centre of zone (+0x60, +0x64), standing on the ground (world_getChunkCell256).
    /// The boss of a type-1 mission is the mission target (flag 0x2000); the others are plain bosses.
    /// PARTIAL: the two generated equipment items, appearance and the Sequential[Combat(20), RandomWalk] behaviour of
    /// 0x5D9460 are not ported; the creature uses the common creature driver. Killed bosses stay dead through the
    /// population deltas (CUBEGAME_CONTRACT; Alpha re-runs zone population).
    /// </summary>
    public sealed class AlphaMissionBossPopulation : IWorldPopulationSource
    {
        public const int Channel = MissionJournal.BossChannel;
        public const string BehaviourKey = "alpha_mission_boss";

        private readonly AlphaTerrainGenerator _terrain;
        private readonly MissionJournal _missions;

        public AlphaMissionBossPopulation(AlphaTerrainGenerator terrain, MissionJournal missions)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _missions = missions ?? throw new ArgumentNullException(nameof(missions));
        }

        public IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone)
        {
            if (!_missions.TryBossAt(zone, out AlphaMission column))
            {
                return Array.Empty<SpawnDefinition>();
            }

            int x = zone.X * WorldLayout.ZoneSize + 0x80;
            int z = zone.Z * WorldLayout.ZoneSize + 0x80;
            int surface = _terrain.Voxels(x, z).TopSolid();
            return new[]
            {
                new SpawnDefinition
                {
                    Region = new RegionKey(zone.X >> 6, zone.Z >> 6),
                    SpawnIndex = 0,
                    Kind = new KindId(column.BossKind),
                    Position = new WorldPosition(x + 0.5, surface + 1, z + 0.5),
                    Level = Math.Max(1, column.BossLevel),
                    Faction = (int)CreatureFaction.Monster,
                    BehaviourKey = BehaviourKey,
                    Provenance = column.BossIsMissionTarget
                        ? "ALPHA_CODE_STATIC: 0x5D9460 boss with flag 0x2000 (type-1 mission target)"
                        : "ALPHA_CODE_STATIC: 0x5D9460 column boss",
                },
            };
        }
    }
}
