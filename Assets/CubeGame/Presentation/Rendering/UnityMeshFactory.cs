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

            int count = data.VertexCount;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var colors = new Color[count];

            for (int i = 0; i < count; i++)
            {
                int v = i * 3;
                vertices[i] = new Vector3(data.Vertices[v], data.Vertices[v + 1], data.Vertices[v + 2]);
                normals[i] = new Vector3(data.Normals[v], data.Normals[v + 1], data.Normals[v + 2]);

                int c = i * 4;
                colors[i] = new Color(data.Colors[c], data.Colors[c + 1], data.Colors[c + 2], data.Colors[c + 3]);
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = data.ToIndexArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
