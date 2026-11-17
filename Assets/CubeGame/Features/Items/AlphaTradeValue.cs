using System;
using CubeGame.Core.Progression;

namespace CubeGame.Features.Items
{
    /// <summary>ALPHA_CODE_STATIC, Cube.exe 0x4C76E0. The vendor handler uses the same unit value in both directions.</summary>
    public static class AlphaTradeValue
    {
        public static int Of(InventoryEntry item)
        {
            int type = item.Raw.Type;
            float multiplier;
            switch (type)
            {
                case 3:
                case 4:
                    multiplier = 2f;
                    break;
                case 5:
                case 6:
                case 9:
                    multiplier = 1f;
                    break;
                case 7:
                case 8:
                    multiplier = 1.5f;
                    break;
                case 0x17:
                    multiplier = 100f;
                    break;
                default:
                    multiplier = 0.2f;
                    break;
            }

            if (type == 3)
            {
                switch (item.Raw.Subtype)
                {
                    case 5: case 6: case 7: case 8: case 10: case 11:
                    case 15: case 16: case 17: case 18:
                        multiplier *= 2f;
                        break;
                }
            }

            // P5: every entry carries its Alpha record; pre-P5 raw items restore as L1, r0.
            float scale = (float)AlphaMath.LevelFactor(Math.Max(1, item.Record.Level))
                * (float)AlphaMath.StarFactor(Math.Max(0, item.Record.Rarity));
            return Math.Max(1, (int)(scale * 10f * multiplier));
        }
    }
}
