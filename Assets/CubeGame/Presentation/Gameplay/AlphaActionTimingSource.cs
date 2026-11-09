using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.AlphaTransfer;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Reads attack phases from the S05 action clips of the local Alpha content cache (DeepSeek visual runtime,
    /// public API only: <see cref="CreatureVisualCatalog"/> → <see cref="AlphaActorDefinition.ActionTimings"/>).
    /// Gameplay owns the resulting phases; the visual layer only plays them. Missing cache, species or action →
    /// no timing (the combat system then uses its PLACEHOLDER_NOT_ALPHA fallback and says so in the event).
    /// </summary>
    public sealed class AlphaActionTimingSource : IActionTimingSource
    {
        private readonly AlphaActorLibrary _library;
        private readonly Dictionary<(int Kind, int Action), ActionTiming?> _cache = new Dictionary<(int, int), ActionTiming?>();

        public AlphaActionTimingSource(AlphaActorLibrary library)
        {
            _library = library;
        }

        public string LastProblem { get; private set; } = string.Empty;

        public bool TryGet(KindId kind, ActionId action, out ActionTiming timing)
        {
            if (!_cache.TryGetValue((kind.Value, action.Value), out ActionTiming? cached))
            {
                cached = Lookup(kind, action);
                _cache[(kind.Value, action.Value)] = cached;
            }

            timing = cached ?? default;
            return cached.HasValue;
        }

        /// <summary>
        /// Alpha attack actions of a species: actions of its S05 library that carry a windup (attack timing),
        /// in ascending code order. Empty when the species has no staged action library.
        /// </summary>
        public int[] AttackActionsOf(KindId kind)
        {
            if (_library == null || !CreatureVisualCatalog.TryResolve(kind.Value, _library, out string actorId, out _)
                || !_library.TryGet(actorId, out AlphaActorDefinition definition))
            {
                return System.Array.Empty<int>();
            }

            var codes = new List<int>();
            foreach (KeyValuePair<int, AlphaActionTiming> pair in definition.ActionTimings)
            {
                if (pair.Value.WindupMs > 0f && pair.Value.ActionEndMs > 0f)
                {
                    codes.Add(pair.Key);
                }
            }

            codes.Sort();
            return codes.ToArray();
        }

        private ActionTiming? Lookup(KindId kind, ActionId action)
        {
            if (_library == null)
            {
                LastProblem = "NO_ACTOR_LIBRARY";
                return null;
            }

            if (!CreatureVisualCatalog.TryResolve(kind.Value, _library, out string actorId, out string reason))
            {
                LastProblem = reason;
                return null;
            }

            if (!_library.TryGet(actorId, out AlphaActorDefinition definition)
                || !definition.ActionTimings.TryGetValue(action.Value, out AlphaActionTiming alpha))
            {
                LastProblem = $"ACTION_TIMING_MISSING actor={actorId} action={action.Value}";
                return null;
            }

            if (alpha.WindupMs <= 0f || alpha.ActionEndMs <= 0f)
            {
                LastProblem = $"ACTION_TIMING_INCOMPLETE actor={actorId} action={action.Value}";
                return null;
            }

            return ActionTiming.FromAlpha(alpha.WindupMs, alpha.ActionEndMs, alpha.RecoveryStartMs);
        }
    }
}
