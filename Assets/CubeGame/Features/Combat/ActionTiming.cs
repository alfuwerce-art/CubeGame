using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Content;

namespace CubeGame.Features.Combat
{
    /// <summary>
    /// Gameplay phases of one attack action, in game milliseconds from the action start.
    /// Windup [0, StrikeAtMs) → Strike [StrikeAtMs, StrikeEndMs) → Recover [StrikeEndMs, EndMs) → Finished.
    /// The hit is resolved once, when the Strike phase begins.
    /// </summary>
    public readonly struct ActionTiming
    {
        public ActionTiming(long strikeAtMs, long strikeEndMs, long endMs, string source,
            long repeatMs = 0, long? nativeWindupMs = null)
        {
            if (strikeAtMs < 0 || strikeEndMs < strikeAtMs || endMs < strikeEndMs || repeatMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(strikeAtMs), $"invalid phases {strikeAtMs}/{strikeEndMs}/{endMs}");
            }

            StrikeAtMs = strikeAtMs;
            StrikeEndMs = strikeEndMs;
            EndMs = endMs;
            Source = source ?? string.Empty;
            RepeatMs = repeatMs;
            NativeWindupMs = nativeWindupMs ?? strikeAtMs;
        }

        public long StrikeAtMs { get; }

        public long StrikeEndMs { get; }

        public long EndMs { get; }

        public long RepeatMs { get; }

        public long NativeWindupMs { get; }

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

    /// <summary>Timings from the Alpha windup, reload and end-of-action functions.</summary>
    public static class AlphaDispatcherTiming
    {
        public static bool TryGet(ActionId action, int mainHandSubtype, float hasteAndSpeed, out ActionTiming timing)
        {
            timing = default;
            AttackPath path = AlphaAttackRoute.ForAction(action.Value).Path;
            if (path == AttackPath.Unsupported || hasteAndSpeed <= 0 || float.IsInfinity(hasteAndSpeed) ||
                float.IsNaN(hasteAndSpeed))
            {
                return false;
            }

            if (!AlphaCombatRuntimeData.TryGetAttack(action.Value, out AlphaAttackCombatDef row))
            {
                return false;
            }

            long Scaled(float ms) => (long)(ms / hasteAndSpeed);
            long windup = row.CastMode == AlphaCombatValueMode.Fixed
                ? (long)row.Cast
                : Scaled(row.CastConditionWeaponSubtype == 0x0C && mainHandSubtype != 0x0C ? 1200 : row.Cast);
            long reload = row.RecoverMode == AlphaCombatValueMode.Fixed
                ? (long)row.Recover : Scaled(row.Recover);
            // 0x43D804: lock extension after the two separately truncated terms.
            long recovery;
            switch (action.Value)
            {
                case 0x0A: recovery = 600; break;
                case 0x17: recovery = Scaled(10); break;
                case 0x03: case 0x04: case 0x05: case 0x39: case 0x3A: recovery = Scaled(300); break;
                case 0x07: case 0x0E: case 0x12: case 0x41: case 0x42: recovery = Scaled(200); break;
                case 0x0F: recovery = Scaled(400); break;
                case 0x16: case 0x1A: case 0x1E: case 0x1F: case 0x20: case 0x21:
                case 0x25: case 0x26: case 0x27: case 0x28: case 0x29: case 0x2A:
                case 0x2B: case 0x2C: case 0x2D: case 0x2E: case 0x43: case 0x5E: case 0x5F:
                    recovery = Scaled(100);
                    break;
                case 0x44: case 0x45: recovery = Scaled(800); break;
                default: recovery = Scaled(500); break;
            }

            long strike = windup;
            long repeat = 0;
            long strikeEnd = windup + reload;
            if (path == AttackPath.Melee)
            {
                // 0x612B4B..0x612C05 and 0x6137F7. 0x44 has its own lower bound.
                strike += action.Value == 0x44 ? reload / 3 : Math.Max(0, reload / 2 - 100);
                strikeEnd = windup + Math.Max(1, reload / 2);
                if (action.Value == 0x05)
                {
                    strike = windup;
                    strikeEnd = windup + reload;
                    repeat = Math.Max(1, reload / 3);
                }
            }
            else if (path == AttackPath.AreaEffect || action.Value == 0x2D || action.Value == 0x2E)
            {
                // 0x612D85..0x612E84 (staff) and 0x617B1C..0x617B7E (wand variants).
                int divisor = path == AttackPath.Projectile ? 3 : action.Value == 0x1E || action.Value == 0x20 ? 6 : 12;
                repeat = Math.Max(1, reload / divisor);
            }

            if (action.Value == 0x1C || action.Value == 0x5F) repeat = 200; // 0x615EE0..0x615F56

            timing = new ActionTiming(strike, Math.Max(strike, strikeEnd), windup + reload + recovery,
                "ALPHA_CODE_STATIC (0x43CAA0 / 0x447310 / 0x43D804)", repeat, windup);
            return true;
        }
    }

    /// <summary>Supplies explicit phase overrides for fixtures and integrations.</summary>
    public interface IActionTimingSource
    {
        bool TryGet(KindId kind, ActionId action, out ActionTiming timing);
    }

    /// <summary>Fixed table, used to supply explicit timings.</summary>
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
