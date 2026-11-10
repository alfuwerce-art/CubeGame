using UnityEngine;

namespace CubeGame.Presentation.Rendering
{
    /// <summary>Material access for the pilot terrain (falls back to Shader.Find if no asset assigned).</summary>
    public static class PilotMaterials
    {
        private static Material _fallback;

        public static Material Terrain(Material assigned)
        {
            if (assigned != null)
            {
                return assigned;
            }

            if (_fallback == null)
            {
                Shader shader = Shader.Find("CubeGame/PilotVertexColor");
                // Never new Material(null): a stripped shader falls back to the committed Resources shader.
                _fallback = shader != null
                    ? new Material(shader) { name = "PilotTerrainFallback" }
                    : RuntimeMaterials.Solid("PilotTerrainFallback", new Color(0.45f, 0.6f, 0.35f));
            }

            return _fallback;
        }
    }
}
