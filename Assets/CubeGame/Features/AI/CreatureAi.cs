// CubeGame.Features.AI — Alpha creature behaviour on the proven contracts (D86 M0-06C).
//
// The AI only decides state and asks the world for actions/movement; damage, HP, loot and resources
// belong to Core/Features (Claude). Numbers that Alpha evidence fixed are used verbatim (31 ms think
// clock, threat decay/reset/rescan, potion branches, dodge), engagement ranges that Alpha does not
// prove are marked CUBEGAME_CONTRACT/PLACEHOLDER_NOT_ALPHA.
using System;
using CubeGame.Features.Creatures;

namespace CubeGame.Features.AI
{
    public sealed class CreatureAi : ICreatureAi
    {
        /// <summary>CUBEGAME_CONTRACT: engagement distance used until Alpha's spawn/AI radius is recovered (N-19).</summary>
        public const double DefaultAggroRadius = 16.0;
        /// <summary>Melee distance used by the proven dodge rule (distance &lt; 2).</summary>
        public const double DefaultAttackRange = 2.0;
        /// <summary>ALPHA_CODE: path walk speed of Creature_findPathToTarget (0x5A7EB0), C2 scale of blocks/s.</summary>
        public const double DefaultWalkSpeed = CreatureMovement.WalkSpeed;
        /// <summary>ALPHA_CODE: run speed of the same scale (max 6 × 2, 0x619DB8); used when a behaviour runs.</summary>
        public const double DefaultRunSpeed = CreatureMovement.RunSpeed;
        public const int WanderRepickMs = 2000;
        /// <summary>CUBEGAME_CONTRACT: retry throttle for action requests (Alpha's own action lock is elsewhere).</summary>
        public const int ActionRetryMs = 250;

        public CreatureAiState State { get; private set; } = CreatureAiState.Idle;
        public string TargetId { get; private set; }
        public string LastReason { get; private set; } = "IDLE";
        public readonly AiClock Clock = new AiClock();
        public readonly ThreatTable Threat = new ThreatTable();
        public int ThinkTicks { get; private set; }
        public int LastRequestedAction { get; private set; } = -1;

        /// <summary>Attack id Alpha's C-29 table selected for the last attack attempt (-1 = none yet).</summary>
        public int LastSelectedAttack { get; private set; } = -1;

        /// <summary>
        /// PARTIAL: Alpha's attack table reads one parity bit from field +0x70 (the field its attack gate
        /// tests as "no target"), and what flips that bit is not established. The game therefore uses the
        /// even branch (attack 3 unarmed / 0x42 with a weapon) — the value every observed launch shows.
        /// </summary>
        public bool TargetPointerParity { get; set; }

        int _wanderRemainingMs;
        int _wanderDirection;
        int _actionRetryInMs;
        int _alphaElapsedMs;
        int _alphaApproachMs;
        int _alphaRescanMs;
        readonly bool _recovered;

        public const double AlphaNoticeRadius = 20.0; // 0x61027D → CombatBehavior ctor 0x42C550
        public const double AlphaLookRadius = 8.0; // 0x4C8140: strictly inside 64 squared

        public CreatureAi(bool recovered = false) => _recovered = recovered;

        public void Tick(CreatureState self, int elapsedMs, ICreatureAiWorld world)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");

            if (self.Health <= 0.0) { State = CreatureAiState.Dead; LastReason = "DEAD"; return; }
            // Passive objects never walk and never act (D67/D69); the AI must not animate them as creatures.
            if (self.IsPassiveObject) { State = CreatureAiState.Idle; LastReason = "PASSIVE_OBJECT"; return; }
            if (_recovered)
            {
                if (world is IAlphaCreatureAiWorld alphaWorld) TickRecovered(self, elapsedMs, alphaWorld);
                else LastReason = "ALPHA_WORLD_UNAVAILABLE";
                return;
            }

            if (!self.CanThink) { State = CreatureAiState.Idle; LastReason = "STUNNED"; return; }
            if (self.ActionLockMs > 0) { LastReason = "ACTION_LOCK"; return; }
            if (_actionRetryInMs > 0) _actionRetryInMs = Math.Max(0, _actionRetryInMs - elapsedMs);

            CreatureRules.RegenerateFriendly(self, elapsedMs / 1000.0);

            int ticks = Clock.Add(elapsedMs);
            if (ticks == 0) return;
            for (int i = 0; i < ticks; i++) Threat.Tick();
            ThinkTicks += ticks;

            // Alpha order: potion gates and branch A come before target resolution, branch B after it.
            int actionDuration = world.CurrentActionDurationMs(self);
            bool hasTarget = TargetId != null && world.TryGetTarget(TargetId, out var known) && known.Reachable;
            bool hasPotion = world.HasLifePotion(self);
            var potion = CreatureRules.EvaluatePotion(self, false, hasPotion, actionDuration, hasTarget);
            if (potion == PotionDecision.TickBlocked) { LastReason = "POTION_TICK_BLOCKED"; return; }
            if (potion == PotionDecision.InventoryPotion && world.TryConsumeLifePotion(self))
            {
                CreatureRules.ApplyPotionAction(self);
                LastRequestedAction = CreatureRules.PotionActionId;
                world.TryBeginAction(self, CreatureRules.PotionActionId, out var potionReason);
                State = CreatureAiState.Attack;
                LastReason = "POTION " + potionReason;
                return;
            }

