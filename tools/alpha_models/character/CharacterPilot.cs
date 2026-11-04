// S05A-1 pilot — isolated Unity verification project (NOT the production CubeGame project).
//
// Builds one technical humanoid from separate .cub parts, validates hierarchy / pivots /
// orientation, renders reference images, samples one procedural technical cycle and runs the
// same assembly in real PlayMode.
//
// Entry points (batchmode):
//   -executeMethod CharacterPilotVerifier.Run           build + validate + render + report
//   -executeMethod CharacterPilotVerifier.RunSelfTests validator negative tests (no assets)
//   -executeMethod CharacterPilotVerifier.RunPlayMode  enter PlayMode, sample real Update() frames
//
// Verdicts land in PilotOutput/*.txt as CHARACTER_RESULT / SELFTEST_RESULT / PLAYMODE_RESULT;
// a failing run throws so Unity exits non-zero.

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
    const string DataDir = "Assets/CharacterPilot/data";
    const string OutputDir = "PilotOutput";
    const string MeshDir = "Assets/CharacterPilot/generated";
    const string ScenePath = "Assets/CharacterPilot/PilotScene.unity";
    const float Tol = 0.001f;
    const int CaptureSize = 640;

    static readonly int[][] FaceNormals =
    {
        new[] { 1, 0, 0 }, new[] { -1, 0, 0 },
        new[] { 0, 1, 0 }, new[] { 0, -1, 0 },
        new[] { 0, 0, 1 }, new[] { 0, 0, -1 },
    };

    static readonly int[][][] FaceCorners =
    {
        new[] { new[]{1,0,0}, new[]{1,1,0}, new[]{1,1,1}, new[]{1,0,1} },
        new[] { new[]{0,0,1}, new[]{0,1,1}, new[]{0,1,0}, new[]{0,0,0} },
        new[] { new[]{0,1,0}, new[]{0,1,1}, new[]{1,1,1}, new[]{1,1,0} },
        new[] { new[]{0,0,0}, new[]{1,0,0}, new[]{1,0,1}, new[]{0,0,1} },
        new[] { new[]{0,0,1}, new[]{1,0,1}, new[]{1,1,1}, new[]{0,1,1} },
        new[] { new[]{1,0,0}, new[]{0,0,0}, new[]{0,1,0}, new[]{1,1,0} },
    };

    [Serializable] internal class PartSpec
    {
        public string id;
        public string model;
        public string parent;
        // FIX-02: placement is defined by the source grid frame, not by a trimmed bounding box.
        // frameOffset = origin of the model's voxel grid in character space;
        // joint       = pivot position inside that grid (anatomical joint).
        public float[] frameOffset;
        public float[] joint;
        public float[] offset;   // legacy/derived value kept for the report; not used for placement
        public string evidence;
    }

    [Serializable] internal class AssemblySpec
    {
        public string character;
        public string status;
        public string[] notes;
        public PartSpec[] parts;
    }

    internal class PartResult
    {
        public string id;
        public string model;
        public string parent;
        public Vector3 offset;
        public Vector3 frameOffset;
        public Vector3 joint;
        public Vector3 sourceMin;      // filled-area origin inside the model's own grid
        public Vector3 gridSize;
        public Vector3 worldMin;
        public Vector3 worldSize;
        public Vector3 expectedSize;
        public int faces;
        public int vertices;
        public int triangles;
        public int hiddenMarkers;
        public string evidence = "";
        public List<string> problems = new List<string>();
    }

    internal class AssemblyBuild
    {
        public GameObject root;
        public AssemblySpec spec;
        public Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
        public Dictionary<string, Vector3> pivotOrigins = new Dictionary<string, Vector3>();
        public List<PartResult> results = new List<PartResult>();
        public List<string> problems = new List<string>();
        public ProceduralCharacterAnimator animator;
    }

    // ------------------------------------------------------------------
    // validators (pure; also exercised by the self-tests)
    // ------------------------------------------------------------------

    internal static List<string> ValidateHierarchy(IReadOnlyList<string> expectedIds,
        IReadOnlyList<string> actualIds, IReadOnlyList<string> expectedParents,
        IReadOnlyList<string> actualParents)
    {
        var problems = new List<string>();
        if (expectedIds == null || expectedIds.Count == 0)
            problems.Add("assembly spec lists no parts");
        if (actualIds == null || actualIds.Count == 0)
            problems.Add("no parts were built");
        if (expectedIds == null || actualIds == null) return problems;

        foreach (var id in expectedIds.Where(i => !actualIds.Contains(i)).OrderBy(i => i))
            problems.Add($"missing part '{id}'");
        foreach (var id in actualIds.Where(i => !expectedIds.Contains(i)).OrderBy(i => i))
            problems.Add($"unexpected part '{id}'");
        foreach (var duplicate in actualIds.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key))
            problems.Add($"duplicate part '{duplicate}'");

        for (int i = 0; i < expectedIds.Count && i < actualIds.Count; i++)
        {
            if (expectedIds[i] != actualIds[i]) continue;
            var want = expectedParents[i] ?? "<root>";
            var got = actualParents[i] ?? "<root>";
            if (want != got)
                problems.Add($"part '{expectedIds[i]}' parent is '{got}', expected '{want}'");
        }
        return problems;
    }

    internal static List<string> ValidatePartPlacement(string id, Vector3 expectedOffset,
        Vector3 expectedSize, Vector3 worldMin, Vector3 worldSize)
    {
        var problems = new List<string>();
        if (float.IsNaN(worldMin.x) || float.IsNaN(worldSize.x))
            return new List<string> { $"part '{id}': world bounds are NaN" };
        if ((worldMin - expectedOffset).magnitude > Tol)
            problems.Add($"part '{id}': world min {Fmt(worldMin)} != spec offset {Fmt(expectedOffset)}");
        if ((worldSize - expectedSize).magnitude > Tol)
            problems.Add($"part '{id}': world size {Fmt(worldSize)} != voxel extent {Fmt(expectedSize)}");
        if (worldSize.x <= 0f || worldSize.y <= 0f || worldSize.z <= 0f)
            problems.Add($"part '{id}': degenerate size {Fmt(worldSize)}");
        return problems;
    }

    /// <summary>
    /// The mesh is emitted in the model's grid coordinates, so its minimum must land exactly on the
    /// grid minimum of the visible voxels: any shift means the geometry was re-centred (old FIX-01
    /// bug) or mirrored (OBJ import path).
    /// </summary>
    internal static List<string> ValidateOrientation(string id, Vector3 localMin, Vector3 expectedSize,
        Vector3 gridMin)
    {
        var problems = new List<string>();
        if ((localMin - gridMin).magnitude > Tol)
            problems.Add($"part '{id}': mesh min {Fmt(localMin)} != grid min {Fmt(gridMin)} " +
                         "(geometry re-centred or mirrored)");
        if (localMin.x < -Tol || localMin.y < -Tol || localMin.z < -Tol)
            problems.Add($"part '{id}': mesh min {Fmt(localMin)} is negative (mirrored or mis-centred)");
        if (expectedSize.x <= 0f || expectedSize.y <= 0f || expectedSize.z <= 0f)
            problems.Add($"part '{id}': invalid expected size {Fmt(expectedSize)}");
        return problems;
    }

    /// <summary>Both feet must rest on the ground plane (y = 0) of the character space.</summary>
    internal static List<string> ValidateGroundContact(Vector3 leftFootMin, Vector3 rightFootMin)
    {
        var problems = new List<string>();
        foreach (var (name, min) in new[] { ("foot_l", leftFootMin), ("foot_r", rightFootMin) })
        {
            if (Mathf.Abs(min.y) > 0.02f)
                problems.Add($"part '{name}': bottom is at y={min.y:0.###}, expected 0 (feet must touch the ground)");
        }
        return problems;
    }

    internal static List<string> ValidateHeadAttachment(Vector3 bodyTop, Vector3 headMin)
    {
        var problems = new List<string>();
        var gap = headMin.y - bodyTop.y;
        if (gap > 0.02f) problems.Add($"head floats {gap:0.###} above the torso (pivot detached)");
        if (gap < -0.02f) problems.Add($"head sinks {-gap:0.###} into the torso");
        return problems;
    }

    /// <summary>
    /// The socket contract is "the weapon keeps a fixed offset in the hand's own space".
    /// World-space deltas rotate with the arm, so they are not a valid invariant.
    /// </summary>
    internal static List<string> ValidateWeaponSocket(IReadOnlyList<Vector3> handLocalOffsets)
    {
        if (handLocalOffsets == null || handLocalOffsets.Count < 2)
            return new List<string> { "weapon socket check needs at least two samples" };
        var problems = new List<string>();
        var reference = handLocalOffsets[0];
        for (int i = 1; i < handLocalOffsets.Count; i++)
        {
            var delta = handLocalOffsets[i] - reference;
            if (delta.magnitude > 1e-3f)
            {
                problems.Add($"sword drifted in the hand at sample {i}: offset delta {Fmt(delta)}");
                break;
            }
        }
        return problems;
    }

    internal static List<string> ValidateLoop(IReadOnlyList<Vector3> frames)
    {
        if (frames == null || frames.Count < 2)
            return new List<string> { "loop check needs at least two samples" };
        var drift = frames[frames.Count - 1] - frames[0];
        return drift.magnitude > 1e-3f
            ? new List<string> { $"cycle does not loop: first/last sample differ by {Fmt(drift)}" }
            : new List<string>();
    }

    internal static List<string> ValidateMotion(IReadOnlyList<Vector3> frames, float minTravel)
    {
        if (frames == null || frames.Count < 2)
            return new List<string> { "motion check needs at least two samples" };
        float travel = 0f;
        for (int i = 1; i < frames.Count; i++) travel = Mathf.Max(travel, (frames[i] - frames[0]).magnitude);
        return travel < minTravel
            ? new List<string> { $"animation does not move the part (max travel {travel:0.###})" }
            : new List<string>();
    }

    // ------------------------------------------------------------------
    // build
    // ------------------------------------------------------------------

    internal static AssemblyBuild BuildCharacter(bool saveAssets)
    {
        var build = new AssemblyBuild();
        var specPath = Path.Combine(DataDir, "assembly.json");
        if (!File.Exists(specPath))
            throw new InvalidOperationException($"assembly spec not found: {specPath}");
        build.spec = JsonUtility.FromJson<AssemblySpec>(File.ReadAllText(specPath));
        if (build.spec?.parts == null || build.spec.parts.Length == 0)
            throw new InvalidOperationException("assembly spec lists no parts");

        var shader = Shader.Find("CubeGamePilot/VertexColorUnlit");
        if (shader == null) throw new InvalidOperationException("vertex colour shader missing");
        var markers = LoadMarkerMetadata();

        build.root = new GameObject("character_" + build.spec.character);
        var done = new HashSet<string>();
        var pending = new List<PartSpec>(build.spec.parts);
        var ordered = new List<PartSpec>();
        while (pending.Count > 0)
        {
            var next = pending.FirstOrDefault(p => string.IsNullOrWhiteSpace(p.parent) || done.Contains(p.parent));
            if (next == null)
                throw new InvalidOperationException(
                    "assembly spec has a parent cycle or an unknown parent; unresolved: " +
                    string.Join(", ", pending.Select(p => $"{p.id}->'{p.parent}'")));
            ordered.Add(next);
            done.Add(next.id);
            pending.Remove(next);
        }

        foreach (var part in ordered)
        {
            var result = BuildPart(part, build, markers, shader, saveAssets);
            build.results.Add(result);
            build.problems.AddRange(result.problems);
        }

        // FIX-03: the spec is checked on its own, and the built scene is checked independently by
        // reading real Transform.parent links instead of trusting PartResult (which mirrors the spec).
        build.problems.AddRange(ValidateHierarchy(
            build.spec.parts.Select(p => p.id).ToList(),
            build.spec.parts.Select(p => p.id).ToList(),
            build.spec.parts.Select(p => p.parent).ToList(),
            build.spec.parts.Select(p => p.parent).ToList()));
        build.problems.AddRange(ValidateHierarchyFromScene(build.root.transform, build.spec.parts));

        if (build.parts.TryGetValue("body", out var body) && build.parts.TryGetValue("head", out var head))
        {
            var bodyTop = BoundsOf(body).max.y;
            var headMin = BoundsOf(head).min.y;
            build.problems.AddRange(ValidateHeadAttachment(new Vector3(0f, bodyTop, 0f), new Vector3(0f, headMin, 0f)));
        }

        // FIX-02: head and hair must share the source grid frame, and their skin voxels must still
        // coincide in character space (the evidence the alignment rests on).
        var headResult = build.results.FirstOrDefault(r => r.id == "head");
        var hairResult = build.results.FirstOrDefault(r => r.id == "hair");
        if (headResult != null && hairResult != null)
        {
            build.problems.AddRange(ValidateSharedFrame("head", headResult.frameOffset,
                "hair", hairResult.frameOffset));
            build.problems.AddRange(ValidateSkinAlignment(
                Path.Combine(DataDir, "head.cubdec"), headResult.frameOffset,
                Path.Combine(DataDir, "hair.cubdec"), hairResult.frameOffset));
        }

        // Both feet must touch the ground; a wrong vertical frameOffset lifts or sinks them.
        var leftFoot = Find(build, "foot_l");
        var rightFoot = Find(build, "foot_r");
        if (leftFoot != null && rightFoot != null)
        {
            build.problems.AddRange(ValidateGroundContact(BoundsOf(leftFoot).min,
                BoundsOf(rightFoot).min));
        }

        var required = new[] { "body", "head", "hair", "hand_l", "hand_r", "foot_l", "foot_r", "sword" };
        var missing = required.Where(id => !build.parts.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            build.problems.Add("missing parts after build: " + string.Join(", ", missing));
            return build;
        }

        if (CommandLineFlag("-pilotSkipAnimator"))
        {
            // Test hook: leaves the assembly without an animator so RunPlayMode must refuse to run.
            build.problems.Add("test hook: ProceduralCharacterAnimator intentionally omitted");
            return build;
        }

        build.animator = build.root.AddComponent<ProceduralCharacterAnimator>();
        build.animator.AnimateInPlayMode = false;
        build.animator.Bind(Find(build, "body"), Find(build, "head"), Find(build, "hair"),
            Find(build, "hand_l"), Find(build, "hand_r"), Find(build, "foot_l"),
            Find(build, "foot_r"), Find(build, "sword"));
        return build;
    }

    static Transform Find(AssemblyBuild build, string id) =>
        build.parts.TryGetValue(id, out var t) ? t : null;

    static PartResult BuildPart(PartSpec part, AssemblyBuild build,
        Dictionary<string, Dictionary<string, object>> markers, Shader shader, bool saveAssets)
    {
        var result = new PartResult
        {
            id = part.id, model = part.model, parent = part.parent,
            offset = ToVector(part.offset), evidence = part.evidence ?? "",
        };

        var payloadPath = Path.Combine(DataDir, part.id + ".cubdec");
        if (!File.Exists(payloadPath))
        {
            result.problems.Add($"part '{part.id}': payload {part.id}.cubdec not found");
            return result;
        }

        var hidden = new HashSet<(int, int, int)>();
        if (markers.TryGetValue(part.id, out var meta) && meta.TryGetValue("hidden", out var raw) &&
            raw is List<object> entries)
        {
            foreach (var entry in entries)
            {
                if (entry is List<object> triplet && triplet.Count == 3)
                    hidden.Add((Convert.ToInt32(triplet[0]), Convert.ToInt32(triplet[1]),
                        Convert.ToInt32(triplet[2])));
            }
        }
        result.hiddenMarkers = hidden.Count;

        var built = BuildMeshFromPayload(payloadPath, hidden, part.id);
        if (built.problems.Count > 0)
        {
            result.problems.AddRange(built.problems);
            return result;
        }
        result.faces = built.faces;
        result.vertices = built.vertices;
        result.triangles = built.triangles;
        result.expectedSize = built.extent;

        if (saveAssets)
        {
            Directory.CreateDirectory(MeshDir);
            AssetDatabase.CreateAsset(built.mesh, $"{MeshDir}/{part.id}.asset");
            var material = new Material(shader) { name = part.id + "_mat" };
            AssetDatabase.CreateAsset(material, $"{MeshDir}/{part.id}.mat");
            AssetDatabase.SaveAssets();
        }

        // FIX-02: the part is placed by its SOURCE GRID FRAME, not by its trimmed bounding box.
        // mesh vertices are in grid coordinates, the pivot sits at the anatomical joint inside that
        // grid, and the frame origin (frameOffset) is what the spec pins down. Head and hair share
        // the same 18x16x16 authoring grid, so giving them the same frameOffset reproduces the
        // original voxel relationship (verified separately by the scalp-voxel test).
        var frameOffset = part.frameOffset != null && part.frameOffset.Length >= 3
            ? ToVector(part.frameOffset)
            : ToVector(part.offset) - built.gridMin;
        var joint = part.joint != null && part.joint.Length >= 3
            ? ToVector(part.joint)
            : JointOffsetFor(part.id, built.extent);
        result.frameOffset = frameOffset;
        result.joint = joint;
        result.sourceMin = built.gridMin;
        result.gridSize = built.gridSize;
        Transform parentTransform;
        // The pivot sits at the joint, so a part's absolute origin is (offset + joint). A nested
        // part must therefore be placed relative to the parent pivot origin, not to the parent offset.
        var parentOrigin = Vector3.zero;
        if (string.IsNullOrWhiteSpace(part.parent))
        {
            parentTransform = build.root.transform;
        }
        else if (build.parts.TryGetValue(part.parent, out var parent) &&
                 build.pivotOrigins.TryGetValue(part.parent, out var origin))
        {
            parentTransform = parent;
            parentOrigin = origin;
        }
        else
        {
            result.problems.Add($"part '{part.id}': parent '{part.parent}' was not built");
            return result;
        }

        var pivot = new GameObject("part_" + part.id).transform;
        pivot.SetParent(parentTransform, false);
        pivot.localPosition = frameOffset + joint - parentOrigin;
        pivot.localRotation = Quaternion.identity;
        build.pivotOrigins[part.id] = frameOffset + joint;

        var meshGo = new GameObject("mesh").transform;
        meshGo.SetParent(pivot, false);
        meshGo.localPosition = -joint;
        var filter = meshGo.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = built.mesh;
        var renderer = meshGo.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = saveAssets
            ? AssetDatabase.LoadAssetAtPath<Material>($"{MeshDir}/{part.id}.mat")
            : new Material(shader);
        build.parts[part.id] = pivot;

        var bounds = BoundsOf(pivot);
        result.worldMin = bounds.min;
        result.worldSize = bounds.size;
        // expected world position of the filled area = frame origin + its offset inside the grid
        var expectedWorldMin = frameOffset + built.gridMin;
        result.problems.AddRange(ValidatePartPlacement(part.id, expectedWorldMin, built.extent,
            bounds.min, bounds.size));
        result.problems.AddRange(ValidateOrientation(part.id, built.mesh.bounds.min,
            built.extent, built.gridMin));
        return result;
    }

    static Vector3 JointOffsetFor(string id, Vector3 extent)
    {
        switch (id)
        {
            case "hand_l":
            case "hand_r":
            case "head":
                return new Vector3(extent.x * 0.5f, extent.y, extent.z * 0.5f);
            case "foot_l":
            case "foot_r":
                return new Vector3(extent.x * 0.5f, 0f, extent.z * 0.5f);
            default:
                return Vector3.zero;
        }
    }

    class BuiltMesh
    {
        public Mesh mesh;
        public int faces, vertices, triangles;
        public Vector3 extent;
        public Vector3 gridMin;   // (x, h, d) of the first visible voxel inside the grid
        public Vector3 gridSize;  // full voxel grid size
        public List<string> problems = new List<string>();
    }

    /// <summary>
    /// Builds a mesh from a decoded `.cubdec` payload in GRID coordinates (no trimming), so the
    /// filled area keeps its original offset inside the model's voxel grid (FIX-02).
    ///
    /// FIX-01: min/max are computed in a first pass over all visible voxels; the second pass emits
    /// vertices relative to nothing but the grid origin. The previous single-pass version offset the
    /// first vertices by a still-evolving minimum, which distorted geometry whenever a later voxel
    /// had a smaller coordinate on some axis.
    /// </summary>
    static BuiltMesh BuildMeshFromPayload(string path, HashSet<(int, int, int)> hidden, string name)
    {
        var built = new BuiltMesh();
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 12)
        {
            built.problems.Add($"{name}: payload shorter than the 12-byte header");
            return built;
        }
        int width = BitConverter.ToInt32(bytes, 0);
        int depth = BitConverter.ToInt32(bytes, 4);
        int height = BitConverter.ToInt32(bytes, 8);
        long expectedBytes = 12L + (long)width * depth * height * 3L;
        if (width <= 0 || depth <= 0 || height <= 0 || expectedBytes != bytes.Length)
        {
            built.problems.Add($"{name}: bad header {width}x{depth}x{height}, expected {expectedBytes}, got {bytes.Length}");
            return built;
        }

        bool Solid(int x, int d, int h)
        {
            if (x < 0 || x >= width || d < 0 || d >= depth || h < 0 || h >= height) return false;
            if (hidden.Contains((x, d, h))) return false;
            int o = 12 + ((h * depth + d) * width + x) * 3;
            return bytes[o] != 0 || bytes[o + 1] != 0 || bytes[o + 2] != 0;
        }

        // ---- pass 1: final bounds of the visible voxels (mask applied) ----
        int minX = int.MaxValue, minD = int.MaxValue, minH = int.MaxValue;
        int maxX = int.MinValue, maxD = int.MinValue, maxH = int.MinValue;
        int solidCount = 0;
        for (int h = 0; h < height; h++)
        for (int d = 0; d < depth; d++)
        for (int x = 0; x < width; x++)
        {
            if (!Solid(x, d, h)) continue;
            solidCount++;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minD = Math.Min(minD, d); maxD = Math.Max(maxD, d);
            minH = Math.Min(minH, h); maxH = Math.Max(maxH, h);
        }
        if (solidCount == 0)
        {
            built.problems.Add($"{name}: no visible voxels (empty model or fully masked by markers)");
            return built;
        }

        // ---- pass 2: faces in grid coordinates (no trimming, so grid offsets survive) ----
        var verts = new List<Vector3>();
        var cols = new List<Color>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        int faceCount = 0;
        for (int h = 0; h < height; h++)
        for (int d = 0; d < depth; d++)
        for (int x = 0; x < width; x++)
        {
            if (!Solid(x, d, h)) continue;
            int o = 12 + ((h * depth + d) * width + x) * 3;
            var color = new Color(bytes[o] / 255f, bytes[o + 1] / 255f, bytes[o + 2] / 255f, 1f);
            for (int f = 0; f < 6; f++)
            {
                var n = FaceNormals[f];
                if (Solid(x + n[0], d + n[2], h + n[1])) continue;
                faceCount++;
                int baseIndex = verts.Count;
                foreach (var c in FaceCorners[f])
                {
                    verts.Add(new Vector3(x + c[0], h + c[1], d + c[2]));
                    norms.Add(new Vector3(n[0], n[1], n[2]));
                    cols.Add(color);
                }
                tris.Add(baseIndex); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
                tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
            }
        }
        if (faceCount == 0)
        {
            built.problems.Add($"{name}: no visible faces");
            return built;
        }

        var mesh = new Mesh { name = name };
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        built.mesh = mesh;
        built.faces = faceCount;
        built.vertices = mesh.vertexCount;
        built.triangles = mesh.triangles.Length / 3;
        built.extent = new Vector3(maxX - minX + 1, maxH - minH + 1, maxD - minD + 1);
        built.gridMin = new Vector3(minX, minH, minD);
        built.gridSize = new Vector3(width, height, depth);
        return built;
    }

    /// <summary>
    /// The pre-FIX-01 algorithm, kept only so the self-test can reproduce the defect: it trims with
    /// a minimum that keeps changing while vertices are emitted, so early vertices use a stale min.
    /// </summary>
    static BuiltMesh BuildMeshFromPayloadLegacySinglePass(string path, HashSet<(int, int, int)> hidden,
        string name)
    {
        var built = new BuiltMesh();
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 12)
        {
            built.problems.Add($"{name}: payload shorter than the 12-byte header");
            return built;
        }
        int width = BitConverter.ToInt32(bytes, 0);
        int depth = BitConverter.ToInt32(bytes, 4);
        int height = BitConverter.ToInt32(bytes, 8);
        long expectedBytes = 12L + (long)width * depth * height * 3L;
        if (width <= 0 || depth <= 0 || height <= 0 || expectedBytes != bytes.Length)
        {
            built.problems.Add($"{name}: bad header");
            return built;
        }

        bool Solid(int x, int d, int h)
        {
            if (x < 0 || x >= width || d < 0 || d >= depth || h < 0 || h >= height) return false;
            if (hidden.Contains((x, d, h))) return false;
            int o = 12 + ((h * depth + d) * width + x) * 3;
            return bytes[o] != 0 || bytes[o + 1] != 0 || bytes[o + 2] != 0;
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();
        int minX = int.MaxValue, minD = int.MaxValue, minH = int.MaxValue;
        int maxX = int.MinValue, maxD = int.MinValue, maxH = int.MinValue;
        int faceCount = 0;
        for (int h = 0; h < height; h++)
        for (int d = 0; d < depth; d++)
        for (int x = 0; x < width; x++)
        {
            if (!Solid(x, d, h)) continue;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minD = Math.Min(minD, d); maxD = Math.Max(maxD, d);
            minH = Math.Min(minH, h); maxH = Math.Max(maxH, h);
            for (int f = 0; f < 6; f++)
            {
                var n = FaceNormals[f];
                if (Solid(x + n[0], d + n[2], h + n[1])) continue;
                faceCount++;
                int baseIndex = verts.Count;
                foreach (var c in FaceCorners[f])
                    verts.Add(new Vector3(x + c[0] - minX, h + c[1] - minH, d + c[2] - minD));
                tris.Add(baseIndex); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
                tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
            }
        }
        if (faceCount == 0)
        {
            built.problems.Add($"{name}: no visible faces");
            return built;
        }
        var mesh = new Mesh { name = name };
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        built.mesh = mesh;
        built.faces = faceCount;
        built.vertices = verts.Count;
        built.triangles = tris.Count / 3;
        built.extent = new Vector3(maxX - minX + 1, maxH - minH + 1, maxD - minD + 1);
        built.gridMin = new Vector3(minX, minH, minD);
        built.gridSize = new Vector3(width, height, depth);
        return built;
    }

    // ------------------------------------------------------------------
    // entry point: build + validate + animate + render + report
    // ------------------------------------------------------------------

    [MenuItem("CubeGame Pilot/Run character assembly")]
    public static void Run()
    {
        bool failed;
        try
        {
            failed = BuildAndVerify() > 0;
        }
        catch (Exception e)
        {
            WriteException(e);
            failed = true;
        }
        if (failed)
            throw new Exception("CHARACTER_RESULT=FAIL - see PilotOutput/character_report.json");
    }

    static int BuildAndVerify()
    {
        Directory.CreateDirectory(OutputDir);
        CleanOutputs("character_", "phase_", "head_yaw_", "hierarchy", "build_trace", "report_exception");
        AssetDatabase.Refresh();

        var build = BuildCharacter(true);
        var log = new StringBuilder();
        log.AppendLine($"assembly: {build.spec.character} ({build.spec.status}), parts={build.spec.parts.Length}");
        foreach (var result in build.results)
            log.AppendLine($"part {result.id,-7} parent={result.parent ?? "<root>",-7} offset={Fmt(result.offset)} " +
                           $"worldMin={Fmt(result.worldMin)} size={Fmt(result.worldSize)} " +
                           $"faces={result.faces} hiddenMarkers={result.hiddenMarkers} problems={result.problems.Count}");
        foreach (var problem in build.problems) log.AppendLine("BUILD PROBLEM: " + problem);
        Debug.Log(log.ToString());
        File.WriteAllText(Path.Combine(OutputDir, "build_trace.txt"), log.ToString());

        var animation = SampleCycle(build, build.problems);
        string hierarchy;
        try
        {
            RenderAll(build);
            hierarchy = DumpHierarchy(build.root.transform);
        }
        catch (Exception e)
        {
            build.problems.Add("render/hierarchy failed: " + e.Message);
            hierarchy = "(not available: " + e.Message + ")";
        }

        if (build.problems.Count > 0)
            foreach (var problem in build.problems) log.AppendLine("PROBLEM: " + problem);

        log.AppendLine($"CHARACTER_RESULT={(build.problems.Count == 0 ? "PASS" : "FAIL")}");
        WriteReport(build, animation, hierarchy, log.ToString());
        Debug.Log(log.ToString());
        return build.problems.Count == 0 ? 0 : 1;
    }

    class AnimationReport
    {
        public string clip = ProceduralCharacterAnimator.ClipName;
        public float cycleSeconds = ProceduralCharacterAnimator.CycleSeconds;
        public int samples;
        public string headTravel = "", handTravel = "", footTravel = "", loopDelta = "";
        public List<string> problems = new List<string>();
    }

    static AnimationReport SampleCycle(AssemblyBuild build, List<string> problems)
    {
        var report = new AnimationReport();
        var body = Find(build, "body");
        var head = Find(build, "head");
        var hand = Find(build, "hand_r");
        var foot = Find(build, "foot_l");
        var sword = Find(build, "sword");
        if (body == null || head == null || hand == null || foot == null || sword == null)
        {
            report.problems.Add("animation check needs body, head, hand_r, foot_l and sword");
            problems.AddRange(report.problems);
            return report;
        }

        const int samples = 8;
        var headFrames = new List<Vector3>();
        var handFrames = new List<Vector3>();
        var footFrames = new List<Vector3>();
        var swordFrames = new List<Vector3>();
        var headRotations = new List<Quaternion>();
        for (int i = 0; i <= samples; i++)
        {
            build.animator.Sample((float)i / samples);
            headFrames.Add(head.position);
            headRotations.Add(head.rotation);
            handFrames.Add(hand.position);
            footFrames.Add(foot.position);
            swordFrames.Add(sword.position);
        }
        build.animator.Sample(0f);

        report.samples = samples + 1;
        report.headTravel = Fmt(Extent(headFrames));
        report.handTravel = Fmt(Extent(handFrames));
        report.footTravel = Fmt(Extent(footFrames));
        report.loopDelta = Fmt(headFrames.Last() - headFrames.First());

        report.problems.AddRange(ValidateMotion(headFrames, 0.005f));
        report.problems.AddRange(ValidateMotion(handFrames, 0.05f));
        report.problems.AddRange(ValidateMotion(footFrames, 0.3f));
        var socketOffsets = new List<Vector3>();
        foreach (var frame in swordFrames) socketOffsets.Add(frame);
        // recompute in hand space for the socket invariant
        socketOffsets.Clear();
        for (int i = 0; i <= samples; i++)
        {
            build.animator.Sample((float)i / samples);
            socketOffsets.Add(hand.InverseTransformPoint(sword.position));
        }
        build.animator.Sample(0f);
        report.problems.AddRange(ValidateWeaponSocket(socketOffsets));
        report.problems.AddRange(ValidateLoop(headFrames));
        var rotationTravel = Quaternion.Angle(headRotations.First(), headRotations.Last());
        if (rotationTravel > 1e-3f)
            report.problems.Add($"cycle does not loop: head rotation differs by {rotationTravel:0.###} degrees");
        problems.AddRange(report.problems);
        return report;
    }

    static Vector3 Extent(List<Vector3> frames)
    {
        var min = frames[0];
        var max = frames[0];
        foreach (var f in frames) { min = Vector3.Min(min, f); max = Vector3.Max(max, f); }
        return max - min;
    }

    // ------------------------------------------------------------------
    // PlayMode
    // ------------------------------------------------------------------

    // PlayMode entry point moved to CharacterPilotPlayMode.cs (S05A-1-REVIEW-FIX-02).

    // ------------------------------------------------------------------
    // self-tests
    // ------------------------------------------------------------------

    [MenuItem("CubeGame Pilot/Run character validator self-tests")]
    public static void RunSelfTests()
    {
        var failed = SelfTests.RunAll();
        if (failed)
            throw new Exception("SELFTEST_RESULT=FAIL - see PilotOutput/selftest.json");
    }

    static class SelfTests
    {
        public static bool RunAll()
        {
            var cases = new List<(string name, bool expectProblems, string needle, Func<List<string>> run)>
            {
                ("matching hierarchy passes", false, null, () => ValidateHierarchy(
                    new[] { "body", "head" }, new[] { "body", "head" },
                    new string[] { null, "body" }, new string[] { null, "body" })),
                ("missing part fails", true, "missing part", () => ValidateHierarchy(
                    new[] { "body", "head", "hair" }, new[] { "body", "head" },
                    new string[] { null, "body", "head" }, new string[] { null, "body" })),
                ("unexpected part fails", true, "unexpected part", () => ValidateHierarchy(
                    new[] { "body" }, new[] { "body", "ghost" }, new string[] { null }, new string[] { null, "body" })),
                ("duplicate part fails", true, "duplicate part", () => ValidateHierarchy(
                    new[] { "body" }, new[] { "body", "body" }, new string[] { null }, new string[] { null, null })),
                ("wrong parent fails", true, "parent is", () => ValidateHierarchy(
                    new[] { "body", "head" }, new[] { "body", "head" },
                    new string[] { null, "body" }, new string[] { null, null })),
                ("empty spec fails", true, "lists no parts", () => ValidateHierarchy(
                    new string[0], new string[0], new string[0], new string[0])),
                ("correct placement passes", false, null, () => ValidatePartPlacement(
                    "head", new Vector3(0f, 13f, 0f), new Vector3(14f, 8f, 8f),
                    new Vector3(0f, 13f, 0f), new Vector3(14f, 8f, 8f))),
                ("broken transform fails", true, "spec offset", () => ValidatePartPlacement(
                    "head", new Vector3(0f, 13f, 0f), new Vector3(14f, 8f, 8f),
                    new Vector3(0f, 12f, 0f), new Vector3(14f, 8f, 8f))),
                ("wrong scale fails", true, "voxel extent", () => ValidatePartPlacement(
                    "head", Vector3.zero, new Vector3(14f, 8f, 8f), Vector3.zero, new Vector3(7f, 8f, 8f))),
                ("degenerate part fails", true, "degenerate", () => ValidatePartPlacement(
                    "head", Vector3.zero, new Vector3(1f, 1f, 1f), Vector3.zero, Vector3.zero)),
                ("negative mesh min fails", true, "negative", () => ValidateOrientation(
                    "head", new Vector3(-1f, 0f, 0f), new Vector3(14f, 8f, 8f), new Vector3(-1f, 0f, 0f))),
                ("re-centred mesh fails", true, "grid min", () => ValidateOrientation(
                    "head", Vector3.zero, new Vector3(14f, 8f, 8f), new Vector3(2f, 2f, 4f))),
                ("clean mesh min passes", false, null, () => ValidateOrientation(
                    "head", new Vector3(2f, 2f, 4f), new Vector3(14f, 8f, 8f), new Vector3(2f, 2f, 4f))),
                ("head attached passes", false, null, () =>
                    ValidateHeadAttachment(new Vector3(0f, 13f, 0f), new Vector3(0f, 13f, 0f))),
                ("head detached fails", true, "floats", () =>
                    ValidateHeadAttachment(new Vector3(0f, 13f, 0f), new Vector3(0f, 15f, 0f))),
                ("head sinking fails", true, "sinks", () =>
                    ValidateHeadAttachment(new Vector3(0f, 13f, 0f), new Vector3(0f, 12f, 0f))),
                ("steady weapon passes", false, null, () => ValidateWeaponSocket(
                    new[] { new Vector3(1f, 2f, 0f), new Vector3(1f, 2f, 0f) })),
                ("drifting weapon fails", true, "drifted", () => ValidateWeaponSocket(
                    new[] { new Vector3(1f, 2f, 0f), new Vector3(1.5f, 2f, 0f) })),
                ("single socket sample fails", true, "at least two samples", () => ValidateWeaponSocket(
                    new[] { Vector3.zero })),
                ("looping cycle passes", false, null, () => ValidateLoop(
                    new[] { Vector3.zero, new Vector3(1f, 0f, 0f), Vector3.zero })),
                ("non-looping cycle fails", true, "does not loop", () => ValidateLoop(
                    new[] { Vector3.zero, new Vector3(1f, 0f, 0f) })),
                ("static part fails motion", true, "does not move", () => ValidateMotion(
                    new[] { Vector3.zero, Vector3.zero }, 0.01f)),
                ("moving part passes motion", false, null, () => ValidateMotion(
                    new[] { Vector3.zero, new Vector3(0.5f, 0f, 0f) }, 0.01f)),
            };

            var failed = 0;
            var json = new StringBuilder("[");
            for (int i = 0; i < cases.Count; i++)
            {
                var c = cases[i];
                List<string> problems;
                try { problems = c.run() ?? new List<string>(); }
                catch (Exception e) { problems = new List<string> { "exception: " + e.Message }; }
                bool pass = (problems.Count > 0) == c.expectProblems;
                if (pass && c.needle != null)
                    pass = problems.Any(p => p.IndexOf(c.needle, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!pass) failed++;
                if (i > 0) json.Append(',');
                json.Append($"{{\"name\":{Q(c.name)},\"expected\":{(c.expectProblems ? "\"problems\"" : "\"clean\"")}," +
                            $"\"passed\":{(pass ? "true" : "false")},\"problems\":[{string.Join(",", problems.Select(Q))}]}}");
            }
            // FIX-01/02/03 cases (two-pass geometry, source frames/skin alignment, real scene hierarchy)
            var fix = FixSelfTests.Run();
            if (fix.cases > 0)
            {
                var fixJson = fix.json;
                if (fixJson.Length > 2) json.Append(',').Append(fixJson, 1, fixJson.Length - 2);
                failed += fix.failed;
            }
            var total = cases.Count + fix.cases;
            json.Append(']');

            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(Path.Combine(OutputDir, "selftest.json"), json.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(OutputDir, "selftest.txt"),
                $"SELFTEST_RESULT={(failed == 0 ? "PASS" : "FAIL")}\ncases={total} failed={failed}\n" +
                $"base_cases={cases.Count} fix_cases={fix.cases}\n",
                new UTF8Encoding(false));
            Debug.Log($"SELFTEST_RESULT={(failed == 0 ? "PASS" : "FAIL")} cases={total} failed={failed}");
            return failed > 0;
        }
    }

    // ------------------------------------------------------------------
    // rendering
    // ------------------------------------------------------------------

    static void RenderAll(AssemblyBuild build)
    {
        var camGo = new GameObject("PilotCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.10f, 0.13f, 1f);
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 5000f;
        var lightGo = new GameObject("PilotLight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

        var bounds = AggregateBounds(build.root.transform);
        RenderViews(cam, bounds, "character");

        // A sword-free pass, so the assembled body can be judged without the oversized blade
        // (the greatsword is 37 units long in raw voxel units; see the report).
        var sword = Find(build, "sword");
        if (sword != null)
        {
            sword.gameObject.SetActive(false);
            RenderViews(cam, bounds, "character_nosword");
            sword.gameObject.SetActive(true);
        }

        var head = Find(build, "head");
        if (head != null)
        {
            var home = head.localRotation;
            foreach (var (name, yaw) in new[] { ("head_yaw_left", -35f), ("head_yaw_right", 35f) })
            {
                head.localRotation = Quaternion.Euler(0f, yaw, 0f);
                RenderViews(cam, bounds, name);
            }
            head.localRotation = home;
        }

        foreach (var phase in new[] { 0.25f, 0.5f, 0.75f })
        {
            build.animator.Sample(phase);
            RenderViews(cam, bounds, "phase_" + phase.ToString("0.00", CultureInfo.InvariantCulture));
        }
        build.animator.Sample(0f);
    }

    static void RenderViews(Camera cam, Bounds bounds, string stem)
    {
        var target = bounds.center;
        var radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        var distance = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
        // Face features (eyes at d=11 of the 16-deep head grid, solid range 4..11) sit on the
        // HIGH-depth side, which is +Z after the (x, h, d) -> (x, y, z) mapping. "front" therefore
        // looks from +Z; -Z is the back of the head.
        foreach (var (name, dir) in new[]
        {
            ("front", new Vector3(0f, 0.08f, 1f)),
            ("back", new Vector3(0f, 0.08f, -1f)),
            ("side", new Vector3(1f, 0.08f, 0.05f)),
            ("threequarter", new Vector3(0.8f, 0.35f, 0.9f)),
        })
        {
            cam.transform.position = target + dir.normalized * distance;
            cam.transform.LookAt(target);
            Capture(cam, $"{OutputDir}/{stem}_{name}.png");
        }
    }

    static void Capture(Camera cam, string path)
    {
        var rt = new RenderTexture(CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(CaptureSize, CaptureSize, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, CaptureSize, CaptureSize), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }

    /// <summary>Bounds of one part's own mesh only (parts are nested, so descendants must not leak in).</summary>
    static Bounds BoundsOf(Transform pivot)
    {
        var mesh = pivot.Find("mesh");
        var renderer = mesh != null ? mesh.GetComponent<MeshRenderer>() : pivot.GetComponent<MeshRenderer>();
        return renderer != null ? renderer.bounds : new Bounds(pivot.position, Vector3.zero);
    }

    /// <summary>Bounds of a whole subtree (used for framing renders).</summary>
    static Bounds AggregateBounds(Transform transform)
    {
        var renderers = transform.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return new Bounds(transform.position, Vector3.zero);
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static string DumpHierarchy(Transform root)
    {
        var sb = new StringBuilder();
        DumpRecursive(root, 0, sb);
        return sb.ToString();
    }

    static void DumpRecursive(Transform t, int depth, StringBuilder sb)
    {
        sb.Append(new string(' ', depth * 2)).Append(t.name)
          .Append($" localPos={Fmt(t.localPosition)} localScale={Fmt(t.localScale)}")
          .AppendLine();
        foreach (Transform child in t) DumpRecursive(child, depth + 1, sb);
    }

    // ------------------------------------------------------------------
    // io
    // ------------------------------------------------------------------

    static Dictionary<string, Dictionary<string, object>> LoadMarkerMetadata()
    {
        var result = new Dictionary<string, Dictionary<string, object>>();
        var path = Path.Combine(DataDir, "markers.json");
        if (!File.Exists(path)) return result;
        var parsed = MiniJson.ParseObject(File.ReadAllText(path));
        foreach (var kv in parsed)
            if (kv.Value is Dictionary<string, object> entry) result[kv.Key] = entry;
        return result;
    }

    /// <summary>
    /// Removes stale artifacts. Each entry point cleans only what it owns, so an assembly run, a
    /// self-test run and a PlayMode run can all keep their evidence in PilotOutput.
    /// </summary>
    static void CleanOutputs(params string[] prefixes)
    {
        if (!Directory.Exists(OutputDir)) return;
        foreach (var file in Directory.GetFiles(OutputDir))
        {
            var name = Path.GetFileName(file);
            foreach (var prefix in prefixes)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    File.Delete(file);
                    break;
                }
            }
        }
    }

    static void WriteReport(AssemblyBuild build, AnimationReport animation, string hierarchy, string log)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"character\": {Q(build.spec.character)},");
        sb.AppendLine($"  \"status\": {Q(build.spec.status)},");
        sb.AppendLine($"  \"unityVersion\": {Q(Application.unityVersion)},");
        sb.AppendLine($"  \"verdict\": {Q(build.problems.Count == 0 ? "PASS" : "FAIL")},");
        sb.AppendLine("  \"parts\": [");
        for (int i = 0; i < build.results.Count; i++)
        {
            var r = build.results[i];
            sb.AppendLine("  {");
            sb.AppendLine($"    \"id\": {Q(r.id)}, \"model\": {Q(r.model)}, \"parent\": {Q(r.parent ?? "<root>")},");
            sb.AppendLine($"    \"legacyOffset\": {Q(Fmt(r.offset))},");
            // FIX-02 coordinate chain: source grid frame -> filled-area offset -> pivot -> world
            sb.AppendLine($"    \"frameOffset\": {Q(Fmt(r.frameOffset))}, \"joint\": {Q(Fmt(r.joint))},");
            sb.AppendLine($"    \"sourceMin\": {Q(Fmt(r.sourceMin))}, \"gridSize\": {Q(Fmt(r.gridSize))},");
            sb.AppendLine($"    \"worldMin\": {Q(Fmt(r.worldMin))}, \"worldSize\": {Q(Fmt(r.worldSize))},");
            sb.AppendLine($"    \"expectedSize\": {Q(Fmt(r.expectedSize))},");
            sb.AppendLine($"    \"faces\": {r.faces}, \"vertices\": {r.vertices}, \"triangles\": {r.triangles},");
            sb.AppendLine($"    \"hiddenMarkers\": {r.hiddenMarkers}, \"evidence\": {Q(r.evidence)},");
            sb.AppendLine($"    \"problems\": [{string.Join(",", r.problems.Select(Q))}]");
            sb.AppendLine(i == build.results.Count - 1 ? "  }" : "  },");
        }
        sb.AppendLine("  ],");
        sb.AppendLine("  \"animation\": {");
        sb.AppendLine($"    \"clip\": {Q(animation.clip)},");
        sb.AppendLine($"    \"cycleSeconds\": {animation.cycleSeconds.ToString(CultureInfo.InvariantCulture)},");
        sb.AppendLine($"    \"samples\": {animation.samples}, \"headTravel\": {Q(animation.headTravel)},");
        sb.AppendLine($"    \"handTravel\": {Q(animation.handTravel)}, \"footTravel\": {Q(animation.footTravel)},");
        sb.AppendLine($"    \"loopDelta\": {Q(animation.loopDelta)},");
        sb.AppendLine($"    \"problems\": [{string.Join(",", animation.problems.Select(Q))}]");
        sb.AppendLine("  },");
        var imageNames = Directory.GetFiles(OutputDir, "*.png")
            .Select(f => Q(Path.GetFileName(f)))
            .OrderBy(x => x, StringComparer.Ordinal);
        sb.AppendLine("  \"images\": [" + string.Join(",", imageNames) + "],");
        sb.AppendLine($"  \"problems\": [{string.Join(",", build.problems.Select(Q))}]");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(OutputDir, "character_report.json"), sb.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(OutputDir, "character_report.txt"), log, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(OutputDir, "hierarchy.txt"), hierarchy, new UTF8Encoding(false));
    }

    static void WriteException(Exception e)
    {
        try
        {
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(Path.Combine(OutputDir, "report_exception.txt"), e.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(OutputDir, "character_report.txt"),
                $"CHARACTER_RESULT=EXCEPTION\n{e}", new UTF8Encoding(false));
        }
        catch (Exception nested)
        {
            Debug.LogError("cannot write exception report: " + nested);
        }
    }

    internal static string Q(string value) =>
        "\"" + (value ?? "").Replace("\\", "/").Replace("\"", "'").Replace("\n", " ").Replace("\r", " ") + "\"";

    static Vector3 ToVector(float[] values) =>
        values == null || values.Length < 3 ? Vector3.zero : new Vector3(values[0], values[1], values[2]);

    internal static string Fmt(Vector3 v) => string.Format(CultureInfo.InvariantCulture,
        "({0:0.###},{1:0.###},{2:0.###})", v.x, v.y, v.z);
}

