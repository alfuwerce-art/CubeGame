// CubeGame.Features.Creatures — registration of the DeepSeek content providers (D3, integration seam).
//
// The world generator runs one content stage per zone (IZoneContentProvider, see Features/World/ZoneContent.cs).
// This file is the single place the integrator calls to add the DeepSeek providers in the right order and with
// the predicates that come from Alpha data:
//   * vegetation/creatures/buildings — every zone               (AlphaZoneContentProvider)
//   * town furniture and services    — town records (type 1) (AlphaCityContentProvider)
//   * dungeon rooms and chests       — records 3 and 5 in the game route (AlphaDungeonContentProvider).
using System;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>One-call registration of the DeepSeek zone content providers.</summary>
    public static class AlphaContentProviders
    {
        public const string Notes =
            "order: W-18/buildings → city (record 1) → dungeon (caller predicate; Alpha call uses records 3 and 5)";

        /// <summary>Alpha zone record type of a town. Distinct from the landmark column type.</summary>
        public const int CityZoneType = AlphaZoneRecord.Town;

        /// <summary>Predicate for Alpha town zones (zone record type 1).</summary>
        public static bool IsCityZone(ZoneContentContext context) =>
            context != null && context.Record != null && context.Record.Type == CityZoneType;

        /// <summary>Record byte tested at 0x5E84A2/0x5E84D4 before calls to 0x5CC510.</summary>
        public static bool IsDungeonRecord(int type) => type == AlphaZoneRecord.Type3 || type == AlphaZoneRecord.Type5;

        /// <summary>Predicate factory for a caller that knows the dungeon rule.</summary>
        public static Func<ZoneContentContext, bool> ByRecordType(int type) =>
            context => context != null && context.Record != null && context.Record.Type == type;

        /// <summary>
        /// Providers in registration order. <paramref name="isDungeonZone"/> may be null — then no dungeon
        /// provider is created (nothing is invented about which zones hold dungeons).
        /// </summary>
        public static IZoneContentProvider[] CreateDefault(
            Func<ZoneContentContext, bool> isDungeonZone = null,
            ContentPlanSettings settings = null,
            Func<ZoneContentContext, bool> allowsStructures = null)
        {
            if (isDungeonZone == null)
            {
                return new IZoneContentProvider[]
                {
                    new AlphaZoneContentProvider(settings, allowsStructures),
                    new AlphaCityContentProvider(IsCityZone),
                };
            }

            return new IZoneContentProvider[]
            {
                new AlphaZoneContentProvider(settings, allowsStructures),
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
