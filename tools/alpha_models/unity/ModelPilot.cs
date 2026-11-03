// S05A-0 conversion pilot — isolated verification project (NOT the production CubeGame project).
//
// Two independent paths are checked for every sample:
//   1. "voxel": decoded .cub payload (Assets/PilotData/*.cubdec) -> Mesh with vertex colours
//      (unlit vertex-colour shader), saved as a mesh asset and rendered to PNG.
//   2. "obj": the OBJ/MTL written by tools/alpha_models/cub_to_obj.py imported by Unity's
//      own ModelImporter (Assets/PilotModels/*.obj) -> mesh statistics reported.
//
// Usage (batchmode):
//   Unity.exe -batchmode -quit -projectPath <this> -executeMethod ModelPilot.Run -logFile run.log

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        public int processedFaces;
        public int vertices;
        public int triangles;
        public int subMeshes;
        public string materialInfo;
        public string objMaterials;
        public string boundsMin;
        public string boundsMax;
        public string boundsSize;
        public string center;
        public int expectedX, expectedY, expectedZ;
        public string boundsSizeSize;
        public string meshAsset;
        public string objImport;
        public List<string> images = new List<string>();
        public List<string> problems = new List<string>();
    }

    [MenuItem("CubeGame Pilot/Run conversion verification")]
    public static void Run()
    {
        var log = new StringBuilder();
        var reports = new List<SampleReport>();
        int exitCode = 0;
        try
        {
            Directory.CreateDirectory(OutputDir);
            Directory.CreateDirectory(MeshAssetDir);
            AssetDatabase.Refresh();

            var shader = Shader.Find("CubeGamePilot/VertexColorUnlit");
            if (shader == null)
            {
                log.AppendLine("FATAL: shader CubeGamePilot/VertexColorUnlit not found");
                File.WriteAllText(Path.Combine(OutputDir, "report.txt"), log.ToString());
                return;
            }

            var decoded = Directory.GetFiles(DataDir, "*.cubdec");
            Array.Sort(decoded, StringComparer.Ordinal);
            log.AppendLine($"decoded payloads: {decoded.Length}");

            var root = new GameObject("PilotRoot");
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

            var spacing = 0f;
            var placeholders = new List<GameObject>();
            foreach (var path in decoded)
            {
                var report = BuildFromDecoded(path, shader, root.transform, ref spacing);
                reports.Add(report);
                if (report.problems.Count > 0) exitCode = 1;
                var go = GameObject.Find("voxel_" + report.key);
                if (go != null) placeholders.Add(go);
                log.AppendLine($"built {report.key}: faces={report.faces} verts={report.vertices} bounds={report.boundsSize}");
            }

            // OBJ path: import through Unity's own ModelImporter and report what Unity sees.
            var objs = Directory.GetFiles(ModelsDir, "*.obj");
            Array.Sort(objs, StringComparer.Ordinal);
            log.AppendLine($"obj files: {objs.Length}");
            foreach (var objPath in objs)
            {
                AssetDatabase.ImportAsset(objPath, ImportAssetOptions.ForceUpdate |
                                                   ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
                var key = Path.GetFileNameWithoutExtension(objPath);
                var report = reports.Find(r => r.key == key);
                if (report == null)
                {
                    report = new SampleReport { key = key };
                    reports.Add(report);
                }
                if (asset == null)
                {
                    report.problems.Add("OBJ import failed: no GameObject asset loaded");
                    report.objImport = "FAILED";
                    exitCode = 1;
                    continue;
                }
                // Unity's OBJ importer creates one GameObject (and one submesh) per `g` group,
                // so every MeshFilter under the imported root has to be aggregated.
                var filters = asset.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length == 0)
                {
                    report.problems.Add("OBJ import produced no mesh");
                    report.objImport = "FAILED";
                    exitCode = 1;
                    continue;
                }
                int verts = 0, tris = 0, subMeshes = 0, groups = 0;
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                var matList = new List<Material>();
                foreach (var filter in filters)
                {
                    if (filter.sharedMesh == null) continue;
                    groups++;
                    var mesh = filter.sharedMesh;
                    verts += mesh.vertexCount;
                    tris += mesh.triangles.Length / 3;
                    subMeshes += mesh.subMeshCount;
                    var mb = mesh.bounds;
                    min = Vector3.Min(min, mb.min);
                    max = Vector3.Max(max, mb.max);
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer != null) matList.AddRange(renderer.sharedMaterials);
                }
                if (groups == 0)
                {
                    report.problems.Add("OBJ import produced only empty meshes");
                    report.objImport = "FAILED";
                    exitCode = 1;
                    continue;
                }
                var size = max - min;
                report.meshAsset = report.meshAsset ?? "(obj import)";
                report.subMeshes = subMeshes;
                report.objImport = string.Format(CultureInfo.InvariantCulture,
                    "PASS groups={0} verts={1} tris={2} submeshes={3} bounds={4} materials={5}",
                    groups, verts, tris, subMeshes, Fmt(size), matList.Count);
                report.objMaterials = DescribeMaterials(matList.ToArray());
                if (report.vertices > 0)
                {
                    var delta = size - new Vector3(report.expectedX, report.expectedY, report.expectedZ);
                    if (delta.magnitude > 0.01f)
                        report.problems.Add($"OBJ bounds differ from voxel mesh: obj={Fmt(size)} voxel={report.boundsSizeSize}");
                }
                log.AppendLine($"obj {key}: {report.objImport}");
            }

            // Render: lay the samples out first so that per-sample captures are isolated.
            Layout(placeholders);
            foreach (var go in placeholders)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                var b = renderer.bounds;
                var report = reports.Find(r => r.key == go.name.Substring("voxel_".Length));
                RenderViews(cam, b, report);
            }

            // One wide overview of every voxel-built sample laid out in a row.
            RenderOverview(cam, placeholders, reports);

            var reportPath = Path.Combine(OutputDir, "report.json");
            File.WriteAllText(reportPath, ToJson(reports), new UTF8Encoding(false));
            log.AppendLine(exitCode == 0 ? "PILOT_RESULT=PASS" : "PILOT_RESULT=FAIL");
            log.AppendLine("REPORT_SAVED " + reportPath);
            File.WriteAllText(Path.Combine(OutputDir, "report.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log(log.ToString());
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(OutputDir, "report_exception.txt"),
                e.ToString(), new UTF8Encoding(false));
            Debug.LogError(e);
            log.AppendLine("PILOT_RESULT=EXCEPTION");
        }
    }

    static SampleReport BuildFromDecoded(string path, Shader shader, Transform root, ref float spacing)
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
        for (int vh = 0; vh < height; vh++)
        for (int vd = 0; vd < depth; vd++)
        for (int vx = 0; vx < width; vx++)
        {
            if (!Solid(vx, vd, vh)) continue;
            solidCount++;
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
        report.processedFaces = faceCount;
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

        var assetPath = $"{MeshAssetDir}/{key}.asset";
        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssets();
        report.meshAsset = assetPath;

        var material = new Material(shader) { name = key + "_mat" };
        var matPath = $"{MeshAssetDir}/{key}.mat";
        AssetDatabase.CreateAsset(material, matPath);
        report.materialInfo = "vertex colour unlit (CubeGamePilot/VertexColorUnlit)";

        var go = new GameObject("voxel_" + key);
        go.transform.SetParent(root.transform);
        go.transform.position = new Vector3(spacing, 0f, 0f);
        go.transform.rotation = Quaternion.identity;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        spacing += 0f; // layout is re-done below once every bound is known

        // Expected extents come from the solid voxels, not from the grid: shipped models pad
        // their bounding box with empty space. Map voxel (x, d, h) -> Unity (x, h, d).
        int minVx = int.MaxValue, minVd = int.MaxValue, minVh = int.MaxValue;
        int maxVx = int.MinValue, maxVd = int.MinValue, maxVh = int.MinValue;
        for (int vh = 0; vh < height; vh++)
        for (int vd = 0; vd < depth; vd++)
        for (int vx = 0; vx < width; vx++)
        {
            if (!Solid(vx, vd, vh)) continue;
            minVx = Math.Min(minVx, vx); maxVx = Math.Max(maxVx, vx);
            minVd = Math.Min(minVd, vd); maxVd = Math.Max(maxVd, vd);
            minVh = Math.Min(minVh, vh); maxVh = Math.Max(maxVh, vh);
        }
        report.expectedX = maxVx - minVx + 1;
        report.expectedY = maxVh - minVh + 1;
        report.expectedZ = maxVd - minVd + 1;
        report.boundsSizeSize = string.Format(CultureInfo.InvariantCulture, "({0},{1},{2})",
            report.expectedX, report.expectedY, report.expectedZ);
        var expected = new Vector3(report.expectedX, report.expectedY, report.expectedZ);
        var got = mesh.bounds.size;
        if (Mathf.Abs(expected.x - got.x) > 0.001f ||
            Mathf.Abs(expected.y - got.y) > 0.001f ||
            Mathf.Abs(expected.z - got.z) > 0.001f)
        {
            report.problems.Add($"bounds mismatch: expected solid extent {Fmt(expected)}, got {Fmt(got)}");
        }
        if (mesh.bounds.min.x < -0.001f || mesh.bounds.min.y < -0.001f || mesh.bounds.min.z < -0.001f)
            report.problems.Add($"negative bounds min {Fmt(mesh.bounds.min)} (models start at 0 on every axis)");
        return report;
    }

    static void Layout(List<GameObject> objects)
    {
        float cursor = 0f;
        foreach (var go in objects)
        {
            var bounds = go.GetComponent<MeshRenderer>().bounds;
            var size = bounds.size;
            // Mesh vertices start at voxel (0,0,0) while the object origin sits there too,
            // so subtract the current bounds centre to place the sample on the floor.
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
        // Samples are already laid out in a row; frame the whole row.
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
        var png = tex.EncodeToPNG();
        File.WriteAllBytes(file, png);
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

    static string Fmt(Vector3 v) => string.Format(CultureInfo.InvariantCulture,
        "({0:0.###},{1:0.###},{2:0.###})", v.x, v.y, v.z);

    static string ToJson(List<SampleReport> reports)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < reports.Count; i++)
        {
            var r = reports[i];
            sb.AppendLine("  {");
            sb.AppendLine($"    \"key\": \"{r.key}\",");
            sb.AppendLine($"    \"dimensions\": \"{r.width}x{r.depth}x{r.height}\",");
            sb.AppendLine($"    \"solidVoxels\": {r.solidVoxels},");
            sb.AppendLine($"    \"expectedFaces\": {r.faces},");
            sb.AppendLine($"    \"vertices\": {r.vertices},");
            sb.AppendLine($"    \"triangles\": {r.triangles},");
            sb.AppendLine($"    \"subMeshes\": {r.subMeshes},");
            sb.AppendLine($"    \"meshAsset\": \"{r.meshAsset}\",");
            sb.AppendLine($"    \"voxelMaterial\": \"{Esc(r.materialInfo)}\",");
            sb.AppendLine($"    \"objMaterials\": \"{Esc(r.objMaterials ?? "n/a")}\",");
            sb.AppendLine($"    \"boundsMin\": \"{r.boundsMin}\",");
            sb.AppendLine($"    \"boundsMax\": \"{r.boundsMax}\",");
            sb.AppendLine($"    \"boundsSize\": \"{r.boundsSize}\",");
            sb.AppendLine($"    \"center\": \"{r.center}\",");
            sb.AppendLine($"    \"objImport\": \"{Esc(r.objImport ?? "n/a")}\",");
            sb.AppendLine($"    \"images\": [{string.Join(", ", r.images.ConvertAll(p => "\"" + Esc(p) + "\""))}],");
            sb.AppendLine($"    \"problems\": [{string.Join(", ", r.problems.ConvertAll(p => "\"" + Esc(p) + "\""))}]");
            sb.AppendLine(i == reports.Count - 1 ? "  }" : "  },");
        }
        sb.AppendLine("]");
        return sb.ToString();
    }

    static string Esc(string s) => (s ?? string.Empty).Replace("\\", "/").Replace("\"", "'");
}
