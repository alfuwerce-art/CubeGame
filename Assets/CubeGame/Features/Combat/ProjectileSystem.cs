using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;

namespace CubeGame.Features.Combat
{
    /// <summary>What the terrain query returns for one block (projectile collision).</summary>
    public enum TerrainCell
    {
        Air = 0,
        Solid = 1,

        /// <summary>The block is outside the streamed world (the projectile is removed — CUBEGAME_CONTRACT).</summary>
        NotLoaded = 2,
    }

    /// <summary>Why a projectile left the game.</summary>
    public enum ProjectileEndReason
    {
        HitTarget = 0,
        HitTerrain = 1,
        Expired = 2,
        Caught = 3,
        OwnerGone = 4,
        NotLoaded = 5,
        Cleared = 6,
    }

    /// <summary>
    /// Numbers of the projectile runtime. Values marked ALPHA were read from the bytes of <c>Cube.exe</c>
    /// (<c>GameController_updateSubsystems</c> 0x60C510; capstone, see M0_F2B report §Alpha provenance).
    /// Units: one Alpha speed unit = one block per second (C2), Alpha Z (up) = Unity Y.
    /// </summary>
    public static class ProjectileRules
    {
        /// <summary>ALPHA 0x617200: arrow/boomerang velocity = normalize(aim +0x160) · 100.</summary>
        public const double ArrowSpeed = 100.0;

        /// <summary>ALPHA 0x617200 (same push as the arrow, type 2).</summary>
        public const double BoomerangSpeed = 100.0;

        /// <summary>ALPHA 0x617CC4: wand magic velocity = normalize(aim) · 50.</summary>
        public const double MagicSpeed = 50.0;

        /// <summary>ALPHA 0x617CA7: ·100 when the off-hand item subtype is 0x0C (bracelet).</summary>
        public const double MagicSpeedWithBracelet = 100.0;

        /// <summary>ALPHA 0x620C6F: <c>vz −= dt · 30 · 0.25</c> for every type except magic (1) and boomerang (2).</summary>
        public const double Gravity = 7.5;

        /// <summary>ALPHA 0x620CD3: a non-boomerang projectile older than 5000 ms is removed.</summary>
        public const long LifetimeMs = 5000;

        /// <summary>ALPHA 0x617231: projectile size (+0x4C) of the arrow and boomerang = 0.5.</summary>
        public const double ArrowScale = 0.5;

        /// <summary>ALPHA 0x617CEA: magic size = 0.5 + 4 · charge (+0x144); CubeGame has no charge, so 0.5.</summary>
        public const double MagicScale = 0.5;

        /// <summary>ALPHA 0x620B0C…0x620BB3: boomerang flies out until 800 ms, then velocity = (owner − pos) · 10.</summary>
        public const long BoomerangReturnAtMs = 800;

        /// <summary>ALPHA 0x620B0C: from 1100 ms the boomerang position is pulled to the owner over 100 ms.</summary>
        public const long BoomerangPullAtMs = 1100;

        /// <summary>ALPHA 0x620B01: older than 1200 ms the boomerang is back (removed).</summary>
        public const long BoomerangCatchAtMs = 1200;

        /// <summary>ALPHA 0x620BBA: return velocity gain.</summary>
        public const double BoomerangReturnGain = 10.0;

        /// <summary>
        /// CUBEGAME_CONTRACT: integration sub-step length in blocks. Alpha moves a projectile once per rendered
        /// frame and casts a voxel ray along it (0x622293); CubeGame ticks at 30 Hz, so the flight is split into
        /// steps no longer than this to keep hits independent of the tick rate.
        /// </summary>
        public const double MaxStepBlocks = 0.25;

        /// <summary>CUBEGAME_CONTRACT: forward offset of the staff hit volume (Alpha offset at 0x6142A0 is not decoded).</summary>
        public const double StaffBurstForward = 1.5;

