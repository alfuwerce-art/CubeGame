using System;
using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.World;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>
    /// CUBEGAME_CONTRACT: service NPCs stand by existing town service objects. The auxiliary population
    /// channel leaves the terrain generator and the identities of the existing town objects intact.
    /// Role codes 0x80 and 0x84 come from Alpha's town constructors (D1/D2 review fix).
    /// </summary>
    public sealed class TownNpcPopulation : IWorldPopulationSource
    {
        public const int Channel = 2;
        public const string BehaviourKey = "town_service_npc";
        public const int MerchantClass = 0x80;
        public const int InnkeeperClass = 0x84;
        public static bool IsTrainer(int classId) => classId >= 1 && classId <= CharacterClasses.Count;

        private readonly AlphaTerrainGenerator _terrain;

        public TownNpcPopulation(AlphaTerrainGenerator terrain) => _terrain = terrain;

        public IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone)
        {
            foreach (SpawnDefinition anchor in _terrain.Zone(zone.X, zone.Z).ContentSpawns)
            {
                if (anchor.BehaviourKey == AlphaCityContentProvider.ServiceBehaviourKey)
                {
                    return new[]
                    {
                        At(anchor, 0, MerchantClass, -3, 0),
                        At(anchor, 1, InnkeeperClass, 3, 0),
                        At(anchor, 2, 1, -3, 3),
                        At(anchor, 3, 2, 3, 3),
                        At(anchor, 4, 3, -3, -3),
                        At(anchor, 5, 4, 3, -3),
                    };
                }
            }

            return Array.Empty<SpawnDefinition>();
        }

        private SpawnDefinition At(SpawnDefinition anchor, int index, int classId, int offsetX, int offsetZ)
        {
            int x = (int)anchor.Position.X + offsetX;
            int z = (int)anchor.Position.Z + offsetZ;
            int surface = _terrain.Voxels(x, z).TopSolid();
            return new SpawnDefinition
            {
                Region = anchor.Region,
                SpawnIndex = index,
                Kind = new KindId(0x02),
                Position = new WorldPosition(x + 0.5, surface + 1, z + 0.5),
                Yaw = anchor.Yaw,
                Level = anchor.Level,
                Faction = (int)CreatureFaction.Npc,
                ClassId = classId,
                BehaviourKey = BehaviourKey,
                Provenance = "CUBEGAME_CONTRACT: placement by a town service object; ALPHA_CODE_STATIC: role code",
            };
        }
    }
}
