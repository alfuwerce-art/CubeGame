// S05A-2 GRIP (D52) — derive the sword grip from its own geometry and place the hand on the handle.
//
// Evidence: iron-greatsword.cub is 3x13x37 voxels. Slice analysis (y = blade axis):
//   y 0..6   : 5 voxels per slice, cross-section x 0..2, z 5..7   -> handle (pommel included)
//   y 7..8   : 29 / 39 voxels, z 0..12                           -> cross-guard
//   y 9..32  : 11 voxels, x = 1 only, z 1..11                    -> flat blade
//   y 33..36 : tapering to 3 voxels                              -> tip
// The grip points are therefore on the handle axis (x = 1, z = 6), between the pommel and the guard.
// These are GEOMETRY-DERIVED values (RECONSTRUCTED_FROM_EVIDENCE), not game constants.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public struct GripGeometry
{
    public int width, depth, height;
    public int handleYMin, handleYMax;     // inclusive voxel rows of the handle
    public int guardYMin, guardYMax;       // inclusive rows of the cross-guard
    public int bladeYMin, bladeYMax;
    public float centreX, centreZ;         // handle axis centre in voxel units
    public float handleRadius;             // half of the widest handle cross-section
    public Vector3 primaryGrip;            // voxel-space point, upper part of the handle
    public Vector3 secondaryGrip;          // voxel-space point, lower part of the handle
    public string status;

    public Vector3 Pommel => new Vector3(centreX, handleYMin, centreZ);
    public Vector3 GuardCentre => new Vector3(centreX, guardYMax + 0.5f, centreZ);
}

public static partial class CharacterPilotVerifier
{
    /// <summary>
    /// Reads the payload and derives the handle / guard / blade rows. No hard-coded numbers: the grip is
    /// a function of the model itself, so a different weapon yields its own grip.
    /// </summary>
    internal static GripGeometry DeriveGrip(string payloadPath)
    {
        var bytes = File.ReadAllBytes(payloadPath);
        int width = BitConverter.ToInt32(bytes, 0);
        int depth = BitConverter.ToInt32(bytes, 4);
        int height = BitConverter.ToInt32(bytes, 8);

        var counts = new int[height];
        var minX = new int[height];
        var maxX = new int[height];
        var minZ = new int[height];
        var maxZ = new int[height];
        for (int y = 0; y < height; y++)
        {
            minX[y] = minZ[y] = int.MaxValue;
            maxX[y] = maxZ[y] = int.MinValue;
            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
            {
                int o = 12 + ((y * depth + z) * width + x) * 3;
                if (bytes[o] == 0 && bytes[o + 1] == 0 && bytes[o + 2] == 0) continue;
                counts[y]++;
                minX[y] = Math.Min(minX[y], x); maxX[y] = Math.Max(maxX[y], x);
                minZ[y] = Math.Min(minZ[y], z); maxZ[y] = Math.Max(maxZ[y], z);
            }
        }
        int widest = 0;
        for (int y = 0; y < height; y++) widest = Math.Max(widest, counts[y]);
        // The cross-guard is a wide band, not a single row: every row above 60% of the widest row
        // belongs to it. (For iron-greatsword this is y = 7..8, matching the manual slice analysis.)
        int guardStart = Array.IndexOf(counts, widest);
        while (guardStart > 0 && counts[guardStart - 1] >= widest * 0.6f) guardStart--;
        int guardEnd = guardStart;
        while (guardEnd + 1 < height && counts[guardEnd + 1] >= widest * 0.6f) guardEnd++;

        var grip = new GripGeometry
        {
            width = width, depth = depth, height = height,
            handleYMin = 0, handleYMax = Math.Max(0, guardStart - 1),
            guardYMin = guardStart, guardYMax = guardEnd,
            bladeYMin = Math.Min(height - 1, guardEnd + 1), bladeYMax = height - 1,
            status = "RECONSTRUCTED_FROM_EVIDENCE",
        };
        // handle axis: centre of the handle rows
        float sumX = 0f, sumZ = 0f;
        int rows = 0;
        for (int y = grip.handleYMin; y <= grip.handleYMax; y++)
        {
            if (counts[y] == 0) continue;
            sumX += (minX[y] + maxX[y] + 1) * 0.5f;
            sumZ += (minZ[y] + maxZ[y] + 1) * 0.5f;
            rows++;
        }
        grip.centreX = rows > 0 ? sumX / rows : width * 0.5f;
        grip.centreZ = rows > 0 ? sumZ / rows : depth * 0.5f;
        var maxHandleHalf = 0f;
        for (int y = grip.handleYMin; y <= grip.handleYMax; y++)
        {
            if (counts[y] == 0) continue;
            maxHandleHalf = Mathf.Max(maxHandleHalf,
                0.5f * Mathf.Max(maxX[y] - minX[y] + 1, maxZ[y] - minZ[y] + 1));
        }
        grip.handleRadius = maxHandleHalf;
        // the primary hand sits just below the guard, the secondary hand near the pommel;
        // both stay inside the handle rows (no palm on the guard or on the blade)
        float span = grip.handleYMax - grip.handleYMin;
        grip.primaryGrip = new Vector3(grip.centreX, grip.handleYMin + Mathf.Round(span * 0.75f) + 0.5f, grip.centreZ);
        grip.secondaryGrip = new Vector3(grip.centreX, grip.handleYMin + Mathf.Round(span * 0.25f) + 0.5f, grip.centreZ);
        return grip;
    }

