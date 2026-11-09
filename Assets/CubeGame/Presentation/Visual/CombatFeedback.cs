// CubeGame.Presentation.Visual — combat feedback curves of the visual runtime (D4/D5 step).
//
// Alpha's own hit/death presentation is NOT restored: hit flashes and death animations are separate renderers
// and effects (R-3/R-8, blocked research), and the corpse rule was never observed in the shipped game. What this
// file provides is an explicit CUBEGAME_CONTRACT: a short flash on a hit and a fall-and-sink corpse, so the
// game shows something honest while the Alpha presentation stays unknown. Pure functions — tests call them.
using System;

namespace CubeGame.Presentation.Visual
{
    /// <summary>Time-based feedback values; nothing here claims Alpha parity.</summary>
    public static class CombatFeedback
    {
        public const string Provenance =
            "CUBEGAME_CONTRACT: Alpha hit/death presentation not restored (R-3/R-8 blocked); flash and corpse are CubeGame's";

        /// <summary>Duration of the hit flash (CubeGame choice).</summary>
        public const float FlashSeconds = 0.12f;

        /// <summary>Time a body takes to sink into the ground after death (CubeGame choice).</summary>
        public const float DeathSinkSeconds = 2.0f;

        /// <summary>Depth the body sinks to (blocks), so a corpse does not block the view forever.</summary>
        public const float DeathSinkBlocks = 0.5f;

        /// <summary>Corpse tilt: the body lies on its side (90° around the forward axis).</summary>
        public const float DeathTiltDegrees = 90f;

        /// <summary>Flash strength 1 → 0 over <see cref="FlashSeconds"/>; 0 when the hit is older.</summary>
        public static float FlashIntensity(long sinceHitMs)
        {
            if (sinceHitMs <= 0) return 1f;
            double seconds = sinceHitMs / 1000.0;
            if (seconds >= FlashSeconds) return 0f;
            return (float)(1.0 - seconds / FlashSeconds);
        }

        /// <summary>Sink depth of a corpse after <paramref name="sinceDeathMs"/> milliseconds.</summary>
        public static float DeathSink(long sinceDeathMs)
        {
            if (sinceDeathMs <= 0) return 0f;
            double seconds = sinceDeathMs / 1000.0;
            if (seconds >= DeathSinkSeconds) return DeathSinkBlocks;
            return (float)(DeathSinkBlocks * seconds / DeathSinkSeconds);
        }

        /// <summary>True once the corpse has fully sunk (the visual may be hidden).</summary>
        public static bool CorpseFinished(long sinceDeathMs) => DeathSink(sinceDeathMs) >= DeathSinkBlocks - 1e-6f;

        /// <summary>Sanity check of the constants (tests and diagnostics).</summary>
        public static void Validate()
        {
            if (FlashSeconds <= 0f) throw new InvalidOperationException("FLASH_SECONDS");
            if (DeathSinkSeconds <= 0f) throw new InvalidOperationException("DEATH_SINK_SECONDS");
            if (DeathSinkBlocks <= 0f) throw new InvalidOperationException("DEATH_SINK_BLOCKS");
        }
    }
}
