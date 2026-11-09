// CubeGame.AlphaModels — held-item model attached to the right hand (D86 M0-04B).
//
// Alpha draws the held block (slot 0x1e8) as a child of the hand frame with the "center" anchor; this
// layer does exactly that with the model selected by item_icon_id (AlphaHeldItemCatalog). The game
// decides which item is held — this class only positions and draws it.
using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaTransfer;
using CubeGame.Content;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    public static class AlphaHeldItemLayer
    {
        public const string MeshChildName = "held_mesh";
        /// <summary>Grid size of the last equipped held model (mesh bounds are solid-voxel bounds, not the grid).</summary>
        public static Vector3Int LastEquippedSize { get; private set; }

        /// <summary>Equip a model by item (type, subtype); the model comes from the held-item table.</summary>
        public static bool TryEquipFromItem(AlphaRig rig, string itemsDirectory, int type, int subtype, out string reason)
        {
            reason = null;
            if (!AlphaHeldItemCatalog.TryModel(type, subtype, out var def))
            {
                reason = $"HELD_ITEM_NO_MODEL {type}/{subtype}";
                return false;
            }
            var law = AlphaHeldItemCatalog.LawOf(type);
            if (law != "center")
            {
                // Lamp (lamp_item) and pet (pet_item) blocks use their own draw laws; drawing them with
                // the held "center" law would be a silent invention, so they are refused explicitly.
                reason = $"HELD_ITEM_LAW_NOT_IMPLEMENTED {law} type={type}";
                return false;
            }
            if (string.IsNullOrEmpty(itemsDirectory))
            {
                reason = "HELD_ITEMS_DIRECTORY_MISSING";
                return false;
            }
            var path = Path.Combine(itemsDirectory, Path.ChangeExtension(def.Model, ".cubdec"));
            if (!File.Exists(path)) { reason = "HELD_ITEM_FILE_MISSING " + def.Model; return false; }
            return TryEquip(rig, File.ReadAllBytes(path), out reason);
        }

        public static bool TryEquip(AlphaRig rig, byte[] cubdec, out string reason)
        {
            reason = null;
            if (rig == null) { reason = "RIG_NULL"; return false; }
            int hand = rig.IndexOf("hand_r");
            if (hand < 0) { reason = "RIG_HAS_NO_RIGHT_HAND"; return false; }
            var built = VoxelMesh.Build(cubdec, "held", false);
            if (built.problem != null) { reason = "HELD_MESH " + built.problem; return false; }
            if (built.markerVoxels > 0) { reason = "HELD_MODEL_HAS_MARKERS"; return false; }
            var material = AlphaVisualResources.Base;
            if (material == null) { reason = "HELD_MATERIAL_MISSING"; return false; }

            Clear(rig);
            LastEquippedSize = built.size;
            var go = new GameObject(MeshChildName);
            go.transform.SetParent(rig.parts[hand], false);
            // Alpha held block law: "center" (slot 0x1e8, item_layer slots).
            go.transform.localPosition = CenterOffset(built.size);
            go.AddComponent<MeshFilter>().sharedMesh = built.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return true;
        }

        public static void Clear(AlphaRig rig)
        {
            if (rig == null) return;
            int hand = rig.IndexOf("hand_r");
            if (hand < 0) return;
            var existing = rig.parts[hand].Find(MeshChildName);
            if (existing == null) return;
            var mesh = existing.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
            existing.gameObject.name = MeshChildName + "_replaced";
            if (Application.isPlaying) UnityEngine.Object.Destroy(existing.gameObject);
            else UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        public static bool HasHeldItem(AlphaRig rig)
        {
            if (rig == null) return false;
            int hand = rig.IndexOf("hand_r");
            return hand >= 0 && rig.parts[hand].Find(MeshChildName) != null;
        }

        /// <summary>Unity offset of the held mesh under the "center" anchor (Alpha voxels → Unity).</summary>
        public static Vector3 CenterOffset(Vector3Int size) =>
            -AlphaSpace.GridToUnity(new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f));

        public static List<string> Validate()
        {
            var p = new List<string>();
            foreach (var item in AlphaHeldItemCatalog.Items)
                if (string.IsNullOrEmpty(item.Model)) p.Add($"HELD_MODEL {item.Type}/{item.Subtype}");
            return p;
        }
    }
}
