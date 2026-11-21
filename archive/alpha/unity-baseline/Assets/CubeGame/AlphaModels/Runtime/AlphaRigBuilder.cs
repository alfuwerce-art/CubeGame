// CubeGame.AlphaModels — builds a playable actor from an Alpha actor definition (D86 M0-01).
//
// Same flat rig and mesh rule as the accepted S05 pilot (AlphaRig/VoxelMesh), but the data is read
// from the local content cache at runtime instead of Assets/AlphaTransfer/Data, so the same code
// serves the editor, a player build and the tests. Verified numbers are not weakened: a part whose
// Unity vertex count differs from the manifest's expectedVertices is reported as a problem.
using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaTransfer;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    public static class AlphaRigBuilder
    {
        /// <summary>One Alpha voxel = 0.1 Unity units (S05A-1 accepted root scale).</summary>
        public const float RootScale = 0.1f;

        /// <summary>Build the rig + clip player (+ locomotion driver) into an existing GameObject.</summary>
        public static AlphaRig BuildInto(GameObject go, AlphaActorDefinition def, List<string> problems)
        {
            if (go == null) { problems.Add("RIG_NO_TARGET"); return null; }
            if (def == null || !def.Ready)
            {
                problems.Add("RIG_DEFINITION_NOT_READY");
                if (def != null) problems.AddRange(def.Problems);
                return null;
            }
            go.transform.localScale = Vector3.one * RootScale;
            var rig = go.GetComponent<AlphaRig>();
            if (rig == null) rig = go.AddComponent<AlphaRig>();
            var spec = def.Rig;
            rig.rigId = spec.rig;
            rig.variant = spec.variant;
            rig.weaponSubtype = spec.weaponSubtype;
            rig.weaponHandleMinZ = spec.weaponHandleMinZ;
            rig.weaponHandleMaxZ = spec.weaponHandleMaxZ;

            int n = spec.parts.Length;
            rig.partNames = new string[n]; rig.parts = new Transform[n]; rig.meshes = new Transform[n];
            rig.sizes = new Vector3Int[n]; rig.anchors = new string[n];
            var baseMat = AlphaVisualResources.Base;
            var hairMat = AlphaVisualResources.Hair;
            if (baseMat == null) problems.Add("ALPHA_MATERIAL_MISSING " + AlphaVisualResources.ShaderName);

            for (int i = 0; i < n; i++)
            {
                var ps = spec.parts[i];
                var path = Path.Combine(def.Directory, ps.file);
                var bytes = File.ReadAllBytes(path);
                var built = VoxelMesh.Build(bytes, ps.name + "_" + Path.GetFileNameWithoutExtension(ps.model), ps.markerTransparent);
                bool alphaEmptyPart = built.problem == "NO_VISIBLE_FACES" && ps.expectedVertices == 0;
                if (built.problem != null && !alphaEmptyPart) problems.Add($"RIG_MESH {ps.name}: {built.problem}");
                if (built.size != new Vector3Int(ps.size[0], ps.size[1], ps.size[2]))
                    problems.Add($"RIG_MESH_SIZE {ps.name} {built.size} != spec");
                if (built.mesh != null && ps.expectedVertices > 0 && built.mesh.vertexCount != ps.expectedVertices)
                    problems.Add($"RIG_VERTEX_COUNT {ps.name} {built.mesh.vertexCount} != {ps.expectedVertices}");

                var part = new GameObject("part_" + ps.name).transform;
                part.SetParent(go.transform, false);
                var meshT = new GameObject("mesh").transform;
                meshT.SetParent(part, false);
                var filter = meshT.gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = built.mesh;
                var renderer = meshT.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = AlphaRig.MaterialFor(ps.name, baseMat, hairMat);
                rig.partNames[i] = ps.name; rig.parts[i] = part; rig.meshes[i] = meshT;
                rig.sizes[i] = built.size; rig.anchors[i] = ps.anchor;
                if (ps.HasTint) rig.SetTint(i, ps.TintColor);
            }
            rig.SetWeaponOnBack(true);
            problems.AddRange(rig.Validate());

            var player = go.GetComponent<AlphaClipPlayer>();
            if (player == null) player = go.AddComponent<AlphaClipPlayer>();
            player.clipAssets = Array.Empty<TextAsset>();
            player.SetClipJson(def.ClipJsonTexts.ToArray());

            if (!string.IsNullOrEmpty(def.LocomotionJson))
            {
                var driver = go.GetComponent<AlphaLocomotionDriver>();
                if (driver == null) driver = go.AddComponent<AlphaLocomotionDriver>();
                driver.SetSpecJson(def.LocomotionJson);
            }

            if (def.HasContract)
            {
                var contract = def.Contract.Validate(rig, def.Clips, def.HasActionLibrary);
                problems.AddRange(contract);
            }
            return rig;
        }
    }
}
