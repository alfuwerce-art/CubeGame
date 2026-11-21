using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Combat
{
    /// <summary>
    /// Gameplay phases of one attack action, in game milliseconds from the action start.
    /// Windup [0, StrikeAtMs) → Strike [StrikeAtMs, StrikeEndMs) → Recover [StrikeEndMs, EndMs) → Finished.
    /// The hit is resolved once, when the Strike phase begins.
    /// </summary>
    public readonly struct ActionTiming
    {
        public ActionTiming(long strikeAtMs, long strikeEndMs, long endMs, string source)
        {
            if (strikeAtMs < 0 || strikeEndMs < strikeAtMs || endMs < strikeEndMs)
            {
                throw new ArgumentOutOfRangeException(nameof(strikeAtMs), $"invalid phases {strikeAtMs}/{strikeEndMs}/{endMs}");
            }

            StrikeAtMs = strikeAtMs;
            StrikeEndMs = strikeEndMs;
            EndMs = endMs;
            Source = source ?? string.Empty;
        }

        public long StrikeAtMs { get; }

        public long StrikeEndMs { get; }

        public long EndMs { get; }

        /// <summary>Provenance: EMULATED_ALPHA_CODE (from S05 action clips) or PLACEHOLDER_NOT_ALPHA.</summary>
        public string Source { get; }

        public long WindupMs => StrikeAtMs;

        public long StrikeMs => StrikeEndMs - StrikeAtMs;

        public long RecoverMs => EndMs - StrikeEndMs;

        /// <summary>
        /// Builds gameplay phases from Alpha action timings (S05 clip provenance, <c>combat_getAttackWindup</c>
        /// 0x43CAA0 / <c>combat_getAttackTiming</c> 0x43D1A0): windup → strike until action end → recover until
        /// the recovery state. Using the end of windup as the hit moment is a CubeGame decision
        /// (PLACEHOLDER_NOT_ALPHA, coverage row C-28/C-31): the exact Alpha hit frame is not restored.
        /// </summary>
        public static ActionTiming FromAlpha(double windupMs, double actionEndMs, double recoveryStartMs)
        {
            long windup = (long)Math.Round(windupMs);
            long end = Math.Max(windup, (long)Math.Round(actionEndMs));
            long recover = Math.Max(end, (long)Math.Round(recoveryStartMs));
            return new ActionTiming(windup, end, recover, "EMULATED_ALPHA_CODE");
        }

        public IReadOnlyList<long> PhaseDurations => new[] { WindupMs, StrikeMs, RecoverMs };

        public override string ToString() => $"windup {WindupMs} strike {StrikeMs} recover {RecoverMs} ({Source})";
    }

    /// <summary>Supplies attack phases per species and action (content/visual data owned by DeepSeek, read through this seam).</summary>
    public interface IActionTimingSource
    {
        bool TryGet(KindId kind, ActionId action, out ActionTiming timing);
    }

    /// <summary>Fixed table, used by tests and as a fallback when the Alpha content cache is unavailable.</summary>
    public sealed class ActionTimingTable : IActionTimingSource
    {
        private readonly Dictionary<(int Kind, int Action), ActionTiming> _entries = new Dictionary<(int, int), ActionTiming>();

        public ActionTimingTable Add(KindId kind, ActionId action, ActionTiming timing)
        {
            _entries[(kind.Value, action.Value)] = timing;
            return this;
        }

        public bool TryGet(KindId kind, ActionId action, out ActionTiming timing)
        {
            return _entries.TryGetValue((kind.Value, action.Value), out timing);
        }
    }

    /// <summary>Tries several sources in order (e.g. Alpha clip timings, then the placeholder table).</summary>
    public sealed class ChainedActionTimingSource : IActionTimingSource
    {
        private readonly IActionTimingSource[] _sources;

        public ChainedActionTimingSource(params IActionTimingSource[] sources)
        {
            _sources = sources ?? Array.Empty<IActionTimingSource>();
        }

        public bool TryGet(KindId kind, ActionId action, out ActionTiming timing)
        {
            foreach (IActionTimingSource source in _sources)
            {
                if (source != null && source.TryGet(kind, action, out timing))
                {
                    return true;
                }
            }

            timing = default;
            return false;
        }
    }
}
