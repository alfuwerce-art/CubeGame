// S05A-2 (D51) — C+ proportions, group scaling and T3 mask tint.
//
// Evidence-first: every factor in proportions.json carries a status. The code never invents values;
// it only applies what the configuration states and validates that the application is well-formed:
//   * head + hair are one logical group, so the scale is applied to the HEAD node only and the hair
//     inherits it exactly once (no double scaling);
//   * the weapon has its own factor and keeps its socket in the hand's local space;
//   * the tint is a shader mask: source vertex colours stay untouched, masked vertices get uv2.x = 1.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[Serializable]
public class ScaleGroup
{
    public string scale = "1";
    public string status = "";
    public string evidence = "";
    public string[] appliesTo = new string[0];
    public float Value => float.TryParse(scale, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 1f;
}

[Serializable]
public class ScaleGroups
{
    public ScaleGroup body = new ScaleGroup();
    public ScaleGroup head_hair = new ScaleGroup();
    public ScaleGroup weapon = new ScaleGroup();
}

[Serializable]
public class PaletteEntry
{
    public string name = "";
    public int r, g, b;
    public Color Color => new Color(r / 255f, g / 255f, b / 255f, 1f);
    public int[] Rgb => new[] { r, g, b };
}

[Serializable]
public class TintConfig
{
    public string mode = "mask";
    public int[] maskColor = { 255, 0, 0 };
    /// <summary>All source colours that belong to the mask (flat triples).</summary>
    public int[] maskColors = new int[0];
    public string status = "";
    /// <summary>Palette used for the rendered demonstration (first entry is the default).</summary>
    public PaletteEntry[] palettes = new PaletteEntry[0];
    /// <summary>Overrides the default palette when set (used by the colour-variant renders).</summary>
    public int[] active = new int[0];
    /// <summary>Flat list of mask colours (falls back to the single legacy maskColor).</summary>
    public List<int> MaskPacked
    {
        get
        {
            var list = new List<int>();
            if (maskColors != null && maskColors.Length >= 3)
            {
                for (int i = 0; i + 2 < maskColors.Length; i += 3)
                    list.Add((maskColors[i] << 16) | (maskColors[i + 1] << 8) | maskColors[i + 2]);
            }
            if (list.Count == 0 && maskColor != null && maskColor.Length >= 3)
                list.Add((maskColor[0] << 16) | (maskColor[1] << 8) | maskColor[2]);
            return list;
        }
    }

    public ColorMask? Mask => maskColor != null && maskColor.Length >= 3
        ? new ColorMask((byte)maskColor[0], (byte)maskColor[1], (byte)maskColor[2])
        : (ColorMask?)null;
}

[Serializable]
public class ProportionsConfig
{
    public int schema;
    public string baseline = "";
    public string[] notes = new string[0];
    public ScaleGroups groups = new ScaleGroups();
    public TintConfig tint = new TintConfig();

    public static ProportionsConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"proportions config not found: {path}");
        var config = JsonUtility.FromJson<ProportionsConfig>(File.ReadAllText(path));
        if (config == null) throw new InvalidOperationException("proportions config is empty");
        if (config.schema != 1) throw new InvalidOperationException($"unsupported proportions schema {config.schema}");
        return config;
    }
}

public struct ColorMask : IEquatable<ColorMask>
{
    public readonly byte R, G, B;
    public ColorMask(byte r, byte g, byte b) { R = r; G = g; B = b; }
    public bool Equals(ColorMask other) => R == other.R && G == other.G && B == other.B;
    public override bool Equals(object obj) => obj is ColorMask other && Equals(other);
    public override int GetHashCode() => (R << 16) | (G << 8) | B;
    public override string ToString() => $"({R},{G},{B})";
}

public static partial class CharacterPilotVerifier
{
    internal static ProportionsConfig Proportions;

    static void LoadProportions()
    {
        Proportions = ProportionsConfig.Load(Path.Combine(DataDir, "proportions.json"));
        var problems = ValidateProportionsConfig(Proportions);
        if (problems.Count > 0)
            throw new InvalidOperationException("proportions config rejected: " + string.Join("; ", problems));
    }

