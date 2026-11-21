// CubeGame.AlphaModels — shared materials for Alpha voxel geometry (D86 M0-01).
//
// The pilot used "AlphaTransfer/VoxelShaded" found by Shader.Find. A player build only contains
// shaders that an asset references, so the Editor bootstrap (AlphaModels.Import) writes the two
// materials into Assets/CubeGame/AlphaModels/Resources and the runtime loads them from there.
// In the Editor (and in tests) the shader is found directly, so nothing has to be prepared first.
using CubeGame.AlphaTransfer;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    public static class AlphaVisualResources
    {
        public const string BaseResourceName = "AlphaVoxel";
        public const string HairResourceName = "AlphaVoxelHair";
        public const string ShaderName = "AlphaTransfer/VoxelShaded";

        static Material _base;
        static Material _hair;

        /// <summary>Opaque voxel material with Alpha's per-draw tint (D61 uniform30).</summary>
        public static Material Base
        {
            get
            {
                if (_base != null) return _base;
                _base = Resources.Load<Material>(BaseResourceName);
                if (_base == null) _base = CreateFallback(BaseResourceName, 2000);
                return _base;
            }
        }

        /// <summary>Hair material: same shading, one queue step later (Alpha draws hair after the head).</summary>
        public static Material Hair
        {
            get
            {
                if (_hair != null) return _hair;
                _hair = Resources.Load<Material>(HairResourceName);
                if (_hair == null)
                {
                    var b = Base;
                    _hair = b != null ? AlphaRig.MakeHairMaterial(b) : null;
                }
                return _hair;
            }
        }

        public static bool ShaderAvailable => Shader.Find(ShaderName) != null;

        static Material CreateFallback(string name, int queue)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) return null;
            return new Material(shader) { name = name, renderQueue = queue };
        }

        /// <summary>Drop cached materials (tests that change the content root or the shader).</summary>
        public static void Reset()
        {
            _base = null;
            _hair = null;
        }
    }
}