            string target = SelectTarget(self, world, hasTarget, elapsedMs);
            if (string.IsNullOrEmpty(target))
            {
                Wander(self, world, elapsedMs);
                return;
            }

            world.TryGetTarget(target, out var targetPos);
            double dx = targetPos.X - self.X;
            double dz = targetPos.Z - self.Z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            if (!targetPos.Reachable)
            {
                Threat.MarkUnreachable(target, elapsedMs);
                State = CreatureAiState.Chase;
                LastReason = "TARGET_UNREACHABLE";
                return;
            }
            Threat.MarkReachable(target);

            double range = world.AttackRange > 0 ? world.AttackRange : DefaultAttackRange;
            if (distance > range)
            {
                world.MoveTowards(self, targetPos.X, targetPos.Z, world.WalkSpeed > 0 ? world.WalkSpeed : DefaultWalkSpeed, elapsedMs);
                State = CreatureAiState.Chase;
                LastReason = "CHASE";
                return;
            }

            State = CreatureAiState.Attack;
            LastReason = "IN_RANGE";
            if (_actionRetryInMs > 0) { LastReason = "ACTION_RETRY_WAIT"; return; }

            int action = AlphaAttackSelection.SelectAttackId(self.Kind, self.ClassId, self.SpecialisationId,
                self.WeaponItemType, self.WeaponItemSubtype, TargetPointerParity);
            LastSelectedAttack = action;
            LastRequestedAction = action;
            LastReason = world.TryBeginAction(self, action, out string reason)
                ? "ACTION_C29 " + action : "ACTION_REFUSED " + action + " " + reason;
            _actionRetryInMs = ActionRetryMs;
        }

        private void TickRecovered(CreatureState self, int elapsedMs, IAlphaCreatureAiWorld world)
        {
            // GameController accumulates +0x13C4 to >31 ms and clears on dispatch.
            // Combat owns the active action phases.
            _alphaElapsedMs += elapsedMs;
            if (world.TryGetActionState(self, out bool recovering))
            {
                _alphaElapsedMs = 0;
                State = recovering ? CreatureAiState.Recover : CreatureAiState.Attack;
                LastReason = recovering ? "ACTION_RECOVER" : "ACTION_ACTIVE";
                return;
            }
            if (!self.CanThink || _alphaElapsedMs <= 31) return;
            int dt = _alphaElapsedMs;
            _alphaElapsedMs = 0;
            ThinkTicks++;

            _alphaRescanMs = Math.Max(0, _alphaRescanMs - dt);
            bool noticed = false;
            if (TargetId == null && _alphaRescanMs == 0)
            {
                TargetId = world.FindNearestHostile(self, AlphaNoticeRadius);
                noticed = TargetId != null;
            }

            if (TargetId == null || !world.TryGetTarget(TargetId, out CreatureTarget target))
            {
                TargetId = null;
                _alphaApproachMs = 0;
                if (world.TryGetTarget("hero", out CreatureTarget hero) && DistanceSquared(self, hero) < AlphaLookRadius * AlphaLookRadius)
                {
                    world.Face(self, hero);
                    State = CreatureAiState.Idle;
                    LastReason = "LOOK_AT_PLAYER";
                }
                else if (AlphaMovement != null)
                {
                    Wander(self, world, dt);
                }
                else
                {
                    State = CreatureAiState.Idle;
                    LastReason = "NO_MOVEMENT_EVIDENCE";
                }
                return;
            }

            world.Face(self, target);
            double range = 2.0 * self.Width + target.Width; // CombatBehavior 0x42CB20, appearance field +0x80.
            if (DistanceSquared(self, target) > range * range || !world.HasLineOfSight(self, target))
            {
                // The original counts time spent in the approach branch, including unobstructed pursuit.
                _alphaApproachMs += dt;
                if (_alphaApproachMs > ThreatTable.UnreachableResetMs)
                {
                    TargetId = null;
                    _alphaApproachMs = 0;
                    _alphaRescanMs = ThreatTable.RescanMs;
                    State = CreatureAiState.Idle;
                    LastReason = "APPROACH_TIMEOUT";
                    return;
                }

                // Until the voxel path follower is ported, use the existing grounded walk adapter.
                world.MoveTowards(self, target.X, target.Z, DefaultWalkSpeed, dt);
                State = noticed ? CreatureAiState.Notice : CreatureAiState.Chase;
                LastReason = noticed ? "NOTICE" : "CHASE";
                return;
            }

            _alphaApproachMs = 0;
            State = CreatureAiState.Attack;
            // 0x42D87F dispatcher. These unarmed groups have single outcomes in the emulated inputs.
            // Other kinds, equipped cases and special/ranged branches need their own dispatcher evidence.
            int action = -1;
            if (!AlphaNpcWeaponActions.HasWeapon(self))
            {
                if (self.Kind >= 0x1E && self.Kind <= 0x20) action = 75;
                else if (self.Kind >= 0x3F && self.Kind <= 0x42) action = 6;
            }
            LastSelectedAttack = action;
            if (action < 0)
            {
                LastReason = "UNKNOWN_ALPHA_DISPATCH";
                return;
            }
            LastRequestedAction = action;
            LastReason = world.TryBeginAction(self, action, out string reason)
                ? "ALPHA_ACTION " + action : "ACTION_REFUSED " + action + " " + reason;
        }

