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
        // CUBEGAME_CONTRACT: staged local variants (variant names of the transfer pipeline).
        static readonly CreatureVisualProfile[] Known =
        {
            new CreatureVisualProfile { Kind = 0x02, ActorId = "d86_human_0x02_greatsword", Evidence = "D85 S05A-2 human, 94 clips" },
            new CreatureVisualProfile { Kind = 0x65, ActorId = "d86_dragon_0x65", Evidence = "D83 dragon_0x65_v1, 5 clips" },
            new CreatureVisualProfile { Kind = 0x20, ActorId = "whitecat_0x20_formula_vs_alpha", Evidence = "captured WhiteCat, formula walk" },
            new CreatureVisualProfile { Kind = 0x3f, ActorId = "plainrunner_0x3f_formula_vs_alpha", Evidence = "captured PlainRunner, formula walk" },
            new CreatureVisualProfile { Kind = 0x1c, ActorId = "wolf_0x1c_formula_only", Evidence = "formula walk (no capture)" },
        };

        public static IReadOnlyList<CreatureVisualProfile> Staged => Known;

        /// <summary>Resolve a kind to a staged actor id; false + reason when the cache lacks that variant.</summary>
        public static bool TryResolve(int kind, AlphaActorLibrary library, out string actorId, out string reason)
        {
            actorId = null;
            reason = null;
            foreach (var profile in Known)
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
            foreach (var profile in Known)
                if (TryResolve(profile.Kind, library, out _, out _)) kinds.Add(profile.Kind);
            return kinds;
        }

        public static bool IsStaged(int kind) => Array.FindIndex(Known, p => p.Kind == kind) >= 0;

        public static string Describe() =>
            $"{Known.Length} staged kinds: " + string.Join(", ", Array.ConvertAll(Known, p => $"0x{p.Kind:x2}->{p.ActorId}"));
    }
}
