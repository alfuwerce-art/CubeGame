// AlphaCatalogImport.cs — D64: imports the Alpha model catalog (static geometry of every route) into an
// ISOLATED Unity project. Input: Assets/AlphaCatalog/Data/manifest.json + models/*.cubdec written by
// tools/alpha_models/catalog/static_export.py (local .scratch only, never committed).
//
// Every model is meshed by the accepted VoxelMesh (D61 marker rule) and saved as a Mesh asset +
// prefab under Assets/AlphaCatalog/Generated/<route>/. Each model is CHECKED, not just imported:
// grid size, solid and marker voxel counts must equal the manifest (computed independently in Python
// from the same blob), the mesh must have faces, and a prefab must exist afterwards. Resume: a model
// whose manifest record (sha256 + flags) equals the stored import record is skipped.
//
// Output: CatalogOutput/catalog_import.txt (CATALOG_IMPORT_RESULT=PASS|FAIL ...) and
// CatalogOutput/catalog_import.json (per model). Batch: -executeMethod CubeGame.AlphaCatalog.AlphaCatalogImport.Run
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CubeGame.AlphaTransfer;
using UnityEditor;
using UnityEngine;

namespace CubeGame.AlphaCatalog
{
    [Serializable] public class CatalogModel
    {
        public string name, file, sha256, route;
        public int loadFlag, solid, markers;
        public bool markerTransparent;
        public int[] size;
    }

    [Serializable] public class CatalogManifest { public string format; public CatalogModel[] models; }

    [Serializable] public class ImportRecord
    {
        public string name, route, sha256, verdict, problem, prefab;
        public int vertices, faces, solid, markers;
        public bool markerTransparent;
    }

    [Serializable] public class ImportState { public List<ImportRecord> records = new List<ImportRecord>(); }

