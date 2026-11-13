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
    /// Role codes 0x80…0x84 come from Alpha's town constructors (D1/D2 review fix; P5: 0x80 item, 0x81/0x83 weapon,
    /// 0x82 armour vendor, byte-checked at 0x5BCE42/0x5BD1FA/0x5BD3D6/0x5BD01E).
    /// </summary>
    public sealed class TownNpcPopulation : IWorldPopulationSource
    {
        public const int Channel = 2;
        public const string BehaviourKey = "town_service_npc";
        public const int MerchantClass = 0x80;
        public const int InnkeeperClass = 0x84;
        public const int WeaponVendorClass = 0x81;
        public const int ArmorVendorClass = 0x82;
        public const int WeaponVendor2Class = 0x83;
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
                        // P5: the remaining Alpha vendor roles; spawn indices 6…8 keep the earlier identities stable.
                        At(anchor, 6, WeaponVendorClass, -6, 0),
                        At(anchor, 7, ArmorVendorClass, 6, 0),
                        At(anchor, 8, WeaponVendor2Class, 0, 6),
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
