// AlphaRig.cs — a flat rig of Alpha humanoid parts under one root.
//   root
//    ├─ part_<name>   <- pose (joint frame J converted to Unity), one per rig part
//    │    └─ mesh     <- voxel mesh in grid coordinates, localPosition = -GridToUnity(anchor)
// Alpha computes every part matrix in creature space (parts are copies of the root/hand frames),
// so the rig is flat on purpose: no Unity parenting can re-introduce an offset Alpha never had.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    [Serializable]
    public class AlphaRigPartSpec
    {
        public string name;
        public string model;
        public string file;
        public int[] size;
        public string anchor;         // "center" | "weapon"
        public bool mirrored;
        public int expectedVertices;
    }

    [Serializable]
    public class AlphaRigSpec
    {
        public string rig;
        public string variant;
        public int weaponSubtype;
        public float weaponHandleMinZ, weaponHandleMaxZ;
        public AlphaRigPartSpec[] parts;
    }

    public class AlphaRig : MonoBehaviour
    {
        public const string RigId = "alpha_humanoid_v1";
        /// <summary>Universal pipeline rigs: "alpha_creature/&lt;motion family hash&gt;" (creature_rig.py).</summary>
        public const string CreatureRigPrefix = "alpha_creature/";
        public string rigId = RigId;
        public string variant;
        public int weaponSubtype = -1;
        public float weaponHandleMinZ, weaponHandleMaxZ;
        public string[] partNames = new string[0];
        public Transform[] parts = new Transform[0];
        public Transform[] meshes = new Transform[0];
        public Vector3Int[] sizes = new Vector3Int[0];
        public string[] anchors = new string[0];
        bool _weaponOnBack = true;

        public static readonly int[] TwoHandedSubtypes = { 15, 16, 17 };

        public int IndexOf(string part) => Array.IndexOf(partNames, part);

        /// <summary>Alpha anchor of a part grid (GameController.cpp draw blocks; see humanoid_rig.py).</summary>
        public static Vector3 Anchor(string rule, Vector3Int size, bool weaponOnBack)
        {
            var centre = new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
            if (rule == "center") return centre;
            // tail block (GameController.cpp:6212-6219): translate(-sx/2, -sy, -sz/2)
            if (rule == "tail") return new Vector3(size.x * 0.5f, size.y, size.z * 0.5f);
            // wings (slot_9a, drawModel emulation): attached by the root edge
            if (rule == "wing") return new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            if (rule == "weapon") return weaponOnBack ? centre : new Vector3(size.x * 0.5f, size.y * 0.5f, 4f);
            throw new ArgumentException($"unknown anchor rule {rule}");
        }

        public Vector3 AnchorOf(int part) => Anchor(anchors[part], sizes[part], _weaponOnBack);

        /// <summary>Switch the weapon anchor (hand / back) — clips declare which one they need.</summary>
        public void SetWeaponOnBack(bool onBack)
        {
            _weaponOnBack = onBack;
            for (int i = 0; i < meshes.Length; i++)
                if (meshes[i] != null) meshes[i].localPosition = -AlphaSpace.GridToUnity(AnchorOf(i));
        }

        public bool WeaponOnBack => _weaponOnBack;

        bool[] _visibility;
        public bool IsVisible(int part){var r=meshes[part]?.GetComponent<Renderer>();return r!=null?r.enabled:_visibility==null||_visibility[part];}
        public void SetVisible(int part,bool visible)
        {
            if(_visibility==null||_visibility.Length!=parts.Length){_visibility=new bool[parts.Length];for(int i=0;i<_visibility.Length;i++)_visibility[i]=true;}
            _visibility[part]=visible;var r=meshes[part]?.GetComponent<Renderer>();if(r!=null)r.enabled=visible;
        }
        public void SetAllVisible(bool visible){for(int p=0;p<parts.Length;p++)SetVisible(p,visible);}

        public void Apply(PartPose[] poses, int[] clipToRig, bool[] visible=null)
        {
            for (int i = 0; i < clipToRig.Length; i++)
            {
                int r = clipToRig[i];
                if (r < 0) continue;
                SetVisible(r,visible==null||visible[i]);
                if(visible!=null&&!visible[i])continue;
                var t = parts[r];
                t.localPosition = poses[i].position;
                t.localRotation = poses[i].rotation;
                t.localScale = poses[i].scale;
            }
        }

        /// <summary>Full Alpha-space model matrix of a drawn part, read back from the scene.</summary>
        public float[] ReadAlphaModelMatrix(int part)
        {
            var rootInv = transform.worldToLocalMatrix;
            var m = rootInv * meshes[part].localToWorldMatrix;
            return AlphaSpace.ToAlpha(m);
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            int n = partNames.Length;
            if (parts.Length != n || meshes.Length != n || sizes.Length != n || anchors.Length != n)
                problems.Add("RIG_ARRAYS_INCONSISTENT");
            for (int i = 0; i < Mathf.Min(n, parts.Length); i++)
            {
                if (parts[i] == null) problems.Add($"RIG_PART_MISSING {partNames[i]}");
                else if (parts[i].parent != transform) problems.Add($"RIG_PART_NOT_FLAT {partNames[i]}");
                if (i < meshes.Length && (meshes[i] == null || meshes[i].parent != parts[i]))
                    problems.Add($"RIG_MESH_NOT_CHILD {partNames[i]}");
            }
            return problems;
        }
    }
}
