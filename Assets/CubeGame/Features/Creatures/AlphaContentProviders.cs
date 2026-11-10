// CubeGame.Features.Creatures — registration of the DeepSeek content providers (D3, integration seam).
//
// The world generator runs one content stage per zone (IZoneContentProvider, see Features/World/ZoneContent.cs).
// This file is the single place the integrator calls to add the DeepSeek providers in the right order and with
// the predicates that come from Alpha data:
//   * vegetation/creatures/buildings — every zone               (AlphaZoneContentProvider)
//   * town furniture and services    — settlement zones (type 1) (AlphaCityContentProvider)
//   * dungeon rooms and chests       — caller-provided predicate (AlphaDungeonContentProvider); Alpha's dungeon
//                                      zone type is NOT established, so it is never guessed here.
using System;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>One-call registration of the DeepSeek zone content providers.</summary>
    public static class AlphaContentProviders
    {
        public const string Notes =
            "order: vegetation/creatures/buildings → city (type 1) → dungeon (caller predicate); Alpha dungeon zone type UNKNOWN";

        /// <summary>Alpha zone record type of a town. Distinct from the landmark column type.</summary>
        public const int CityZoneType = 1;

        /// <summary>Predicate for Alpha town zones (zone record type 1).</summary>
        public static bool IsCityZone(ZoneContentContext context) =>
            context != null && context.Record != null && context.Record.Type == CityZoneType;

        /// <summary>Predicate factory for a caller that knows the dungeon rule.</summary>
        public static Func<ZoneContentContext, bool> ByRecordType(int type) =>
            context => context != null && context.Record != null && context.Record.Type == type;

        /// <summary>
        /// Providers in registration order. <paramref name="isDungeonZone"/> may be null — then no dungeon
        /// provider is created (nothing is invented about which zones hold dungeons).
        /// </summary>
        public static IZoneContentProvider[] CreateDefault(
            Func<ZoneContentContext, bool> isDungeonZone = null,
            ContentPlanSettings settings = null)
        {
            if (isDungeonZone == null)
            {
                return new IZoneContentProvider[]
                {
                    new AlphaZoneContentProvider(settings),
                    new AlphaCityContentProvider(IsCityZone),
                };
            }

            return new IZoneContentProvider[]
            {
                new AlphaZoneContentProvider(settings),
                new AlphaCityContentProvider(IsCityZone),
                new AlphaDungeonContentProvider(isDungeonZone),
            };
        }

        /// <summary>
        /// Registers the providers in <see cref="ZoneContentRegistry"/> and returns how many were added.
        /// The world generator id must change when the provider set changes (documented in the contract).
        /// </summary>
        public static int RegisterDefaults(Func<ZoneContentContext, bool> isDungeonZone = null,
            ContentPlanSettings settings = null)
        {
            IZoneContentProvider[] providers = CreateDefault(isDungeonZone, settings);
            for (int i = 0; i < providers.Length; i++) ZoneContentRegistry.Register(providers[i]);
            return providers.Length;
        }
    }
}