    public static class AlphaCatalogImport
    {
        const string Root = "Assets/AlphaCatalog";
        const string DataDir = Root + "/Data";
        const string GenDir = Root + "/Generated";
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "CatalogOutput"));
        static string StatePath => Path.Combine(OutDir, "catalog_import.json");

        public static void Run()
        {
            int exit = 1;
            var log = new StringBuilder();
            try { exit = Import(log) ? 0 : 1; }
            catch (Exception e)
            {
                Directory.CreateDirectory(OutDir);
                File.WriteAllText(Path.Combine(OutDir, "catalog_import.txt"), "CATALOG_IMPORT_RESULT=FAIL exception=" + e);
            }
            EditorApplication.Exit(exit);
        }

        static bool Import(StringBuilder log)
        {
            Directory.CreateDirectory(OutDir);
            var manPath = Path.Combine(Application.dataPath, "AlphaCatalog", "Data", "manifest.json");
            if (!File.Exists(manPath)) throw new FileNotFoundException("manifest missing", manPath);
            var man = JsonUtility.FromJson<CatalogManifest>(File.ReadAllText(manPath));
            if (man == null || man.models == null || man.models.Length == 0) throw new Exception("MANIFEST_EMPTY");
            var state = File.Exists(StatePath) ? JsonUtility.FromJson<ImportState>(File.ReadAllText(StatePath)) : new ImportState();
            var done = new Dictionary<string, ImportRecord>();
            foreach (var r in state.records) done[r.name] = r;

            int pass = 0, fail = 0, skipped = 0;
            var problems = new List<string>();
            var records = new List<ImportRecord>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var m in man.models)
                {
                    if (done.TryGetValue(m.name, out var prev) && prev.verdict == "PASS" && prev.sha256 == m.sha256
                        && prev.markerTransparent == m.markerTransparent && prev.prefab != null
                        && File.Exists(Path.Combine(Application.dataPath, "..", prev.prefab)))
                    { records.Add(prev); skipped++; pass++; continue; }
                    var rec = ImportOne(m);
                    records.Add(rec);
                    if (rec.verdict == "PASS") pass++; else { fail++; problems.Add($"{m.name}: {rec.problem}"); }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.SaveAssets(); }

            // prefabs must exist after the asset database is flushed
            foreach (var r in records)
                if (r.verdict == "PASS" && AssetDatabase.LoadAssetAtPath<GameObject>(r.prefab) == null)
                { r.verdict = "FAIL"; r.problem = "PREFAB_NOT_FOUND_AFTER_SAVE"; pass--; fail++; problems.Add($"{r.name}: {r.problem}"); }

            state.records = records;
            File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
            var byRoute = new SortedDictionary<string, int[]>();
            foreach (var r in records)
            {
                if (!byRoute.TryGetValue(r.route, out var c)) byRoute[r.route] = c = new int[2];
                c[r.verdict == "PASS" ? 0 : 1]++;
            }
            var sb = new StringBuilder();
            bool ok = fail == 0 && pass == man.models.Length;
            sb.AppendLine($"CATALOG_IMPORT_RESULT={(ok ? "PASS" : "FAIL")} models={man.models.Length} pass={pass} fail={fail} resumed={skipped}");
            foreach (var kv in byRoute) sb.AppendLine($"ROUTE {kv.Key} pass={kv.Value[0]} fail={kv.Value[1]}");
            foreach (var p in problems) sb.AppendLine("PROBLEM " + p);
            File.WriteAllText(Path.Combine(OutDir, "catalog_import.txt"), sb.ToString());
            return ok;
        }

        static ImportRecord ImportOne(CatalogModel m)
        {
            var rec = new ImportRecord { name = m.name, route = m.route, sha256 = m.sha256, markerTransparent = m.markerTransparent };
            var path = Path.Combine(Application.dataPath, "AlphaCatalog", "Data", m.file);
            if (!File.Exists(path)) { rec.verdict = "FAIL"; rec.problem = "CUBDEC_MISSING"; return rec; }
            var built = VoxelMesh.Build(File.ReadAllBytes(path), Path.GetFileNameWithoutExtension(m.name), m.markerTransparent);
            rec.solid = built.solidVoxels; rec.markers = built.markerVoxels; rec.faces = built.faces;
            var want = new Vector3Int(m.size[0], m.size[1], m.size[2]);
            if (built.problem != null) { rec.verdict = "FAIL"; rec.problem = built.problem; return rec; }
            if (built.size != want) { rec.verdict = "FAIL"; rec.problem = $"SIZE {built.size} != {want}"; return rec; }
            if (built.solidVoxels != m.solid) { rec.verdict = "FAIL"; rec.problem = $"SOLID {built.solidVoxels} != {m.solid}"; return rec; }
            if (built.markerVoxels != m.markers) { rec.verdict = "FAIL"; rec.problem = $"MARKERS {built.markerVoxels} != {m.markers}"; return rec; }
            rec.vertices = built.mesh.vertexCount;
            if (rec.vertices != 4 * built.faces) { rec.verdict = "FAIL"; rec.problem = $"VERTICES {rec.vertices} != 4*faces"; return rec; }

            var dir = $"{GenDir}/{m.route}";
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", dir));
            var safe = Path.GetFileNameWithoutExtension(m.name);
            var meshPath = $"{dir}/{safe}.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(built.mesh, meshPath);
            var go = new GameObject(safe);
            go.AddComponent<MeshFilter>().sharedMesh = built.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("AlphaTransfer/VoxelShaded") ?? Shader.Find("Unlit/Color");
            if (shader != null) mr.sharedMaterial = SharedMaterial(shader);
            var prefabPath = $"{dir}/{safe}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            rec.prefab = prefabPath;
            rec.verdict = "PASS";
            return rec;
        }

        static Material _mat;
        static Material SharedMaterial(Shader shader)
        {
            if (_mat != null) return _mat;
            var p = $"{Root}/VoxelCatalog.mat";
            _mat = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (_mat == null) { _mat = new Material(shader); AssetDatabase.CreateAsset(_mat, p); }
            return _mat;
        }
    }
}
