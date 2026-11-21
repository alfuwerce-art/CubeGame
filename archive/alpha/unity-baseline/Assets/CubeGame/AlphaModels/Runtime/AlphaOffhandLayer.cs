// CubeGame.AlphaModels — off-hand model attached to the left hand with the measured Alpha law
// (D86 M0-04B). The off-hand block is drawn by Alpha with its own anchor (dagger/fist/bow/crossbow/
// bracelet/shield…), so it becomes a child of the posed left-hand part and inherits the animation;
// the offset is the measured `anchor = coeff*size + const`, never a guessed "hold point".
using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaTransfer;
using CubeGame.Content;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    public static class AlphaOffhandLayer
    {
        public const string MeshChildName = "offhand_mesh";

        public static bool TryEquip(AlphaRig rig, byte[] cubdec, int subtype, out string reason)
        {
            reason = null;
            if (rig == null) { reason = "RIG_NULL"; return false; }
            int hand = rig.IndexOf("hand_l");
            if (hand < 0) { reason = "RIG_HAS_NO_LEFT_HAND"; return false; }
            if (!AlphaItemRules.TryOffhandAnchors(subtype, out var set)) { reason = $"OFFHAND_SUBTYPE_UNKNOWN {subtype}"; return false; }
            if (!AlphaItemRules.IsAnchorMeasured(set.InHand)) { reason = $"OFFHAND_ANCHOR_NOT_MEASURED {subtype} {set.InHand.Law} {set.InHand.Status}"; return false; }
            var built = VoxelMesh.Build(cubdec, "offhand_" + subtype, false);
            if (built.problem != null) { reason = "OFFHAND_MESH " + built.problem; return false; }
            if (built.markerVoxels > 0) { reason = "OFFHAND_MODEL_HAS_MARKERS"; return false; }

            Clear(rig);
            var go = new GameObject(MeshChildName);
            go.transform.SetParent(rig.parts[hand], false);
            go.transform.localScale = Vector3.one * set.InHand.Scale;
            go.transform.localPosition = Offset(set.InHand, built.size);
            go.AddComponent<MeshFilter>().sharedMesh = built.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AlphaVisualResources.Base;
            if (renderer.sharedMaterial == null) { reason = "OFFHAND_MATERIAL_MISSING"; Clear(rig); return false; }
            return true;
        }

        public static bool TryEquipFromFile(AlphaRig rig, string cubdecPath, int subtype, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(cubdecPath) || !File.Exists(cubdecPath))
            {
                reason = "OFFHAND_FILE_MISSING " + cubdecPath;
                return false;
            }
            return TryEquip(rig, File.ReadAllBytes(cubdecPath), subtype, out reason);
        }

        public static void Clear(AlphaRig rig)
        {
            if (rig == null) return;
            int hand = rig.IndexOf("hand_l");
            if (hand < 0) return;
            var existing = rig.parts[hand].Find(MeshChildName);
            if (existing != null)
            {
                var mesh = existing.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                    else UnityEngine.Object.DestroyImmediate(mesh);
                }
                // Rename first: Destroy in play mode is deferred, and a second equip must not see two
                // meshes with the same name in the same frame.
                existing.gameObject.name = MeshChildName + "_replaced";
                if (Application.isPlaying) UnityEngine.Object.Destroy(existing.gameObject);
                else UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        public static bool HasOffhand(AlphaRig rig)
        {
            if (rig == null) return false;
            int hand = rig.IndexOf("hand_l");
            return hand >= 0 && rig.parts[hand].Find(MeshChildName) != null;
        }

        /// <summary>Unity local offset of the off-hand mesh for a model size (Alpha voxels → Unity).</summary>
        public static Vector3 Offset(in AlphaAnchorDef law, Vector3Int size)
        {
            law.Anchor(size.x, size.y, size.z, out var x, out var y, out var z);
            return -AlphaSpace.GridToUnity(new Vector3(x, y, z));
        }

        public static List<int> EquippableSubtypes()
        {
            var list = new List<int>();
            foreach (var s in AlphaItemRules.OffhandAnchorSets)
                if (AlphaItemRules.IsAnchorMeasured(s.InHand)) list.Add(s.Subtype);
            return list;
        }
    }
}
