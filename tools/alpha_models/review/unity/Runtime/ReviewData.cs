// ReviewData.cs — index + per-specimen data of the S05B visual review scene.
// All data is produced by tools/alpha_models/review/build_review.py from the existing universal
// pipeline (prepare_unity) and lives in Assets/StreamingAssets/Review (local .scratch project only).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CubeGame.AlphaTransfer.Review
{
    [Serializable] public class ReviewClipInfo { public string name, status, category; }

    [Serializable]
    public class ReviewSpecimenInfo
    {
        public string id, kind, name, source, job, prepare, family, controllerMode, actionProfile,
            actionsStatus, actionsReason, trigger, liveCheck, authorization;
        public int[] seed;
        public string[] problems = new string[0], skippedClips = new string[0], blockedExamples = new string[0];
        public ReviewClipInfo[] clips = new ReviewClipInfo[0];
        public string SeedLabel => seed != null && seed.Length == 2 ? $"s{seed[0]}-{seed[1]}" : "s?";
        public bool IsPlayerLibrary => id != null && id.EndsWith("_player");
        public bool IsCaptureSetup => id != null && id.EndsWith("_capture");
        public bool IsBaseVariant => !IsPlayerLibrary && !IsCaptureSetup;
        public string VariantLabel => SeedLabel + (IsPlayerLibrary ? " + библиотека действий игрока (двуручный меч)"
            : IsCaptureSetup ? " + оружие живого захвата (булава): клипы TRANSFERRED" : "");
    }

    [Serializable]
    public class ReviewIndex
    {
        public string format;
        public string[] kinds = new string[0];
        public ReviewSpecimenInfo[] specimens = new ReviewSpecimenInfo[0];
        public string[] notes = new string[0];
    }

    [Serializable] public class ReviewContractPart { public string name, role, mirrorOf, anchor; public bool mirrored; }

    [Serializable]
    public class ReviewContract
    {
        public string kind, category, bodyPlan, rig, controllerMode, actionProfile, actionTrigger, actionsStatus,
            actionsReason, weaponLayer, transitions, readiness;
        public ReviewContractPart[] parts = new ReviewContractPart[0];
        public string[] walkSources = new string[0], idleSources = new string[0];
    }

    [Serializable] public class ReviewBlockedAction { public int action; public string clip, status, reason; }

    [Serializable]
    public class ReviewActions
    {
        public string[] requiredClips = new string[0];
        public ReviewBlockedAction[] blockedActions = new ReviewBlockedAction[0];
    }

    /// <summary>Capture header of a TRANSFERRED_FROM_ALPHA clip (provenance JSON).</summary>
    [Serializable] public class ReviewCaptureInfo { public string kind, capture; public int frames_used, cycles; }

    public enum ReviewMode { Idle, Walk, Action }

    /// <summary>One playable source of a mode: an imported clip or the live parametric walk formula.</summary>
    public class ReviewSource
    {
        public string label, status, clip, category = "";
        public bool formula;
        public float amp;
        public int actionCode = -1;
    }

    public static class ReviewData
    {
        public const string Format = "cubegame.visual_review/1";
        public const string FormulaStatus = "RECONSTRUCTED_FROM_EVIDENCE";
        public static string Root => Path.Combine(Application.streamingAssetsPath, "Review");

        public static ReviewIndex LoadIndex(List<string> problems)
        {
            var path = Path.Combine(Root, "index.json");
            if (!File.Exists(path)) { problems.Add($"REVIEW_INDEX_MISSING {path} (run setup_review.ps1)"); return null; }
            var idx = JsonUtility.FromJson<ReviewIndex>(File.ReadAllText(path));
            if (idx == null || idx.format != Format) { problems.Add($"REVIEW_INDEX_FORMAT {idx?.format}"); return null; }
            foreach (var s in idx.specimens)
                if (s.prepare != "PASS") problems.Add($"SPECIMEN_PREPARE_FAIL {s.id}: {string.Join("; ", s.problems)}");
            return idx;
        }

        /// <summary>Pure (255,0,0) / (0,255,0) / (0,0,255) voxels of a .cubdec — the colours Alpha's loader
        /// collects as marker lists (VoxelModel_set_data). Shown raw: tint is NOT applied here.</summary>
        public static int[] MarkerCounts(byte[] b)
        {
            var c = new int[3];
            if (b == null || b.Length < 12) return c;
            int n = BitConverter.ToInt32(b, 0) * BitConverter.ToInt32(b, 4) * BitConverter.ToInt32(b, 8);
            for (int i = 0; i < n && 12 + i * 3 + 2 < b.Length; i++)
            {
                int o = 12 + i * 3;
                byte r = b[o], g = b[o + 1], bl = b[o + 2];
                if (r == 255 && g == 0 && bl == 0) c[0]++;
                else if (r == 0 && g == 255 && bl == 0) c[1]++;
                else if (r == 0 && g == 0 && bl == 255) c[2]++;
            }
            return c;
        }

        public static Color StatusColor(string status)
        {
            switch (status)
            {
                case "TRANSFERRED_FROM_ALPHA": return new Color(0.45f, 0.95f, 0.45f);
                case "EMULATED_ALPHA_CODE": return new Color(1f, 0.78f, 0.3f);
                case "RECONSTRUCTED_FROM_EVIDENCE": return new Color(0.5f, 0.8f, 1f);
                case "BLOCKED": return new Color(1f, 0.4f, 0.4f);
                default: return new Color(1f, 0.5f, 1f);
            }
        }
    }
}
