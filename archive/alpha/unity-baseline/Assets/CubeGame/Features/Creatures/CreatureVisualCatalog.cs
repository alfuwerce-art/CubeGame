// CubeGame.Features.Creatures — which verified actor represents a kind (D86 M0-06A/06B).
//
// One local content variant per kind is staged at a time (D86: do not rebuild the 156-kind catalog
// without a technical need). The mapping below lists what the current cache really contains; a kind
// without a variant is reported instead of being replaced by a random model. A capsule marked
// VISUAL_PLACEHOLDER is the only fallback and its status says so explicitly.
using System;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;

namespace CubeGame.Features.Creatures
{
    public sealed class CreatureVisualProfile
    {
        public int Kind;
        public string ActorId;
        public string Evidence;
    }

    public static class CreatureVisualCatalog
    {
        // Evidence notes for kinds whose preparation is documented in a report; the rest is the
        // pipeline's own status (formula walk / static object) taken from the staged table.
        static readonly Dictionary<int, string> EvidenceNotes = new Dictionary<int, string>
        {
            { 0x00, "ElfMale, same motion family as the captured human; no live run (LOCOMOTION_ONLY)" },
            { 0x02, "D85 S05A-2 human, 94 clips" },
            { 0x1c, "formula walk (no capture)" },
            { 0x20, "captured WhiteCat, formula walk" },
            { 0x3f, "captured PlainRunner, formula walk" },
            { 0x4c, "SpikeCreature family, formula walk (D62 family)" },
            { 0x62, "horse body plan, formula walk" },
            { 0x65, "D83 dragon_0x65_v1, 5 clips" },
            { 0x76, "RuneGiant: geometry + formula walk staged; special behaviour/actions BLOCKED (R-9)" },
        };

        static CreatureVisualProfile[] _cached;

        /// <summary>Kind -> staged variant pairs (committed list; the variants live in the local cache).</summary>
        public static IReadOnlyList<CreatureVisualProfile> Staged
        {
            get
            {
                if (_cached != null) return _cached;
                var list = new List<CreatureVisualProfile>(AlphaStagedVisuals.All.Length);
                foreach (var staged in AlphaStagedVisuals.All)
                {
                    list.Add(new CreatureVisualProfile
                    {
                        Kind = staged.Kind,
                        ActorId = staged.Variant,
                        Evidence = EvidenceNotes.TryGetValue(staged.Kind, out var note)
                            ? note
                            : $"staged {staged.ControllerMode} ({staged.Parts} parts, {staged.Clips} clips)",
                    });
                }
                _cached = list.ToArray();
                return _cached;
            }
        }

        /// <summary>Resolve a kind to a staged actor id; false + reason when the cache lacks that variant.</summary>
        public static bool TryResolve(int kind, AlphaActorLibrary library, out string actorId, out string reason)
        {
            actorId = null;
            reason = null;
            foreach (var profile in Staged)
                if (profile.Kind == kind) { actorId = profile.ActorId; reason = profile.Evidence; }
            if (actorId == null)
            {
                reason = $"MISSING_VISUAL_CONTENT kind=0x{kind:x2} (no staged variant; not substituted)";
                return false;
            }
            if (library == null || !library.TryGet(actorId, out var def))
            {
                reason = $"MISSING_VISUAL_CONTENT kind=0x{kind:x2} actor={actorId} (variant not staged in the local cache)";
                actorId = null;
                return false;
            }
            if (!def.Ready) { reason = "ACTOR_NOT_READY " + actorId; actorId = null; return false; }
            return true;
        }

        /// <summary>Kinds that can be spawned with a real Alpha visual right now.</summary>
        public static List<int> AvailableKinds(AlphaActorLibrary library)
        {
            var kinds = new List<int>();
            foreach (var profile in Staged)
                if (TryResolve(profile.Kind, library, out _, out _)) kinds.Add(profile.Kind);
            return kinds;
        }

        public static bool IsStaged(int kind)
        {
            foreach (var profile in Staged) if (profile.Kind == kind) return true;
            return false;
        }

        public static string Describe() =>
            $"{Staged.Count} staged kinds: " + string.Join(", ", System.Linq.Enumerable.Select(Staged, p => $"0x{p.Kind:x2}->{p.ActorId}"));
    }
}