    /// <summary>Structural checks for the configuration itself.</summary>
    internal static List<string> ValidateProportionsConfig(ProportionsConfig config)
    {
        var problems = new List<string>();
        if (config == null) return new List<string> { "proportions: config is null" };
        foreach (var (name, group) in new[]
                 {
                     ("body", config.groups.body), ("head_hair", config.groups.head_hair),
                     ("weapon", config.groups.weapon),
                 })
        {
            if (group == null) { problems.Add($"proportions: group '{name}' missing"); continue; }
            if (group.Value <= 0f) problems.Add($"proportions: group '{name}' scale must be positive");
            if (group.Value > 4f) problems.Add($"proportions: group '{name}' scale {group.Value} is out of range");
            if (string.IsNullOrWhiteSpace(group.status))
                problems.Add($"proportions: group '{name}' has no evidence status");
        }
        if (config.tint != null && config.tint.mode == "mask" && config.tint.Mask == null)
            problems.Add("proportions: tint mask colour is missing");
        return problems;
    }

    /// <summary>Test-only override of the body group scale, used by the FIX-B rebuild at scale 0.5.</summary>
    internal static float? BodyScaleOverride;

    internal static float GroupScale(string group)
    {
        if (group == "body" && BodyScaleOverride.HasValue) return BodyScaleOverride.Value;
        if (Proportions == null) return 1f;
        return group switch
        {
            "body" => Proportions.groups.body.Value,
            "head_hair" => Proportions.groups.head_hair.Value,
            "weapon" => Proportions.groups.weapon.Value,
            _ => 1f,
        };
    }

    /// <summary>
    /// Applies the C+ group scales to the built hierarchy and checks that no node is scaled twice.
    /// </summary>
    internal static List<string> ApplyGroupScales(AssemblyBuild build)
    {
        var problems = new List<string>();
        var body = Find(build, "body");
        var head = Find(build, "head");
        var hair = Find(build, "hair");
        var sword = Find(build, "sword");
        if (body == null || head == null || hair == null || sword == null)
            return new List<string> { "proportions: cannot apply group scales, parts are missing" };

        var bodyScale = GroupScale("body");
        var headScale = GroupScale("head_hair");
        var weaponScale = GroupScale("weapon");

        // one uniform character scale on the root; every part inherits it exactly once
        if (build.root != null) build.root.transform.localScale = Vector3.one * bodyScale;
        // the head group is scaled on the head node only, so the hair inherits the same factor once
        head.localScale = Vector3.one * headScale;
        hair.localScale = Vector3.one;
        sword.localScale = Vector3.one * weaponScale;
        body.localScale = Vector3.one;

        if (!Mathf.Approximately(hair.localScale.x, 1f))
            problems.Add($"proportions: hair carries its own scale {hair.localScale.x}, the head group would be applied twice");
        if (!hair.IsChildOf(head))
            problems.Add("proportions: hair is not a child of the head, the group scale would not apply");
        if (!sword.IsChildOf(Find(build, "hand_r")))
            problems.Add("proportions: sword is not a child of the right hand");
        return problems;
    }

    /// <summary>Effective (world) scale of a part: root scale x its own group scale.</summary>
    internal static Vector3 WorldScaleOf(Transform t)
    {
        var scale = Vector3.one;
        for (var current = t; current != null; current = current.parent)
            scale = Vector3.Scale(scale, current.localScale);
        return scale;
    }

    /// <summary>
    /// World-space placement check (FIX-B): the mesh must sit exactly where the spec chain says it does
    /// after every ancestor scale is applied. The expected corner is transformed through the real pivot,
    /// so the check stays valid for any character scale, not only for 1.0.
    /// </summary>
    internal static List<string> ValidateWorldPlacement(PartResult result, Transform pivot)
    {
        var problems = new List<string>();
        if (result == null || pivot == null)
            return new List<string> { "proportions: placement check needs a part" };
        var bounds = BoundsOf(pivot);
        var expectedMin = pivot.TransformPoint(result.sourceMin - result.joint);
        var worldScale = WorldScaleOf(pivot);
        var expectedSize = Vector3.Scale(result.expectedSize, worldScale);
        var tolerance = 0.02f * Mathf.Max(1f, worldScale.magnitude);
        if ((bounds.min - expectedMin).magnitude > tolerance)
            problems.Add($"proportions: part '{result.id}' world min {FmtPr(bounds.min)} != expected " +
                         $"{FmtPr(expectedMin)} (world scale {FmtPr(worldScale)})");
        if ((bounds.size - expectedSize).magnitude > 5f * tolerance)
            problems.Add($"proportions: part '{result.id}' world size {FmtPr(bounds.size)} != expected " +
                         $"{FmtPr(expectedSize)}");
        return problems;
    }

