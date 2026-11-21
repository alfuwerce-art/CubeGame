// S05A-2 pilot — Unity adapter between Alpha-style pose data and the assembled 8-part character.
//
// Cube World Alpha stores a creature pose as a 4x4 matrix per bone (Creature_drawModel / set_mat4 in the
// reverse-engineering notes), while the assembled pilot character is a plain Unity Transform hierarchy
// (body -> head -> hair, body -> hand_l/hand_r/foot_l/foot_r, hand_r -> sword). This adapter is the single
// place where pose data becomes a Unity pose, and it keeps the three possible input kinds explicitly
// separated instead of mixing them:
//
//   a) PartMatrices  — one world-space 4x4 matrix per part. The hierarchy-local pose is
//                      local = inverse(parentWorld) * partWorld, decomposed into
//                      localPosition / localRotation / localScale after validation.
//   b) LocalTrs      — hierarchy-local position / rotation / scale, applied directly.
//   c) Procedural    — a delegate t -> local TRS per part, sampled at a phase.
//
// Contract (enforced here, exercised by PoseAdapterSelfTests in the Editor assembly):
//   * the adapter only writes parts it owns (constructed with); every other part is reported as a named
//     problem and left untouched — nothing is silently ignored;
//   * each applied pose carries an explicit provenance label (PoseProvenance), the label is part of the
//     adapter's own record, and a pose labelled TEST_DATA_NOT_ALPHA can never be labelled as
//     ALPHA_DERIVED: that claim is refused outright unless the caller opts in explicitly;
//   * validation covers finiteness of every input element, an invertible and plausibly scaled parent
//     matrix, the determinant/scale sanity and the TRS-representability of the resulting local transform.
//     A rejected part is reported and not written; the report describes exactly what was written.
//
// Status: no real Alpha pose data has been extracted yet (the controlled live capture is BLOCKED, see
// docs/research/alpha2013/ANIMATION_EXTRACTION_RESEARCH.md), so everything exercised by the self-tests is
// TEST_DATA_NOT_ALPHA. The adapter exists so real matrices can be fed in later without touching the
// assembly code, and so test data can never be mistaken for Alpha evidence.
//
// Runtime-agnostic: no UnityEditor dependency, no input source of its own. No MonoBehaviour is attached
// because there is no live data source yet; an empty driver component would only be dead code.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeGame.CharacterPilot
{
    /// <summary>Where a pose came from. Kept explicit so reports cannot mix the three sources up.</summary>
    public enum PoseSourceKind
    {
        PartMatrices = 0,
        LocalTrs = 1,
        Procedural = 2,
    }

    /// <summary>Hierarchy-local transform (position / rotation / scale) of one part.</summary>
    public struct PoseTrs
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public PoseTrs(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        public static PoseTrs Identity =>
            new PoseTrs(Vector3.zero, Quaternion.identity, Vector3.one);

        public bool IsFinite =>
            PoseValidation.IsFinite(position) && PoseValidation.IsFinite(rotation) &&
            PoseValidation.IsFinite(scale);
    }

    /// <summary>Provenance labels a pose may carry. Anything else is refused.</summary>
    public static class PoseProvenance
    {
        /// <summary>Real pose data extracted from the original game.</summary>
        public const string AlphaDerived = "ALPHA_DERIVED";

        /// <summary>Rebuilt from evidence (timing, geometry) but not the original pose itself.</summary>
        public const string Reconstructed = "RECONSTRUCTED_FROM_EVIDENCE";

        /// <summary>Authored here, no Alpha origin at all.</summary>
        public const string OriginalNotAlpha = "ORIGINAL_NOT_ALPHA";

        /// <summary>Synthetic data written for tests. Must never be reported as Alpha-derived.</summary>
        public const string TestData = "TEST_DATA_NOT_ALPHA";

        /// <summary>
        /// Numbers extracted from the original client, but the mapping from a channel to a body
        /// part is still provisional (S05A2-D58-A1). Real values, unproven binding.
        /// </summary>
        public const string PartialBinding = "PARTIAL_BINDING";

        static readonly string[] Known =
        {
            AlphaDerived, Reconstructed, OriginalNotAlpha, TestData, PartialBinding,
        };

        public static IEnumerable<string> KnownLabels => Known;

        public static bool IsKnown(string label) =>
            !string.IsNullOrEmpty(label) && Array.IndexOf(Known, label) >= 0;

        /// <summary>
        /// Validates a provenance label together with an origin claim. A label is mandatory; unknown
        /// labels are refused; TEST_DATA_NOT_ALPHA can never be presented as ALPHA_DERIVED, and an
        /// Alpha-origin claim is only valid together with the ALPHA_DERIVED label.
        /// </summary>
        public static List<string> Validate(string label, bool claimsAlphaDerived)
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(label))
            {
                problems.Add("provenance: the pose carries no explicit provenance label");
                return problems;
            }
            if (!IsKnown(label))
                problems.Add($"provenance: unknown label '{label}'; expected one of {string.Join(", ", Known)}");
            if (label == TestData && claimsAlphaDerived)
                problems.Add($"provenance: {TestData} must not be labelled as {AlphaDerived}");
            if (claimsAlphaDerived && label != AlphaDerived)
                problems.Add($"provenance: an Alpha-origin claim requires the {AlphaDerived} label, " +
                             $"got '{label}'");
            return problems;
        }
    }

    /// <summary>Shared numeric checks; public so the self-tests can exercise them directly.</summary>
    public static class PoseValidation
    {
        /// <summary>1 voxel = 1 Unity unit; part scale groups are 0.6..1.0, so 1e-4..1e4 is generous.</summary>
        public const float MinScale = 1e-4f;
        public const float MaxScale = 1e4f;

        /// <summary>Below this the parent matrix is treated as singular (no usable inverse).</summary>
        public const float MinDeterminant = 1e-9f;

        /// <summary>Decomposition error above this means the matrix is not a TRS (shear or skew).</summary>
        public const float TrsResidualTolerance = 1e-4f;

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        public static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);

        public static bool IsFinite(Matrix4x4 value)
        {
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    if (!IsFinite(value[row, column])) return false;
            return true;
        }

        public static float MaxAbsDifference(Matrix4x4 a, Matrix4x4 b)
        {
            var worst = 0f;
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    worst = Mathf.Max(worst, Mathf.Abs(a[row, column] - b[row, column]));
            return worst;
        }

        /// <summary>True when every scale axis is inside the plausible range (sign is checked separately).</summary>
        public static bool IsPlausibleScale(Vector3 scale) =>
            Mathf.Abs(scale.x) >= MinScale && Mathf.Abs(scale.x) <= MaxScale &&
            Mathf.Abs(scale.y) >= MinScale && Mathf.Abs(scale.y) <= MaxScale &&
            Mathf.Abs(scale.z) >= MinScale && Mathf.Abs(scale.z) <= MaxScale;
    }

    /// <summary>
    /// Drives an assembled character from pose data. See the file header for the contract.
    /// </summary>
    public sealed class CharacterPoseAdapter
    {
        readonly Dictionary<string, Transform> _parts = new Dictionary<string, Transform>(StringComparer.Ordinal);

        /// <summary>Only a caller that explicitly opts in may claim Alpha origin for a pose.</summary>
        public bool AllowAlphaDerivedClaims { get; private set; }

        public PoseSourceKind LastSource { get; private set; }
        public string LastProvenance { get; private set; } = "";
        public int LastAppliedParts { get; private set; }
        public string LastRecord { get; private set; } = "pose: nothing applied yet";

        public CharacterPoseAdapter(IReadOnlyDictionary<string, Transform> parts,
            bool allowAlphaDerivedClaims = false)
        {
            if (parts != null)
            {
                foreach (var entry in parts)
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Value != null)
                        _parts[entry.Key] = entry.Value;
            }
            AllowAlphaDerivedClaims = allowAlphaDerivedClaims;
        }

        /// <summary>Builds an adapter for the assembled character: part id "head" -> child "part_head".</summary>
        public static CharacterPoseAdapter ForAssembledCharacter(Transform root,
            IEnumerable<string> partIds, bool allowAlphaDerivedClaims = false)
        {
            var parts = new Dictionary<string, Transform>(StringComparer.Ordinal);
            if (root != null && partIds != null)
            {
                foreach (var id in partIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    var child = FindDeep(root, "part_" + id);
                    if (child != null) parts[id] = child;
                }
            }
            return new CharacterPoseAdapter(parts, allowAlphaDerivedClaims);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        public IReadOnlyCollection<string> OwnedParts => _parts.Keys;

        /// <summary>The adapter's record of the last applied pose, provenance label included.</summary>
        public string Describe() => LastRecord;

        // ------------------------------------------------------------------
        // a) part matrices (world space 4x4 per part)
        // ------------------------------------------------------------------

        /// <summary>
        /// Applies one world-space matrix per part. Parts are written parent-first: a child's local matrix
        /// is resolved against the parent's ALREADY APPLIED pose, which is the only order in which
        /// local = inverse(parentWorld) * partWorld describes the same pose for the whole hierarchy.
        /// </summary>
        public List<string> ApplyPartMatrices(IReadOnlyDictionary<string, Matrix4x4> worldMatrices,
            string provenance, bool claimsAlphaDerived = false)
        {
            var problems = CheckProvenance("part_matrices", provenance, claimsAlphaDerived);
            if (problems.Count > 0) return problems;
            if (worldMatrices == null || worldMatrices.Count == 0)
                return new List<string> { "pose: part_matrices input is empty" };

            var ordered = new List<KeyValuePair<string, Matrix4x4>>(worldMatrices);
            ordered.Sort((a, b) => HierarchyDepth(a.Key).CompareTo(HierarchyDepth(b.Key)));

            var applied = 0;
            foreach (var entry in ordered)
            {
                if (!_parts.TryGetValue(entry.Key, out var part))
                {
                    problems.Add($"pose: part '{entry.Key}' is not owned by this adapter (owned: {OwnedList()})");
                    continue;
                }
                var parentWorld = part.parent != null ? part.parent.localToWorldMatrix : Matrix4x4.identity;
                var local = ResolveLocalPose(entry.Key, entry.Value, parentWorld, out var pose);
                problems.AddRange(local);
                if (local.Count > 0) continue;
                Write(part, pose);
                applied++;
            }
            Record(PoseSourceKind.PartMatrices, provenance, applied, problems.Count);
            return problems;
        }

        /// <summary>
        /// Pure conversion used by the matrix path: local = inverse(parentWorld) * partWorld, validated and
        /// decomposed. No Transform is touched, so the conversion itself is unit-testable.
        /// </summary>
        public static List<string> ResolveLocalPose(string partName, Matrix4x4 partWorld,
            Matrix4x4 parentWorld, out PoseTrs pose)
        {
            pose = PoseTrs.Identity;
            var problems = new List<string>();
            var label = string.IsNullOrEmpty(partName) ? "<unnamed>" : partName;

            if (!PoseValidation.IsFinite(partWorld))
            {
                problems.Add($"pose: part '{label}': part matrix is not finite (NaN or infinity)");
                return problems;
            }
            if (!PoseValidation.IsFinite(parentWorld))
            {
                problems.Add($"pose: part '{label}': parent matrix is not finite (NaN or infinity)");
                return problems;
            }
            var determinant = parentWorld.determinant;
            if (!PoseValidation.IsFinite(determinant) ||
                Mathf.Abs(determinant) < PoseValidation.MinDeterminant)
            {
                problems.Add($"pose: part '{label}': parent matrix is singular (determinant {determinant:0.#####}), " +
                             "it cannot be inverted");
                return problems;
            }
            var parentScale = parentWorld.lossyScale;
            if (!PoseValidation.IsPlausibleScale(parentScale))
            {
                problems.Add($"pose: part '{label}': parent scale ({parentScale.x:0.#####},{parentScale.y:0.#####}," +
                             $"{parentScale.z:0.#####}) is outside the plausible range " +
                             $"[{PoseValidation.MinScale}, {PoseValidation.MaxScale}]");
                return problems;
            }

            var local = parentWorld.inverse * partWorld;
            if (!PoseValidation.IsFinite(local))
            {
                problems.Add($"pose: part '{label}': the converted local matrix is not finite");
                return problems;
            }
            var candidate = new PoseTrs(local.GetColumn(3), local.rotation, local.lossyScale);
            problems.AddRange(ValidatePose(label, candidate, out pose));
            if (problems.Count > 0) return problems;

            // Shear/skew cannot be expressed by a Unity Transform; refuse instead of writing a pose that
            // is not the one we were given.
            var reconstructed = Matrix4x4.TRS(pose.position, pose.rotation, pose.scale);
            var residual = PoseValidation.MaxAbsDifference(local, reconstructed);
            if (residual > PoseValidation.TrsResidualTolerance)
            {
                pose = PoseTrs.Identity;
                problems.Add($"pose: part '{label}': the local matrix is not a TRS (residual {residual:0.#####} " +
                             $"of {PoseValidation.TrsResidualTolerance}); shear or skew cannot be applied");
            }
            return problems;
        }

        // ------------------------------------------------------------------
        // b) local positions / rotations (applied directly)
        // ------------------------------------------------------------------

        public List<string> ApplyLocalTrs(IReadOnlyDictionary<string, PoseTrs> poses, string provenance,
            bool claimsAlphaDerived = false)
        {
            var problems = CheckProvenance("local_trs", provenance, claimsAlphaDerived);
            if (problems.Count > 0) return problems;
            if (poses == null || poses.Count == 0)
                return new List<string> { "pose: local_trs input is empty" };

            var applied = 0;
            foreach (var entry in poses)
            {
                if (!_parts.TryGetValue(entry.Key, out var part))
                {
                    problems.Add($"pose: part '{entry.Key}' is not owned by this adapter (owned: {OwnedList()})");
                    continue;
                }
                var validation = ValidatePose(entry.Key, entry.Value, out var pose);
                problems.AddRange(validation);
                if (validation.Count > 0) continue;
                Write(part, pose);
                applied++;
            }
            Record(PoseSourceKind.LocalTrs, provenance, applied, problems.Count);
            return problems;
        }

        // ------------------------------------------------------------------
        // c) procedural curves
        // ------------------------------------------------------------------

        public List<string> ApplyProcedural(IReadOnlyDictionary<string, Func<float, PoseTrs>> curves,
            float phase, string provenance, bool claimsAlphaDerived = false)
        {
            var problems = CheckProvenance("procedural", provenance, claimsAlphaDerived);
            if (problems.Count > 0) return problems;
            if (curves == null || curves.Count == 0)
                return new List<string> { "pose: procedural input is empty" };
            if (!PoseValidation.IsFinite(phase))
                return new List<string> { "pose: procedural phase is not finite" };

            var applied = 0;
            foreach (var entry in curves)
            {
                if (!_parts.TryGetValue(entry.Key, out var part))
                {
                    problems.Add($"pose: part '{entry.Key}' is not owned by this adapter (owned: {OwnedList()})");
                    continue;
                }
                if (entry.Value == null)
                {
                    problems.Add($"pose: part '{entry.Key}': procedural curve is null");
                    continue;
                }
                PoseTrs sampled;
                try
                {
                    sampled = entry.Value(phase);
                }
                catch (Exception e)
                {
                    problems.Add($"pose: part '{entry.Key}': procedural curve threw {e.GetType().Name}: {e.Message}");
                    continue;
                }
                var validation = ValidatePose(entry.Key, sampled, out var pose);
                problems.AddRange(validation);
                if (validation.Count > 0) continue;
                Write(part, pose);
                applied++;
            }
            Record(PoseSourceKind.Procedural, provenance, applied, problems.Count);
            return problems;
        }

        // ------------------------------------------------------------------
        // internals
        // ------------------------------------------------------------------

        List<string> CheckProvenance(string source, string provenance, bool claimsAlphaDerived)
        {
            var problems = PoseProvenance.Validate(provenance, claimsAlphaDerived);
            if (claimsAlphaDerived && !AllowAlphaDerivedClaims)
                problems.Add($"pose: this adapter refuses Alpha-origin claims (allowAlphaDerivedClaims=false), " +
                             $"source={source}");
            return problems;
        }

        static List<string> ValidatePose(string partName, PoseTrs pose, out PoseTrs normalised)
        {
            normalised = PoseTrs.Identity;
            var problems = new List<string>();
            var label = string.IsNullOrEmpty(partName) ? "<unnamed>" : partName;
            if (!PoseValidation.IsFinite(pose.position))
            {
                problems.Add($"pose: part '{label}': local position is not finite");
                return problems;
            }
            if (!PoseValidation.IsFinite(pose.rotation))
            {
                problems.Add($"pose: part '{label}': local rotation is not finite");
                return problems;
            }
            var magnitude = Mathf.Sqrt(pose.rotation.x * pose.rotation.x + pose.rotation.y * pose.rotation.y +
                                       pose.rotation.z * pose.rotation.z + pose.rotation.w * pose.rotation.w);
            if (magnitude < 1e-6f)
            {
                problems.Add($"pose: part '{label}': local rotation is degenerate (|q| = {magnitude:0.######})");
                return problems;
            }
            if (!PoseValidation.IsFinite(pose.scale))
            {
                problems.Add($"pose: part '{label}': local scale is not finite");
                return problems;
            }
            // A negative scale component mirrors the part; Unity expresses that as a negative scale, so it
            // is applied but reported, because a mirrored part is never a legitimate Alpha pose.
            if (pose.scale.x * pose.scale.y * pose.scale.z < 0f)
            {
                problems.Add($"pose: part '{label}': local scale ({pose.scale.x:0.#####},{pose.scale.y:0.#####}," +
                             $"{pose.scale.z:0.#####}) is mirrored (negative determinant)");
                return problems;
            }
            if (!PoseValidation.IsPlausibleScale(pose.scale))
            {
                problems.Add($"pose: part '{label}': local scale ({pose.scale.x:0.#####},{pose.scale.y:0.#####}," +
                             $"{pose.scale.z:0.#####}) is outside the plausible range " +
                             $"[{PoseValidation.MinScale}, {PoseValidation.MaxScale}]");
                return problems;
            }
            normalised = new PoseTrs(pose.position, pose.rotation.normalized, pose.scale);
            return problems;
        }

        static void Write(Transform part, PoseTrs pose)
        {
            part.localPosition = pose.position;
            part.localRotation = pose.rotation;
            part.localScale = pose.scale;
        }

        int HierarchyDepth(string partName)
        {
            if (!_parts.TryGetValue(partName, out var part)) return int.MaxValue;
            var depth = 0;
            for (var current = part.parent; current != null; current = current.parent) depth++;
            return depth;
        }

        string OwnedList()
        {
            var names = new List<string>(_parts.Keys);
            names.Sort(StringComparer.Ordinal);
            return names.Count == 0 ? "<none>" : string.Join(", ", names);
        }

        void Record(PoseSourceKind source, string provenance, int applied, int problems)
        {
            LastSource = source;
            LastProvenance = provenance;
            LastAppliedParts = applied;
            LastRecord = $"pose: source={source} provenance={provenance} parts={applied}/{_parts.Count} " +
                         $"problems={problems}";
            Debug.Log("[pose-adapter] " + LastRecord);
        }
    }
}
