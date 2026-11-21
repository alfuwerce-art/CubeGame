// AlphaSpace.cs — the ONE place where Alpha matrices become Unity matrices.
//
// Alpha 2013 (Direct3D 9) uses the row-vector convention: worldRow = vRow * A, rows 0..2 are the
// basis axes, row 3 the translation; axes X right, Y forward, Z up; the .cub grid stores (x, y, z)
// with z = height. Our voxel meshes put a .cub voxel (x, y, z) at Unity (x, z, y) (see VoxelMesh),
// so the change of basis is C = swap(Y, Z). For column-vector Unity matrices:
//
//     U = C * transpose(A) * C        i.e.  U[r, c] = A[sigma(c) * 4 + sigma(r)],  sigma = (0, 2, 1, 3)
//
// C is a reflection, so U has the same determinant sign as A: a mirrored Alpha part (the right
// hand is drawn with scale(-s, s, s)) stays mirrored in Unity. Decompose() keeps that sign on X.
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    public static class AlphaSpace
    {
        static readonly int[] Sigma = { 0, 2, 1, 3 };

        public static Matrix4x4 ToUnity(float[] a, int offset = 0)
        {
            var u = new Matrix4x4();
            for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                u[r, c] = a[offset + Sigma[c] * 4 + Sigma[r]];
            return u;
        }

        public static float[] ToAlpha(Matrix4x4 u)
        {
            var a = new float[16];
            for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                a[Sigma[c] * 4 + Sigma[r]] = u[r, c];
            return a;
        }

        /// <summary>Alpha grid point (x, y, z) -> Unity local point.</summary>
        public static Vector3 GridToUnity(Vector3 alpha) => new Vector3(alpha.x, alpha.z, alpha.y);

        /// <summary>TRS decomposition; a negative determinant is carried by a negative X scale.</summary>
        public static bool Decompose(Matrix4x4 m, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = m.GetColumn(3);
            Vector3 cx = m.GetColumn(0), cy = m.GetColumn(1), cz = m.GetColumn(2);
            scale = new Vector3(cx.magnitude, cy.magnitude, cz.magnitude);
            rotation = Quaternion.identity;
            if (scale.x < 1e-8f || scale.y < 1e-8f || scale.z < 1e-8f) return false;
            float det = Vector3.Dot(Vector3.Cross(cx, cy), cz);
            if (det < 0) { scale.x = -scale.x; cx = -cx; }
            cx /= Mathf.Abs(scale.x); cy /= scale.y; cz /= scale.z;
            // reject shear: the basis must be orthonormal after removing scale
            if (Mathf.Abs(Vector3.Dot(cx, cy)) > 1e-3f || Mathf.Abs(Vector3.Dot(cx, cz)) > 1e-3f ||
                Mathf.Abs(Vector3.Dot(cy, cz)) > 1e-3f) return false;
            rotation = Quaternion.LookRotation(cz, cy);
            return true;
        }

        public static float MaxAbsDiff(float[] a, float[] b)
        {
            float d = 0f;
            for (int i = 0; i < 16; i++) d = Mathf.Max(d, Mathf.Abs(a[i] - b[i]));
            return d;
        }

        /// <summary>Row-vector Alpha product a*b (apply a first).</summary>
        public static float[] Mul(float[] a, float[] b)
        {
            var o = new float[16];
            for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
            {
                float s = 0f;
                for (int k = 0; k < 4; k++) s += a[r * 4 + k] * b[k * 4 + c];
                o[r * 4 + c] = s;
            }
            return o;
        }

        public static float[] Translate(Vector3 t) => new float[]
            { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, t.x, t.y, t.z, 1 };
    }
}
