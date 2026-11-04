// S05A2-D58-A1 — end-to-end probe: numbers extracted from the Alpha client driven through the pose adapter.
//
// What this proves and what it does NOT:
//   * proves that the adapter accepts real Alpha channel values (read from a local export produced
//     by tools/alpha_models/anim/pose_table.py), that the Alpha -> Unity basis change is applied,
//     and that the applied rotation reproduces the Alpha angle and axis numerically;
//   * does NOT prove that channel `local_15fc` belongs to the part it is applied to. The binding is
//     provisional (S05A2-D58-A1 §4), so the pose is labelled `PARTIAL_BINDING` and the run must never
//     be reported as `ALPHA_DERIVED`.
//
// The export contains original game values, so it stays local in PilotOutput/ and is never committed.
// Without the export the probe reports SKIP instead of inventing data.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CubeGame.CharacterPilot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class CharacterPilotVerifier
{
    const string AlphaChannelPath = "PilotOutput/alpha_pose_channels.json";

    [Serializable]
    class AlphaChannelDoc
    {
        public string source;
        public string generator;
        public string function;
        public int state;
        public string blockStart;
        public string slot;
        public int component;
        public AlphaMoment[] moments;
    }

    [Serializable]
    class AlphaMoment
    {
        public string name;
        public float value;
        public string address;
        public string encoding;
    }

    /// <summary>
    /// Alpha basis (Z up, right handed) -> Unity basis (Y up, left handed): (x, y, z) becomes (x, z, y).
    /// The mapping is one matrix so it stays reviewable in a single place instead of being spread over
    /// per-axis fixups. Note it is a reflection (det = -1): as a consequence the *sense* of every
    /// rotation flips, so a rotation of +θ about an Alpha axis becomes -θ about the mapped Unity axis.
    /// </summary>
    internal static Matrix4x4 AlphaToUnityBasis()
    {
        var swap = Matrix4x4.identity;   // m[row, column]; output y <- input z, output z <- input y
        swap.m11 = 0f; swap.m12 = 1f;
        swap.m21 = 1f; swap.m22 = 0f;
        return swap;
    }

    /// <summary>Axis of an Alpha Euler component, expressed in the Alpha basis (used to build the rotation).</summary>
    internal static Vector3 AlphaAxis(int component) => component switch
    {
        0 => new Vector3(1f, 0f, 0f),   // X
        1 => new Vector3(0f, 1f, 0f),   // Y
        2 => new Vector3(0f, 0f, 1f),   // Z
        _ => Vector3.zero,
    };

    /// <summary>Unity axis that carries an Alpha Euler component after the basis change.</summary>
    internal static Vector3 AlphaComponentAxis(int component) => component switch
    {
        0 => Vector3.right,      // Alpha X  -> Unity X
        1 => Vector3.forward,    // Alpha Y  -> Unity Z
        2 => Vector3.up,         // Alpha Z  -> Unity Y
        _ => Vector3.zero,
    };

    /// <summary>
    /// Builds the Unity rotation for one Alpha channel value. Alpha composes T * Rz * Ry * Rx, so the
    /// rotation is assembled in that order and then moved into the Unity basis; converting the raw
    /// Euler triple with Quaternion.Euler would silently change the order of multiplication.
    /// </summary>
    internal static Quaternion AlphaChannelRotation(int component, float degrees, out string problem)
    {
        problem = null;
        var axis = AlphaAxis(component);
        if (axis == Vector3.zero)
        {
            problem = $"channel component {component} has no Unity axis";
            return Quaternion.identity;
        }
        if (float.IsNaN(degrees) || float.IsInfinity(degrees))
        {
            problem = "channel value is not finite";
            return Quaternion.identity;
        }
        var alphaRotation = Matrix4x4.Rotate(Quaternion.AngleAxis(degrees, axis));
        var basis = AlphaToUnityBasis();
        return (basis * alphaRotation * basis).rotation;
    }

    /// <summary>
    /// Checks the basis change itself: the swap must be an involution and a quarter turn about each
    /// Alpha axis must land on the Unity axis the mapping claims.
    /// </summary>
    internal static List<string> ValidateBasisMapping()
    {
        var problems = new List<string>();
        var basis = AlphaToUnityBasis();
        var squared = basis * basis;
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                var expected = row == column ? 1f : 0f;
                if (Mathf.Abs(squared[row, column] - expected) > 1e-4f)
                {
                    problems.Add($"probe: basis change is not an involution at [{row},{column}] " +
                                 $"({squared[row, column]:0.####})");
                    return problems;
                }
            }
        }
        for (int component = 0; component < 3; component++)
        {
            // The basis change is a reflection, so the same physical rotation is expressed with the
            // opposite sign; a +90 deg Alpha rotation must appear as -90 deg about the mapped axis.
            var expected = Quaternion.AngleAxis(-90f, AlphaComponentAxis(component));
            var actual = AlphaChannelRotation(component, 90f, out var problem);
            if (problem != null)
            {
                problems.Add("probe: basis check: " + problem);
                continue;
            }
            var divergence = Quaternion.Angle(expected, actual);
            if (divergence > 0.01f)
                problems.Add($"probe: Alpha component {component} maps to the wrong Unity axis " +
                             $"(divergence {divergence:0.###} deg)");
        }
        return problems;
    }

    [MenuItem("CubeGame Pilot/Apply extracted Alpha pose channel (PARTIAL binding)")]
    public static void RunAlphaPoseProbe()
    {
        Directory.CreateDirectory(OutputDir);
        // Only the probe's own artefacts are cleaned: the input export must survive, otherwise the
        // probe would delete the very file it is about to read (caught by a real SKIP run).
        CleanOutputs("alpha_pose_probe");
        var problems = new List<string>();
        var lines = new List<string>();

        if (!File.Exists(AlphaChannelPath))
        {
            lines.Add($"probe: no local Alpha channel export at {AlphaChannelPath}");
            lines.Add("probe: create it with " +
                      "python -m tools.alpha_models.anim.pose_table --exe <Cube.exe> --probe-out " +
                      "PilotOutput/alpha_pose_channels.json --probe-state 1 --probe-slot local_15fc");
            lines.Add("probe: nothing to apply; the repository does not ship Alpha values");
            WriteAlphaPoseReport("SKIP", problems, lines);
            Debug.Log("ALPHA_POSE_PROBE=SKIP (no local Alpha channel export)");
            return;
        }

        AlphaChannelDoc doc;
        try
        {
            doc = JsonUtility.FromJson<AlphaChannelDoc>(File.ReadAllText(AlphaChannelPath));
        }
        catch (Exception e)
        {
            problems.Add("probe: cannot read the Alpha channel export: " + e.Message);
            WriteAlphaPoseReport("FAIL", problems, lines);
            Debug.Log("ALPHA_POSE_PROBE=FAIL (unreadable export)");
            return;
        }
        if (doc == null || doc.moments == null || doc.moments.Length == 0)
        {
            problems.Add("probe: the Alpha channel export contains no moments");
            WriteAlphaPoseReport("FAIL", problems, lines);
            Debug.Log("ALPHA_POSE_PROBE=FAIL (empty export)");
            return;
        }

        lines.Add($"probe: export {AlphaChannelPath} (source {doc.source})");
        lines.Add($"probe: state {doc.state}, block {doc.blockStart}, channel {doc.slot}" +
                  $"[{doc.component}], moments {doc.moments.Length}");

        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var build = BuildCharacter(false);
            problems.AddRange(build.problems);
            problems.AddRange(ValidateBasisMapping());

            // The binding is unknown: the channel is applied to the torso as a mechanism check only.
            const string targetPart = "body";
            var part = Find(build, targetPart);
            if (part == null)
            {
                problems.Add($"probe: part '{targetPart}' is missing from the assembled character");
            }
            else
            {
                var adapter = new CharacterPoseAdapter(
                    new Dictionary<string, Transform> { { targetPart, part } });

                var homePosition = part.localPosition;
                var homeRotation = part.localRotation;
                var homeScale = part.localScale;

                foreach (var moment in doc.moments)
                {
                    var rotation = AlphaChannelRotation(doc.component, moment.value, out var axisProblem);
                    if (axisProblem != null)
                    {
                        problems.Add($"probe: moment {moment.name}: {axisProblem}");
                        continue;
                    }
                    var pose = new PoseTrs(homePosition, rotation, homeScale);
                    var poses = new Dictionary<string, PoseTrs> { { targetPart, pose } };
                    var applied = adapter.ApplyLocalTrs(poses, PoseProvenance.PartialBinding);
                    problems.AddRange(applied);

                    // Numeric comparison against the Alpha value. The expectation is built from the Alpha
                    // number and the (reflection) basis change only; the applied rotation is read back from
                    // the Transform after the adapter wrote it. Comparing whole quaternions keeps angles
                    // beyond 180 degrees meaningful (-250 and 110 are NOT the same rotation).
                    var expected = Quaternion.AngleAxis(-moment.value, AlphaComponentAxis(doc.component));
                    var appliedError = Quaternion.Angle(expected, part.localRotation);
                    var requested = AlphaChannelRotation(doc.component, moment.value, out _);
                    var requestError = Quaternion.Angle(requested, part.localRotation);
                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "probe: {0} value {1:0.###} deg -> applied differs from the Alpha expectation by " +
                        "{2:0.####} deg (adapter-vs-request {3:0.#####})",
                        moment.name, moment.value, appliedError, requestError));
                    if (appliedError > 0.01f)
                        problems.Add($"probe: {moment.name}: the applied rotation differs from the Alpha " +
                                     $"expectation by {appliedError:0.###} deg");
                    if (requestError > 1e-3f)
                        problems.Add($"probe: {moment.name}: the adapter stored a different rotation " +
                                     $"({requestError:0.####} deg off)");

                    part.localPosition = homePosition;
                    part.localRotation = homeRotation;
                    part.localScale = homeScale;
                }

                // Provenance guard: the same numbers must never be reported as Alpha-derived poses.
                var claim = adapter.ApplyLocalTrs(
                    new Dictionary<string, PoseTrs> { { targetPart, new PoseTrs(
                        homePosition, Quaternion.AngleAxis(-doc.moments[0].value,
                            AlphaComponentAxis(doc.component)),
                        homeScale) } },
                    PoseProvenance.PartialBinding, claimsAlphaDerived: true);
                if (claim.Count == 0)
                    problems.Add("probe: a PARTIAL_BINDING pose was accepted as ALPHA_DERIVED");
                else
                    lines.Add($"probe: provenance guard refused the Alpha claim ({claim[0]})");

                lines.Add($"probe: binding for channel {doc.slot} is PROVISIONAL " +
                          $"(applied to '{targetPart}' only to exercise the adapter)");
                lines.Add($"probe: {adapter.Describe()}");
            }
        }
        catch (Exception e)
        {
            problems.Add("probe: exception: " + e.Message);
        }

        var result = problems.Count == 0 ? "PASS" : "FAIL";
        WriteAlphaPoseReport(result, problems, lines);
        Debug.Log($"ALPHA_POSE_PROBE={result} moments={doc.moments.Length} channel={doc.slot}");
    }

    static void WriteAlphaPoseReport(string result, List<string> problems, List<string> lines)
    {
        Directory.CreateDirectory(OutputDir);
        var text = new StringBuilder();
        text.AppendLine($"ALPHA_POSE_PROBE={result}");
        text.AppendLine("provenance=PARTIAL_BINDING");
        text.AppendLine("note=binding of the extracted channel to a body part is not proven (S05A2-D58-A1)");
        foreach (var line in lines) text.AppendLine(line);
        foreach (var problem in problems) text.AppendLine("problem: " + problem);
        File.WriteAllText(Path.Combine(OutputDir, "alpha_pose_probe.txt"), text.ToString(),
            new UTF8Encoding(false));

        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine($"  \"result\": \"{result}\",");
        json.AppendLine("  \"provenance\": \"" + PoseProvenance.PartialBinding + "\",");
        json.AppendLine("  \"lines\": [" + string.Join(",",
            lines.ConvertAll(l => "\"" + l.Replace("\"", "'") + "\"")) + "],");
        json.AppendLine("  \"problems\": [" + string.Join(",",
            problems.ConvertAll(p => "\"" + p.Replace("\"", "'") + "\"")) + "]");
        json.AppendLine("}");
        File.WriteAllText(Path.Combine(OutputDir, "alpha_pose_probe.json"), json.ToString(),
            new UTF8Encoding(false));
    }
}
