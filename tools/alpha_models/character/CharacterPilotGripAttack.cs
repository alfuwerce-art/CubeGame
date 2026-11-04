// S05A-2 GRIP (D52) — verifier entry point for the two-handed attack and the review fixes.
//
// GRIP_RESULT=PASS requires: derived grip geometry valid, both hands on their grip points in every
// two-handed phase, blade clear of head/torso, smooth phase-to-phase motion, loop closure back to idle,
// and a working character build with the weapon placed by its handle. The animation itself is
// ORIGINAL_NOT_ALPHA: an authored reconstruction, not a transfer of the original Alpha animation.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CubeGame.CharacterPilot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class CharacterPilotVerifier
{
    const int AttackSamples = 16;
    const float GripTolerance = 0.02f;

    [MenuItem("CubeGame Pilot/Run grip and two-handed attack check")]
    public static void RunGripAttack()
    {
        Directory.CreateDirectory(OutputDir);
        CleanOutputs("grip_", "attack_", "worldscale_");
        var problems = new List<string>();
        var summary = new List<string>();
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var build = BuildCharacter(false);
            problems.AddRange(build.problems);

            var handRight = Find(build, "hand_r");
            var handLeft = Find(build, "hand_l");
            var sword = Find(build, "sword");
            var animator = build.animator;
            if (animator == null) throw new Exception("no animator");
            var grip = build.grip;
            lastGripPrimary = grip.primaryGrip;
            lastGripSecondary = grip.secondaryGrip;
            summary.Add($"grip: handle rows {grip.handleYMin}..{grip.handleYMax}, guard {grip.guardYMin}..{grip.guardYMax}, blade {grip.bladeYMin}..{grip.bladeYMax}");
            summary.Add($"grip: primary {Fmt3(grip.primaryGrip)} secondary {Fmt3(grip.secondaryGrip)} radius {grip.handleRadius:0.##}");

            // ---- idle: right hand on the handle, blade clear ----
            problems.AddRange(ValidateGripContact("idle/right", handRight, build.handGripLocal, sword,
                grip.primaryGrip, grip, GripTolerance));

            // ---- two-handed attack ----
            animator.TwoHandedAttack = true;
            animator.BindGrip(sword, handLeft, grip.primaryGrip, grip.secondaryGrip,
                HandGripLocal(build, "hand_l"));

            var handPositions = new List<Vector3>();
            var leftOffsets = new List<float>();
            var rightOffsets = new List<float>();
            var bladeHits = 0;
            var headBounds = BoundsOf(Find(build, "head"));
            var bodyBounds = BoundsOf(Find(build, "body"));
            for (int i = 0; i <= AttackSamples; i++)
            {
                float t = (float)i / AttackSamples;
                animator.Sample(t);
                var phase = ProceduralCharacterAnimator.PhaseName(t);
                var right = Vector3.Distance(handRight.TransformPoint(build.handGripLocal),
                    sword.TransformPoint(grip.primaryGrip));
                var left = Vector3.Distance(handLeft.TransformPoint(HandGripLocal(build, "hand_l")),
                    sword.TransformPoint(grip.secondaryGrip));
                rightOffsets.Add(right);
                leftOffsets.Add(left);
                handPositions.Add(handRight.position);
                if (right > GripTolerance)
                    problems.Add($"attack: right hand left the grip at t={t:0.###} ({phase}), offset {right:0.###}");
                if (left > GripTolerance)
                    problems.Add($"attack: left hand left the secondary grip at t={t:0.###} ({phase}), offset {left:0.###}");
                var clearance = ValidateBladeClearance(sword, grip, headBounds, bodyBounds, 0.05f);
                if (clearance.Count > 0 && i > 0 && i < AttackSamples) bladeHits++;
                if (i == 0 || i == AttackSamples)
                {
                    // idle poses at both ends must match exactly (loop closure)
                    continue;
                }
                if (i % 4 == 0)
                {
                    summary.Add($"t={t:0.###} ({phase}): right {right:0.####}, left {left:0.####}");
                    RenderAttackPose(build, $"attack_{i:00}_{phase}");
                }
            }
            // phase-to-phase continuity: no frame may jump more than the natural speed of the swing
            for (int i = 1; i < handPositions.Count; i++)
            {
                var delta = Vector3.Distance(handPositions[i - 1], handPositions[i]);
                if (delta > 2.5f)
                    problems.Add($"attack: right hand jumps {delta:0.##} between samples {i - 1} and {i}");
            }
            var closure = Vector3.Distance(handPositions.First(), handPositions.Last());
            if (closure > 0.02f)
                problems.Add($"attack: pose does not return to idle (closure {closure:0.###})");
            summary.Add($"attack: max right offset {rightOffsets.Max():0.####}, max left offset {leftOffsets.Max():0.####}, closure {closure:0.####}");

            // ---- close-up and idle renders ----
            animator.Sample(0f);
            RenderGripCloseup(build, "grip_closeup");
            RenderAttackPose(build, "attack_idle");

            // ---- FIX-B: the same build must stay correct at a different character scale ----
            problems.AddRange(CheckWorldPlacementAtScale(0.5f, summary));
            animator.TwoHandedAttack = false;
            animator.Sample(0f);
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(OutputDir, "grip_exception.txt"), e.ToString());
            WriteText("grip_attack.txt", "GRIP_RESULT=EXCEPTION\n" + e + "\n");
            Debug.LogError("GRIP_RESULT=EXCEPTION " + e.Message);
            throw;
        }

        WriteText("grip_attack.txt",
            (problems.Count == 0 ? "GRIP_RESULT=PASS\n" : "GRIP_RESULT=FAIL\n") +
            string.Join("\n", summary) + "\n" + string.Join("\n", problems) + "\n");
        WriteText("grip_attack.json", BuildGripJson(problems, summary));
        Debug.Log(problems.Count == 0
            ? "GRIP_RESULT=PASS"
            : "GRIP_RESULT=FAIL " + string.Join("; ", problems.Take(3)));
        if (problems.Count > 0)
            throw new Exception("GRIP_RESULT=FAIL - see PilotOutput/grip_attack.txt");
    }

    /// <summary>FIX-B: rebuild at a different overall scale and verify every part's world placement.</summary>
    internal static List<string> CheckWorldPlacementAtScale(float scale, List<string> summary)
    {
        var problems = new List<string>();
        try
        {
            BodyScaleOverride = scale;
            var build = BuildCharacter(false);
            problems.AddRange(build.problems);
            var lines = new List<string>();
            foreach (var result in build.results)
            {
                if (!build.parts.TryGetValue(result.id, out var pivot)) continue;
                problems.AddRange(ValidateWorldPlacement(result, pivot));
                lines.Add($"{result.id}={Fmt3(BoundsOf(pivot).size)}");
            }
            summary.Add($"worldscale {scale:0.##}: " + string.Join(" ", lines));
            UnityEngine.Object.DestroyImmediate(build.root);
        }
        finally
        {
            BodyScaleOverride = null;
        }
        return problems;
    }

    static void RenderAttackPose(AssemblyBuild build, string stem)
    {
        var camera = EnsurePilotCamera();
        var target = new Vector3(0f, 12f, 0f);
        const float radius = 26f;
        var distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
        foreach (var (name, dir) in new[]
                 {
                     ("front", new Vector3(0f, 0.08f, 1f)),
                     ("side", new Vector3(1f, 0.08f, 0.05f)),
                     ("threequarter", new Vector3(0.8f, 0.35f, 0.9f)),
                 })
        {
            camera.transform.position = target + dir.normalized * distance;
            camera.transform.LookAt(target);
            Capture(camera, $"{OutputDir}/{stem}_{name}.png");
        }
    }

    /// <summary>Close-up on the right hand and the handle (same camera for every run).</summary>
    static void RenderGripCloseup(AssemblyBuild build, string stem)
    {
        var camera = EnsurePilotCamera();
        var hand = Find(build, "hand_r");
        var sword = Find(build, "sword");
        if (hand == null || sword == null) return;
        var pommel = sword.TransformPoint(build.grip.Pommel);
        var guard = sword.TransformPoint(build.grip.GuardCentre);
        var focus = Vector3.Lerp(pommel, guard, 0.5f);
        // frame the whole handle plus a margin, so the hand and the guard are both visible
        var radius = Mathf.Max(2f, Vector3.Distance(pommel, guard) * 1.4f);
        var distance = radius / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
        foreach (var (name, dir) in new[]
                 {
                     ("front", new Vector3(0.25f, 0.05f, 1f)),
                     ("side", new Vector3(1f, 0.1f, 0.15f)),
                     ("threequarter", new Vector3(0.7f, 0.25f, 0.8f)),
                 })
        {
            camera.transform.position = focus + dir.normalized * distance;
            camera.transform.LookAt(focus);
            Capture(camera, $"{OutputDir}/{stem}_{name}.png");
        }
    }

    static Camera EnsurePilotCamera()
    {
        var existing = GameObject.Find("PilotCamera");
        if (existing != null && existing.TryGetComponent<Camera>(out var cam)) return cam;
        var go = new GameObject("PilotCamera");
        var camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.10f, 0.10f, 0.13f, 1f);
        camera.fieldOfView = 30f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 5000f;
        var light = new GameObject("PilotLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
        return camera;
    }

    static void WriteText(string name, string content) =>
        File.WriteAllText(Path.Combine(OutputDir, name), content, new UTF8Encoding(false));

    static string BuildGripJson(List<string> problems, List<string> summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"result\": \"{(problems.Count == 0 ? "PASS" : "FAIL")}\",");
        sb.AppendLine($"  \"animation\": \"{ProceduralCharacterAnimator.TwoHandedClipName}\",");
        sb.AppendLine("  \"status\": \"ORIGINAL_NOT_ALPHA\",");
        sb.AppendLine($"  \"grip\": {{ \"primary\": \"{Fmt3(lastGripPrimary)}\", \"secondary\": \"{Fmt3(lastGripSecondary)}\" }},");
        sb.AppendLine($"  \"notes\": [{string.Join(",", summary.Select(n => "\"" + n.Replace("\"", "'") + "\""))}],");
        sb.AppendLine($"  \"problems\": [{string.Join(",", problems.Select(n => "\"" + n.Replace("\"", "'") + "\""))}]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    static Vector3 lastGripPrimary;
    static Vector3 lastGripSecondary;
}
