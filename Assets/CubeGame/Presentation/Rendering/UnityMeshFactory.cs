using CubeGame.Core.Rendering;
using UnityEngine;

namespace CubeGame.Presentation.Rendering
{
    /// <summary>Uploads engine-independent Core mesh data into a UnityEngine.Mesh.</summary>
    public static class UnityMeshFactory
    {
        public static Mesh Create(MeshData data, string name)
        {
            var mesh = new Mesh { name = name };
            if (data.VertexCount > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = data.ToVertexArray();
            mesh.normals = data.ToNormalArray();
            mesh.colors = data.ToColorArray();
            mesh.triangles = data.ToIndexArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
