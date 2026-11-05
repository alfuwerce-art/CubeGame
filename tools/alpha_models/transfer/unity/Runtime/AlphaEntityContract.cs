// AlphaEntityContract.cs — runtime side of the unified entity/part/animation contract
// (tools/alpha_models/universal/contract.py, format cubegame.entity_contract/1).
// Data/contract.json is written by `pipeline prepare-unity` for every creature. The rig, the clip
// library and the controller are checked against it, without any creature name or limb count:
//   * parts: same set as the rig, known structural roles, the rig's anchor rule, symmetric mirror pairs
//   * clips: provenance and category allowed for THIS entity (a LOCOMOTION_ONLY entity has no action
//     clips; humanoid input rules are never inherited by another body plan)
//   * controller mode: ACTIONS needs an evidenced action profile + registry, LOCOMOTION_ONLY needs the
//     explicit reason why actions are blocked
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    [Serializable]
    public class AlphaEntityPart
    {
        public string name, role, mirrorOf, anchor;
        public bool mirrored;
    }

    [Serializable]
    public class AlphaImportedClip
    {
        public string name, status, category;
    }

    [Serializable]
    public class AlphaEntityContract
    {
        public const string Format = "cubegame.entity_contract/1";
        public const string ModeActions = "ACTIONS", ModeLocomotionOnly = "LOCOMOTION_ONLY";
        public static readonly string[] Provenance =
            { "TRANSFERRED_FROM_ALPHA", "EMULATED_ALPHA_CODE", "RECONSTRUCTED_FROM_EVIDENCE", "UNITY_TRANSITION", "BLOCKED" };
        public static readonly string[] ClipProvenance =
            { "TRANSFERRED_FROM_ALPHA", "EMULATED_ALPHA_CODE", "RECONSTRUCTED_FROM_EVIDENCE" };
        public static readonly string[] Categories = { "LOOP_LOCOMOTION", "STATIC_POSE", "MOTION", "HELD_STATE", "NATIVE_LIFECYCLE" };
        public static readonly string[] ActionCategories = { "MOTION", "HELD_STATE", "NATIVE_LIFECYCLE" };
        public static readonly string[] Roles =
            { "core", "head", "head_attachment", "tail", "fore_limb", "hind_limb", "mid_limb", "wing", "equipment" };

        public string format, kind, category, bodyPlan, rig, controllerMode;
        public string actionProfile, actionTrigger, actionsStatus, actionsReason;
        public string weaponLayer, transitions, readiness;
        public AlphaEntityPart[] parts;
        public string[] walkSources, idleSources, allowedClipProvenance, allowedCategories;
        public AlphaImportedClip[] importedClips;

        public bool LocomotionOnly => controllerMode == ModeLocomotionOnly;

        public static AlphaEntityContract Parse(string json, List<string> problems)
        {
            AlphaEntityContract c;
            try { c = JsonUtility.FromJson<AlphaEntityContract>(json); }
            catch (Exception e) { problems.Add("CONTRACT_JSON_INVALID " + e.Message); return null; }
            if (c == null) { problems.Add("CONTRACT_JSON_EMPTY"); return null; }
            if (c.format != Format) problems.Add($"CONTRACT_FORMAT {c.format}");
            if (c.parts == null || c.parts.Length == 0) problems.Add("CONTRACT_NO_PARTS");
            if (c.controllerMode != ModeActions && c.controllerMode != ModeLocomotionOnly)
                problems.Add($"CONTRACT_CONTROLLER_MODE_UNKNOWN {c.controllerMode}");
            if (c.controllerMode == ModeActions && (string.IsNullOrEmpty(c.actionProfile) || c.actionsStatus != "AVAILABLE"
                || string.IsNullOrEmpty(c.actionTrigger)))
                problems.Add("CONTRACT_ACTIONS_WITHOUT_EVIDENCED_PROFILE");
            if (c.controllerMode == ModeLocomotionOnly && (c.actionsStatus != "BLOCKED" || string.IsNullOrEmpty(c.actionsReason)
                || !string.IsNullOrEmpty(c.actionProfile)))
                problems.Add("CONTRACT_BLOCKED_ACTIONS_NEED_REASON");
            foreach (var s in (c.walkSources ?? new string[0]).Concat(c.idleSources ?? new string[0]))
                if (Array.IndexOf(ClipProvenance, s) < 0) problems.Add($"CONTRACT_SOURCE_PROVENANCE_UNKNOWN {s}");
            if (c.readiness == "AUTO_READY" && (c.walkSources == null || c.walkSources.Length == 0))
                problems.Add("CONTRACT_READY_WITHOUT_WALK_SOURCE");
            foreach (var s in c.allowedClipProvenance ?? new string[0])
                if (Array.IndexOf(ClipProvenance, s) < 0) problems.Add($"CONTRACT_ALLOWED_PROVENANCE_INVALID {s}");
            foreach (var s in c.allowedCategories ?? new string[0])
                if (Array.IndexOf(Categories, s) < 0) problems.Add($"CONTRACT_CATEGORY_UNKNOWN {s}");
            if (c.LocomotionOnly && (c.allowedCategories ?? new string[0]).Any(s => Array.IndexOf(ActionCategories, s) >= 0))
                problems.Add("CONTRACT_LOCOMOTION_ONLY_ALLOWS_ACTION_CATEGORY");
            if (c.transitions != "UNITY_TRANSITION") problems.Add($"CONTRACT_TRANSITIONS_PROVENANCE {c.transitions}");
            return c;
        }

        /// <summary>Playback category of a clip (same rule as contract.clip_category).</summary>
        public static string CategoryOf(AlphaClipData d)
        {
            if (d.loop && d.param == "walk_phase") return "LOOP_LOCOMOTION";
            if (d.param == "time_ms" && d.duration == 0) return "STATIC_POSE";
            return string.IsNullOrEmpty(d.category) ? "MOTION" : d.category;
        }

        public List<string> Validate(AlphaRig rig, IEnumerable<AlphaClip> clips, bool hasActionRegistry)
        {
            var problems = new List<string>();
            if (rig == null) { problems.Add("CONTRACT_NO_RIG"); return problems; }
            if (rig.rigId != this.rig) problems.Add($"CONTRACT_RIG_MISMATCH {this.rig} != {rig.rigId}");
            var names = parts.Select(p => p.name).ToArray();
            if (names.Distinct().Count() != names.Length) problems.Add("CONTRACT_DUPLICATE_PART");
            if (!new HashSet<string>(names).SetEquals(rig.partNames))
                problems.Add($"CONTRACT_PART_SET_MISMATCH [{string.Join(",", names)}] != [{string.Join(",", rig.partNames)}]");
            foreach (var p in parts)
            {
                if (Array.IndexOf(Roles, p.role) < 0) problems.Add($"CONTRACT_ROLE_UNKNOWN {p.name} {p.role}");
                int i = rig.IndexOf(p.name);
                if (i >= 0 && i < rig.anchors.Length && rig.anchors[i] != p.anchor)
                    problems.Add($"CONTRACT_ANCHOR_MISMATCH {p.name} {p.anchor} != {rig.anchors[i]}");
                if (!string.IsNullOrEmpty(p.mirrorOf))
                {
                    var m = parts.FirstOrDefault(q => q.name == p.mirrorOf);
                    if (m == null || m.mirrorOf != p.name || m.role != p.role)
                        problems.Add($"CONTRACT_MIRROR_PAIR_BROKEN {p.name}<->{p.mirrorOf}");
                }
            }
            bool hasWeapon = parts.Any(p => p.role == "equipment"); // equipment layer, by role
            if (weaponLayer == "BLOCKED") problems.Add("CONTRACT_WEAPON_LAYER_BLOCKED");
            if ((weaponLayer == "SUPPORTED") != hasWeapon) problems.Add($"CONTRACT_WEAPON_LAYER_MISMATCH {weaponLayer} weapon_part={hasWeapon}");
            if (hasActionRegistry != (controllerMode == ModeActions))
                problems.Add($"CONTRACT_ACTION_REGISTRY_MISMATCH mode={controllerMode} registry={hasActionRegistry}");
            var loaded = new Dictionary<string, AlphaClipData>();
            foreach (var clip in clips ?? Enumerable.Empty<AlphaClip>())
            {
                var d = clip.Data; loaded[d.name] = d;
                string cat = CategoryOf(d);
                if (Array.IndexOf(allowedClipProvenance ?? new string[0], d.status) < 0)
                    problems.Add($"CONTRACT_CLIP_PROVENANCE_NOT_ALLOWED {d.name} {d.status}");
                if (Array.IndexOf(allowedCategories ?? new string[0], cat) < 0)
                    problems.Add($"CONTRACT_CLIP_CATEGORY_NOT_ALLOWED {d.name} {cat}");
                if (d.rig != this.rig) problems.Add($"CONTRACT_CLIP_RIG {d.name} {d.rig}");
            }
            if (importedClips != null)
            {
                foreach (var ic in importedClips)
                {
                    if (!loaded.TryGetValue(ic.name, out var d)) { problems.Add($"CONTRACT_IMPORTED_CLIP_NOT_LOADED {ic.name}"); continue; }
                    if (d.status != ic.status || CategoryOf(d) != ic.category)
                        problems.Add($"CONTRACT_IMPORTED_CLIP_DIFFERS {ic.name} {ic.status}/{ic.category} != {d.status}/{CategoryOf(d)}");
                }
                foreach (var n in loaded.Keys)
                    if (!importedClips.Any(ic => ic.name == n)) problems.Add($"CONTRACT_CLIP_NOT_DECLARED {n}");
            }
            return problems;
        }
    }
}
