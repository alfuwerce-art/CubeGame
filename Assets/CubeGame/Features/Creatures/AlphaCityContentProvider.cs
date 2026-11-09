// CubeGame.Features.Creatures — D3: town furniture and services of a city zone.
//
// Alpha's object table names the town furniture (MarketStand1-3, Furnace, Anvil, Workbench, CustomizationBench,
// BedTable/SleepingMat, Loom, SpinningWheel, SawBench); which interaction each represents is a CubeGame
// contract built from those names (AlphaNpcServices), because trading rules, assortments, prices and restock
// are UNKNOWN in Alpha evidence (F-U5). This provider places the furniture as passive objects with the service
// category carried in the provenance, so gameplay (Claude, C7) can attach interactions without guessing.
//
// Which zones are cities is NOT assumed: the caller passes the predicate (zone record type 1 = town in Alpha),
// exactly like the dungeon provider.
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>Content stage of a city zone: service furniture as passive spawn records.</summary>
    public sealed class AlphaCityContentProvider : IZoneContentProvider
    {
        public const string ProviderKey = "deepseek.city-content.v1";

        /// <summary>Behaviour key of a service object: no AI, no movement; the game attaches the interaction.</summary>
        public const string ServiceBehaviourKey = "service_object";

        public const string ServiceProvenance =
            "CUBEGAME_CONTRACT: service category from the Alpha object name; trade rules/prices UNKNOWN (F-U5)";

        /// <summary>Default number of service objects per city zone (CUBEGAME_CONTRACT).</summary>
        public const int DefaultServices = 5;

        readonly Func<ZoneContentContext, bool> _isCityZone;
        readonly int _services;

        public AlphaCityContentProvider(Func<ZoneContentContext, bool> isCityZone, int services = DefaultServices)
        {
            _isCityZone = isCityZone;
            _services = services;
        }

        public string Key => ProviderKey;

        public void Populate(ZoneContentContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_isCityZone == null || !_isCityZone(context)) return;
            var zone = new WorldZoneKey(context.Zone.Zx, context.Zone.Zz);
            var spawns = Plan(context.Terrain.MasterSeed, zone,
                (x, z) => context.Contains(x, z) ? context.Zone.Column(x, z).TopSolid() : 0,
                _services, context.Spawns.Count);
            LastSpawns = spawns;
            for (int i = 0; i < spawns.Count; i++) context.Spawns.Add(spawns[i]);
        }

        public List<SpawnDefinition> LastSpawns { get; private set; }

        /// <summary>
        /// Deterministic service-object plan of one city zone. Pure: same seed and zone → same list.
        /// <paramref name="surface"/> maps world x/z to the top solid voxel (0 = no ground there).
        /// </summary>
        public static List<SpawnDefinition> Plan(uint seed, WorldZoneKey zone, Func<int, int, int> surface,
            int services, int startIndex)
        {
            var result = new List<SpawnDefinition>();
            if (services <= 0 || surface == null) return result;
            var all = AlphaNpcServices.All();
            if (all.Count == 0) return result;
            var region = new RegionKey((int)Math.Floor(zone.X / (double)AlphaWorldScale.ZonesPerRegion),
                (int)Math.Floor(zone.Z / (double)AlphaWorldScale.ZonesPerRegion));
            int originX = zone.X * AlphaWorldScale.ZoneVoxels;
            int originZ = zone.Z * AlphaWorldScale.ZoneVoxels;
            int center = AlphaWorldScale.ZoneVoxels / 2;
            int radius = Math.Max(8, AlphaWorldScale.ZoneVoxels / 6);
            ulong state = Mix(seed, zone.X, zone.Z);
            int index = startIndex;
            for (int i = 0; i < services; i++)
            {
                state = Next(state);
                var service = all[(int)(state % (ulong)all.Count)];
                state = Next(state);
                double angle = 2.0 * Math.PI * i / services + (state % 360UL) * Math.PI / 180.0;
                state = Next(state);
                double distance = radius * (0.5 + (state % 500UL) / 1000.0);
                int x = originX + center + (int)(Math.Cos(angle) * distance);
                int z = originZ + center + (int)(Math.Sin(angle) * distance);
                int top = surface(x, z);
                if (top <= 0) continue;   // no ground in this column: the object is not placed
                result.Add(new SpawnDefinition
                {
                    Region = region,
                    SpawnIndex = index++,
                    Kind = new KindId(service.ObjectId),
                    Position = new WorldPosition(x, top + 1, z),
                    Yaw = (float)(state % 360UL),
                    Faction = (int)CreatureFaction.Passive,
                    Level = 1,
                    ClassId = -1,
                    BehaviourKey = ServiceBehaviourKey,
                    Provenance = ServiceProvenance + $" [{service.ObjectName} -> {service.Kind}]",
                });
            }

            return result;
        }

        static ulong Mix(uint seed, int x, int z)
        {
            ulong h = (ulong)seed * 0x9E3779B97F4A7C15UL;
            h ^= (ulong)(uint)x * 0xC2B2AE3D27D4EB4FUL;
            h ^= (ulong)(uint)z * 0x27D4EB2F165667C5UL;
            return h == 0UL ? 1UL : h;
        }

        static ulong Next(ulong state)
        {
            state ^= state >> 33;
            state *= 0xFF51AFD7ED558CCDUL;
            state ^= state >> 29;
            return state == 0UL ? 1UL : state;
        }
    }
}
