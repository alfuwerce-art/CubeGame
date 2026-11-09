// VoxelMesh.cs — builds a Unity Mesh from a decoded .cub payload (.cubdec).
// .cubdec = int32 width (x), int32 depth (y), int32 height (z), then width*depth*height RGB
// triples, index ((z * depth + y) * width + x); (0,0,0) = empty.
// Vertices stay in GRID coordinates: voxel (x, y, z) -> Unity (x, z, y), so the grid origin is
// the mesh origin and Alpha's anchor offsets apply unchanged. Only faces between a solid and an
// empty voxel are emitted — the same rule Alpha uses: our vertex count equals the vertex count of
// Alpha's own mesh object (mesh+0x3c), which the verifier checks per part.
// D61: models Alpha loads with flag 0 (hair, eyes, "-random" items; model+0x55 = 0) treat exact
// (255,0,0)/(0,255,0)/(0,0,255) voxels as transparent markers (match_transparent_color 0x4E71D0,
// executed on the original bytes). Neighbour faces are still tested against (0,0,0) only, so faces
// next to a marker are not emitted (captured hair mesh: 2244 vertices = this rule).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    public static class VoxelMesh
    {
        static readonly int[][] FaceNormals =
        {
            new[] { 1, 0, 0 }, new[] { -1, 0, 0 },
            new[] { 0, 1, 0 }, new[] { 0, -1, 0 },
            new[] { 0, 0, 1 }, new[] { 0, 0, -1 },
        };

        static readonly int[][][] FaceCorners =
        {
            new[] { new[]{1,0,0}, new[]{1,1,0}, new[]{1,1,1}, new[]{1,0,1} },   // +X
            new[] { new[]{0,0,1}, new[]{0,1,1}, new[]{0,1,0}, new[]{0,0,0} },   // -X
            new[] { new[]{0,1,0}, new[]{0,1,1}, new[]{1,1,1}, new[]{1,1,0} },   // +Y (up)
            new[] { new[]{0,0,0}, new[]{1,0,0}, new[]{1,0,1}, new[]{0,0,1} },   // -Y
            new[] { new[]{0,0,1}, new[]{1,0,1}, new[]{1,1,1}, new[]{0,1,1} },   // +Z (forward)
            new[] { new[]{1,0,0}, new[]{0,0,0}, new[]{0,1,0}, new[]{1,1,0} },   // -Z
        };

        public struct Result
        {
            public Mesh mesh;
            public Vector3Int size;      // Alpha grid size (x, y=depth, z=height)
            public int solidVoxels;
            public int markerVoxels;     // skipped as transparent markers (markerTransparent only)
            public int faces;
            public string problem;
        }

        public static Result Build(byte[] bytes, string name) => Build(bytes, name, false);

        public static bool IsMarker(byte r, byte g, byte b) =>
            (r == 255 && g == 0 && b == 0) || (r == 0 && g == 255 && b == 0) || (r == 0 && g == 0 && b == 255);

        public static Result Build(byte[] bytes, string name, bool markerTransparent)
        {
            var r = new Result();
            if (bytes == null || bytes.Length < 12) { r.problem = "CUBDEC_TOO_SHORT"; return r; }
            int w = BitConverter.ToInt32(bytes, 0), d = BitConverter.ToInt32(bytes, 4), h = BitConverter.ToInt32(bytes, 8);
            r.size = new Vector3Int(w, d, h);
            if (w <= 0 || d <= 0 || h <= 0 || w > 256 || d > 256 || h > 256) { r.problem = $"CUBDEC_BAD_SIZE {w}x{d}x{h}"; return r; }
            if (bytes.Length != 12 + w * d * h * 3) { r.problem = $"CUBDEC_LENGTH {bytes.Length} != {12 + w * d * h * 3}"; return r; }

            bool Solid(int x, int y, int z)
            {
                if (x < 0 || x >= w || y < 0 || y >= d || z < 0 || z >= h) return false;
                int o = 12 + ((z * d + y) * w + x) * 3;
                return bytes[o] != 0 || bytes[o + 1] != 0 || bytes[o + 2] != 0;
            }

            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var cols = new List<Color32>(); var tris = new List<int>();
            for (int z = 0; z < h; z++)
            for (int y = 0; y < d; y++)
            for (int x = 0; x < w; x++)
            {
                if (!Solid(x, y, z)) continue;
                int o = 12 + ((z * d + y) * w + x) * 3;
                if (markerTransparent && IsMarker(bytes[o], bytes[o + 1], bytes[o + 2])) { r.markerVoxels++; continue; }
                r.solidVoxels++;
                var c = new Color32(bytes[o], bytes[o + 1], bytes[o + 2], 255);
                for (int f = 0; f < 6; f++)
                {
                    var n = FaceNormals[f];                 // Unity order (x, up, forward)
                    if (Solid(x + n[0], y + n[2], z + n[1])) continue;
                    r.faces++;
                    int b = verts.Count;
                    var k = FaceCorners[f];
                    for (int i = 0; i < 4; i++)
                    {
                        verts.Add(new Vector3(x + k[i][0], z + k[i][1], y + k[i][2]));
                        norms.Add(new Vector3(n[0], n[1], n[2]));
                        cols.Add(c);
                    }
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            r.mesh = mesh;
            if (r.faces == 0) r.problem = "NO_VISIBLE_FACES";
            return r;
        }
    }
}