    /// <summary>Sanity checks for the derived grip against the model itself.</summary>
    internal static List<string> ValidateGripGeometry(GripGeometry g)
    {
        var problems = new List<string>();
        if (g.handleYMax < g.handleYMin) problems.Add("grip: handle rows are empty");
        if (g.guardYMax < g.guardYMin) problems.Add("grip: no cross-guard row was found");
        if (g.bladeYMin > g.bladeYMax) problems.Add("grip: no blade rows were found");
        if (g.primaryGrip.y <= g.handleYMin || g.primaryGrip.y >= g.handleYMax + 1f)
            problems.Add($"grip: primary grip y={g.primaryGrip.y} is outside the handle rows " +
                         $"[{g.handleYMin}..{g.handleYMax}]");
        if (g.secondaryGrip.y <= g.handleYMin || g.secondaryGrip.y >= g.handleYMax + 1f)
            problems.Add($"grip: secondary grip y={g.secondaryGrip.y} is outside the handle rows " +
                         $"[{g.handleYMin}..{g.handleYMax}]");
        if (Mathf.Abs(g.primaryGrip.y - g.secondaryGrip.y) < 1f)
            problems.Add("grip: primary and secondary grips are too close for a two-handed hold");
        if (g.handleRadius <= 0f) problems.Add("grip: handle radius is zero");
        return problems;
    }

    /// <summary>
    /// World-space contact check: the hand's grip point must coincide with the sword's grip point, and the
    /// palm must sit on the handle (not on the guard, not on the blade).
    /// </summary>
    internal static List<string> ValidateGripContact(string label, Transform hand, Vector3 handGripLocal,
        Transform sword, Vector3 gripVoxel, GripGeometry grip, float tolerance)
    {
        var problems = new List<string>();
        if (hand == null || sword == null)
            return new List<string> { $"grip: {label} needs both hand and sword" };
        var handPoint = hand.TransformPoint(handGripLocal);
        var swordPoint = sword.TransformPoint(gripVoxel);
        var distance = Vector3.Distance(handPoint, swordPoint);
        if (distance > tolerance)
            problems.Add($"grip: {label} hand is {distance:0.###} away from its grip point (tolerance {tolerance})");

        // the contact point must be inside the handle rows, measured back in sword-local voxel space
        // (InverseTransformPoint already undoes the weapon scale, so no extra division is needed)
        var voxel = sword.InverseTransformPoint(swordPoint);
        if (voxel.y < grip.handleYMin - 0.1f || voxel.y > grip.handleYMax + 1.1f)
            problems.Add($"grip: {label} contact is at handle y={voxel.y:0.##}, outside the handle rows");

        // and it must not sit inside the blade
        if (voxel.y >= grip.guardYMin)
            problems.Add($"grip: {label} contact y={voxel.y:0.##} is on the guard/blade, not on the handle");
        return problems;
    }

    /// <summary>Blade clearance: the blade segment must not pass through the head or the torso.</summary>
    internal static List<string> ValidateBladeClearance(Transform sword, GripGeometry grip,
        Bounds headBounds, Bounds bodyBounds, float margin)
    {
        var problems = new List<string>();
        var bladeStart = sword.TransformPoint(new Vector3(grip.centreX, grip.bladeYMin, grip.centreZ));
        var bladeEnd = sword.TransformPoint(new Vector3(grip.centreX, grip.bladeYMax, grip.centreZ));
        foreach (var (name, bounds) in new[] { ("head", headBounds), ("body", bodyBounds) })
        {
            var expanded = bounds;
            expanded.Expand(margin * 2f);
            if (SegmentHitsBox(bladeStart, bladeEnd, expanded))
                problems.Add($"grip: blade passes through the {name} (segment {Fmt3(bladeStart)}..{Fmt3(bladeEnd)})");
        }
        return problems;
    }

    static bool SegmentHitsBox(Vector3 a, Vector3 b, Bounds box)
    {
        // sample the segment: cheap, deterministic and precise enough for a voxel character
        const int steps = 24;
        for (int i = 0; i <= steps; i++)
        {
            var p = Vector3.Lerp(a, b, (float)i / steps);
            if (box.Contains(p)) return true;
        }
        return false;
    }

    static string Fmt3(Vector3 v) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.##},{1:0.##},{2:0.##})",
            v.x, v.y, v.z);
}
