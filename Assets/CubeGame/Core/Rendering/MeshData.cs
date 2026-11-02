using System;
using System.Collections.Generic;

namespace CubeGame.Core.Rendering
{
    /// <summary>
    /// Engine-independent mesh description. Core produces it, the presentation layer uploads it.
    /// Keeping this type free of UnityEngine is what lets EditMode tests verify the geometry
    /// without entering play mode (S01-PILOT P2, determinism cases).
    /// </summary>
    public sealed class MeshData
    {
        private readonly List<float> _vertices = new List<float>();
        private readonly List<float> _normals = new List<float>();
        private readonly List<float> _colors = new List<float>();
        private readonly List<int> _indices = new List<int>();

        public IReadOnlyList<float> Vertices => _vertices;
        public IReadOnlyList<float> Normals => _normals;
        public IReadOnlyList<float> Colors => _colors;
        public IReadOnlyList<int> Indices => _indices;

        public int VertexCount => _vertices.Count / 3;
        public int IndexCount => _indices.Count;

        public void AddVertex(float x, float y, float z, float nx, float ny, float nz, float r, float g, float b, float a)
        {
            _vertices.Add(x);
            _vertices.Add(y);
            _vertices.Add(z);
            _normals.Add(nx);
            _normals.Add(ny);
            _normals.Add(nz);
            _colors.Add(r);
            _colors.Add(g);
            _colors.Add(b);
            _colors.Add(a);
        }

        public void AddTriangle(int a, int b, int c)
        {
            _indices.Add(a);
            _indices.Add(b);
            _indices.Add(c);
        }

        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        /// <summary>Deterministic 64-bit fingerprint of the geometry, used by the tests.</summary>
        public ulong ComputeFingerprint()
        {
            ulong hash = 1469598103934665603UL;
            unchecked
            {
                for (int i = 0; i < _vertices.Count; i++)
                {
                    hash ^= (ulong)BitConverter.SingleToInt32Bits(_vertices[i]);
                    hash *= 1099511628211UL;
                }

                for (int i = 0; i < _normals.Count; i++)
                {
                    hash ^= (ulong)BitConverter.SingleToInt32Bits(_normals[i]);
                    hash *= 1099511628211UL;
                }

                for (int i = 0; i < _indices.Count; i++)
                {
                    hash ^= (ulong)_indices[i];
                    hash *= 1099511628211UL;
                }
            }

            return hash;
        }

        public float[] ToVertexArray() => _vertices.ToArray();
        public float[] ToNormalArray() => _normals.ToArray();
        public float[] ToColorArray() => _colors.ToArray();
        public int[] ToIndexArray() => _indices.ToArray();
    }
}
