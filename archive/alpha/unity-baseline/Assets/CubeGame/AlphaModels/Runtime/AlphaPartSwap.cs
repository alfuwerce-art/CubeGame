// CubeGame.AlphaModels — replace the model of one rig part (M0-F2A hero appearance).
//
// Alpha draws every humanoid block as drawModel(model, anchor) under the part matrix; which model a block
// uses is creature data (head/hair = the face/haircut chosen in the editor, body/hands/feet = the species
// parts). The pose does not depend on the model, so a swap changes only the mesh, the grid size (the
// "center" anchor follows the model size) and, for head/hair, the species scale of the part matrix.
// The old generated mesh is destroyed with the swap; a failed swap leaves the part untouched.
using System.IO;
using CubeGame.AlphaTransfer;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    public static class AlphaPartSwap
    {
        public static bool TrySwapFromFile(AlphaRig rig, string part, string cubdecPath, bool markerTransparent, float scale,
            out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(cubdecPath) || !File.Exists(cubdecPath))
            {
                reason = $"PART_MODEL_FILE_MISSING {part} {cubdecPath}";
                return false;
            }

            return TrySwap(rig, part, File.ReadAllBytes(cubdecPath), Path.GetFileNameWithoutExtension(cubdecPath),
                markerTransparent, scale, out reason);
        }

        public static bool TrySwap(AlphaRig rig, string part, byte[] cubdec, string name, bool markerTransparent, float scale,
            out string reason)
        {
            reason = null;
            if (rig == null) { reason = "RIG_NULL"; return false; }
            int index = rig.IndexOf(part);
            if (index < 0) { reason = "RIG_HAS_NO_PART " + part; return false; }
            if (rig.anchors[index] != "center") { reason = $"PART_ANCHOR_NOT_CENTER {part} {rig.anchors[index]}"; return false; }
            if (scale <= 0f) { reason = $"PART_SCALE_INVALID {part} {scale}"; return false; }
            var built = VoxelMesh.Build(cubdec, part + "_" + name, markerTransparent);
            if (built.problem != null || built.mesh == null)
            {
                reason = $"PART_MESH {part}: {built.problem ?? "NO_MESH"}";
                return false;
            }

            var filter = rig.meshes[index] != null ? rig.meshes[index].GetComponent<MeshFilter>() : null;
            if (filter == null)
            {
                Object.DestroyImmediate(built.mesh);
                reason = "PART_MESH_CHILD_MISSING " + part;
                return false;
            }

            var old = filter.sharedMesh;
            filter.sharedMesh = built.mesh;
            if (old != null)
            {
                if (Application.isPlaying) Object.Destroy(old);
                else Object.DestroyImmediate(old);
            }

            rig.sizes[index] = built.size;
            rig.SetPartScale(index, scale);
            return true;
        }

        /// <summary>Name of the mesh currently drawn by a part (tests/diagnostics).</summary>
        public static string MeshName(AlphaRig rig, string part)
        {
            int index = rig != null ? rig.IndexOf(part) : -1;
            var filter = index >= 0 && rig.meshes[index] != null ? rig.meshes[index].GetComponent<MeshFilter>() : null;
            return filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : null;
        }
    }
}
