// S05A-0 conversion pilot — isolated verification project (NOT the production CubeGame project).
//
// Two independent paths are checked for every sample:
//   1. "voxel": decoded .cub payload (Assets/PilotData/*.cubdec) -> Mesh with vertex colours
//      (unlit vertex-colour shader), saved as a mesh asset and rendered to PNG.
//   2. "obj": the OBJ/MTL written by tools/alpha_models/cub_to_obj.py imported by Unity's
//      own ModelImporter (Assets/PilotModels/*.obj) -> mesh statistics compared with the
//      expectations derived from the decoded voxels.
//
// S05A-0-REVIEW-FIX-01: every collected problem now reaches the final verdict, the input
// sets are cross-checked (a missing/extra OBJ is a failure), imported geometry is compared
// with the expected triangle count and bounds, stale reports are removed before a run, and
// exceptions are recorded as PILOT_RESULT=EXCEPTION. Validators are pure static functions so
// the editor-side self-test can feed them broken values without touching any asset.
//
// Usage (batchmode):
//   Unity.exe -batchmode -quit -projectPath <this> -executeMethod ModelPilot.Run -logFile run.log
//   Unity.exe -batchmode -quit -projectPath <this> -executeMethod ModelPilot.RunSelfTests -logFile selftest.log
//
// Exit code is 0 only when the run reports PASS; a failed run throws, so Unity exits non-zero.
// The authoritative verdict is the PILOT_RESULT / SELFTEST_RESULT marker in PilotOutput/report.txt.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ModelPilot
{
    const string DataDir = "Assets/PilotData";
    const string ModelsDir = "Assets/PilotModels";
    const string MeshAssetDir = "Assets/PilotGenerated";
    const string OutputDir = "PilotOutput";
    const int CaptureSize = 640;
    const float GeometryTolerance = 0.001f;

    // face definitions must match tools/alpha_models/cub_to_obj.py
    static readonly int[][] FaceNormals =
    {
        new[] { 1, 0, 0 }, new[] { -1, 0, 0 },
        new[] { 0, 1, 0 }, new[] { 0, -1, 0 },
        new[] { 0, 0, 1 }, new[] { 0, 0, -1 },
    };

    static readonly int[][][] FaceCorners =
    {
        new[] { new[]{1,0,0}, new[]{1,1,0}, new[]{1,1,1}, new[]{1,0,1} },   // +X
        new[] { new[]{0,0,1}, new[]{0,1,1}, new[]{0,1,0}, new[]{0,0,0} },   // -X
        new[] { new[]{0,1,0}, new[]{0,1,1}, new[]{1,1,1}, new[]{1,1,0} },   // +Y
        new[] { new[]{0,0,0}, new[]{1,0,0}, new[]{1,0,1}, new[]{0,0,1} },   // -Y
        new[] { new[]{0,0,1}, new[]{1,0,1}, new[]{1,1,1}, new[]{0,1,1} },   // +Z
        new[] { new[]{1,0,0}, new[]{0,0,0}, new[]{0,1,0}, new[]{1,1,0} },   // -Z
    };

    class SampleReport
    {
        public string key;
        public int width, depth, height;
        public int solidVoxels;
        public int faces;
        public int vertices;
        public int triangles;
        public int subMeshes;
        public string voxelMaterial;
        public string objMaterials;
        public string objImport;
        public string boundsMin;
        public string boundsMax;
        public string boundsSize;
        public string expectedExtent;
        public string center;
        public string meshAsset;
        public List<string> images = new List<string>();
        public List<string> problems = new List<string>();
    }

    internal class ObjImportStats
    {
        public bool ok;
        public int groups;
        public int vertices;
        public int triangles;
        public int subMeshes;
        public Vector3 min;
        public Vector3 max;
        public string materials = "n/a";
        public string error = "";
    }

    // ------------------------------------------------------------------
    // Pure validators (also exercised by RunSelfTests)
    // ------------------------------------------------------------------

    internal static List<string> ValidateSampleSets(IReadOnlyList<string> decodedKeys, IReadOnlyList<string> objKeys)
    {
        var problems = new List<string>();
        if (decodedKeys == null || decodedKeys.Count == 0)
            problems.Add($"no .cubdec inputs found in {DataDir}");
        if (objKeys == null || objKeys.Count == 0)
            problems.Add($"no .obj inputs found in {ModelsDir}");
        if (decodedKeys == null || objKeys == null)
            return problems;

        foreach (var duplicate in decodedKeys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key))
            problems.Add($"duplicate .cubdec key: {duplicate}");
        foreach (var duplicate in objKeys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key))
            problems.Add($"duplicate .obj key: {duplicate}");

        var decoded = new HashSet<string>(decodedKeys);
        var imported = new HashSet<string>(objKeys);
        foreach (var key in decoded.Where(k => !imported.Contains(k)).OrderBy(k => k))
            problems.Add($"missing OBJ for sample '{key}'");
        foreach (var key in imported.Where(k => !decoded.Contains(k)).OrderBy(k => k))
            problems.Add($"OBJ without matching .cubdec: '{key}'");
        return problems;
    }

    internal static List<string> ValidateVoxelGeometry(int expectedFaces, Vector3 expectedExtent,
        int vertices, int triangles, Vector3 boundsMin, Vector3 boundsSize)
    {
        var problems = new List<string>();
        if (expectedFaces <= 0)
            problems.Add("model exposes no faces");
        if (vertices != expectedFaces * 4)
            problems.Add($"voxel mesh vertices {vertices} != 4 per face ({expectedFaces * 4})");
        if (triangles != expectedFaces * 2)
            problems.Add($"voxel mesh triangles {triangles} != 2 per face ({expectedFaces * 2})");
        problems.AddRange(CompareBounds("voxel mesh", expectedExtent, boundsMin, boundsSize));
        return problems;
    }

    /// <summary>
    /// Validates the OBJ import produced by Unity's ModelImporter.
    ///
    /// Unity treats OBJ as a right-handed format and converts it to its left-handed space by
    /// negating X, so the imported bounds are the mirror of the file (`x' = -x`). This is
    /// expected behaviour, not corruption, and it is checked explicitly instead of being
    /// tolerated: the caller passes the mirrored expectation and `mirroredX` only affects the
    /// wording of the report.
    /// </summary>
    internal static List<string> ValidateObjImport(int expectedFaces, Vector3 expectedMin,
        Vector3 expectedSize, ObjImportStats stats, bool mirroredX)
    {
        var problems = new List<string>();
        if (stats == null)
        {
            problems.Add("OBJ import produced no statistics");
            return problems;
        }
        if (!stats.ok)
        {
            problems.Add($"OBJ import failed: {stats.error}");
            return problems;
        }
        if (stats.groups <= 0)
            problems.Add("OBJ import produced no mesh groups");
        if (expectedFaces <= 0)
            problems.Add("no expected faces to compare the OBJ import against");
        else if (stats.triangles != expectedFaces * 2)
            problems.Add($"OBJ import triangles {stats.triangles} != 2 per face ({expectedFaces * 2})");

        var label = mirroredX ? "OBJ import (X mirrored by the importer)" : "OBJ import";
        problems.AddRange(CompareBounds(label, expectedSize, stats.min, stats.max - stats.min, mirroredX));
        if ((stats.min - expectedMin).magnitude > GeometryTolerance)
            problems.Add($"{label}: min {Fmt(stats.min)} != expected min {Fmt(expectedMin)}");
        return problems;
    }

    static List<string> CompareBounds(string label, Vector3 expectedExtent, Vector3 boundsMin, Vector3 boundsSize, bool allowNegativeX = false)
    {
        var problems = new List<string>();
        if (float.IsNaN(boundsSize.x) || float.IsNaN(boundsSize.y) || float.IsNaN(boundsSize.z))
        {
            problems.Add($"{label}: bounds are NaN");
            return problems;
        }
        if (Mathf.Abs(expectedExtent.x - boundsSize.x) > GeometryTolerance ||
            Mathf.Abs(expectedExtent.y - boundsSize.y) > GeometryTolerance ||
            Mathf.Abs(expectedExtent.z - boundsSize.z) > GeometryTolerance)
        {
            problems.Add($"{label}: bounds {Fmt(boundsSize)} != expected solid extent {Fmt(expectedExtent)}");
        }
        if (allowNegativeX)
        {
            if (boundsMin.y < -GeometryTolerance || boundsMin.z < -GeometryTolerance)
                problems.Add($"{label}: negative bounds min {Fmt(boundsMin)} on Y/Z (models start at 0)");
        }
        else if (boundsMin.x < -GeometryTolerance || boundsMin.y < -GeometryTolerance || boundsMin.z < -GeometryTolerance)
        {
            problems.Add($"{label}: negative bounds min {Fmt(boundsMin)} (models start at 0 on every axis)");
        }
        return problems;
    }

    // ------------------------------------------------------------------
    // Editor entry points
    // ------------------------------------------------------------------

    [MenuItem("CubeGame Pilot/Run conversion verification")]
    public static void Run()
    {
        var failed = false;
        try
        {
            failed = RunVerification() > 0;
        }
        catch (Exception e)
        {
            WriteExceptionReport(e);
            failed = true;
        }
        if (failed)
            throw new Exception("PILOT_RESULT=FAIL - see PilotOutput/report.json and PilotOutput/report.txt");
    }

    [MenuItem("CubeGame Pilot/Run validator self-tests")]
    public static void RunSelfTests()
    {
        var failed = SelfTests.RunAll();
        if (failed)
            throw new Exception("SELFTEST_RESULT=FAIL — see PilotOutput/selftest.json");
    }

    static int RunVerification()
    {
        Directory.CreateDirectory(OutputDir);
        Directory.CreateDirectory(MeshAssetDir);
        // Never leave a previous verdict on disk: a stale PASS would be indistinguishable
        // from a fresh one if the run dies before writing.
        CleanPreviousOutput();
        AssetDatabase.Refresh();

        var log = new StringBuilder();
        var setProblems = new List<string>();
        var reports = new List<SampleReport>();
        var exitCode = 0;

        try
        {
            var shader = Shader.Find("CubeGamePilot/VertexColorUnlit");
            if (shader == null)
                throw new InvalidOperationException("shader CubeGamePilot/VertexColorUnlit not found");

            var decodedPaths = SortedFiles(DataDir, "*.cubdec");
            var objPaths = SortedFiles(ModelsDir, "*.obj");
            log.AppendLine($"decoded payloads: {decodedPaths.Count}");
            log.AppendLine($"obj files: {objPaths.Count}");

            var decodedKeys = decodedPaths.Select(Path.GetFileNameWithoutExtension).ToList();
            var objKeys = objPaths.Select(Path.GetFileNameWithoutExtension).ToList();
            setProblems.AddRange(ValidateSampleSets(decodedKeys, objKeys));
            foreach (var problem in setProblems)
                log.AppendLine("SET PROBLEM: " + problem);

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
            light.intensity = 1.0f;
            light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);

            var root = new GameObject("PilotRoot");

            foreach (var path in decodedPaths)
            {
                var report = BuildFromDecoded(path, shader, root.transform);
                reports.Add(report);
                log.AppendLine($"built {report.key}: faces={report.faces} verts={report.vertices} " +
                               $"bounds={report.boundsSize} problems={report.problems.Count}");
            }

            foreach (var objPath in objPaths)
            {
                var key = Path.GetFileNameWithoutExtension(objPath);
                var report = reports.FirstOrDefault(r => r.key == key);
                if (report == null)
                {
                    report = new SampleReport { key = key };
                    reports.Add(report);
                }
                var stats = ImportObj(objPath, log);
                report.objImport = stats.ok
                    ? $"groups={stats.groups} verts={stats.vertices} tris={stats.triangles} " +
                      $"submeshes={stats.subMeshes} bounds={Fmt(stats.max - stats.min)}"
                    : "FAILED: " + stats.error;
                report.objMaterials = stats.materials;
                if (report.faces > 0)
                {
                    // Unity's OBJ importer mirrors X (right-handed -> left-handed), so the
                    // expectation for the imported bounds is the mirror of the voxel mesh.
                    var voxelMin = ToVector(report.boundsMin);
                    var voxelMax = ToVector(report.boundsMax);
                    var expectedMin = new Vector3(-voxelMax.x, voxelMin.y, voxelMin.z);
                    var expectedSize = voxelMax - voxelMin;
                    report.problems.AddRange(ValidateObjImport(
                        report.faces, expectedMin, expectedSize, stats, true));
                }
                log.AppendLine($"obj {key}: {report.objImport}");
            }

            // Render only samples whose geometry passed, so a broken mesh is not "proven" visually.
            var visual = new List<GameObject>();
            foreach (var go in GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                         .Select(r => r.gameObject)
                         .Where(g => g.name.StartsWith("voxel_"))
                         .OrderBy(g => g.name, StringComparer.Ordinal))
            {
                var report = reports.FirstOrDefault(r => "voxel_" + r.key == go.name);
                if (report == null || report.problems.Count > 0)
                    continue;
                visual.Add(go);
            }
            Layout(visual);
            foreach (var go in visual)
            {
                var report = reports.First(r => "voxel_" + r.key == go.name);
                RenderViews(cam, go.GetComponent<MeshRenderer>().bounds, report);
            }
            if (visual.Count > 0)
                RenderOverview(cam, visual, reports);

            foreach (var report in reports)
                if (report.problems.Count > 0)
                    exitCode = 1;
            if (setProblems.Count > 0)
                exitCode = 1;
            if (reports.Count == 0)
                exitCode = 1;

            log.AppendLine(exitCode == 0 ? "PILOT_RESULT=PASS" : "PILOT_RESULT=FAIL");
            WriteOutputs(reports, setProblems, log, exitCode);
            Debug.Log(log.ToString());
            return exitCode;
        }
        catch (Exception e)
        {
            log.AppendLine("EXCEPTION: " + e);
            log.AppendLine("PILOT_RESULT=EXCEPTION");
            setProblems.Add("exception: " + e.Message);
            WriteOutputs(reports, setProblems, log, 1);
            WriteExceptionReport(e);
            throw;
        }
    }

    static void CleanPreviousOutput()
    {
        if (!Directory.Exists(OutputDir)) return;
        foreach (var file in Directory.GetFiles(OutputDir))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                name == "report.txt" || name == "report.json" ||
                name == "report_exception.txt" || name == "selftest.json" || name == "selftest.txt")
            {
                File.Delete(file);
            }
        }
    }

    static List<string> SortedFiles(string dir, string pattern)
    {
        if (!Directory.Exists(dir)) return new List<string>();
        var files = Directory.GetFiles(dir, pattern);
        Array.Sort(files, StringComparer.Ordinal);
        return files.ToList();
    }

    static void WriteOutputs(List<SampleReport> reports, List<string> setProblems, StringBuilder log, int exitCode)
    {
        var jsonPath = Path.Combine(OutputDir, "report.json");
        File.WriteAllText(jsonPath, ToJson(reports, setProblems), new UTF8Encoding(false));
        log.AppendLine("REPORT_SAVED " + jsonPath);
        File.WriteAllText(Path.Combine(OutputDir, "report.txt"), log.ToString(), new UTF8Encoding(false));
    }

    static void WriteExceptionReport(Exception e)
    {
        try
        {
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(Path.Combine(OutputDir, "report_exception.txt"), e.ToString(),
                new UTF8Encoding(false));
            var marker = Path.Combine(OutputDir, "report.txt");
            var text = File.Exists(marker) ? File.ReadAllText(marker) : "";
            text += "\nPILOT_RESULT=EXCEPTION\n" + e;
            File.WriteAllText(Path.Combine(OutputDir, "report.txt"), text, new UTF8Encoding(false));
        }
        catch (Exception nested)
        {
            Debug.LogError("cannot write exception report: " + nested);
        }
    }

    static SampleReport BuildFromDecoded(string path, Shader shader, Transform root)
    {
        var key = Path.GetFileNameWithoutExtension(path);
        var report = new SampleReport { key = key };
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception e)
        {
            report.problems.Add("cannot read payload: " + e.Message);
            return report;
        }
        if (bytes.Length < 12)
        {
            report.problems.Add("payload shorter than 12-byte header");
            return report;
        }
        int width = BitConverter.ToInt32(bytes, 0);
        int depth = BitConverter.ToInt32(bytes, 4);
        int height = BitConverter.ToInt32(bytes, 8);
        long expectedBytes = 12L + (long)width * depth * height * 3L;
        if (width <= 0 || depth <= 0 || height <= 0 || expectedBytes != bytes.Length)
        {
            report.problems.Add($"bad header/size: {width}x{depth}x{height}, expected {expectedBytes}, got {bytes.Length}");
            return report;
        }
        report.width = width;
        report.depth = depth;
        report.height = height;

        bool Solid(int vx, int vd, int vh)
        {
            if (vx < 0 || vx >= width || vd < 0 || vd >= depth || vh < 0 || vh >= height) return false;
            int o = 12 + ((vh * depth + vd) * width + vx) * 3;
            return bytes[o] != 0 || bytes[o + 1] != 0 || bytes[o + 2] != 0;
        }

        var verts = new List<Vector3>();
        var cols = new List<Color>();
        var norms = new List<Vector3>();
        var tris = new List<int>();
        int solidCount = 0;
        int faceCount = 0;
        int minVx = int.MaxValue, minVd = int.MaxValue, minVh = int.MaxValue;
        int maxVx = int.MinValue, maxVd = int.MinValue, maxVh = int.MinValue;
        for (int vh = 0; vh < height; vh++)
        for (int vd = 0; vd < depth; vd++)
        for (int vx = 0; vx < width; vx++)
        {
            if (!Solid(vx, vd, vh)) continue;
            solidCount++;
            minVx = Math.Min(minVx, vx); maxVx = Math.Max(maxVx, vx);
            minVd = Math.Min(minVd, vd); maxVd = Math.Max(maxVd, vd);
            minVh = Math.Min(minVh, vh); maxVh = Math.Max(maxVh, vh);
            int o = 12 + ((vh * depth + vd) * width + vx) * 3;
            var color = new Color(bytes[o] / 255f, bytes[o + 1] / 255f, bytes[o + 2] / 255f, 1f);
            for (int f = 0; f < 6; f++)
            {
                var n = FaceNormals[f];
                if (Solid(vx + n[0], vd + n[2], vh + n[1])) continue;
                faceCount++;
                int baseIndex = verts.Count;
                var corners = FaceCorners[f];
                for (int c = 0; c < 4; c++)
                {
                    verts.Add(new Vector3(vx + corners[c][0], vh + corners[c][1], vd + corners[c][2]));
                    norms.Add(new Vector3(n[0], n[1], n[2]));
                    cols.Add(color);
                }
                tris.Add(baseIndex + 0); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
                tris.Add(baseIndex + 0); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
            }
        }
        report.solidVoxels = solidCount;
        report.faces = faceCount;

        var expectedExtent = faceCount == 0
            ? Vector3.zero
            : new Vector3(maxVx - minVx + 1, maxVh - minVh + 1, maxVd - minVd + 1);
        report.expectedExtent = Fmt(expectedExtent);
        if (faceCount == 0)
        {
            report.problems.Add("model produced no visible faces");
            return report;
        }

        var mesh = new Mesh { name = key };
        if (verts.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        report.vertices = mesh.vertexCount;
        report.triangles = mesh.triangles.Length / 3;
        report.subMeshes = mesh.subMeshCount;
        report.boundsMin = Fmt(mesh.bounds.min);
        report.boundsMax = Fmt(mesh.bounds.max);
        report.boundsSize = Fmt(mesh.bounds.size);
        report.center = Fmt(mesh.bounds.center);

        report.problems.AddRange(ValidateVoxelGeometry(
            report.faces, expectedExtent, report.vertices, report.triangles,
            mesh.bounds.min, mesh.bounds.size));

        var assetPath = $"{MeshAssetDir}/{key}.asset";
        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssets();
        report.meshAsset = assetPath;

        var material = new Material(shader) { name = key + "_mat" };
        AssetDatabase.CreateAsset(material, $"{MeshAssetDir}/{key}.mat");
        report.voxelMaterial = "vertex colour unlit (CubeGamePilot/VertexColorUnlit)";

        var go = new GameObject("voxel_" + key);
        go.transform.SetParent(root.transform);
        go.transform.rotation = Quaternion.identity;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        return report;
    }

    static ObjImportStats ImportObj(string objPath, StringBuilder log)
    {
        var stats = new ObjImportStats();
        try
        {
            AssetDatabase.ImportAsset(objPath, ImportAssetOptions.ForceUpdate |
                                               ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            if (asset == null)
            {
                stats.error = "no GameObject asset produced";
                return stats;
            }
            var filters = asset.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
            {
                stats.error = "no MeshFilter in the imported asset";
                return stats;
            }
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var materials = new List<Material>();
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh;
                stats.groups++;
                stats.vertices += mesh.vertexCount;
                stats.triangles += mesh.triangles.Length / 3;
                stats.subMeshes += mesh.subMeshCount;
                min = Vector3.Min(min, mesh.bounds.min);
                max = Vector3.Max(max, mesh.bounds.max);
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null) materials.AddRange(renderer.sharedMaterials);
            }
            if (stats.groups == 0)
            {
                stats.error = "imported asset contains only empty meshes";
                return stats;
            }
            stats.min = min;
            stats.max = max;
            stats.materials = DescribeMaterials(materials.ToArray());
            stats.ok = true;
            return stats;
        }
        catch (Exception e)
        {
            stats.error = e.Message;
            log.AppendLine($"obj import exception for {objPath}: {e}");
            return stats;
        }
    }

    static void Layout(List<GameObject> objects)
    {
        float cursor = 0f;
        foreach (var go in objects)
        {
            var bounds = go.GetComponent<MeshRenderer>().bounds;
            var size = bounds.size;
            go.transform.position = new Vector3(
                cursor + size.x * 0.5f - bounds.center.x,
                -bounds.min.y,
                -bounds.center.z);
            cursor += size.x + 4f;
        }
    }

    static void RenderViews(Camera cam, Bounds bounds, SampleReport report)
    {
        var target = bounds.center;
        var radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        var distance = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.35f;

        var views = new (string name, Vector3 dir)[]
        {
            ("front", new Vector3(0f, 0.05f, 1f)),
            ("side", new Vector3(1f, 0.05f, 0.05f)),
            ("threequarter", new Vector3(0.75f, 0.45f, 0.9f)),
        };
        foreach (var view in views)
        {
            var dir = view.dir.normalized;
            cam.transform.position = target + dir * distance;
            cam.transform.LookAt(target);
            var file = $"{OutputDir}/{report.key}_{view.name}.png";
            Capture(cam, file);
            report.images.Add(file);
        }
    }

    static void RenderOverview(Camera cam, List<GameObject> objects, List<SampleReport> reports)
    {
        if (objects.Count == 0) return;
        var bounds = new Bounds(objects[0].GetComponent<MeshRenderer>().bounds.center, Vector3.zero);
        foreach (var go in objects)
            bounds.Encapsulate(go.GetComponent<MeshRenderer>().bounds);
        var target = bounds.center;
        var radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
        var distance = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f;
        cam.transform.position = target + new Vector3(0.35f, 0.25f, 1f).normalized * distance;
        cam.transform.LookAt(target);
        Capture(cam, $"{OutputDir}/overview.png");
        foreach (var r in reports) r.images.Add($"{OutputDir}/overview.png");
    }

    static void Capture(Camera cam, string file)
    {
        var rt = new RenderTexture(CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(CaptureSize, CaptureSize, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, CaptureSize, CaptureSize), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }

    static string DescribeMaterials(Material[] mats)
    {
        if (mats == null || mats.Length == 0) return "no materials";
        var sb = new StringBuilder();
        for (int i = 0; i < mats.Length && i < 6; i++)
        {
            var m = mats[i];
            if (m == null) { sb.Append("<null>; "); continue; }
            var color = m.HasProperty("_Color") ? m.color : Color.white;
            sb.AppendFormat(CultureInfo.InvariantCulture, "{0}[{1}]; ", m.name, ColorUtility.ToHtmlStringRGB(color));
        }
        if (mats.Length > 6) sb.Append($"+{mats.Length - 6} more");
        return sb.ToString().Trim();
    }

    static Vector3 ToVector(string formatted)
    {
        var trimmed = formatted.Trim('(', ')');
        var parts = trimmed.Split(',');
        return new Vector3(
            float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture),
            float.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    static string Fmt(Vector3 v) => string.Format(CultureInfo.InvariantCulture,
        "({0:0.###},{1:0.###},{2:0.###})", v.x, v.y, v.z);

    static string ToJson(List<SampleReport> reports, List<string> setProblems)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"unityVersion\": \"{Application.unityVersion}\",");
        sb.AppendLine($"  \"generatedUtc\": \"{DateTime.UtcNow:O}\",");
        sb.AppendLine($"  \"verdict\": \"{(setProblems.Count == 0 && reports.All(r => r.problems.Count == 0) && reports.Count > 0 ? "PASS" : "FAIL")}\",");
        sb.AppendLine("  \"setProblems\": [" + string.Join(", ", setProblems.Select(p => "\"" + Esc(p) + "\"")) + "],");
        sb.AppendLine("  \"samples\": [");
        for (int i = 0; i < reports.Count; i++)
        {
            var r = reports[i];
            sb.AppendLine("  {");
            sb.AppendLine($"    \"key\": \"{r.key}\",");
            sb.AppendLine($"    \"dimensions\": \"{r.width}x{r.depth}x{r.height}\",");
            sb.AppendLine($"    \"solidVoxels\": {r.solidVoxels},");
            sb.AppendLine($"    \"faces\": {r.faces},");
            sb.AppendLine($"    \"vertices\": {r.vertices},");
            sb.AppendLine($"    \"triangles\": {r.triangles},");
            sb.AppendLine($"    \"subMeshes\": {r.subMeshes},");
            sb.AppendLine($"    \"expectedExtent\": \"{r.expectedExtent}\",");
            sb.AppendLine($"    \"boundsMin\": \"{r.boundsMin}\",");
            sb.AppendLine($"    \"boundsMax\": \"{r.boundsMax}\",");
            sb.AppendLine($"    \"boundsSize\": \"{r.boundsSize}\",");
            sb.AppendLine($"    \"center\": \"{r.center}\",");
            sb.AppendLine($"    \"meshAsset\": \"{r.meshAsset}\",");
            sb.AppendLine($"    \"voxelMaterial\": \"{Esc(r.voxelMaterial)}\",");
            sb.AppendLine($"    \"objMaterials\": \"{Esc(r.objMaterials ?? "n/a")}\",");
            sb.AppendLine($"    \"objImport\": \"{Esc(r.objImport ?? "n/a")}\",");
            sb.AppendLine($"    \"images\": [{string.Join(", ", r.images.Select(p => "\"" + Esc(p) + "\""))}],");
            sb.AppendLine($"    \"problems\": [{string.Join(", ", r.problems.Select(p => "\"" + Esc(p) + "\""))}]");
            sb.AppendLine(i == reports.Count - 1 ? "  }" : "  },");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    internal static string Esc(string s) => (s ?? string.Empty)
        .Replace("\\", "/").Replace("\"", "'").Replace("\n", " ").Replace("\r", " ");

    // ------------------------------------------------------------------
    // Self-tests for the validators (run in the editor, no assets involved)
    // ------------------------------------------------------------------

    static class SelfTests
    {
        class Case
        {
            public string name;
            public bool expectProblems;
            public Func<List<string>> run;
            public string expectSubstring;
            public List<string> problems;
            public bool pass;
        }

        public static bool RunAll()
        {
            var extent = new Vector3(2f, 3f, 4f);
            var goodImport = new ObjImportStats
            {
                ok = true, groups = 3, vertices = 40, triangles = 20, subMeshes = 3,
                min = Vector3.zero, max = extent, materials = "c1[FFFFFF]",
            };
            var mirroredImport = new ObjImportStats
            {
                ok = true, groups = 3, vertices = 40, triangles = 20, subMeshes = 3,
                min = new Vector3(-extent.x, 0f, 0f), max = new Vector3(0f, extent.y, extent.z),
                materials = "c1[FFFFFF]",
            };

            var cases = new List<Case>
            {
                new Case { name = "matching sets pass", expectProblems = false,
                    run = () => ValidateSampleSets(new[] { "a", "b" }, new[] { "a", "b" }) },
                new Case { name = "missing OBJ fails", expectProblems = true, expectSubstring = "missing OBJ",
                    run = () => ValidateSampleSets(new[] { "a", "b" }, new[] { "a" }) },
                new Case { name = "extra OBJ fails", expectProblems = true, expectSubstring = "without matching",
                    run = () => ValidateSampleSets(new[] { "a" }, new[] { "a", "ghost" }) },
                new Case { name = "empty decoded set fails", expectProblems = true, expectSubstring = "no .cubdec",
                    run = () => ValidateSampleSets(new string[0], new[] { "a" }) },
                new Case { name = "empty obj set fails", expectProblems = true, expectSubstring = "no .obj",
                    run = () => ValidateSampleSets(new[] { "a" }, new string[0]) },
                new Case { name = "duplicate key fails", expectProblems = true, expectSubstring = "duplicate",
                    run = () => ValidateSampleSets(new[] { "a", "a" }, new[] { "a" }) },
                new Case { name = "correct voxel geometry passes", expectProblems = false,
                    run = () => ValidateVoxelGeometry(10, extent, 40, 20, Vector3.zero, extent) },
                new Case { name = "wrong vertex count fails", expectProblems = true, expectSubstring = "vertices",
                    run = () => ValidateVoxelGeometry(10, extent, 39, 20, Vector3.zero, extent) },
                new Case { name = "wrong triangle count fails", expectProblems = true, expectSubstring = "triangles",
                    run = () => ValidateVoxelGeometry(10, extent, 40, 19, Vector3.zero, extent) },
                new Case { name = "wrong bounds fails", expectProblems = true, expectSubstring = "bounds",
                    run = () => ValidateVoxelGeometry(10, extent, 40, 20, Vector3.zero, new Vector3(2f, 3f, 3f)) },
                new Case { name = "negative bounds min fails", expectProblems = true, expectSubstring = "negative",
                    run = () => ValidateVoxelGeometry(10, extent, 40, 20, new Vector3(0f, -1f, 0f), extent) },
                new Case { name = "no faces fails", expectProblems = true, expectSubstring = "no faces",
                    run = () => ValidateVoxelGeometry(0, Vector3.zero, 0, 0, Vector3.zero, Vector3.zero) },
                new Case { name = "correct obj import passes", expectProblems = false,
                    run = () => ValidateObjImport(10, Vector3.zero, extent, goodImport, false) },
                new Case { name = "mirrored obj import passes", expectProblems = false,
                    run = () => ValidateObjImport(10, new Vector3(-2f, 0f, 0f), extent, mirroredImport, true) },
                new Case { name = "mirror without X flip fails", expectProblems = true, expectSubstring = "min",
                    run = () => ValidateObjImport(10, Vector3.zero, extent, mirroredImport, true) },
                new Case { name = "failed obj import fails", expectProblems = true, expectSubstring = "import failed",
                    run = () => ValidateObjImport(10, Vector3.zero, extent,
                        new ObjImportStats { ok = false, error = "no MeshFilter" }, false) },
                new Case { name = "obj triangle mismatch fails", expectProblems = true, expectSubstring = "triangles",
                    run = () => ValidateObjImport(11, Vector3.zero, extent, goodImport, false) },
                new Case { name = "obj bounds mismatch fails", expectProblems = true, expectSubstring = "bounds",
                    run = () => ValidateObjImport(10, Vector3.zero, new Vector3(2f, 3f, 5f), goodImport, false) },
                new Case { name = "obj import without groups fails", expectProblems = true, expectSubstring = "no mesh groups",
                    run = () => ValidateObjImport(10, Vector3.zero, extent,
                        new ObjImportStats { ok = true, groups = 0, triangles = 20, min = Vector3.zero, max = extent },
                        false) },
            };

            var failed = 0;
            var json = new StringBuilder();
            json.AppendLine("[");
            for (int i = 0; i < cases.Count; i++)
            {
                var c = cases[i];
                try
                {
                    c.problems = c.run() ?? new List<string>();
                }
                catch (Exception e)
                {
                    c.problems = new List<string> { "exception: " + e.Message };
                }
                var hasProblems = c.problems.Count > 0;
                c.pass = hasProblems == c.expectProblems;
                if (c.pass && c.expectSubstring != null)
                    c.pass = c.problems.Any(p => p.IndexOf(c.expectSubstring, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!c.pass) failed++;
                json.AppendLine($"  {{\"name\": \"{Esc(c.name)}\", \"expected\": \"{(c.expectProblems ? "problems" : "clean")}\", " +
                                $"\"passed\": {(c.pass ? "true" : "false")}, " +
                                $"\"problems\": [{string.Join(", ", c.problems.Select(p => "\"" + Esc(p) + "\""))}]}}");
            }
            json.AppendLine("]");

            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(Path.Combine(OutputDir, "selftest.json"), json.ToString(), new UTF8Encoding(false));
            var summary = new StringBuilder();
            summary.AppendLine($"SELFTEST_RESULT={(failed == 0 ? "PASS" : "FAIL")}");
            summary.AppendLine($"cases={cases.Count} failed={failed}");
            File.WriteAllText(Path.Combine(OutputDir, "selftest.txt"), summary.ToString(), new UTF8Encoding(false));
            Debug.Log(summary.ToString());
            return failed > 0;
        }
    }
}