        /// <summary>CUBEGAME_CONTRACT: half size added to the target box for the staff hit volume.</summary>
        public const double StaffBurstScale = 1.0;

        /// <summary>CUBEGAME_CONTRACT: the staff hit volume exists for one strike window.</summary>
        public const long StaffBurstLifetimeMs = 100;

        /// <summary>ALPHA 0x615FBB: <c>push 200.0</c> — length limit of <c>World_raycastVoxels</c> for the hitscan actions.</summary>
        public const double HitscanRange = 200.0;

        /// <summary>ALPHA 0x615FE9 / [0x745E74] = 10.0: the bomb (action 0x68) uses this distance instead of the voxel ray.</summary>
        public const double BombRange = 10.0;

        /// <summary>
        /// PARTIAL (decompilation 98638…98720): a creature on the ray is hit when its distance from the ray is below
        /// size / 2 + 1 on every axis (the exact horizontal test, <c>AStar_coordExceedsBound</c>, is not decoded).
        /// </summary>
        public const double HitscanTolerance = 1.0;

        /// <summary>CUBEGAME_CONTRACT: voxel ray step in blocks (Alpha's <c>World_raycastVoxels</c> 0x5A35D0 is not ported).</summary>
        public const double RayStepBlocks = 0.1;

        /// <summary>
        /// Action ids whose muzzle/trail path is byte-verified (gates 0x4B630D / 0x4B467B; DeepSeek evidence 08ae4e3):
        /// only these draw a trail. Same list as <c>AlphaCombatRuntimeData.MuzzleAttackIds</c> (checked by a test).
        /// </summary>
        public static readonly int[] TrailActionIds = { 0x1C, 0x25, 0x26, 0x2B, 0x2C, 0x5E, 0x5F };

        public static bool HasTrail(int action) => Array.IndexOf(TrailActionIds, action) >= 0;

        public static double SpeedOf(ProjectileKind kind, bool braceletOffHand)
        {
            switch (kind)
            {
                case ProjectileKind.Arrow: return ArrowSpeed;
                case ProjectileKind.Boomerang: return BoomerangSpeed;
                case ProjectileKind.Magic: return braceletOffHand ? MagicSpeedWithBracelet : MagicSpeed;
                default: return 0.0;
            }
        }

        public static double GravityOf(ProjectileKind kind) => kind == ProjectileKind.Arrow ? Gravity : 0.0;

        public static double ScaleOf(ProjectileKind kind)
        {
            switch (kind)
            {
                case ProjectileKind.Magic: return MagicScale;
                case ProjectileKind.StaffBurst: return StaffBurstScale;
                default: return ArrowScale;
            }
        }

        public static long LifetimeOf(ProjectileKind kind) =>
            kind == ProjectileKind.StaffBurst ? StaffBurstLifetimeMs : kind == ProjectileKind.Boomerang ? BoomerangCatchAtMs : LifetimeMs;

        /// <summary>Removed after the first creature hit (ALPHA 0x621A1F: every type except the boomerang).</summary>
        public static bool RemovedOnHit(ProjectileKind kind) => kind == ProjectileKind.Arrow || kind == ProjectileKind.Magic;

        /// <summary>Terrain stops the projectile (ALPHA 0x6220D1/0x622293); a boomerang stops (v = 0) instead of vanishing.</summary>
        public static bool CollidesWithTerrain(ProjectileKind kind) => kind != ProjectileKind.StaffBurst;

        public static string Provenance(ProjectileKind kind) => kind == ProjectileKind.StaffBurst
            ? "CUBEGAME_CONTRACT volume (Alpha 0x1E is a forward melee-group volume; offset/size not decoded)"
            : kind == ProjectileKind.Boomerang
                ? "ALPHA_CODE_STATIC speed/return/catch; out-phase steering PARTIAL"
                : "ALPHA_CODE_STATIC speed/gravity/lifetime/size/removal";
    }

