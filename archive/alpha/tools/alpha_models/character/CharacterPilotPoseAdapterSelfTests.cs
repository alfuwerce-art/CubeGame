// S05A-2 pilot — EditMode self-tests for CharacterPoseAdapter (the Alpha-pose -> Unity adapter).
//
// All pose data in this file is SYNTHETIC TEST DATA. It is labelled TEST_DATA_NOT_ALPHA in the code, in the
// adapter's own record and in PilotOutput/pose_adapter_selftest.json: nothing here comes from the original
// game, and the tests must not be read as evidence of an Alpha transfer.
//
// The cases cover the four claims the adapter makes:
//   * a world-space 4x4 per part converts to the Unity hierarchy as local = inverse(parentWorld)*partWorld
//     (hand-computed numbers, exact), and matrix -> TRS -> matrix round-trips inside 1e-4;
//   * a procedural curve t -> local TRS is sampled at the requested phase and lands exactly;
//   * invalid input (NaN, infinity, a singular/absurdly scaled parent, shear, a mirrored part, an unowned
//     part, a null curve) is reported by name and NOT written;
//   * the provenance guard refuses unknown labels and refuses to present test data as Alpha-derived.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CubeGame.CharacterPilot;
using UnityEngine;

public static partial class CharacterPilotVerifier
{
    internal static class PoseAdapterSelfTests
    {
        internal static (int cases, int failed, string json) Run()
        {
            var entries = new List<(string name, bool expectProblems, bool pass, List<string> problems)>();
            int failed = 0;
            string adapterRecord = "pose: not applied";

            void Add(string name, bool expectProblems, List<string> problems, string needle = null)
            {
                bool pass = (problems.Count > 0) == expectProblems;
                if (pass && needle != null)
                    pass = problems.Any(p => p.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!pass) failed++;
                entries.Add((name, expectProblems, pass, problems));
            }

            List<string> ExpectVector(string label, Vector3 actual, Vector3 expected, float tolerance)
            {
                var delta = actual - expected;
                if (delta.magnitude <= tolerance) return new List<string>();
                return new List<string> { $"{label}: got {Fmt(actual)}, expected {Fmt(expected)}" };
            }

            List<string> ExpectAngle(string label, Quaternion actual, Quaternion expected, float tolerance)
            {
                var angle = Quaternion.Angle(actual, expected);
                if (angle <= tolerance) return new List<string>();
                return new List<string> { $"{label}: differs by {angle:0.####} degrees from the expectation" };
            }

            const float Exact = 1e-5f;

            // ------------------------------------------------------------------
            // (a) part matrices — hand-computed parent-relative conversion
            // ------------------------------------------------------------------

            // Parent: translate (10,0,0), no rotation, uniform scale 2.
            // Child world: translate (14,0,4), no rotation, uniform scale 3.
            // By hand: local position = ((14,0,4) - (10,0,0)) / 2 = (2,0,2); local scale = 3/2 = 1.5.
            {
                var parentWorld = Matrix4x4.TRS(new Vector3(10f, 0f, 0f), Quaternion.identity,
                    new Vector3(2f, 2f, 2f));
                var childWorld = Matrix4x4.TRS(new Vector3(14f, 0f, 4f), Quaternion.identity,
                    new Vector3(3f, 3f, 3f));
                var problems = CharacterPoseAdapter.ResolveLocalPose("head", childWorld, parentWorld, out var pose);
                var check = problems.Count > 0
                    ? problems
                    : ExpectVector("local position", pose.position, new Vector3(2f, 0f, 2f), Exact)
                        .Concat(ExpectVector("local scale", pose.scale, new Vector3(1.5f, 1.5f, 1.5f), Exact))
                        .Concat(ExpectAngle("local rotation", pose.rotation, Quaternion.identity, 0.001f))
                        .ToList();
                Add("matrix: translate+uniform-scale conversion matches the hand-computed numbers",
                    false, check);
            }

            // Parent: translate (10,0,0), rotate +90 deg about Y, uniform scale 2.
            // Child world: translate (10,0,4), no rotation, uniform scale 2.
            // By hand: (0,0,4) under the inverse parent rotation (0,0,4) -> (-4,0,0), then /2 = (-2,0,0);
            // local rotation = inverse parent rotation = -90 deg about Y; local scale = 1.
            {
                var parentWorld = Matrix4x4.TRS(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f),
                    new Vector3(2f, 2f, 2f));
                var childWorld = Matrix4x4.TRS(new Vector3(10f, 0f, 4f), Quaternion.identity,
                    new Vector3(2f, 2f, 2f));
                var problems = CharacterPoseAdapter.ResolveLocalPose("hair", childWorld, parentWorld, out var pose);
                var check = problems.Count > 0
                    ? problems
                    : ExpectVector("local position", pose.position, new Vector3(-2f, 0f, 0f), Exact)
                        .Concat(ExpectVector("local scale", pose.scale, Vector3.one, Exact))
                        .Concat(ExpectAngle("local rotation", pose.rotation, Quaternion.Euler(0f, -90f, 0f), 0.01f))
                        .ToList();
                Add("matrix: rotated parent conversion matches the hand-computed numbers", false, check);
            }

