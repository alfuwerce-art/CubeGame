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
        /// <summary>PLACEHOLDER_NOT_ALPHA: median captured AI walk speed (OQ-23).</summary>
        public const double DefaultWalkSpeed = 3.7;
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

        public void Tick(CreatureState self, int elapsedMs, ICreatureAiWorld world)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");

            if (self.Health <= 0.0) { State = CreatureAiState.Dead; LastReason = "DEAD"; return; }
            // Passive objects never walk and never act (D67/D69); the AI must not animate them as creatures.
            if (self.IsPassiveObject) { State = CreatureAiState.Idle; LastReason = "PASSIVE_OBJECT"; return; }
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
            var actions = world.AvailableActions(self);
            if (actions == null || actions.Length == 0) { LastReason = "IN_RANGE_NO_ACTION_EVIDENCE"; return; }
            // CUBEGAME_CONTRACT: Alpha's per-action cooldown/lock lives in the attack choice, not in the
            // AI tick; without it the AI would ask the game every 31 ms tick. 250 ms is a CubeGame
            // throttle and is NOT an Alpha constant.
            if (_actionRetryInMs > 0) { LastReason = "ACTION_RETRY_WAIT"; return; }
            // C-29 (M0-06C): Alpha decides the attack from the weapon/class table (AlphaAttackSelection),
            // then requires it to exist in the creature's own action set; library order is only a fallback
            // for a kind whose evidence does not contain the Alpha-selected id.
            int preferred = AlphaAttackSelection.SelectAttackId(self.Kind, self.ClassId, self.SpecialisationId,
                self.WeaponItemType, self.WeaponItemSubtype, TargetPointerParity);
            LastSelectedAttack = preferred;
            bool preferredInLibrary = Array.IndexOf(actions, preferred) >= 0;
            for (int i = -1; i < actions.Length; i++)
            {
                if (i < 0 && !preferredInLibrary) continue;
                int action = i < 0 ? preferred : actions[i];
                if (action < 0 || (i >= 0 && action == preferred)) continue;
                LastRequestedAction = action;
                if (world.TryBeginAction(self, action, out var reason))
                {
                    LastReason = (preferredInLibrary ? "ACTION_C29 " : "ACTION_C29_NOT_IN_LIBRARY " + preferred + " ") + action;
                    _actionRetryInMs = ActionRetryMs;
                    return;
                }
                LastReason = "ACTION_REFUSED " + action + " " + reason;
            }
            _actionRetryInMs = ActionRetryMs;
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

        void Wander(CreatureState self, ICreatureAiWorld world, int elapsedMs)
        {
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