        private static double DistanceSquared(CreatureState self, CreatureTarget target)
        {
            double dx = target.X - self.X, dy = target.Y - self.Y, dz = target.Z - self.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        string SelectTarget(CreatureState self, ICreatureAiWorld world, bool hasTarget, int elapsedMs)
        {
            string best = Threat.TopTarget();
            if (!string.IsNullOrEmpty(best) && world.TryGetTarget(best, out _))
            {
                TargetId = best;
                return best;
            }
            if (Threat.RescanRemainingMs > 0)
            {
                // The proven model pauses target acquisition for 3 s after a lost target.
                TargetId = null;
                return null;
            }
            string nearest = world.FindNearestHostile(self, world.AggroRadius > 0 ? world.AggroRadius : DefaultAggroRadius);
            if (string.IsNullOrEmpty(nearest))
            {
                TargetId = null;
                return null;
            }
            Threat.Add(nearest, 1.0);
            TargetId = nearest;
            return nearest;
        }

        /// <summary>Optional CUBEGAME_FALLBACK patrol ring; null → the creature wanders the way it did before.</summary>
        public PatrolRoute Patrol { get; private set; }

        /// <summary>Attaches the fallback ring; the AI walks it while it has no target (CUBEGAME_FALLBACK; Alpha rules: AlphaNpcMovement.cs).</summary>
        public void AttachPatrol(PatrolRoute patrol) => Patrol = patrol;

        /// <summary>
        /// Recovered Alpha movement without a target (SpawnLocation / WalkPath / RandomWalk, M0-F1A). When attached it
        /// takes precedence over the CUBEGAME_FALLBACK <see cref="Patrol"/> ring and the CubeGame wander.
        /// </summary>
        public AlphaNpcMovementDriver AlphaMovement { get; private set; }

        public void AttachAlphaMovement(AlphaNpcMovementDriver driver) => AlphaMovement = driver;

        void Wander(CreatureState self, ICreatureAiWorld world, int elapsedMs)
        {
            if (AlphaMovement != null)
            {
                AlphaNpcMovementStep step = AlphaMovement.Tick(self.X, self.Y, self.Z, elapsedMs,
                    () => _recovered && world is IAlphaCreatureAiWorld alphaWorld
                        ? alphaWorld.NextAlphaRoll() : (int)(world.NextRoll() & 0x7FFFu));
                State = step.Move ? CreatureAiState.Wander : CreatureAiState.Idle;
                if (_recovered && step.Move && step.ReturningHome)
                {
                    State = CreatureAiState.Return;
                }
                LastReason = step.Reason;
                if (step.Move)
                {
                    world.MoveTowards(self, step.TargetX, step.TargetZ,
                        world.WalkSpeed > 0 ? world.WalkSpeed : DefaultWalkSpeed, elapsedMs);
                }

                return;
            }

            if (Patrol != null)
            {
                Patrol.AdvanceIfArrived(self.X, self.Z);
                State = CreatureAiState.Wander;
                LastReason = "PATROL " + Patrol.Index;
                world.MoveTowards(self, Patrol.X, Patrol.Z, world.WalkSpeed > 0 ? world.WalkSpeed : DefaultWalkSpeed,
                    elapsedMs);
                return;
            }

            State = CreatureAiState.Wander;
            LastReason = "WANDER";
            _wanderRemainingMs -= elapsedMs;
            if (_wanderRemainingMs <= 0)
            {
                _wanderRemainingMs = WanderRepickMs;
                _wanderDirection = (int)(world.NextRoll() % 4u);
            }
            double angle = _wanderDirection * (Math.PI / 2.0);
            double distance = (world.WalkSpeed > 0 ? world.WalkSpeed : DefaultWalkSpeed) * 0.5 * (elapsedMs / 1000.0);
            world.MoveTowards(self, self.X + Math.Cos(angle) * distance, self.Z + Math.Sin(angle) * distance,
                world.WalkSpeed > 0 ? world.WalkSpeed : DefaultWalkSpeed, elapsedMs);
        }

        public bool TryDodge(CreatureState self, double distance, ICreatureAiWorld world)
        {
            if (self == null || world == null) return false;
            if (self.IsPassiveObject || !self.CanThink || self.Health <= 0.0) return false;
            int roll = (int)(world.NextRoll() % 100u);
            bool dodged = CreatureRules.TryDodge(roll, self.IsBoss, distance, self);
            if (dodged) { State = CreatureAiState.Idle; LastReason = "DODGE roll=" + roll; }
            return dodged;
        }

        public void AddThreat(string targetId, double amount) => Threat.Add(targetId, amount);
    }
}