    /// <summary>One flying projectile (or the staff hit volume). Game-space blocks; the hero's floating origin never touches it.</summary>
    public sealed class Projectile
    {
        internal readonly HashSet<StableEntityId> HitTargets = new HashSet<StableEntityId>();

        internal Projectile(long id, StableEntityId owner, ProjectileKind kind, ActionId action, long attackSerial, long spawnMs)
        {
            Id = id;
            Owner = owner;
            Kind = kind;
            Action = action;
            AttackSerial = attackSerial;
            SpawnTimeMs = spawnMs;
            LastUpdateMs = spawnMs;
        }

        public long Id { get; }

        public StableEntityId Owner { get; }

        public ProjectileKind Kind { get; }

        public ActionId Action { get; }

        /// <summary>Serial of the attack that released it (stable per swing; also seeds the damage roll).</summary>
        public long AttackSerial { get; }

        public long SpawnTimeMs { get; }

        public double X { get; internal set; }

        public double Y { get; internal set; }

        public double Z { get; internal set; }

        public double Vx { get; internal set; }

        public double Vy { get; internal set; }

        public double Vz { get; internal set; }

        public double Gravity { get; internal set; }

        /// <summary>Alpha projectile size (+0x4C): added to the half size of the target box on every axis.</summary>
        public double Scale { get; internal set; }

        public long AgeMs { get; internal set; }

        public long LifetimeMs { get; internal set; }

        public bool Removed { get; internal set; }

        public int HitCount => HitTargets.Count;

        internal long LastUpdateMs { get; set; }

        public WorldPosition Position => new WorldPosition(X, Y, Z);

        public override string ToString() => $"projectile {Id} {Kind} owner {Owner} at ({X:F2},{Y:F2},{Z:F2}) age {AgeMs}";
    }

    /// <summary>
    /// Gameplay owner of projectiles (M0-F2B): spawn at the strike moment → step on the game clock → hit a creature
    /// box or terrain → <see cref="CombatSystem.ResolveHit"/> → removed exactly once. Runtime-only (never saved).
    /// Created by <see cref="CombatSystem"/>, which advances it on every update.
    /// </summary>
    public sealed class ProjectileSystem
    {
        private readonly CombatSystem _combat;
        private readonly GameEventBus _events;
        private readonly List<Projectile> _active = new List<Projectile>();
        private long _serial;

        internal ProjectileSystem(CombatSystem combat, GameEventBus events)
        {
            _combat = combat;
            _events = events;
        }

        /// <summary>Terrain query in blocks (null: no terrain collision). Set by the world owner.</summary>
        public Func<int, int, int, TerrainCell> TerrainAt { get; set; }

        public IReadOnlyList<Projectile> Active => _active;

        public int Count => _active.Count;

        public long Spawned { get; private set; }

        public bool TryGet(long id, out Projectile projectile)
        {
            foreach (Projectile p in _active)
            {
                if (p.Id == id)
                {
                    projectile = p;
                    return true;
                }
            }

            projectile = null;
            return false;
        }

        /// <summary>
        /// Releases the projectile of an attack from the attacker's body centre along its aim. The staff volume is put
        /// <see cref="ProjectileRules.StaffBurstForward"/> blocks in front of the attacker and does not move.
        /// </summary>
        public Projectile Launch(Combatant owner, ProjectileKind kind, ActionId action, long attackSerial, long nowMs)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            owner.GetAim(out double ax, out double ay, out double az);
            var p = new Projectile(++_serial, owner.Id, kind, action, attackSerial, nowMs)
            {
                X = owner.Position.X,
                Y = owner.Position.Y + owner.HitHeight * 0.5,
                Z = owner.Position.Z,
                Gravity = ProjectileRules.GravityOf(kind),
                Scale = ProjectileRules.ScaleOf(kind),
                LifetimeMs = ProjectileRules.LifetimeOf(kind),
            };