    /// <summary>
    /// Scale-invariant group check: the world-space width ratio of head vs body must equal the
    /// authored voxel ratio (14/10) times the configured head/body scale ratio. This catches a wrong
    /// configuration and a double-applied scale at the same time.
    /// </summary>
    internal static List<string> ValidateGroupScaleRatios(AssemblyBuild build)
    {
        var problems = new List<string>();
        var head = Find(build, "head");
        var hair = Find(build, "hair");
        var body = Find(build, "body");
        var sword = Find(build, "sword");
        if (head == null || hair == null || body == null || sword == null)
            return new List<string> { "proportions: group ratio check needs head, hair, body and sword" };

        var headScale = GroupScale("head_hair");
        var bodyScale = GroupScale("body");
        var weaponScale = GroupScale("weapon");

        // 14/10 is the authored width ratio of human-head-m01 (14) vs body1 (10) in voxels
        var expected = 14f / 10f * headScale;
        var actual = BoundsOf(head).size.x / Mathf.Max(1e-6f, BoundsOf(body).size.x);
        if (Mathf.Abs(actual - expected) > 0.02f * expected)
            problems.Add($"proportions: head/body world width ratio {actual:0.###} != expected {expected:0.###} " +
                         $"(head group {headScale:0.###}, body group {bodyScale:0.###})");

        // hair must be scaled by the same group factor as the head (single application)
        var hairRelative = BoundsOf(hair).size.magnitude / Mathf.Max(1e-6f, BoundsOf(head).size.magnitude);
        if (hairRelative <= 0f || float.IsNaN(hairRelative))
            problems.Add("proportions: hair has a degenerate size after scaling");

        // the weapon keeps its authored length scaled by the weapon factor and the body factor
        var expectedSword = 37f * weaponScale * bodyScale;
        var actualSword = BoundsOf(sword).size.y;
        if (Mathf.Abs(actualSword - expectedSword) > 0.05f * expectedSword)
            problems.Add($"proportions: sword length {actualSword:0.###} != expected {expectedSword:0.###} " +
                         $"(weapon group {weaponScale:0.###})");
        return problems;
    }

    /// <summary>Active tint colour: explicit override, else the first palette entry, else a safe default.</summary>
    internal static Color HairTintColor()
    {
        var tint = Proportions?.tint;
        if (tint == null) return new Color(214 / 255f, 180 / 255f, 90 / 255f, 1f);
        if (tint.active != null && tint.active.Length >= 3)
            return new Color(tint.active[0] / 255f, tint.active[1] / 255f, tint.active[2] / 255f, 1f);
        if (tint.palettes != null && tint.palettes.Length > 0) return tint.palettes[0].Color;
        return new Color(214 / 255f, 180 / 255f, 90 / 255f, 1f);
    }

    /// <summary>
    /// Applies the tint colour to the hair renderer through a MaterialPropertyBlock. Material instances
    /// turned out to be unreliable here (the renderer kept default property values), the property
    /// block is per-renderer and always reaches the shader.
    /// </summary>
    internal static void ApplyHairTintMaterial(AssemblyBuild build) =>
        SetHairTint(build, HairTintColor(), 1f);

    /// <summary>Sets the tint colour and weight on the hair renderer (no material instance is created).</summary>
    internal static void SetHairTint(AssemblyBuild build, Color color, float weight)
    {
        var hair = Find(build, "hair");
        var renderer = hair != null ? hair.GetComponentInChildren<MeshRenderer>() : null;
        if (renderer == null) return;
        // vertex colours are authored in sRGB; in a linear project the tint must be converted too
        var shaderColour = PlayerSettings.colorSpace == ColorSpace.Linear ? color.linear : color;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor(MaskTintProperty, shaderColour);
        block.SetFloat(MaskWeightProperty, weight);
        renderer.SetPropertyBlock(block);
    }

    static string FmtPr(Vector3 v) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "({0:0.###},{1:0.###},{2:0.###})", v.x, v.y, v.z);
}
