// S05A-2 (D51) — before/after comparison renders for the producer's visual decision.
//
// Produces, from ONE fixed camera rig (identical position, FOV, lighting and framing for every shot):
//   before_*            baseline S05A-1 look (all group scales 1.0, no tint)
//   after_*             C+ configuration (weapon factor from the code evidence, tint enabled)
//   hair_<palette>_*    the same pose with each configured hair colour
// and a machine-readable summary (sizes, factors, mask coverage, exit-critical values).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class CharacterPilotVerifier
{
    // Fixed rig: the framing is derived from a constant box, not from the built bounds, so the
    // before/after frames are pixel-comparable.
    static readonly Vector3 CompareTarget = new Vector3(0f, 12f, 0f);
    const float CompareRadius = 22f;

    [MenuItem("CubeGame Pilot/Run S05A-2 proportions comparison")]
    public static void RunProportionsCompare()
    {
        Directory.CreateDirectory(OutputDir);
        CleanOutputs("before_", "after_", "hair_", "proportions_compare");
        var summary = new List<string>();
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- before: baseline S05A-1 (scale 1, tint off) ----------
            var baselinePath = Path.Combine(DataDir, "proportions.baseline.json");
            var saved = File.Exists(Path.Combine(DataDir, "proportions.json"))
                ? File.ReadAllText(Path.Combine(DataDir, "proportions.json"))
                : null;
            try
            {
                if (File.Exists(baselinePath))
                    File.Copy(baselinePath, Path.Combine(DataDir, "proportions.json"), true);
                var before = BuildCharacter(false);
                if (before.problems.Count > 0)
                    throw new Exception("baseline build problems: " + string.Join("; ", before.problems));
                SetHairTintWeight(before, 0f);
                RenderCompareSet(before, "before");
                summary.Add($"before: sword={FmtPr(CleanSize(before, "sword"))} head={FmtPr(CleanSize(before, "head"))}");
                UnityEngine.Object.DestroyImmediate(before.root);
            }
            finally
            {
                if (saved != null) File.WriteAllText(Path.Combine(DataDir, "proportions.json"), saved);
            }

            // ---------- after: C+ configuration ----------
            var after = BuildCharacter(false);
            if (after.problems.Count > 0)
                throw new Exception("C+ build problems: " + string.Join("; ", after.problems));
            SetHairTintWeight(after, 1f);
            RenderCompareSet(after, "after");
            summary.Add($"after: sword={FmtPr(CleanSize(after, "sword"))} head={FmtPr(CleanSize(after, "head"))}");

            // ---------- hair colour variants ----------
            var palettes = Proportions?.tint?.palettes ?? new PaletteEntry[0];
            foreach (var palette in palettes)
            {
                SetHairTintColor(after, palette.Color);
                RenderCompareSet(after, "hair_" + palette.name);
                summary.Add($"hair_{palette.name}: rgb=({palette.r},{palette.g},{palette.b})");
            }
            if (palettes.Length > 0) SetHairTintColor(after, palettes[0].Color);

            // Debug: force the mask on for every hair vertex. If the hair does not change colour,
            // the material/property path is broken; if it does, the uv2 data is the problem.
            SetHairTintOverlay(after, 1f);
            RenderCompareSet(after, "debug_overlay");
            SetHairTintOverlay(after, 0f);

            DumpTintDiagnostics(after);
            var hair = after.results.FirstOrDefault(r => r.id == "hair");
            var palettesReport = Proportions?.tint?.palettes ?? new PaletteEntry[0];
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"baseline\": \"{Proportions?.baseline}\",");
            json.AppendLine($"  \"groups\": {{ \"body\": {V(GroupScale("body"))}, \"head_hair\": {V(GroupScale("head_hair"))}, \"weapon\": {V(GroupScale("weapon"))} }},");
            json.AppendLine($"  \"sword\": {{ \"before\": \"{FmtPr(CleanSizeFromNote(summary, "before"))}\", \"after\": \"{FmtPr(CleanSize(after, "sword"))}\" }},");
            json.AppendLine($"  \"head\": \"{FmtPr(CleanSize(after, "head"))}\",");
            json.AppendLine($"  \"hairTint\": {{ \"voxels\": {hair?.maskVoxels ?? 0}, \"vertices\": {hair?.maskVertices ?? 0}, \"status\": \"{Proportions?.tint?.status}\", \"maskColors\": [{string.Join(",", Proportions?.tint?.MaskPacked ?? new List<int>())}] }},");
            json.AppendLine($"  \"camera\": {{ \"target\": \"{FmtPr(CompareTarget)}\", \"radius\": {V(CompareRadius)}, \"fov\": 30 }},");
            json.AppendLine($"  \"palettes\": [{string.Join(",", palettesReport.Select(p => $"\"{p.name}\""))}],");
            json.AppendLine($"  \"views\": [\"front\", \"back\", \"side\", \"threequarter\", \"weapon\"],");
            json.AppendLine($"  \"notes\": [{string.Join(",", summary.Select(n => "\"" + n + "\""))}]");
            json.AppendLine("}");
            File.WriteAllText(Path.Combine(OutputDir, "proportions_compare.json"), json.ToString(), new UTF8Encoding(false));
            Debug.Log($"PROPORTIONS_RESULT=PASS rendered comparison for {palettes.Length + 2} looks");
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(OutputDir, "proportions_exception.txt"), e.ToString());
            File.WriteAllText(Path.Combine(OutputDir, "proportions_compare.txt"),
                "PROPORTIONS_RESULT=EXCEPTION\n" + e + "\n");
            Debug.LogError("PROPORTIONS_RESULT=EXCEPTION " + e.Message);
            throw;
        }

        File.WriteAllText(Path.Combine(OutputDir, "proportions_compare.txt"),
            "PROPORTIONS_RESULT=PASS\n" + string.Join("\n", summary) + "\n");
    }

    static void RenderCompareSet(AssemblyBuild build, string stem)
    {
        var camGo = GameObject.Find("PilotCamera");
        var cam = camGo != null ? camGo.GetComponent<Camera>() : null;
        if (cam == null)
        {
            cam = new GameObject("PilotCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.13f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 5000f;
            var light = new GameObject("PilotLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
        }
        // identical framing for every stem: derived from the constant box, never from the model
        var distance = CompareRadius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
        foreach (var (name, dir) in new[]
                 {
                     ("front", new Vector3(0f, 0.08f, 1f)),
                     ("back", new Vector3(0f, 0.08f, -1f)),
                     ("side", new Vector3(1f, 0.08f, 0.05f)),
                     ("threequarter", new Vector3(0.8f, 0.35f, 0.9f)),
                     ("weapon", new Vector3(0.45f, 0.2f, 1f)),
                 })
        {
            cam.transform.position = CompareTarget + dir.normalized * distance;
            cam.transform.LookAt(CompareTarget);
            Capture(cam, $"{OutputDir}/{stem}_{name}.png");
        }
    }

    static Vector3 CleanSize(AssemblyBuild build, string id)
    {
        var t = Find(build, id);
        return t == null ? Vector3.zero : BoundsOf(t).size;
    }

    static Vector3 CleanSizeFromNote(List<string> notes, string prefix)
    {
        // the note "before: sword=(3,37,13) head=(14,8,8)" keeps the baseline numbers in the report
        var note = notes.FirstOrDefault(n => n.StartsWith(prefix + ":"));
        if (note == null) return Vector3.zero;
        var start = note.IndexOf("sword=(", StringComparison.Ordinal) + 7;
        var end = note.IndexOf(')', start);
        var parts = note.Substring(start, end - start).Split(',');
        return new Vector3(float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture),
            float.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    static void SetHairTintWeight(AssemblyBuild build, float weight) =>
        SetHairTint(build, HairTintColor(), weight);

    static void SetHairTintColor(AssemblyBuild build, Color color) =>
        SetHairTint(build, color, 1f);

    /// <summary>Technical dump: proves where the tint colour actually lands (material, uv2, colours).</summary>
    static void DumpTintDiagnostics(AssemblyBuild build)
    {
        var lines = new List<string>();
        var hair = Find(build, "hair");
        var renderer = hair != null ? hair.GetComponentInChildren<MeshRenderer>() : null;
        var material = renderer != null ? renderer.sharedMaterial : null;
        lines.Add("renderer=" + (renderer != null));
        lines.Add("material=" + (material != null));
        if (material != null)
        {
            lines.Add("shader=" + material.shader.name);
            lines.Add("hasMaskTint=" + material.HasProperty(MaskTintProperty));
            lines.Add("maskTint=" + (material.HasProperty(MaskTintProperty)
                ? material.GetColor(MaskTintProperty).ToString() : "n/a"));
            lines.Add("maskWeight=" + (material.HasProperty(MaskWeightProperty)
                ? material.GetFloat(MaskWeightProperty).ToString(CultureInfo.InvariantCulture) : "n/a"));
            lines.Add("keywords=" + string.Join(",", material.shaderKeywords));
        }
        var filter = hair != null ? hair.GetComponentInChildren<MeshFilter>() : null;
        var mesh = filter != null ? filter.sharedMesh : null;
        if (mesh != null)
        {
            var uv2 = mesh.uv2;
            var masked = uv2.Count(v => v.x > 0.5f);
            lines.Add("mesh=" + mesh.name + " vertices=" + mesh.vertexCount + " uv2=" + uv2.Length +
                      " masked=" + masked);
            var colours = mesh.colors.Select(c => c.ToString()).Distinct().ToList();
            lines.Add("distinctColours=" + colours.Count);
            lines.Add("coloursSample=" + string.Join(" ", colours.Take(6)));
        }
        File.WriteAllText(Path.Combine(OutputDir, "tint_diagnostics.txt"), string.Join("\n", lines));
    }

    static void SetHairTintOverlay(AssemblyBuild build, float value)
    {
        var hair = Find(build, "hair");
        var renderer = hair != null ? hair.GetComponentInChildren<MeshRenderer>() : null;
        if (renderer == null) return;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetFloat("_MaskOverlay", value);
        renderer.SetPropertyBlock(block);
    }

    static string V(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