            if (kind == ProjectileKind.StaffBurst)
            {
                p.X += ax * ProjectileRules.StaffBurstForward;
                p.Y += ay * ProjectileRules.StaffBurstForward;
                p.Z += az * ProjectileRules.StaffBurstForward;
            }
            else
            {
                double speed = ProjectileRules.SpeedOf(kind, owner.BraceletOffHand);
                p.Vx = ax * speed;
                p.Vy = ay * speed;
                p.Vz = az * speed;
            }

            _active.Add(p);
            Spawned++;
            _events.Publish(new ProjectileSpawned
            {
                TimeMs = nowMs,
                Projectile = p.Id,
                Owner = owner.Id,
                Kind = (int)kind,
                Action = action,
                AttackSerial = attackSerial,
                Position = p.Position,
                VelocityX = p.Vx,
                VelocityY = p.Vy,
                VelocityZ = p.Vz,
                Scale = p.Scale,
            });

            // A volume hits what is inside it at once (the Alpha strike is the same frame).
            if (kind == ProjectileKind.StaffBurst)
            {
                TestCreatures(p, nowMs);
            }

            return p;
        }

        /// <summary>
        /// Hitscan (ALPHA 0x615F62…0x615FDD): aim → body centre → voxel ray up to <see cref="ProjectileRules.HitscanRange"/>
        /// stopped by the first solid block (the bomb 0x68: fixed <see cref="ProjectileRules.BombRange"/>, no ray) → every
        /// hostile living creature along the ray is hit once through <see cref="CombatSystem.ResolveHit"/>. The owner is
        /// never hit. Returns the number of hits.
        /// </summary>
        public int FireRay(Combatant owner, ActionId action, long attackSerial, long nowMs)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            owner.GetAim(out double dx, out double dy, out double dz);
            double ox = owner.Position.X, oy = owner.Position.Y + owner.HitHeight * 0.5, oz = owner.Position.Z;
            bool bomb = action.Value == AlphaAttackRoute.BombAction;
            double length = bomb ? ProjectileRules.BombRange : ProjectileRules.HitscanRange;
            bool wall = false;
            if (!bomb && TerrainAt != null)
            {
                for (double t = ProjectileRules.RayStepBlocks; t <= length; t += ProjectileRules.RayStepBlocks)
                {
                    TerrainCell cell = TerrainAt((int)Math.Floor(ox + dx * t), (int)Math.Floor(oy + dy * t), (int)Math.Floor(oz + dz * t));
                    if (cell != TerrainCell.Air)
                    {
                        length = t;
                        wall = cell == TerrainCell.Solid;
                        break;
                    }
                }
            }

            int hits = 0;
            foreach (Combatant target in new List<Combatant>(_combat.Combatants))
            {
                if (target.Id.Equals(owner.Id) || target.IsDead || target.Hostile == owner.Hostile)
                {
                    continue;
                }

                double cx = target.Position.X, cy = target.Position.Y + target.HitHeight * 0.5, cz = target.Position.Z;
                double along = (cx - ox) * dx + (cy - oy) * dy + (cz - oz) * dz;
                if (along < 0.0 || along > length)
                {
                    continue;
                }

                double px = ox + dx * along, py = oy + dy * along, pz = oz + dz * along;
                if (Math.Abs(cx - px) < target.HitWidth * 0.5 + ProjectileRules.HitscanTolerance
                    && Math.Abs(cz - pz) < target.HitDepth * 0.5 + ProjectileRules.HitscanTolerance
                    && Math.Abs(cy - py) < target.HitHeight * 0.5 + ProjectileRules.HitscanTolerance)
                {
                    _combat.ResolveHit(owner.Id, target.Id, nowMs, attackSerial);
                    hits++;
                }
            }

