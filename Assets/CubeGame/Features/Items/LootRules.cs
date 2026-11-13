using CubeGame.Features.Contracts;

namespace CubeGame.Features.Items
{
    /// <summary>
    /// Shared Alpha item ids used by inventory, AI and UI. The former CubeGame placeholder creature loot table
    /// (PLACEHOLDER_NOT_ALPHA, M0-04C) was removed in P5: kills, chests and mission rewards now use the
    /// recovered Alpha routines in <see cref="AlphaLoot"/>.
    /// </summary>
    public static class LootRules
    {
        /// <summary>Alpha life potion (type 1, subtype 1).</summary>
        public static readonly RawAlphaItemId LifePotion = new RawAlphaItemId(1, 1);
    }
}
