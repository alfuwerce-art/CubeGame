// CubeGame.Presentation.Visual — flying projectiles of the visual runtime (D5 step, CubeGame contract).
//
// Alpha's projectile/effect renderers are NOT ported: CombatBehavior_spawnProjectileHit (0x596D30) and
// Creature_spawnDeathEffects (0x59B180) build their own meshes/particles, and R-3/R-8 stay BLOCKED (they are not
// drawModel poses). What is real Alpha data here is the MODEL: an arrow (weapon subtype 9, "arrow.cub"),
// a boomerang (8, "wood-boomerang1.cub") and stones reuse the item layer models that are already staged.
//
// Flight itself is a CubeGame contract: straight motion with optional gravity, a lifetime and no collision
// (hits are resolved by gameplay through CombatSystem.ResolveHit). Nothing here claims Alpha ballistics.
//
// M0-F2B: NOT the runtime path. Gameplay projectiles live in Features/Combat/ProjectileSystem (numbers read from the
// Cube.exe bytes); only ModelOf (item model names) is used, by Gameplay/ProjectileVisuals. Launch/Step stay a
// DATA_IMPLEMENTED contract library with no game callers.
using System;
using CubeGame.Content;

namespace CubeGame.Presentation.Visual
{
    /// <summary>Motion state of one projectile (pure; the sink applies it to a Transform).</summary>
    public struct ProjectileState
    {
        public double X, Y, Z;
        public double Vx, Vy, Vz;
        public double Gravity;
        public long AgeMs;
        public long LifetimeMs;

        public bool Expired => LifetimeMs > 0 && AgeMs >= LifetimeMs;
    }

    /// <summary>Projectile models and integration, both explicitly CubeGame's where Alpha has no data.</summary>
    public static class Projectiles
    {
        public const string Provenance =
            "MODEL=ALPHA_DATA (item layer); flight/lifetime/collision=CUBEGAME_CONTRACT (Alpha projectile renderers R-3 not ported)";

        /// <summary>Alpha weapon subtype of an arrow (item layer model "arrow.cub").</summary>
        public const int ArrowSubtype = 9;

        /// <summary>Alpha weapon subtype of a boomerang.</summary>
        public const int BoomerangSubtype = 8;

        /// <summary>Default lifetime of a projectile when the caller gives none (CubeGame choice).</summary>
        public const int DefaultLifetimeMs = 4000;

        /// <summary>Alpha model name of a projectile item subtype, or null when the subtype has no staged model.</summary>
        public static string ModelOf(int weaponSubtype)
        {
            if (!AlphaItemRules.TryWeaponSubtype(weaponSubtype, out AlphaWeaponSubtypeDef def)) return null;
            return string.IsNullOrEmpty(def.DefaultModel) ? null : def.DefaultModel;
        }

        public static bool IsProjectileSubtype(int weaponSubtype) =>
            weaponSubtype == ArrowSubtype || weaponSubtype == BoomerangSubtype;

        /// <summary>Advances one projectile by <paramref name="elapsedMs"/> (semi-implicit Euler, blocks).</summary>
        public static void Step(ref ProjectileState state, int elapsedMs)
        {
            if (elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs));
            double dt = elapsedMs / 1000.0;
            state.Vy -= state.Gravity * dt;
            state.X += state.Vx * dt;
            state.Y += state.Vy * dt;
            state.Z += state.Vz * dt;
            state.AgeMs += elapsedMs;
        }

        /// <summary>Creates a projectile aimed at a target point with a flat speed (no ballistics claim).</summary>
        public static ProjectileState Launch(double x, double y, double z, double targetX, double targetY, double targetZ,
            double speed, double gravity = 0.0, int lifetimeMs = DefaultLifetimeMs)
        {
            if (speed <= 0.0) throw new ArgumentOutOfRangeException(nameof(speed));
            double dx = targetX - x, dy = targetY - y, dz = targetZ - z;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length < 1e-9) throw new ArgumentException("target equals the origin", nameof(targetX));
            return new ProjectileState
            {
                X = x, Y = y, Z = z,
                Vx = dx / length * speed, Vy = dy / length * speed, Vz = dz / length * speed,
                Gravity = gravity,
                AgeMs = 0,
                LifetimeMs = lifetimeMs,
            };
        }
    }
}
