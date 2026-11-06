// ReviewData.cs — index + per-specimen data of the S05B visual review scene.
// All data is produced by tools/alpha_models/review/build_review.py from the existing universal
// pipeline (prepare_unity) and lives in Assets/StreamingAssets/Review (local .scratch project only).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public bool equipmentObserved;
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
        public ReviewHairSample[] hairSamples = new ReviewHairSample[0];
    }

    /// <summary>A live Alpha hair colour (creature+0x7A, hair_colour_evidence.json).</summary>
    [Serializable]
    public class ReviewHairSample
    {
        public string kind, capture;
        public int[] hair_rgb = new int[0];
        public int records, creatures;
        public Color Colour => hair_rgb.Length == 3 ? new Color(hair_rgb[0] / 255f, hair_rgb[1] / 255f, hair_rgb[2] / 255f, 1f) : Color.white;
        public string Label => $"({hair_rgb[0]},{hair_rgb[1]},{hair_rgb[2]}) {kind} · {capture}";
    }

    /// <summary>Responsive layout of the viewer: one side panel (tabs) + timeline, never overlapping,
    /// always inside the screen (checked for many sizes by ReviewSceneBuilder.Check).</summary>
    public struct ReviewLayout
    {
        public float scale, width, height;
        public Rect panel, timeline, status;
        public bool timelineBelow;

        public static float AutoScale(int screenW, int screenH) =>
            Mathf.Clamp(Mathf.Min(screenW / 1100f, screenH / 680f), 0.6f, 2f);

        public static ReviewLayout Compute(int screenW, int screenH, float scale, bool panelOpen)
        {
            var l = new ReviewLayout { scale = scale, width = screenW / scale, height = screenH / scale };
            const float m = 6f, th = 84f;
            float W = l.width, H = l.height;
            float pw = panelOpen ? Mathf.Clamp(W * 0.42f, 230f, 380f) : 120f;
            if (pw > W - 2 * m) pw = W - 2 * m;
            float rest = W - pw - 3 * m;
            l.timelineBelow = rest < 380f;
            if (l.timelineBelow)
            {
                l.timeline = new Rect(m, H - th - m, W - 2 * m, th);
                l.panel = new Rect(m, m, pw, Mathf.Max(60f, H - th - 3 * m));
                l.status = new Rect(pw + 2 * m, m, Mathf.Max(0f, W - pw - 3 * m), 44f);
            }
            else
            {
                l.panel = new Rect(m, m, pw, panelOpen ? H - 2 * m : 34f);
                l.timeline = new Rect(pw + 2 * m, H - th - m, rest, th);
                l.status = new Rect(pw + 2 * m, m, rest, 44f);
            }
            if (!panelOpen) l.panel.height = 34f;
            if (l.status.width < 160f) l.status.width = 0f;
            return l;
        }

        public IEnumerable<string> Problems()
        {
            var r = new List<(string, Rect)> { ("panel", panel), ("timeline", timeline) };
            if (status.width > 0) r.Add(("status", status));
            for (int i = 0; i < r.Count; i++)
            {
                var a = r[i].Item2;
                if (a.xMin < 0 || a.yMin < 0 || a.xMax > width + 0.01f || a.yMax > height + 0.01f || a.width <= 0 || a.height <= 0)
                    yield return $"{r[i].Item1} outside screen {a} in {width}x{height}";
                for (int j = i + 1; j < r.Count; j++)
                    if (a.Overlaps(r[j].Item2)) yield return $"{r[i].Item1} overlaps {r[j].Item1}";
            }
        }
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

        /// <summary>Default hair colour of a specimen: fixed-by-kind value, else the first live sample of
        /// the kind, else identity (x1). Never invented.</summary>
        public static Color DefaultHairColour(ReviewIndex idx, ReviewSpecimen spec)
        {
            if (spec == null || spec.hairPart < 0) return Color.white;
            if (spec.HairFixedByKind) return spec.partSpecs[spec.hairPart].TintColor;
            var h = idx?.hairSamples?.FirstOrDefault(x => x.kind == spec.info.kind && x.hair_rgb != null && x.hair_rgb.Length == 3);
            return h != null ? h.Colour : Color.white;
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
