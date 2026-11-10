using System;
using UnityEngine;

namespace CubeGame.Presentation.Rendering
{
    /// <summary>
    /// Materials the game creates in code (M0 runtime recovery R1). A Player build strips every shader
    /// that no asset references, so <c>Shader.Find("Standard")</c> returns null there and
    /// <c>new Material(null)</c> renders magenta. Everything here is built on a shader committed in a
    /// Resources folder, loaded by asset reference; a missing shader is an error, never a null material.
    /// </summary>
    public static class RuntimeMaterials
    {
        public const string SolidShaderResource = "CubeGameSolidColor";
        public const string SolidShaderName = "CubeGame/SolidColor";

        /// <summary>Unity's internal error shader: what a null/unsupported shader is drawn with (magenta).</summary>
        public const string ErrorShaderName = "Hidden/InternalErrorShader";

        private static Shader _solid;

        public static Shader SolidShader
        {
            get
            {
                if (_solid == null) _solid = Resources.Load<Shader>(SolidShaderResource);
                if (_solid == null) _solid = Shader.Find(SolidShaderName);
                return _solid;
            }
        }

        /// <summary>A new opaque lit material of one colour. Throws when the committed shader is absent.</summary>
        public static Material Solid(string name, Color color)
        {
            Shader shader = SolidShader;
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"RUNTIME_SHADER_MISSING {SolidShaderName} (Resources/{SolidShaderResource}.shader is not in this build)");
            }

            return new Material(shader) { name = name, color = color };
        }

        /// <summary>True when a renderer drawn with this material shows Unity's magenta error colour.</summary>
        public static bool IsErrorMaterial(Material material)
        {
            if (material == null) return true;
            Shader shader = material.shader;
            return shader == null || shader.name == ErrorShaderName || !shader.isSupported;
        }
    }
}
