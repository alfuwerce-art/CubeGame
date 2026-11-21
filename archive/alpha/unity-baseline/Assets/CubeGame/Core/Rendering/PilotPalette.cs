namespace CubeGame.Core.Rendering
{
    /// <summary>
    /// Flat colors of the pilot terrain. No textures and no Alpha assets are used:
    /// this is a debugging palette (PLACEHOLDER_NOT_ALPHA), see S01-PILOT P1.6.
    /// </summary>
    public static class PilotPalette
    {
        public static void ColorFor(int heightBlocks, float variation, out float r, out float g, out float b)
        {
            float shade = 1f - variation * 0.08f;

            if (heightBlocks < 14)
            {
                r = 0.78f;
                g = 0.72f;
                b = 0.50f;
            }
            else if (heightBlocks < 34)
            {
                r = 0.36f;
                g = 0.62f;
                b = 0.30f;
            }
            else if (heightBlocks < 56)
            {
                r = 0.33f;
                g = 0.45f;
                b = 0.28f;
            }
            else if (heightBlocks < 78)
            {
                r = 0.48f;
                g = 0.46f;
                b = 0.44f;
            }
            else
            {
                r = 0.92f;
                g = 0.94f;
                b = 0.96f;
            }

            r *= shade;
            g *= shade;
            b *= shade;
        }

        public static void RockColor(float variation, out float r, out float g, out float b)
        {
            float shade = 1f - variation * 0.06f;
            r = 0.40f * shade;
            g = 0.37f * shade;
            b = 0.34f * shade;
        }
    }
}
