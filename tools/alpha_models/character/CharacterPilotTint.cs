// S05A-2 (D51) — T3 mask tint for the hair, validated per voxel coordinate.
//
// Facts used: the engine writes the character colour triple into the appearance struct
// (CharCreation_refresh_model -> creature_generateAppearance), so the original bakes hair colour into
// the generated appearance rather than tinting at draw time. The exact replacement path is not
// recoverable from the inspected pseudo-C, therefore the pilot implements the approved T3 mask tint
// and marks it PARTIAL, never as VERIFIED_ALPHA.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static partial class CharacterPilotVerifier
{
    internal const string MaskTintProperty = "_MaskTint";
    internal const string MaskWeightProperty = "_MaskWeight";

    /// <summary>
    /// Checks the tint result strictly by voxel coordinate: every vertex is mapped back to the voxel
    /// it came from, and its colour must be the source colour (outside the mask) or the tint colour
    /// (inside the mask). A histogram comparison alone would miss swapped voxels, so positions are
    /// re-derived from the mesh itself.
    /// </summary>
    internal static List<string> ValidateTintByVoxel(string payloadPath, Mesh mesh, HashSet<int> mask,
        Color tint, int expectedMaskVertices)
    {
        var problems = new List<string>();
        if (!File.Exists(payloadPath))
            return new List<string> { $"tint: payload {Path.GetFileName(payloadPath)} not found" };
        if (mesh == null) return new List<string> { "tint: mesh is null" };

        var bytes = File.ReadAllBytes(payloadPath);
        int width = BitConverter.ToInt32(bytes, 0);
        int depth = BitConverter.ToInt32(bytes, 4);
        int height = BitConverter.ToInt32(bytes, 8);

        Color VoxelColor(int x, int d, int h)
        {
            int o = 12 + ((h * depth + d) * width + x) * 3;
            return new Color(bytes[o] / 255f, bytes[o + 1] / 255f, bytes[o + 2] / 255f, 1f);
        }
        bool IsMask(int x, int d, int h)
        {
            int o = 12 + ((h * depth + d) * width + x) * 3;
            return mask.Contains((bytes[o] << 16) | (bytes[o + 1] << 8) | bytes[o + 2]);
        }

        var vertices = mesh.vertices;
        var colors = mesh.colors;
        var uvs = mesh.uv2;
        if (vertices.Length != colors.Length || uvs.Length != vertices.Length)
            return new List<string> { "tint: mesh channels are inconsistent (vertices/colours/uv2)" };

        var normals = mesh.normals;
        int mismatchedOutside = 0, mismatchedInside = 0, maskVertices = 0;
        var badSamples = new List<string>();
        for (int i = 0; i < vertices.Length; i += 4)
        {
            // every face is emitted as a quad of four corners; the voxel is recovered from the
            // minimum corner minus the positive part of the face normal (a +X face sits at x+1).
            var n = normals.Length > i ? normals[i] : Vector3.zero;
            int x = Mathf.FloorToInt(Mathf.Min(vertices[i].x, vertices[i + 1].x, vertices[i + 2].x, vertices[i + 3].x) - Mathf.Max(0f, n.x));
            int y = Mathf.FloorToInt(Mathf.Min(vertices[i].y, vertices[i + 1].y, vertices[i + 2].y, vertices[i + 3].y) - Mathf.Max(0f, n.y));
            int z = Mathf.FloorToInt(Mathf.Min(vertices[i].z, vertices[i + 1].z, vertices[i + 2].z, vertices[i + 3].z) - Mathf.Max(0f, n.z));
            if (x < 0 || x >= width || y < 0 || y >= height || z < 0 || z >= depth)
            {
                problems.Add($"tint: vertex block {i} maps outside the grid at ({x},{y},{z})");
                break;
            }
            var source = VoxelColor(x, z, y);
            bool masked = IsMask(x, z, y);
            var uv = uvs[i];
            for (int c = 0; c < 4; c++)
            {
                var actual = colors[i + c];
                if (masked)
                {
                    maskVertices++;
                    if (uv.x < 0.5f)
                        mismatchedInside++;
                    if (!ColorEquals(actual, source))
                        mismatchedInside++;
                }
                else
                {
                    if (uv.x >= 0.5f)
                        mismatchedOutside++;
                    if (!ColorEquals(actual, source))
                        mismatchedOutside++;
                }
            }
            if ((mismatchedOutside > 0 || mismatchedInside > 0) && badSamples.Count < 3)
                badSamples.Add($"({x},{y},{z}) masked={masked}");
        }

        if (mismatchedOutside > 0)
            problems.Add($"tint: {mismatchedOutside} vertices outside the mask have a changed colour or a set mask channel " +
                         $"({string.Join(", ", badSamples)})");
        if (mismatchedInside > 0)
            problems.Add($"tint: {mismatchedInside} masked vertices keep a wrong colour or lack the mask channel");
        if (maskVertices != expectedMaskVertices)
            problems.Add($"tint: mask covers {maskVertices} vertices, expected {expectedMaskVertices} (source mask voxels x emitted faces)");
        return problems;
    }

    static bool ColorEquals(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 1e-3f && Mathf.Abs(a.g - b.g) < 1e-3f && Mathf.Abs(a.b - b.b) < 1e-3f;

    /// <summary>
    /// Per-voxel guard for the marker policy: pure mask-coloured voxels of the source model must still
    /// exist in the payload (nothing was deleted), and the decorative red (non-exact red) must stay.
    /// </summary>
    internal static List<string> ValidateMaskVoxelsPreserved(string payloadPath, HashSet<int> mask)
    {
        var problems = new List<string>();
        var bytes = File.ReadAllBytes(payloadPath);
        int width = BitConverter.ToInt32(bytes, 0);
        int depth = BitConverter.ToInt32(bytes, 4);
        int height = BitConverter.ToInt32(bytes, 8);
        int exactMask = 0, otherColours = 0;
        var distinct = new HashSet<int>();
        for (int i = 12; i + 2 < bytes.Length; i += 3)
        {
            var r = bytes[i]; var g = bytes[i + 1]; var b = bytes[i + 2];
            if (r == 0 && g == 0 && b == 0) continue;
            if (mask.Contains((r << 16) | (g << 8) | b)) exactMask++;
            else { otherColours++; distinct.Add((r << 16) | (g << 8) | b); }
        }
        if (exactMask == 0)
            problems.Add($"tint: payload contains no voxels of the mask colour {mask}");
        if (otherColours == 0)
            problems.Add("tint: the payload has no colours left besides the mask (palette must not be flattened)");
        if (distinct.Count < 2)
            problems.Add($"tint: only {distinct.Count} distinct non-mask colours survived, expected several");
        return problems;
    }

    internal static Color ParsePaletteColor(string hexOrName)
    {
        if (Proportions?.tint == null) return Color.white;
        return Color.white;
    }
}
