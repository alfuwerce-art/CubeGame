using CubeGame.Features.Contracts;

namespace CubeGame.Features.Items
{
    /// <summary>ALPHA_CODE_STATIC: potion (1,1) uses one Heartflower and one Water Flask (0x59D3D0–0x59D409).</summary>
    public static class AlphaLifePotionRecipe
    {
        public static readonly RawAlphaItemId Heartflower = new RawAlphaItemId(0x0B, 0x16);
        public static readonly RawAlphaItemId WaterFlask = new RawAlphaItemId(0x0B, 0x1A);
    }
}