/// <summary>Minimal JSON reader for the marker metadata (Unity has no generic JSON reader here).</summary>
public static class MiniJson
{
    public static Dictionary<string, object> ParseObject(string text)
    {
        int index = 0;
        return Parse(text, ref index) as Dictionary<string, object> ?? new Dictionary<string, object>();
    }

    static object Parse(string s, ref int i)
    {
        Skip(s, ref i);
        if (s[i] == '{') return ParseObjectBody(s, ref i);
        if (s[i] == '[') return ParseArray(s, ref i);
        if (s[i] == '"') return ParseString(s, ref i);
        return ParseScalar(s, ref i);
    }

    static Dictionary<string, object> ParseObjectBody(string s, ref int i)
    {
        var result = new Dictionary<string, object>();
        i++;
        Skip(s, ref i);
        if (s[i] == '}') { i++; return result; }
        while (true)
        {
            Skip(s, ref i);
            var key = ParseString(s, ref i);
            Skip(s, ref i);
            i++;
            result[key] = Parse(s, ref i);
            Skip(s, ref i);
            if (s[i] == ',') { i++; continue; }
            i++;
            return result;
        }
    }

    static List<object> ParseArray(string s, ref int i)
    {
        var result = new List<object>();
        i++;
        Skip(s, ref i);
        if (s[i] == ']') { i++; return result; }
        while (true)
        {
            result.Add(Parse(s, ref i));
            Skip(s, ref i);
            if (s[i] == ',') { i++; continue; }
            i++;
            return result;
        }
    }

    static string ParseString(string s, ref int i)
    {
        i++;
        var sb = new StringBuilder();
        while (s[i] != '"')
        {
            if (s[i] == '\\' && i + 1 < s.Length) { i++; sb.Append(s[i]); }
            else sb.Append(s[i]);
            i++;
        }
        i++;
        return sb.ToString();
    }

    static object ParseScalar(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && !char.IsWhiteSpace(s[i])) i++;
        var slice = s.Substring(start, i - start);
        if (slice == "true") return true;
        if (slice == "false") return false;
        if (slice == "null") return null;
        return double.TryParse(slice, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? (object)number : slice;
    }

    static void Skip(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }
}
