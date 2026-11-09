// CubeGame.AlphaModels — armour swap layer (D86 M0-04B).
//
// item_layer evidence: chest swaps the body block, hands swap hand_l/hand_r, feet swap foot_l/foot_r,
// each with the "center" anchor. The armour mesh is drawn as a child of the posed part with the centre
// offset, and the original part renderer is disabled; Unequip restores the original state. The shoulder
// item block has no part in the prepared humanoid rig, so that slot is refused, not approximated.
using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaTransfer;
using CubeGame.Content;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    public sealed class AlphaEquipmentState : MonoBehaviour
    {
        public readonly List<Part> Parts = new List<Part>();
        public struct Part
        {
            public string Name;
            public GameObject Child;
            public Mesh OriginalMesh;
            public bool OriginalEnabled;
            public Vector3 OriginalLocalPosition;
        }
    }

    public static class AlphaEquipmentLayer
    {
        public const string ChildName = "equip_swap";
        /// <summary>Grid size of the last swapped armour model (mesh bounds are solid-voxel bounds).</summary>
        public static Vector3Int LastEquippedSize { get; private set; }

        public static bool TryEquipFromItem(AlphaRig rig, string itemsDirectory, int type, int subtype, out string reason)
        {
            reason = null;
            if (!AlphaEquipmentCatalog.TryModel(type, subtype, out var def))
            {
                reason = $"EQUIPMENT_NO_MODEL {type}/{subtype}";
                return false;
            }
            if (AlphaEquipmentCatalog.SlotOf(type) == EquipmentSlot.Shoulder)
            {
                reason = "EQUIPMENT_SHOULDER_BLOCK_NOT_IN_HUMAN_RIG";
                return false;
            }
            if (string.IsNullOrEmpty(itemsDirectory)) { reason = "EQUIPMENT_ITEMS_DIRECTORY_MISSING"; return false; }
            var path = Path.Combine(itemsDirectory, Path.ChangeExtension(def.Model, ".cubdec"));
            if (!File.Exists(path)) { reason = "EQUIPMENT_FILE_MISSING " + def.Model; return false; }
            return TryEquip(rig, File.ReadAllBytes(path), type, out reason);
        }

        public static bool TryEquipFromFile(AlphaRig rig, string cubdecPath, int type, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(cubdecPath) || !File.Exists(cubdecPath))
            {
                reason = "EQUIPMENT_FILE_MISSING";
                return false;
            }
            if (AlphaEquipmentCatalog.SlotOf(type) == EquipmentSlot.Shoulder)
            {
                reason = "EQUIPMENT_SHOULDER_BLOCK_NOT_IN_HUMAN_RIG";
                return false;
            }
            return TryEquip(rig, File.ReadAllBytes(cubdecPath), type, out reason);
        }

        public static bool TryEquip(AlphaRig rig, byte[] cubdec, int type, out string reason)
        {
            reason = null;
            if (rig == null) { reason = "RIG_NULL"; return false; }
            var parts = PartsFor(AlphaEquipmentCatalog.SlotOf(type));
            if (parts == null || parts.Count == 0)
            {
                reason = "EQUIPMENT_SLOT_WITHOUT_SWAP_PART " + type;
                return false;
            }
            var built = VoxelMesh.Build(cubdec, "equip", false);
            if (built.problem != null) { reason = "EQUIPMENT_MESH " + built.problem; return false; }
            if (built.markerVoxels > 0) { reason = "EQUIPMENT_MODEL_HAS_MARKERS"; return false; }
            var material = AlphaVisualResources.Base;
            if (material == null) { reason = "EQUIPMENT_MATERIAL_MISSING"; return false; }

            UnequipSlot(rig, type);
            LastEquippedSize = built.size;
            var state = rig.GetComponent<AlphaEquipmentState>();
            if (state == null) state = rig.gameObject.AddComponent<AlphaEquipmentState>();
            foreach (var partName in parts)
            {
                int index = rig.IndexOf(partName);
                if (index < 0) { reason = "EQUIPMENT_RIG_HAS_NO_PART " + partName; return false; }
                var draw = rig.meshes[index];
                var renderer = draw.GetComponent<MeshRenderer>();
                var child = new GameObject(ChildName);
                child.transform.SetParent(draw, false);
                child.transform.localPosition = CenterOffset(built.size);
                child.AddComponent<MeshFilter>().sharedMesh = built.mesh;
                var childRenderer = child.AddComponent<MeshRenderer>();
                childRenderer.sharedMaterial = material;
                state.Parts.Add(new AlphaEquipmentState.Part
                {
                    Name = partName,
                    Child = child,
                    OriginalEnabled = renderer != null && renderer.enabled,
                    OriginalLocalPosition = child.transform.localPosition,
                });
                if (renderer != null) renderer.enabled = false;
            }
            return true;
        }

        public static void Unequip(AlphaRig rig)
        {
            var state = rig != null ? rig.GetComponent<AlphaEquipmentState>() : null;
            if (state == null) return;
            foreach (var part in state.Parts) Restore(part, rig);
            state.Parts.Clear();
        }

        public static void UnequipSlot(AlphaRig rig, int type)
        {
            var state = rig != null ? rig.GetComponent<AlphaEquipmentState>() : null;
            if (state == null) return;
            var wanted = PartsFor(AlphaEquipmentCatalog.SlotOf(type));
            if (wanted == null) return;
            for (int i = state.Parts.Count - 1; i >= 0; i--)
            {
                if (!wanted.Contains(state.Parts[i].Name)) continue;
                Restore(state.Parts[i], rig);
                state.Parts.RemoveAt(i);
            }
        }

        static void Restore(AlphaEquipmentState.Part part, AlphaRig rig)
        {
            if (part.Child != null)
            {
                var mesh = part.Child.GetComponent<MeshFilter>() != null
                    ? part.Child.GetComponent<MeshFilter>().sharedMesh : null;
                if (mesh != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                    else UnityEngine.Object.DestroyImmediate(mesh);
                }
                part.Child.gameObject.name = ChildName + "_replaced";
                if (Application.isPlaying) UnityEngine.Object.Destroy(part.Child.gameObject);
                else UnityEngine.Object.DestroyImmediate(part.Child.gameObject);
            }
            int index = rig != null ? rig.IndexOf(part.Name) : -1;
            if (index < 0) return;
            var renderer = rig.meshes[index].GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = part.OriginalEnabled;
            // The original mesh is untouched: the swap only hides its renderer.
        }

        public static bool HasEquipment(AlphaRig rig, int type)
        {
            var state = rig != null ? rig.GetComponent<AlphaEquipmentState>() : null;
            var wanted = PartsFor(AlphaEquipmentCatalog.SlotOf(type));
            if (state == null || wanted == null) return false;
            foreach (var part in state.Parts)
                if (wanted.Contains(part.Name) && part.Child != null) return true;
            return false;
        }

        static List<string> PartsFor(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Chest: return new List<string> { "body" };
                case EquipmentSlot.Hands: return new List<string> { "hand_l", "hand_r" };
                case EquipmentSlot.Feet: return new List<string> { "foot_l", "foot_r" };
                default: return null;
            }
        }

        /// <summary>Unity offset of the swapped mesh under the "center" anchor (Alpha voxels → Unity).</summary>
        public static Vector3 CenterOffset(Vector3Int size) =>
            -AlphaSpace.GridToUnity(new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f));
    }
}
