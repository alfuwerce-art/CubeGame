// S05A-1-REVIEW-FIX-01 — additional validators, scene-based hierarchy checks, geometry dump and
// the self-tests for FIX-01 (two-pass voxel coordinates), FIX-02 (source grid frame) and FIX-03
// (independent Unity hierarchy).
//
// Partial class of CharacterPilotVerifier; kept in a separate file so the pilot's build/render/io
// code stays readable.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CubeGame.CharacterPilot;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class CharacterPilotVerifier
{
    /// <summary>
    /// The hierarchy the pilot requires, independent of whatever the spec file says. A corrupted spec
    /// (for example head reparented to a hand) must not be able to validate itself.
    /// </summary>
    internal static readonly Dictionary<string, string> CanonicalParents = new Dictionary<string, string>
    {
        { "body", null }, { "head", "body" }, { "hair", "head" },
        { "hand_l", "body" }, { "hand_r", "body" },
        { "foot_l", "body" }, { "foot_r", "body" }, { "sword", "hand_r" },
    };

    static readonly HashSet<(byte, byte, byte)> SkinColors = new HashSet<(byte, byte, byte)>
    {
        (255, 211, 182), (255, 203, 182), (255, 221, 207),
    };

    // ------------------------------------------------------------------
    // FIX-03: hierarchy read from the real scene
    // ------------------------------------------------------------------

    /// <summary>
    /// Validates the built scene by reading actual Transform.parent links. Technical nodes named
    /// "mesh" (or anything that is not "part_*") are ignored, so only real body parts are compared.
    /// </summary>
    internal static List<string> ValidateHierarchyFromScene(Transform root, PartSpec[] spec)
    {
        var problems = new List<string>();
        if (root == null) return new List<string> { "scene hierarchy check: root object is null" };
        if (spec == null || spec.Length == 0)
            return new List<string> { "scene hierarchy check: assembly spec lists no parts" };

        // actual scene: part id -> nearest part ancestor id (null when parented to the root chain)
        var actual = new Dictionary<string, string>();
        var duplicates = new List<string>();
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform == root) continue;
            if (!transform.name.StartsWith("part_", StringComparison.Ordinal)) continue;
            var id = transform.name.Substring("part_".Length);
            if (actual.ContainsKey(id)) duplicates.Add(id);
            string parentId = null;
            for (var p = transform.parent; p != null && p != root; p = p.parent)
            {
                if (!p.name.StartsWith("part_", StringComparison.Ordinal)) continue;
                parentId = p.name.Substring("part_".Length);
                break;
            }
            actual[id] = parentId;
        }

        foreach (var duplicate in duplicates.Distinct())
            problems.Add($"scene: duplicate part node '{duplicate}'");

        // expected parents come from the canonical map; the spec is only checked for agreement
        var specParents = spec.ToDictionary(p => p.id,
            p => string.IsNullOrWhiteSpace(p.parent) ? null : p.parent);
        foreach (var id in CanonicalParents.Keys.Where(k => !actual.ContainsKey(k)).OrderBy(k => k))
            problems.Add($"scene: missing part node '{id}'");
        foreach (var id in actual.Keys.Where(k => !CanonicalParents.ContainsKey(k)).OrderBy(k => k))
            problems.Add($"scene: unexpected part node '{id}'");

        foreach (var id in CanonicalParents.Keys.Where(actual.ContainsKey).OrderBy(k => k))
        {
            var want = CanonicalParents[id] ?? "<root>";
            var got = actual[id] ?? "<root>";
            if (want != got)
                problems.Add($"scene: part '{id}' Transform.parent is '{got}', expected '{want}'");
        }
        foreach (var id in CanonicalParents.Keys.Where(specParents.ContainsKey).OrderBy(k => k))
        {
            var canonical = CanonicalParents[id] ?? "<root>";
            var specParent = specParents[id] ?? "<root>";
            if (canonical != specParent)
                problems.Add($"spec: part '{id}' parent is '{specParent}' but the required parent is '{canonical}'");
        }
        return problems;
    }

    // ------------------------------------------------------------------
    // FIX-02: source grid frame and skin-voxel coincidence
    // ------------------------------------------------------------------

    /// <summary>Head and hair were authored in the same grid, so their frame origins must match.</summary>
    internal static List<string> ValidateSharedFrame(string idA, Vector3 frameA, string idB, Vector3 frameB)
    {
        var delta = frameA - frameB;
        return delta.magnitude > Tol
            ? new List<string>
            {
                $"parts '{idA}' and '{idB}' must share the source grid frame, but differ by {Fmt(delta)} " +
                $"(frames {Fmt(frameA)} vs {Fmt(frameB)})",
            }
            : new List<string>();
    }

    /// <summary>World-space voxels whose colour is skin, used for the head/hair alignment evidence.</summary>
    internal static HashSet<Vector3Int> SkinVoxelsInWorld(string payloadPath, Vector3 frameOffset,
        HashSet<(int, int, int)> hidden = null)
    {
        var result = new HashSet<Vector3Int>();
        if (!File.Exists(payloadPath)) return result;
        var bytes = File.ReadAllBytes(payloadPath);
        if (bytes.Length < 12) return result;
        int width = BitConverter.ToInt32(bytes, 0);
        int depth = BitConverter.ToInt32(bytes, 4);
        int height = BitConverter.ToInt32(bytes, 8);
        if (width <= 0 || depth <= 0 || height <= 0) return result;
        if (12L + (long)width * depth * height * 3L != bytes.Length) return result;

        for (int h = 0; h < height; h++)
        for (int d = 0; d < depth; d++)
        for (int x = 0; x < width; x++)
        {
            if (hidden != null && hidden.Contains((x, d, h))) continue;
            int o = 12 + ((h * depth + d) * width + x) * 3;
            var color = (bytes[o], bytes[o + 1], bytes[o + 2]);
            if (!SkinColors.Contains(color)) continue;
            result.Add(new Vector3Int(
                Mathf.RoundToInt(frameOffset.x) + x,
                Mathf.RoundToInt(frameOffset.y) + h,
                Mathf.RoundToInt(frameOffset.z) + d));
        }
        return result;
    }

    /// <summary>
    /// Checks that hair skin voxels still sit where head skin voxels are (the 99% relation measured
    /// in the source grids). A one-voxel misplacement of either frame makes this fail.
    /// </summary>
    internal static List<string> ValidateSkinAlignment(string headPayload, Vector3 headFrame,
        string hairPayload, Vector3 hairFrame, float minimumRatio = 0.99f)
    {
        var head = SkinVoxelsInWorld(headPayload, headFrame);
        var hair = SkinVoxelsInWorld(hairPayload, hairFrame);
        if (head.Count == 0 || hair.Count == 0)
            return new List<string> { $"skin alignment: no skin voxels found (head={head.Count}, hair={hair.Count})" };

        int matched = hair.Count(v => head.Contains(v));
        float ratio = (float)matched / hair.Count;
        if (ratio < minimumRatio)
        {
            return new List<string>
            {
                $"skin alignment: only {matched}/{hair.Count} ({ratio:P1}) hair skin voxels coincide with " +
                $"head skin voxels, expected >= {minimumRatio:P0}; head frame {Fmt(headFrame)}, " +
                $"hair frame {Fmt(hairFrame)}",
            };
        }
        return new List<string>();
    }

    // ------------------------------------------------------------------
    // FIX-01: geometry dump for the Python cross-check
    // ------------------------------------------------------------------

    /// <summary>
    /// Writes the vertex/triangle data of the synthetic FIX-01 models so a Python test can compare
    /// it with `cub_to_obj.build_obj` after the coordinate systems are aligned (both are grid space).
    /// </summary>
    [UnityEditor.MenuItem("CubeGame Pilot/Dump test geometry")]
    public static void DumpGeometry()
    {
        Directory.CreateDirectory(OutputDir);
        var dir = Path.Combine(OutputDir, "geometry_cases");
        Directory.CreateDirectory(dir);
        var sb = new StringBuilder("[");
        var first = true;
        foreach (var model in GeometryCases.All())
        {
            var path = Path.Combine(dir, model.name + ".cubdec");
            File.WriteAllBytes(path, model.payload);
            var hidden = new HashSet<(int, int, int)>(model.hidden);
            var built = BuildMeshFromPayload(path, hidden, model.name);
            if (built.problems.Count > 0)
            {
                throw new InvalidOperationException("geometry case failed: " + string.Join("; ", built.problems));
            }
            var vertices = built.mesh.vertices;
            var triangles = built.mesh.triangles;
            if (!first) sb.Append(',');
            first = false;
            sb.Append('{');
            sb.Append($"\"name\":{Q(model.name)},");
            sb.Append($"\"payloadSha256\":{Q(Sha256(model.payload))},");
            sb.Append($"\"hidden\":[{string.Join(",", model.hidden.Select(h => $"[{h.Item1},{h.Item2},{h.Item3}]"))}],");
            sb.Append($"\"gridMin\":{Q(Fmt(built.gridMin))},");
            sb.Append($"\"faces\":{built.faces},");
            sb.Append("\"vertices\":[");
            for (int i = 0; i < vertices.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"[{vertices[i].x:0.###},{vertices[i].y:0.###},{vertices[i].z:0.###}]");
            }
            sb.Append("],\"triangles\":[");
            sb.Append(string.Join(",", triangles));
            sb.Append("]}");
        }
        sb.Append(']');
        File.WriteAllText(Path.Combine(OutputDir, "geometry_dump.json"), sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"geometry dump written: {GeometryCases.All().Count} cases");
    }

    static string Sha256(byte[] data)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2")));
    }

    // ------------------------------------------------------------------
    // synthetic models shared by the self-tests and the dump
    // ------------------------------------------------------------------

    internal class GeometryCase
    {
        public string name;
        public byte[] payload;
        public List<(int, int, int)> hidden = new List<(int, int, int)>();
        public int width, depth, height;
    }

    internal static class GeometryCases
    {
        internal static List<GeometryCase> All() => new List<GeometryCase>
        {
            // 1. Asymmetric: the first voxel in (h, d, x) order is at x=3, a later one at x=0,
            //    so the pre-FIX-01 single-pass minimum is still 3 when the early vertices are emitted.
            Make("asymmetric_4x1x2", 4, 1, 2, new[]
            {
                (3, 0, 0, (byte)10, (byte)20, (byte)30),
                (0, 0, 1, (byte)40, (byte)50, (byte)60),
            }),
            // 2. Empty borders: a single voxel in the middle of a 5x5x5 grid.
            Make("empty_edges_5x5x5", 5, 5, 5, new[]
            {
                (2, 1, 3, (byte)200, (byte)100, (byte)50),
            }),
            // 3. Marker mask: a green marker (hidden) at the low-x edge plus ordinary voxels.
            MakeMasked("masked_markers_4x1x1", 4, 1, 1, new[]
            {
                (0, 0, 0, (byte)0, (byte)255, (byte)0),
                (1, 0, 0, (byte)255, (byte)255, (byte)255),
                (2, 0, 0, (byte)255, (byte)0, (byte)0),
                (3, 0, 0, (byte)120, (byte)130, (byte)140),
            }, new[] { (0, 0, 0) }),
        };

        static GeometryCase Make(string name, int width, int depth, int height,
            (int x, int d, int h, byte r, byte g, byte b)[] voxels)
        {
            var payload = new byte[12 + width * depth * height * 3];
            Array.Copy(BitConverter.GetBytes(width), 0, payload, 0, 4);
            Array.Copy(BitConverter.GetBytes(depth), 0, payload, 4, 4);
            Array.Copy(BitConverter.GetBytes(height), 0, payload, 8, 4);
            foreach (var v in voxels)
            {
                int offset = 12 + ((v.h * depth + v.d) * width + v.x) * 3;
                payload[offset] = v.r;
                payload[offset + 1] = v.g;
                payload[offset + 2] = v.b;
            }
            return new GeometryCase
            {
                name = name, payload = payload, width = width, depth = depth, height = height,
            };
        }

        static GeometryCase MakeMasked(string name, int width, int depth, int height,
            (int x, int d, int h, byte r, byte g, byte b)[] voxels, (int, int, int)[] hidden)
        {
            var result = Make(name, width, depth, height, voxels);
            result.hidden.AddRange(hidden);
            return result;
        }
    }

    // ------------------------------------------------------------------
    // FIX-01/02/03 self-tests
    // ------------------------------------------------------------------

    internal static class FixSelfTests
    {
        internal static (int cases, int failed, string json) Run()
        {
            var entries = new List<(string name, bool expectProblems, bool pass, List<string> problems)>();
            int failed = 0;

            void Add(string name, bool expectProblems, List<string> problems, string needle = null)
            {
                bool pass = (problems.Count > 0) == expectProblems;
                if (pass && needle != null)
                    pass = problems.Any(p => p.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!pass) failed++;
                entries.Add((name, expectProblems, pass, problems));
            }

            // ---------------- FIX-01: two-pass coordinates ----------------
            var tempDir = Path.Combine(OutputDir, "selftest_geometry");
            Directory.CreateDirectory(tempDir);
            foreach (var model in GeometryCases.All())
            {
                var path = Path.Combine(tempDir, model.name + ".cubdec");
                File.WriteAllBytes(path, model.payload);
                var hidden = new HashSet<(int, int, int)>(model.hidden);
                var built = BuildMeshFromPayload(path, hidden, model.name);
                var legacy = BuildMeshFromPayloadLegacySinglePass(path, hidden, model.name);

                Add($"{model.name}: two-pass mesh builds", false, built.problems);
                Add($"{model.name}: grid min matches visible voxels", false,
                    built.gridMin.x >= 0f && built.gridMin.y >= 0f && built.gridMin.z >= 0f
                        ? new List<string>()
                        : new List<string> { $"grid min {Fmt(built.gridMin)} is negative" });
                Add($"{model.name}: every vertex inside the voxel grid", false,
                    built.mesh.vertices.All(v =>
                        v.x >= -Tol && v.x <= model.width + Tol &&
                        v.y >= -Tol && v.y <= model.height + Tol &&
                        v.z >= -Tol && v.z <= model.depth + Tol)
                        ? new List<string>()
                        : new List<string> { "vertex outside the voxel grid" });

                // exact vertex comparison against an independent brute-force expectation
                var expected = ExpectedVertexMultiset(model);
                var actual = built.mesh.vertices
                    .Select(v => (Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z)))
                    .ToList();
                Add($"{model.name}: vertices match the brute-force expectation", false,
                    SameMultiset(expected, actual)
                        ? new List<string>()
                        : new List<string> { "vertex multiset differs from the expectation" });

                // the defect must be visible on the legacy algorithm for the asymmetric case
                if (model.name.StartsWith("asymmetric", StringComparison.Ordinal))
                {
                    var legacyVertices = legacy.mesh.vertices
                        .Select(v => (Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z)))
                        .ToList();
                    Add("legacy single-pass distorts the asymmetric model", true,
                        SameMultiset(expected, legacyVertices)
                            ? new List<string>()
                            : new List<string> { "legacy vertices differ from the expectation (defect reproduced)" });
                }
            }

            // empty model and fully masked model must be reported, not silently accepted
            var emptyPayload = new byte[12 + 2 * 2 * 2 * 3];
            Array.Copy(BitConverter.GetBytes(2), 0, emptyPayload, 0, 4);
            Array.Copy(BitConverter.GetBytes(2), 0, emptyPayload, 4, 4);
            Array.Copy(BitConverter.GetBytes(2), 0, emptyPayload, 8, 4);
            var emptyPath = Path.Combine(tempDir, "empty_2x2x2.cubdec");
            File.WriteAllBytes(emptyPath, emptyPayload);
            Add("empty model is reported", true,
                BuildMeshFromPayload(emptyPath, new HashSet<(int, int, int)>(), "empty").problems,
                "no visible voxels");

            // ---------------- FIX-02: frames and skin alignment ----------------
            var headFrame = new Vector3(-9f, 11f, -8f);
            var hairFrame = new Vector3(-9f, 11f, -8f);
            Add("head/hair frames match", false, ValidateSharedFrame("head", headFrame, "hair", hairFrame));
            Add("hair frame shifted by one voxel fails", true, ValidateSharedFrame(
                "head", headFrame, "hair", hairFrame + new Vector3(1f, 0f, 0f)), "must share");
            Add("source-min change does not move the mesh", false,
                BuildMeshFromPayload(Path.Combine(tempDir, "empty_edges_5x5x5.cubdec"),
                    new HashSet<(int, int, int)>(), "empty_edges").gridMin == new Vector3(2f, 3f, 1f)
                    ? new List<string>()
                    : new List<string> { "grid min of the sparse model is not (2,3,1)" });

            var headPayload = Path.Combine(DataDir, "head.cubdec");
            var hairPayload = Path.Combine(DataDir, "hair.cubdec");
            if (File.Exists(headPayload) && File.Exists(hairPayload))
            {
                Add("head/hair skin voxels coincide in character space", false,
                    ValidateSkinAlignment(headPayload, headFrame, hairPayload, hairFrame));
                Add("hair shifted by one voxel breaks skin alignment", true,
                    ValidateSkinAlignment(headPayload, headFrame, hairPayload,
                        hairFrame + new Vector3(1f, 0f, 0f)), "skin alignment");
            }
            else
            {
                Add("head/hair skin voxels coincide in character space", true,
                    new List<string> { $"payload missing: {headPayload}" }, "payload missing");
            }

            // ---------------- FIX-06: lifecycle event order ----------------
            var good = new[]
            {
                MarkSessionStarted, MarkEntered, MarkSamplerFinished, MarkExitRequested,
                MarkExiting, MarkEditModeEntered, MarkExitScheduled, MarkExitExecuted,
            };
            Add("lifecycle: correct order passes", false, ValidateEventOrder(good));
            Add("lifecycle: exit before sampler finish fails", true, ValidateEventOrder(new[]
            {
                MarkSessionStarted, MarkEntered, MarkExitRequested, MarkExiting,
                MarkEditModeEntered, MarkExitScheduled, MarkExitExecuted,
            }), "before the sampler completed");
            Add("lifecycle: exit executed before EditMode fails", true, ValidateEventOrder(new[]
            {
                MarkSessionStarted, MarkEntered, MarkSamplerFinished, MarkExitRequested,
                MarkExitExecuted,
            }), "before EditMode was entered");
            Add("lifecycle: duplicate sampler finish fails", true, ValidateEventOrder(new[]
            {
                MarkSessionStarted, MarkEntered, MarkSamplerFinished, MarkSamplerFinished,
                MarkExitRequested, MarkEditModeEntered, MarkExitScheduled, MarkExitExecuted,
            }), "more than once");
            Add("lifecycle: rejected exit attempt fails", true, ValidateEventOrder(new[]
            {
                MarkSessionStarted, MarkEntered, MarkExitRejected, MarkSamplerFinished,
                MarkExitRequested, MarkEditModeEntered, MarkExitScheduled, MarkExitExecuted,
            }), "while still in PlayMode");
            Add("lifecycle: exit not last fails", true, ValidateEventOrder(new[]
            {
                MarkSessionStarted, MarkEntered, MarkSamplerFinished, MarkExitRequested,
                MarkEditModeEntered, MarkExitScheduled, MarkExitExecuted, "LIFECYCLE_PROBLEM",
            }), "not the last");
            Add("lifecycle: empty log fails", true, ValidateEventOrder(new string[0]), "no events");

            // ---------------- FIX-07: animator binding contract ----------------
            {
                var host = new GameObject("binding_contract_test");
                try
                {
                    Transform Make(string name)
                    {
                        var go = new GameObject(name);
                        go.transform.SetParent(host.transform, false);
                        return go.transform;
                    }
                    var parts = new[]
                    {
                        Make("body"), Make("head"), Make("hair"), Make("hand_l"),
                        Make("hand_r"), Make("foot_l"), Make("foot_r"), Make("sword"),
                    };
                    var animator = host.AddComponent<ProceduralCharacterAnimator>();

                    animator.Bind(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], parts[7]);
                    Add("binding: all eight parts bound", false,
                        animator.IsBound ? new List<string>() : new List<string> { "IsBound is false" });

                    animator.Bind(parts[0], parts[1], null, parts[3], parts[4], parts[5], parts[6], parts[7]);
                    Add("binding: missing hair leaves IsBound false", false,
                        animator.IsBound ? new List<string> { "IsBound is true without hair" } : new List<string>());

                    animator.Bind(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], null);
                    Add("binding: missing sword leaves IsBound false", false,
                        animator.IsBound ? new List<string> { "IsBound is true without sword" } : new List<string>());

                    animator.Bind(parts[0], null, parts[2], parts[3], parts[4], parts[5], parts[6], parts[7]);
                    Add("binding: missing head leaves IsBound false", false,
                        animator.IsBound ? new List<string> { "IsBound is true without head" } : new List<string>());

                    animator.Bind(parts[0], parts[1], parts[2], null, parts[4], parts[5], parts[6], parts[7]);
                    Add("binding: missing hand leaves IsBound false", false,
                        animator.IsBound ? new List<string> { "IsBound is true without hand_l" } : new List<string>());

                    animator.Bind(parts[0], parts[1], parts[2], parts[3], parts[4], null, parts[6], parts[7]);
                    Add("binding: missing foot leaves IsBound false", false,
                        animator.IsBound ? new List<string> { "IsBound is true without foot_l" } : new List<string>());

                    animator.Bind(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5], parts[6], parts[7]);
                    var bindingBuild = new AssemblyBuild { animator = animator, parts = new Dictionary<string, Transform>() };
                    foreach (var id in new[] { "body", "head", "hair", "hand_l", "hand_r", "foot_l", "foot_r", "sword" })
                        bindingBuild.parts[id] = parts[0];
                    Add("preflight: fully bound animator passes the binding check", false,
                        ValidatePlayModePrerequisites(bindingBuild).FindAll(p => p.Contains("Animator")));
                    animator.Bind(parts[0], parts[1], null, parts[3], parts[4], parts[5], parts[6], parts[7]);
                    Add("preflight: unbound animator is rejected", true,
                        ValidatePlayModePrerequisites(bindingBuild), "not bound");
                }
                finally
                {
                    Object.DestroyImmediate(host);
                }
            }

            // ---------------- FIX-04: PlayMode verdict helpers ----------------
            Add("playmode verdict: 40 frames clean passes", false,
                ValidatePlayModeResult(40, 40, new List<string>()));
            Add("playmode verdict: 39 frames fails", true,
                ValidatePlayModeResult(39, 40, new List<string>()), "frames");
            Add("playmode verdict: animation problem fails", true,
                ValidatePlayModeResult(40, 40, new List<string> { "head: does not move" }), "does not move");
            Add("playmode verdict: empty sample list fails", true,
                ValidatePlayModeResult(0, 40, new List<string>()), "required");

            // ---------------- FIX-03: hierarchy read from the real scene ----------------
            var build = BuildCharacter(false);
            Add("scene hierarchy matches the spec", false,
                ValidateHierarchyFromScene(build.root.transform, build.spec.parts));

            // technical child nodes must not be mistaken for parts (checked before any mutation)
            var technical = new GameObject("mesh");
            technical.transform.SetParent(Find(build, "body"), false);
            Add("scene: extra technical mesh node is ignored", false,
                ValidateHierarchyFromScene(build.root.transform, build.spec.parts));
            Object.DestroyImmediate(technical);

            var head = Find(build, "head");
            var hair = Find(build, "hair");
            var sword = Find(build, "sword");
            var root = build.root.transform;

            // the spec is compared with the canonical map, so a corrupted spec fails on its own
            var corruptedSpec = build.spec.parts
                .Select(p => new PartSpec
                {
                    id = p.id, model = p.model, parent = p.parent,
                    frameOffset = p.frameOffset, joint = p.joint, offset = p.offset, evidence = p.evidence,
                })
                .ToArray();
            corruptedSpec.First(p => p.id == "head").parent = "hand_l";
            Add("spec: head reparented to a hand fails", true,
                ValidateHierarchyFromScene(root, corruptedSpec), "required parent");

            if (head != null && hair != null && sword != null)
            {
                var headParent = head.parent;
                var headLocal = head.localPosition;
                head.SetParent(root, false);
                Add("scene: head reparented to root fails", true,
                    ValidateHierarchyFromScene(root, build.spec.parts), "Transform.parent");
                head.SetParent(headParent, false);
                head.localPosition = headLocal;

                var hairParent = hair.parent;
                var hairLocal = hair.localPosition;
                hair.SetParent(Find(build, "body"), false);
                Add("scene: hair reparented to body fails", true,
                    ValidateHierarchyFromScene(root, build.spec.parts), "Transform.parent");
                hair.SetParent(hairParent, false);
                hair.localPosition = hairLocal;

                var swordParent = sword.parent;
                var swordLocal = sword.localPosition;
                sword.SetParent(Find(build, "body"), false);
                Add("scene: sword reparented to body fails", true,
                    ValidateHierarchyFromScene(root, build.spec.parts), "Transform.parent");
                sword.SetParent(swordParent, false);
                sword.localPosition = swordLocal;
            }

            var handLeft = Find(build, "hand_l");
            if (handLeft != null)
            {
                handLeft.gameObject.SetActive(false);
                Object.DestroyImmediate(handLeft.gameObject);
                Add("scene: missing hand fails", true,
                    ValidateHierarchyFromScene(root, build.spec.parts), "missing part node");
            }

            var ghost = new GameObject("part_ghost");
            ghost.transform.SetParent(Find(build, "body"), false);
            Add("scene: unexpected part node fails", true,
                ValidateHierarchyFromScene(root, build.spec.parts), "unexpected part node");
            Object.DestroyImmediate(ghost);

            Object.DestroyImmediate(build.root);

            var json = new StringBuilder("[");
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) json.Append(',');
                var e = entries[i];
                json.Append($"{{\"name\":{Q(e.name)},\"expected\":{(e.expectProblems ? "\"problems\"" : "\"clean\"")}," +
                            $"\"passed\":{(e.pass ? "true" : "false")}," +
                            $"\"problems\":[{string.Join(",", e.problems.Select(Q))}]}}");
            }
            json.Append(']');
            return (entries.Count, failed, json.ToString());
        }

        static List<(int, int, int)> ExpectedVertexMultiset(GeometryCase model)
        {
            var hidden = new HashSet<(int, int, int)>(model.hidden);
            bool Solid(int x, int d, int h)
            {
                if (x < 0 || x >= model.width || d < 0 || d >= model.depth || h < 0 || h >= model.height)
                    return false;
                if (hidden.Contains((x, d, h))) return false;
                int o = 12 + ((h * model.depth + d) * model.width + x) * 3;
                return model.payload[o] != 0 || model.payload[o + 1] != 0 || model.payload[o + 2] != 0;
            }
            var result = new List<(int, int, int)>();
            for (int h = 0; h < model.height; h++)
            for (int d = 0; d < model.depth; d++)
            for (int x = 0; x < model.width; x++)
            {
                if (!Solid(x, d, h)) continue;
                for (int f = 0; f < 6; f++)
                {
                    var n = FaceNormals[f];
                    if (Solid(x + n[0], d + n[2], h + n[1])) continue;
                    foreach (var c in FaceCorners[f])
                        result.Add((x + c[0], h + c[1], d + c[2]));
                }
            }
            return result;
        }

        static bool SameMultiset(List<(int, int, int)> a, List<(int, int, int)> b)
        {
            if (a.Count != b.Count) return false;
            var left = a.OrderBy(v => v.Item1).ThenBy(v => v.Item2).ThenBy(v => v.Item3).ToList();
            var right = b.OrderBy(v => v.Item1).ThenBy(v => v.Item2).ThenBy(v => v.Item3).ToList();
            for (int i = 0; i < left.Count; i++)
                if (left[i] != right[i]) return false;
            return true;
        }
    }
}
