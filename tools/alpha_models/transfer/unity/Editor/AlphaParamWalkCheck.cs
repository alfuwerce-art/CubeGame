// AlphaParamWalkCheck.cs — EditMode verification of the parametric Alpha walk (AlphaParamWalk.cs).
//   -executeMethod CubeGame.AlphaTransfer.Editor.AlphaParamWalkCheck.Run
// Checks (report <project>/TransferOutput/paramwalk.txt, marker PARAMWALK_RESULT=PASS|FAIL):
//   SPEC       Data/locomotion.json parses, status != NEEDS_RESEARCH, rig id == rig.json
//   SELF       synthetic invariants (TEST_DATA_NOT_ALPHA): rest pose, mirror, quadruped vs biped
//   GOLDEN     C# == Python reference implementation on synthetic appearances (Data/paramwalk_golden.json)
//   ALPHA      C# formula vs captured Alpha joint frames (Data/paramwalk_reference.json, optional:
//              present only when the job names a capture of this motion family)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CubeGame.AlphaTransfer.Editor
{
    [Serializable]
    public class ParamWalkGoldenCase
    {
        public string name;
        public AlphaLocomotionParams p;
        public float phase, amp, lean;
        public bool boost, onGround = true;
        public string part;
        public float[] m;
    }

    [Serializable]
    public class ParamWalkGolden { public string status; public ParamWalkGoldenCase[] cases; }

    [Serializable]
    public class ParamWalkRefFrame
    {
        public int index;
        public float phase, amp, lean;
        public bool boost;
        public string[] parts;
        public float[] m;
    }

    [Serializable]
    public class ParamWalkReference
    {
        public string status, capture, capture_sha256, kind, rig;
        public AlphaLocomotionParams p;
        public ParamWalkRefFrame[] frames;
    }

    public static class AlphaParamWalkCheck
    {
        const string DataDir = "Assets/AlphaTransfer/Data";
        public const float MaxPosVox = 1e-3f, MaxRotDeg = 0.05f, GoldenAbs = 2e-4f;

        static float[] Joint(AlphaLocomotionParams p, float phase, float amp, bool boost, float lean, string part, bool onGround = true) =>
            AlphaParamWalk.Joint(p, new AlphaWalkInput { phase = phase, amp = amp, boost = boost, headLeanDeg = lean, onGround = onGround }, part);

        static (float pos, float rot) PoseError(float[] a, float[] b)
        {
            AlphaSpace.Decompose(AlphaSpace.ToUnity(a), out var pa, out var ra, out _);
            AlphaSpace.Decompose(AlphaSpace.ToUnity(b), out var pb, out var rb, out _);
            return ((pa - pb).magnitude, Quaternion.Angle(ra, rb));
        }

        static AlphaLocomotionParams Synthetic(int flags) => new AlphaLocomotionParams
        {
            flags = flags, size = new[] { 1f, 1f, 2f }, scale_head = 1.1f, scale_body = 0.9f, scale_hand = 1.2f,
            scale_foot = 0.8f, scale_tail = 1.3f, pitch_tail = -20f, angle_58 = 0f,
            offset_body = new[] { 0f, 1f, -3f }, offset_head = new[] { 0f, 2f, 6f }, offset_hand = new[] { 5f, 1f, -1f },
            offset_foot = new[] { 2f, 0.5f, -9f }, offset_tail = new[] { 0f, -6f, 1f },
        };

        static List<string> SelfCases(StringBuilder report)
        {
            var fails = new List<string>();
            void Case(string name, string err) { report.AppendLine($"SELF {(err == null ? "PASS" : "FAIL")} {name}{(err == null ? "" : " :: " + err)}"); if (err != null) fails.Add(name + " " + err); }
            var biped = Synthetic(0); var quad = Synthetic(1);
            // amp = 0: no swing, the body is exactly offset + scale (TEST_DATA_NOT_ALPHA)
            var body = Joint(biped, 1.3f, 0f, false, 0f, "body");
            var expect = AlphaSpace.Mul(AlphaParamWalk.Scale(0.9f), AlphaSpace.Translate(new Vector3(0, 1, -3)));
            Case("rest_body_is_offset_times_scale", AlphaSpace.MaxAbsDiff(body, expect) < 1e-6f ? null : "differs");
            // right hand mirrored (negative determinant), left not
            var hr = AlphaSpace.ToUnity(Joint(biped, 0.4f, 0.19f, false, 0f, "hand_r"));
            var hl = AlphaSpace.ToUnity(Joint(biped, 0.4f, 0.19f, false, 0f, "hand_l"));
            Case("hand_r_mirrored", hr.determinant < 0 && hl.determinant > 0 ? null : $"det r={hr.determinant} l={hl.determinant}");
            // biped hands do not rotate, quadruped hands pitch
            AlphaSpace.Decompose(hl, out _, out var rb, out _);
            AlphaSpace.Decompose(AlphaSpace.ToUnity(Joint(quad, 0f, 0.19f, false, 0f, "hand_l")), out _, out var rq, out _);
            Case("biped_hand_no_rotation", Quaternion.Angle(rb, Quaternion.identity) < 1e-3f ? null : "rotates");
            Case("quadruped_hand_pitches", Quaternion.Angle(rq, Quaternion.identity) > 10f ? null : "static");
            // half a cycle later the left foot takes the right foot's pitch (mirror symmetry of the gait)
            AlphaSpace.Decompose(AlphaSpace.ToUnity(Joint(biped, 0f, 0.19f, false, 0f, "foot_l")), out _, out var fl0, out _);
            AlphaSpace.Decompose(AlphaSpace.ToUnity(Joint(biped, Mathf.PI, 0.19f, false, 0f, "foot_r")), out _, out var frPi, out _);
            Case("gait_half_cycle_symmetry", Quaternion.Angle(fl0, frPi) < 1e-3f ? null : $"{Quaternion.Angle(fl0, frPi)}");
            // flags bit1 off the ground: no swing
            var air = Joint(Synthetic(2), 0f, 0.19f, false, 0f, "foot_l", onGround: false);
            var still = Joint(Synthetic(2), 0f, 0f, false, 0f, "foot_l");
            Case("bit1_airborne_no_swing", AlphaSpace.MaxAbsDiff(air, still) < 1e-6f ? null : "swings");
            // lean smoothing: 1 - 0.99^dt_ms
            float l = AlphaParamWalk.LeanStep(0f, 10f, 100f);
            Case("lean_step_exact", Mathf.Abs(l - 10f * (1f - Mathf.Pow(0.99f, 100f))) < 1e-4f ? null : $"{l}");
            Case("lean_target_quadruped_zero", AlphaParamWalk.HeadLeanTarget(quad, true, true) == 0f ? null : "non-zero");
            return fails;
        }

        public static void Run()
        {
            var problems = new List<string>();
            var report = new StringBuilder();
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TransferOutput"));
            int goldenN = 0, alphaFrames = 0;
            float goldenMax = 0f, alphaPos = 0f, alphaRot = 0f;
            try
            {
                Directory.CreateDirectory(outDir);
                // SPEC
                var specPath = Path.Combine(DataDir, "locomotion.json");
                if (!File.Exists(specPath)) problems.Add("LOCOMOTION_SPEC_MISSING");
                else
                {
                    var spec = AlphaLocomotionSpec.Parse(File.ReadAllText(specPath), problems);
                    var rig = JsonUtility.FromJson<AlphaRigSpec>(File.ReadAllText(Path.Combine(DataDir, "rig.json")));
                    if (spec != null)
                    {
                        report.AppendLine($"SPEC kind={spec.kind} rig={spec.rig} status={spec.status} terms=[{string.Join(",", spec.terms ?? new string[0])}] code_only=[{string.Join(",", spec.code_only_terms ?? new string[0])}]");
                        if (rig != null && rig.rig != spec.rig) problems.Add($"LOCOMOTION_RIG_MISMATCH {spec.rig} != {rig.rig}");
                        foreach (var part in rig?.parts ?? new AlphaRigPartSpec[0])
                            if (!AlphaParamWalk.IsModelled(part.name) && part.name != "weapon") problems.Add($"PART_NOT_MODELLED {part.name}");
                    }
                }
                // SELF
                foreach (var f in SelfCases(report)) problems.Add("SELF " + f);
                // GOLDEN
                var goldenPath = Path.Combine(DataDir, "paramwalk_golden.json");
                if (!File.Exists(goldenPath)) problems.Add("GOLDEN_MISSING");
                else
                {
                    var g = JsonUtility.FromJson<ParamWalkGolden>(File.ReadAllText(goldenPath));
                    foreach (var c in g.cases)
                    {
                        var m = Joint(c.p, c.phase, c.amp, c.boost, c.lean, c.part, c.onGround);
                        float d = AlphaSpace.MaxAbsDiff(m, c.m);
                        goldenMax = Mathf.Max(goldenMax, d); goldenN++;
                        if (d > GoldenAbs) problems.Add($"GOLDEN_MISMATCH {c.name} max_abs={d}");
                    }
                    report.AppendLine($"GOLDEN cases={goldenN} status={g.status} max_abs={goldenMax:0.0000000}");
                }
                // ALPHA
                var refPath = Path.Combine(DataDir, "paramwalk_reference.json");
                if (File.Exists(refPath))
                {
                    var r = JsonUtility.FromJson<ParamWalkReference>(File.ReadAllText(refPath));
                    var perPart = new Dictionary<string, (float pos, float rot, int n)>();
                    foreach (var f in r.frames)
                    {
                        for (int k = 0; k < f.parts.Length; k++)
                        {
                            var cap = new float[16]; Array.Copy(f.m, 16 * k, cap, 0, 16);
                            var e = PoseError(Joint(r.p, f.phase, f.amp, f.boost, f.lean, f.parts[k]), cap);
                            perPart.TryGetValue(f.parts[k], out var acc);
                            perPart[f.parts[k]] = (Mathf.Max(acc.pos, e.pos), Mathf.Max(acc.rot, e.rot), acc.n + 1);
                            alphaPos = Mathf.Max(alphaPos, e.pos); alphaRot = Mathf.Max(alphaRot, e.rot);
                        }
                        alphaFrames++;
                    }
                    report.AppendLine($"ALPHA capture={r.capture} sha256={r.capture_sha256} kind={r.kind} frames={alphaFrames} max_pos_vox={alphaPos:0.000000} max_rot_deg={alphaRot:0.00000}");
                    foreach (var kv in perPart.OrderBy(k => k.Key))
                        report.AppendLine($"  {kv.Key} n={kv.Value.n} max_pos_vox={kv.Value.pos:0.000000} max_rot_deg={kv.Value.rot:0.00000}");
                    if (alphaPos > MaxPosVox || alphaRot > MaxRotDeg) problems.Add($"ALPHA_MISMATCH pos={alphaPos} rot={alphaRot}");
                    if (alphaFrames == 0) problems.Add("ALPHA_NO_FRAMES");
                }
                else report.AppendLine("ALPHA not_available (no capture of this motion family in the job: formula-only kind)");
            }
            catch (Exception e) { problems.Add("EXCEPTION " + e); }
            var verdict = problems.Count == 0 ? "PASS" : "FAIL";
            report.Insert(0, $"PARAMWALK_RESULT={verdict} golden={goldenN} alpha_frames={alphaFrames} thresholds pos<={MaxPosVox} rot<={MaxRotDeg} golden<={GoldenAbs}\nproblems={problems.Count}\n" +
                             string.Join("\n", problems.Select(p => "  - " + p)) + "\n");
            File.WriteAllText(Path.Combine(outDir, "paramwalk.txt"), report.ToString());
            Debug.Log(report.ToString());
            if (problems.Count > 0) throw new Exception($"PARAMWALK_RESULT={verdict}");
        }
    }
}
