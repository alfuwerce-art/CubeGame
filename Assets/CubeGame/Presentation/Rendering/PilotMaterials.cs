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
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                _fallback = new Material(shader) { name = "PilotTerrainFallback" };
            }

            return _fallback;
        }
    }
}
