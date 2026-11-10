// CubeGame.Features.Creatures — life cycle of an Alpha creature template (D1 REVIEW-FIX).
//
// Alpha keeps one `cube::Spawn` template (0x10F0 bytes, ctor Cube.exe 0x5ADB20) per world creature and turns it
// into a live creature in the server tick (0x60C510). Every rule below is verified by exact bytes in
// tools/alpha_models/world/spawn_pipeline_evidence.py (ALPHA_CODE_STATIC):
//
//   template -> creature (0x60FBC1…0x60FC23): the optional time-of-day window (+0x40/+0x44) contains the world
//     time, no live creature carries the template id (+0x48), and the respawn timer (+0x38) is 0 OR the day of
//     death (+0x3C) is not the current day (+0x800160); then a player must stand inside the template radius (+0x08).
//   death (0x596C30): timer = 1 200 000 ms, day of death = current day; the creature knows its template by
//     zone (+0x1B0/+0x1B4) and index (+0x1B8).
//   tick (0x60DF9F): timer -= elapsed real milliseconds, for every template of every loaded zone.
//
// Read from the decompilation only (ALPHA_DECOMPILED, not byte-checked): a creature further than 512 from every
// player is removed (squared distance 262144.0).
// UNKNOWN: whether the timer survives a zone unload / a save; nothing here models that.
namespace CubeGame.Features.Creatures
{
    /// <summary>Recovered rules of the Alpha template → creature life cycle. Pure functions, no state.</summary>
    public static class AlphaSpawnLifecycle
    {
        public const string Status = "ALPHA_CODE_STATIC: Cube.exe 0x60FBC1-0x60FC23, 0x596C30, 0x60DF9F (byte-verified)";

        /// <summary>Template +0x08 as written by the constructor (0x5ADB29); some sites overwrite it (150, 512).</summary>
        public const float DefaultActivationRadius = 200f;

        /// <summary>ALPHA_DECOMPILED: removal distance from the nearest player (sqrt of 262144.0).</summary>
        public const float DespawnDistance = 512f;

        /// <summary>Written to template +0x38 when its creature dies (0x596C30).</summary>
        public const int RespawnTimerMs = 1200000;

        /// <summary>Length of the world day in world-time milliseconds (+0x80015C wraps at this value).</summary>
        public const int DayMs = 86400000;

        /// <summary>True when the template has a time-of-day window (start != end, 0x60FBC4).</summary>
        public static bool HasTimeWindow(int windowStartMs, int windowEndMs) => windowStartMs != windowEndMs;

        /// <summary>Time-of-day test of 0x60FBD4/0x60FBE8: both ends inclusive.</summary>
        public static bool InTimeWindow(int windowStartMs, int windowEndMs, int worldTimeMs) =>
            !HasTimeWindow(windowStartMs, windowEndMs) || (worldTimeMs >= windowStartMs && worldTimeMs <= windowEndMs);

        /// <summary>Respawn test of 0x60FC08…0x60FC23: the timer ran out, or the day changed since the death.</summary>
        public static bool RespawnAllowed(int respawnTimerMs, int dayOfDeath, int currentDay) =>
            respawnTimerMs <= 0 || dayOfDeath != currentDay;

        /// <summary>Whole condition under which Alpha creates the creature of a template this tick.</summary>
        public static bool ShouldCreate(bool hasLiveCreature, int respawnTimerMs, int dayOfDeath, int currentDay,
            int windowStartMs, int windowEndMs, int worldTimeMs, double nearestPlayerDistance, float activationRadius)
        {
            if (!InTimeWindow(windowStartMs, windowEndMs, worldTimeMs)) return false;
            if (hasLiveCreature) return false;
            if (!RespawnAllowed(respawnTimerMs, dayOfDeath, currentDay)) return false;
            return nearestPlayerDistance < activationRadius;
        }

        /// <summary>Timer after one tick (0x60DF9F subtracts; 0x60FC0D clamps a negative value when it is read).</summary>
        public static int TickTimer(int respawnTimerMs, int elapsedMs)
        {
            int next = respawnTimerMs - elapsedMs;
            return next < 0 ? 0 : next;
        }

        /// <summary>ALPHA_DECOMPILED: a live creature this far from every player is removed.</summary>
        public static bool ShouldDespawn(double nearestPlayerDistance) => nearestPlayerDistance >= DespawnDistance;
    }
}
