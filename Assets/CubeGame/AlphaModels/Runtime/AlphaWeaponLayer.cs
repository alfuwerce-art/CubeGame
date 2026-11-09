// CubeGame.AlphaModels — runtime weapon/off-hand model swap with the measured Alpha anchor laws
// (D86 M0-04B, M0-00B item layer). The visual layer only positions and draws the model: item
// ownership, stats and which item may be equipped stay in the game (Features/Items).
//
// Anchor laws come from the executed drawModel blocks (item_layer_evidence): anchor = coeff*size+const
// in Alpha voxel space, measured per weapon subtype (26) and off-hand class (10). A subtype without a
// measured law is refused, never approximated with the generic rule.
using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaTransfer;
using CubeGame.Content;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    public static class AlphaWeaponLayer
    {
        /// <summary>
        /// Replace the weapon model of a rig and install the measured hand/back anchors of the subtype.
        /// Which anchor is used (hand or back) follows the rig's current weapon state, which clips set.
        /// </summary>
        public static bool TryEquip(AlphaRig rig, byte[] cubdec, int subtype, out string reason)
        {
            reason = null;
            if (rig == null) { reason = "RIG_NULL"; return false; }
            int part = rig.IndexOf("weapon");
            if (part < 0) { reason = "RIG_HAS_NO_WEAPON_PART"; return false; }
            if (!AlphaItemRules.TryWeaponAnchors(subtype, out var set)) { reason = $"WEAPON_SUBTYPE_UNKNOWN {subtype}"; return false; }
            if (!AlphaItemRules.IsAnchorMeasured(set.InHand)) { reason = $"ANCHOR_NOT_MEASURED {subtype} in-hand {set.InHand.Law} {set.InHand.Status}"; return false; }
            if (!AlphaItemRules.IsAnchorMeasured(set.OnBack)) { reason = $"ANCHOR_NOT_MEASURED {subtype} on-back {set.OnBack.Law} {set.OnBack.Status}"; return false; }
            var built = VoxelMesh.Build(cubdec, "weapon_" + subtype, false);
            if (built.problem != null) { reason = "WEAPON_MESH " + built.problem; return false; }
            if (built.markerVoxels > 0) { reason = "WEAPON_MODEL_HAS_MARKERS"; return false; }

            var filter = rig.meshes[part].GetComponent<MeshFilter>();
            if (filter == null) { reason = "WEAPON_MESH_CHILD_MISSING"; return false; }
            var old = filter.sharedMesh;
            filter.sharedMesh = built.mesh;
            if (old != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(old);
                else UnityEngine.Object.DestroyImmediate(old);
            }
            rig.sizes[part] = built.size;
            rig.weaponSubtype = subtype;
            set.InHand.Anchor(built.size.x, built.size.y, built.size.z, out var ix, out var iy, out var iz);
            set.OnBack.Anchor(built.size.x, built.size.y, built.size.z, out var bx, out var by, out var bz);
            rig.weaponAnchorInHand = new Vector3(ix, iy, iz);
            rig.weaponAnchorOnBack = new Vector3(bx, by, bz);
            rig.weaponScale = set.InHand.Scale;
            rig.SetWeaponOnBack(rig.WeaponOnBack);
            return true;
        }

        public static bool TryEquipFromFile(AlphaRig rig, string cubdecPath, int subtype, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(cubdecPath) || !File.Exists(cubdecPath))
            {
                reason = "WEAPON_FILE_MISSING " + cubdecPath;
                return false;
            }
            return TryEquip(rig, File.ReadAllBytes(cubdecPath), subtype, out reason);
        }

        /// <summary>Expected Unity offset of the weapon mesh for a size under the given law (for tests/verification).</summary>
        public static Vector3 ExpectedMeshOffset(in AlphaAnchorDef law, Vector3Int size)
        {
            law.Anchor(size.x, size.y, size.z, out var x, out var y, out var z);
            return -AlphaSpace.GridToUnity(new Vector3(x, y, z));
        }

        /// <summary>Which subtypes currently have a measured in-hand law.</summary>
        public static List<int> EquippableInHand()
        {
            var list = new List<int>();
            foreach (var s in AlphaItemRules.WeaponAnchorSets)
                if (AlphaItemRules.IsAnchorMeasured(s.InHand)) list.Add(s.Subtype);
            return list;
        }
    }
}