            RaysFired++;
            _events.Publish(new HitscanFired
            {
                TimeMs = nowMs,
                Owner = owner.Id,
                Action = action,
                AttackSerial = attackSerial,
                Origin = new WorldPosition(ox, oy, oz),
                End = new WorldPosition(ox + dx * length, oy + dy * length, oz + dz * length),
                HitWall = wall,
                Hits = hits,
                Trail = ProjectileRules.HasTrail(action.Value),
            });
            return hits;
        }

        public long RaysFired { get; private set; }

        /// <summary>Advances every projectile to game time <paramref name="nowMs"/>.</summary>
        public void Update(long nowMs)
        {
            if (_active.Count == 0)
            {
                return;
            }

            // Snapshot: hits publish events whose handlers may launch or clear projectiles.
            foreach (Projectile p in _active.ToArray())
            {
                if (p.Removed)
                {
                    continue;
                }

                long elapsed = nowMs - p.LastUpdateMs;
                if (elapsed <= 0)
                {
                    continue;
                }

                p.LastUpdateMs = nowMs;
                Advance(p, elapsed, nowMs);
            }

            _active.RemoveAll(p => p.Removed);
        }

        /// <summary>Removes every projectile (world exit, load, reload). Each one reports <see cref="ProjectileEndReason.Cleared"/> once.</summary>
        public void Clear(long nowMs)
        {
            foreach (Projectile p in _active.ToArray())
            {
                Remove(p, ProjectileEndReason.Cleared, StableEntityId.None, nowMs);
            }

            _active.Clear();
        }

        private void Advance(Projectile p, long elapsedMs, long nowMs)
        {
            if (!_combat.TryGet(p.Owner, out Combatant owner))
            {
                // CUBEGAME_CONTRACT: Alpha reads the owner for damage and the boomerang return (0x620AE9 → removal).
                Remove(p, ProjectileEndReason.OwnerGone, StableEntityId.None, nowMs);
                return;
            }

            double dt = elapsedMs / 1000.0;
            double speed = Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy + p.Vz * p.Vz) + p.Gravity * dt;
            int steps = Math.Max(1, (int)Math.Ceiling(speed * dt / ProjectileRules.MaxStepBlocks));
            steps = Math.Min(steps, 2000);
            long msLeft = elapsedMs;
            for (int i = 0; i < steps && !p.Removed; i++)
            {
                long ms = msLeft / (steps - i);
                msLeft -= ms;
                Step(p, owner, ms, nowMs);
            }
        }

        private void Step(Projectile p, Combatant owner, long ms, long nowMs)
        {
            double dt = ms / 1000.0;
            if (p.Kind == ProjectileKind.Boomerang)
            {
                SteerBoomerang(p, owner);
            }

            p.Vy -= p.Gravity * dt;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
            p.Z += p.Vz * dt;
            p.AgeMs += ms;

            if (p.Kind == ProjectileKind.Boomerang && p.AgeMs > ProjectileRules.BoomerangCatchAtMs)
            {
                Remove(p, ProjectileEndReason.Caught, StableEntityId.None, nowMs);
                return;
            }

            if (p.Kind != ProjectileKind.Boomerang && p.AgeMs > p.LifetimeMs)
            {
                Remove(p, ProjectileEndReason.Expired, StableEntityId.None, nowMs);
                return;
            }

            TestCreatures(p, nowMs);
            if (p.Removed || !ProjectileRules.CollidesWithTerrain(p.Kind) || TerrainAt == null)
            {
                return;
            }

            TerrainCell cell = TerrainAt((int)Math.Floor(p.X), (int)Math.Floor(p.Y), (int)Math.Floor(p.Z));
            if (cell == TerrainCell.NotLoaded)
            {
                Remove(p, ProjectileEndReason.NotLoaded, StableEntityId.None, nowMs);
            }
            else if (cell == TerrainCell.Solid)
            {
                if (p.Kind == ProjectileKind.Boomerang)
                {
                    // ALPHA 0x622382: the boomerang stops in the wall (v = 0) and keeps its return timer.
                    p.X -= p.Vx * dt;
                    p.Y -= p.Vy * dt;
                    p.Z -= p.Vz * dt;
                    p.Vx = p.Vy = p.Vz = 0.0;
                }
                else
                {
                    Remove(p, ProjectileEndReason.HitTerrain, StableEntityId.None, nowMs);
                }
            }
        }

        private static void SteerBoomerang(Projectile p, Combatant owner)
        {
            double ox = owner.Position.X, oy = owner.Position.Y + owner.HitHeight * 0.5, oz = owner.Position.Z;
            if (p.AgeMs > ProjectileRules.BoomerangPullAtMs)
            {
                // ALPHA 0x620B17: position pulled toward the owner by (age − 1100) / 100.
                double t = Math.Min(1.0, (p.AgeMs - ProjectileRules.BoomerangPullAtMs) / 100.0);
                p.X += (ox - p.X) * t;
                p.Y += (oy - p.Y) * t;
                p.Z += (oz - p.Z) * t;
                p.Vx = p.Vy = p.Vz = 0.0;
            }
            else if (p.AgeMs > ProjectileRules.BoomerangReturnAtMs)
            {
                p.Vx = (ox - p.X) * ProjectileRules.BoomerangReturnGain;
                p.Vy = (oy - p.Y) * ProjectileRules.BoomerangReturnGain;
                p.Vz = (oz - p.Z) * ProjectileRules.BoomerangReturnGain;
            }

            // Out phase: Alpha relaxes the velocity toward the owner's aim point (0x620BF6, lerp 0.05·ms) — PARTIAL,
            // CubeGame keeps the release velocity.
        }

        /// <summary>
        /// ALPHA 0x621590…0x621912: a hostile, living creature is hit when on every axis
        /// |projectile − creature centre| &lt; creature size / 2 + projectile size. CUBEGAME_CONTRACT: the creature centre
        /// is its feet position + half its height; each target is hit at most once per projectile.
        /// </summary>
        private void TestCreatures(Projectile p, long nowMs)
        {
            if (!_combat.TryGet(p.Owner, out Combatant owner))
            {
                return;
            }

            foreach (Combatant target in new List<Combatant>(_combat.Combatants))
            {
                if (p.Removed)
                {
                    return;
                }

                if (target.Id.Equals(p.Owner) || target.IsDead || target.Hostile == owner.Hostile || p.HitTargets.Contains(target.Id))
                {
                    continue;
                }

                if (!Overlaps(p, target))
                {
                    continue;
                }

                p.HitTargets.Add(target.Id);
                _combat.ResolveHit(owner.Id, target.Id, nowMs, p.AttackSerial);
                if (ProjectileRules.RemovedOnHit(p.Kind))
                {
                    Remove(p, ProjectileEndReason.HitTarget, target.Id, nowMs);
                }
            }
        }

        /// <summary>The Alpha box test of one projectile against one creature (public for tests).</summary>
        public static bool Overlaps(Projectile p, Combatant target)
        {
            double cy = target.Position.Y + target.HitHeight * 0.5;
            return Math.Abs(p.X - target.Position.X) < target.HitWidth * 0.5 + p.Scale
                && Math.Abs(p.Z - target.Position.Z) < target.HitDepth * 0.5 + p.Scale
                && Math.Abs(p.Y - cy) < target.HitHeight * 0.5 + p.Scale;
        }

        private void Remove(Projectile p, ProjectileEndReason reason, StableEntityId target, long nowMs)
        {
            if (p.Removed)
            {
                return;
            }

            p.Removed = true;
            _events.Publish(new ProjectileRemoved
            {
                TimeMs = nowMs,
                Projectile = p.Id,
                Owner = p.Owner,
                Kind = (int)p.Kind,
                Reason = (int)reason,
                Target = target,
                Position = p.Position,
            });
        }
    }
}