            // matrix -> TRS -> matrix round trip. The parent scale is uniform on purpose: a non-uniform
            // parent scale combined with a child rotation produces shear, which a Transform cannot hold and
            // which the adapter rejects by design (see the shear case below).
            {
                var parentWorld = Matrix4x4.TRS(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 30f),
                    new Vector3(2f, 2f, 2f));
                var childWorld = Matrix4x4.TRS(new Vector3(-4f, 5f, 6.5f), Quaternion.Euler(-15f, 40f, 5f),
                    new Vector3(1.5f, 0.75f, 2.25f));
                var local = parentWorld.inverse * childWorld;
                var problems = CharacterPoseAdapter.ResolveLocalPose("sword", childWorld, parentWorld, out var pose);
                if (problems.Count == 0)
                {
                    var reconstructed = Matrix4x4.TRS(pose.position, pose.rotation, pose.scale);
                    var residual = PoseValidation.MaxAbsDifference(local, reconstructed);
                    if (residual > 1e-4f)
                        problems = new List<string> { $"round trip residual {residual:0.######} exceeds 1e-4" };
                }
                Add("matrix: matrix -> TRS -> matrix round trip is inside 1e-4", false, problems);
            }

            // ------------------------------------------------------------------
            // (b) local TRS + ownership + provenance
            // ------------------------------------------------------------------
            var root = new GameObject("pose_adapter_selftest_root");
            var bodyObject = new GameObject("part_body");
            var headObject = new GameObject("part_head");
            var hairObject = new GameObject("part_hair");
            bodyObject.transform.SetParent(root.transform, false);
            headObject.transform.SetParent(bodyObject.transform, false);
            hairObject.transform.SetParent(headObject.transform, false);
            var owned = new Dictionary<string, Transform>
            {
                { "body", bodyObject.transform },
                { "head", headObject.transform },
            };
            var adapter = new CharacterPoseAdapter(owned);
            try
            {
                bodyObject.transform.localPosition = new Vector3(1f, 2f, 3f);
                headObject.transform.localPosition = new Vector3(0f, 2f, 0f);
                hairObject.transform.localPosition = new Vector3(0f, 1f, 0f);

                // Procedural path: t -> local TRS, sampled at a known phase.
                {
                    var curves = new Dictionary<string, Func<float, PoseTrs>>
                    {
                        {
                            "body", t => new PoseTrs(new Vector3(4f * t, 0f, 0f), Quaternion.identity,
                                Vector3.one)
                        },
                        {
                            "head", t => new PoseTrs(new Vector3(0f, 2f, 0f),
                                Quaternion.Euler(0f, 90f * t, 0f), Vector3.one * 0.5f)
                        },
                    };
                    var problems = adapter.ApplyProcedural(curves, 0.25f, PoseProvenance.TestData);
                    adapterRecord = adapter.Describe();
                    var check = problems.Count > 0
                        ? problems
                        : ExpectVector("body local position", bodyObject.transform.localPosition,
                              new Vector3(1f, 0f, 0f), Exact)
                          .Concat(ExpectVector("head local scale", headObject.transform.localScale,
                              Vector3.one * 0.5f, Exact))
                          .Concat(ExpectAngle("head local rotation", headObject.transform.localRotation,
                              Quaternion.Euler(0f, 22.5f, 0f), 0.01f))
                          .ToList();
                    Add("procedural: the sampled curve lands on the expected local TRS", false, check);
                    Add("procedural: two owned parts are applied and the record carries the label", false,
                        check.Count == 0 && adapter.LastAppliedParts == 2 &&
                        adapter.Describe().Contains(PoseProvenance.TestData)
                            ? new List<string>()
                            : new List<string>
                            {
                                $"applied={adapter.LastAppliedParts}, record='{adapter.Describe()}'",
                            });
                }

                // Ownership: a part the adapter was not constructed with must not be written.
                {
                    var hairBefore = hairObject.transform.localPosition;
                    var matrices = new Dictionary<string, Matrix4x4>
                    {
                        { "hair", Matrix4x4.TRS(new Vector3(9f, 9f, 9f), Quaternion.identity, Vector3.one) },
                    };
                    var problems = adapter.ApplyPartMatrices(matrices, PoseProvenance.TestData);
                    var unchanged = hairObject.transform.localPosition == hairBefore;
                    Add("ownership: an unowned part is reported and left untouched", true,
                        unchanged
                            ? problems
                            : problems.Concat(new[] { "the unowned part 'hair' was moved" }).ToList(),
                        "not owned");
                }

                // Provenance: an unknown label must abort before anything is written.
                {
                    var before = bodyObject.transform.localPosition;
                    var poses = new Dictionary<string, PoseTrs>
                    {
                        { "body", new PoseTrs(new Vector3(7f, 7f, 7f), Quaternion.identity, Vector3.one) },
                    };
                    var problems = adapter.ApplyLocalTrs(poses, "ALPHA_LIKE_GUESS");
                    var unchanged = bodyObject.transform.localPosition == before;
                    Add("provenance: an unknown label is refused and nothing is written", true,
                        unchanged
                            ? problems
                            : problems.Concat(new[] { "the pose was applied despite an unknown label" }).ToList(),
                        "unknown label");
                }

                // Parent-first ordering: the head matrix is derived from the NEW body pose; if the child were
                // resolved before the parent, its local pose would not come back.
                {
                    var bodyWorld = Matrix4x4.TRS(new Vector3(3f, 4f, 5f), Quaternion.Euler(0f, 45f, 0f),
                        Vector3.one);
                    var headLocal = new PoseTrs(new Vector3(0f, 2f, 0f), Quaternion.Euler(0f, 30f, 0f),
                        Vector3.one);
                    var headWorld = bodyWorld *
                                    Matrix4x4.TRS(headLocal.position, headLocal.rotation, headLocal.scale);
                    var matrices = new Dictionary<string, Matrix4x4>
                    {
                        { "head", headWorld },
                        { "body", bodyWorld },
                    };
                    var problems = adapter.ApplyPartMatrices(matrices, PoseProvenance.TestData);
                    var check = problems.Count > 0
                        ? problems
                        : ExpectVector("body local position", bodyObject.transform.localPosition,
                              new Vector3(3f, 4f, 5f), 1e-3f)
                          .Concat(ExpectAngle("body local rotation", bodyObject.transform.localRotation,
                              Quaternion.Euler(0f, 45f, 0f), 0.01f))
                          .Concat(ExpectVector("head local position", headObject.transform.localPosition,
                              new Vector3(0f, 2f, 0f), 1e-3f))
                          .Concat(ExpectAngle("head local rotation", headObject.transform.localRotation,
                              Quaternion.Euler(0f, 30f, 0f), 0.01f))
                          .ToList();
                    Add("matrix: parts are applied parent-first (the child round-trips through the new parent)",
                        false, check);
                }

                // Rejections: nothing may be written when the input is invalid.
                {
                    var parentWorld = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(2f, 2f, 2f));
                    var childWorld = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
                    var before = headObject.transform.localPosition;

                    var nanChild = childWorld;
                    nanChild[0, 3] = float.NaN;
                    var infParent = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
                    infParent[2, 2] = float.PositiveInfinity;
                    var singularParent = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                        new Vector3(1f, 1f, 0f));
                    var absurdParent = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                        new Vector3(1f, 1f, 1e-5f));
                    var sheared = Matrix4x4.identity;
                    sheared[0, 1] = 0.5f;
                    var shearedChild = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one) * sheared;

                    Add("rejection: a NaN part matrix is reported", true,
                        CharacterPoseAdapter.ResolveLocalPose("head", nanChild, parentWorld, out _), "not finite");
                    Add("rejection: an infinite parent matrix is reported", true,
                        CharacterPoseAdapter.ResolveLocalPose("head", childWorld, infParent, out _), "not finite");
                    Add("rejection: a singular parent matrix is reported", true,
                        CharacterPoseAdapter.ResolveLocalPose("head", childWorld, singularParent, out _),
                        "singular");
                    Add("rejection: an implausible parent scale is reported", true,
                        CharacterPoseAdapter.ResolveLocalPose("head", childWorld, absurdParent, out _),
                        "plausible range");
                    Add("rejection: a sheared matrix is not applied as TRS", true,
                        CharacterPoseAdapter.ResolveLocalPose("head", shearedChild,
                            Matrix4x4.identity, out _), "not a TRS");

                    var poses = new Dictionary<string, PoseTrs>
                    {
                        {
                            "head", new PoseTrs(Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, -1f))
                        },
                    };
                    Add("rejection: a mirrored local scale is reported", true,
                        adapter.ApplyLocalTrs(poses, PoseProvenance.TestData), "mirrored");
                    var nanCurves = new Dictionary<string, Func<float, PoseTrs>>
                    {
                        {
                            "head", _ => new PoseTrs(new Vector3(float.NaN, 0f, 0f), Quaternion.identity,
                                Vector3.one)
                        },
                    };
                    Add("rejection: a curve returning NaN is reported", true,
                        adapter.ApplyProcedural(nanCurves, 0.5f, PoseProvenance.TestData), "not finite");
                    Add("rejection: a non-finite phase is reported", true,
                        adapter.ApplyProcedural(nanCurves, float.NaN, PoseProvenance.TestData), "phase");
                    Add("rejection: rejected poses did not move the part", false,
                        headObject.transform.localPosition == before
                            ? new List<string>()
                            : new List<string> { "the head moved although every input was rejected" });
                }

                // Provenance guard: test data must never be presentable as Alpha-derived.
                {
                    Add("provenance: test data cannot be labelled Alpha-derived", true,
                        PoseProvenance.Validate(PoseProvenance.TestData, true), "must not be labelled");
                    Add("provenance: an Alpha-origin claim without the Alpha label is refused", true,
                        PoseProvenance.Validate(PoseProvenance.OriginalNotAlpha, true), "requires the");
                    Add("provenance: an empty label is refused", true,
                        PoseProvenance.Validate("", false), "no explicit provenance label");
                    Add("provenance: test data without an origin claim is accepted", false,
                        PoseProvenance.Validate(PoseProvenance.TestData, false));
                    var alphaClaimAdapter = new CharacterPoseAdapter(owned, allowAlphaDerivedClaims: true);
                    Add("provenance: even an opted-in adapter refuses test data as Alpha-derived", true,
                        alphaClaimAdapter.ApplyLocalTrs(
                            new Dictionary<string, PoseTrs>
                            {
                                { "body", new PoseTrs(Vector3.zero, Quaternion.identity, Vector3.one) },
                            }, PoseProvenance.TestData, claimsAlphaDerived: true), "must not be labelled");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            // Snapshot: the adapter record, explicitly labelled as test data.
            try
            {
                Directory.CreateDirectory(OutputDir);
                var json = new StringBuilder();
                json.AppendLine("{");
                json.AppendLine($"  \"provenance\": {Q(PoseProvenance.TestData)},");
                json.AppendLine("  \"note\": \"Synthetic pose data used by the self-tests. NOT extracted from Alpha.\",");
                json.AppendLine($"  \"adapterRecord\": {Q(adapterRecord)},");
                json.AppendLine($"  \"cases\": {entries.Count},");
                json.AppendLine($"  \"failed\": {failed}");
                json.AppendLine("}");
                File.WriteAllText(Path.Combine(OutputDir, "pose_adapter_selftest.json"), json.ToString(),
                    new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Add("snapshot: the pose adapter snapshot is written", false,
                    new List<string> { "cannot write the snapshot: " + e.Message });
            }

            var casesJson = new StringBuilder("[");
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (i > 0) casesJson.Append(',');
                casesJson.Append($"{{\"name\":{Q(entry.name)}," +
                                 $"\"expected\":{(entry.expectProblems ? "\"problems\"" : "\"clean\"")}," +
                                 $"\"passed\":{(entry.pass ? "true" : "false")}," +
                                 $"\"problems\":[{string.Join(",", entry.problems.Select(Q))}]}}");
            }
            casesJson.Append(']');
            return (entries.Count, failed, casesJson.ToString());
        }
    }
}
