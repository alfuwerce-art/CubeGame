using System;
using System.Collections.Generic;
using CubeGame.Core.Combat;
using CubeGame.Core.Equipment;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;

namespace CubeGame.Features.Combat
{
    /// <summary>
    /// Values the first combat slice needs that are not restored from Alpha. Every field is a CubeGame
    /// decision with status PLACEHOLDER_NOT_ALPHA until the Alpha source is found (coverage rows in brackets).
    /// </summary>
    public sealed class CombatRules
    {
        /// <summary>Hit chance passed to Core (OQ-01 UNKNOWN; S04 demo uses 1.0).</summary>
        public double HitChance { get; set; } = 1.0;

        /// <summary>Share of damage the target blocks (OQ-24 PARTIAL; no blocking in this slice).</summary>
        public double BlockFraction { get; set; }

        /// <summary>Melee reach in blocks from the attacker centre (Alpha hit volume not restored).</summary>
        public double MeleeReach { get; set; } = 3.0;

        /// <summary>Half-angle of the frontal hit sector, degrees (Alpha hit volume not restored).</summary>
        public double MeleeHalfArcDegrees { get; set; } = 70.0;

        /// <summary>Phases used when no Alpha timing is known for a species/action.</summary>
        public ActionTiming FallbackTiming { get; set; } = new ActionTiming(400, 600, 800, "PLACEHOLDER_NOT_ALPHA");
    }

    /// <summary>
    /// Gameplay owner of attacks, hits and HP (M0-05A first slice). Uses Core S03/S04 for every number
    /// (<see cref="EquipmentCombatAdapter"/> → <see cref="DamageResolver"/> → <see cref="HealthPool"/>);
    /// the visual layer only receives events and never changes HP. Driven by the game clock: phases advance
    /// on <see cref="Update"/> with game time, so pause stops combat and results do not depend on frame rate.
    /// </summary>
    public sealed class CombatSystem
    {
        private readonly Dictionary<StableEntityId, Combatant> _combatants = new Dictionary<StableEntityId, Combatant>();
        private readonly Dictionary<StableEntityId, ActiveAction> _actions = new Dictionary<StableEntityId, ActiveAction>();
        private readonly List<StableEntityId> _finished = new List<StableEntityId>();
        private readonly GameEventBus _events;
        private readonly IActionTimingSource _timings;
        private readonly EquipmentCombatAdapter _adapter = new EquipmentCombatAdapter();
        private readonly int _seed;
        private long _actionSerial;

        public CombatSystem(GameEventBus events, IActionTimingSource timings, int seed, CombatRules rules = null)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _timings = timings;
            _seed = seed;
            Rules = rules ?? new CombatRules();
        }

        public CombatRules Rules { get; }

        public IReadOnlyCollection<Combatant> Combatants => _combatants.Values;

        /// <summary>Last refusal reason of <see cref="TryStartAttack"/> (empty when accepted).</summary>
        public string LastRejection { get; private set; } = string.Empty;

        public long ResolvedHits { get; private set; }

        public Combatant Register(Combatant combatant)
        {
            if (combatant == null)
            {
                throw new ArgumentNullException(nameof(combatant));
            }

            _combatants[combatant.Id] = combatant;
            return combatant;
        }

        public bool Unregister(StableEntityId id)
        {
            _actions.Remove(id);
            return _combatants.Remove(id);
        }

        public bool TryGet(StableEntityId id, out Combatant combatant) => _combatants.TryGetValue(id, out combatant);

        public bool IsActing(StableEntityId id) => _actions.ContainsKey(id);

        public bool TryGetPhase(StableEntityId id, out ActionPhase phase)
        {
            phase = ActionPhase.Finished;
            if (!_actions.TryGetValue(id, out ActiveAction action))
            {
                return false;
            }

            phase = action.Phase;
            return true;
        }

        /// <summary>
        /// Starts an attack. Refused (with a reason, no event) when the attacker is unknown, dead or already acting.
        /// </summary>
        public bool TryStartAttack(StableEntityId attackerId, ActionId action, long nowMs, string animationKey = "")
        {
            if (!_combatants.TryGetValue(attackerId, out Combatant attacker))
            {
                return Reject($"unknown attacker {attackerId}");
            }

            if (attacker.IsDead)
            {
                return Reject($"{attacker.Name} is dead");
            }

            if (_actions.ContainsKey(attackerId))
            {
                return Reject($"{attacker.Name} is still performing an action");
            }

            ActionTiming timing = Rules.FallbackTiming;
            if (_timings != null && _timings.TryGet(attacker.Kind, action, out ActionTiming known))
            {
                timing = known;
            }

            var active = new ActiveAction(attackerId, action, nowMs, timing, ++_actionSerial);
            _actions[attackerId] = active;
            LastRejection = string.Empty;
            _events.Publish(new ActionStarted
            {
                TimeMs = nowMs,
                Entity = attackerId,
                Action = action,
                PhaseDurationsMs = timing.PhaseDurations,
                AnimationKey = animationKey ?? string.Empty,
            });
            _events.Publish(new ActionPhaseChanged { TimeMs = nowMs, Entity = attackerId, Action = action, Phase = ActionPhase.Windup });
            return true;
        }

        /// <summary>Advances every running action to game time <paramref name="nowMs"/>; hits resolve when Strike begins.</summary>
        public void Update(long nowMs)
        {
            _finished.Clear();
            // Snapshot: a kill during this update removes the victim's action from the dictionary.
            foreach (ActiveAction action in new List<ActiveAction>(_actions.Values))
            {
                if (!_actions.ContainsKey(action.Attacker))
                {
                    continue;
                }

                long elapsed = nowMs - action.StartMs;
                if (action.Phase == ActionPhase.Windup && elapsed >= action.Timing.StrikeAtMs)
                {
                    SetPhase(action, ActionPhase.Strike, action.StartMs + action.Timing.StrikeAtMs);
                    ResolveMelee(action, action.StartMs + action.Timing.StrikeAtMs);
                }

                if (action.Phase == ActionPhase.Strike && elapsed >= action.Timing.StrikeEndMs)
                {
                    SetPhase(action, ActionPhase.Recover, action.StartMs + action.Timing.StrikeEndMs);
                }

                if (action.Phase == ActionPhase.Recover && elapsed >= action.Timing.EndMs)
                {
                    long endMs = action.StartMs + action.Timing.EndMs;
                    SetPhase(action, ActionPhase.Finished, endMs);
                    _finished.Add(action.Attacker);
                }
            }

            foreach (StableEntityId id in _finished)
            {
                ActiveAction action = _actions[id];
                _actions.Remove(id);
                _events.Publish(new ActionResolved
                {
                    TimeMs = action.StartMs + action.Timing.EndMs,
                    Entity = id,
                    Action = action.Action,
                    Outcome = ActionOutcome.Completed,
                    Reason = action.HitCount > 0 ? $"hits={action.HitCount}" : "no target in reach",
                });
            }
        }

        /// <summary>
        /// One strike against one target through Core S03/S04 (deterministic per world seed and action serial).
        /// Public so that systems with their own hit detection (projectiles, AI) use the same damage path.
        /// </summary>
        public CombatOutcome ResolveHit(StableEntityId attackerId, StableEntityId targetId, long nowMs, long actionSerial)
        {
            Combatant attacker = _combatants[attackerId];
            Combatant target = _combatants[targetId];
            double armor = EquipmentCombatAdapter.ComputeTargetArmor(target.Loadout);
            CombatAttack attack = _adapter.BuildAttack(attacker.Sheet, attacker.Loadout, Rules.HitChance, armor, Rules.BlockFraction);
            var resolver = new DamageResolver(new SeededCombatRandom(StrikeSeed(attackerId, targetId, actionSerial)));
            CombatOutcome outcome = resolver.Resolve(attack, target.Health);
            ResolvedHits++;

            _events.Publish(new HitResolved
            {
                TimeMs = nowMs,
                Attacker = attackerId,
                Target = targetId,
                Damage = outcome.AppliedDamage,
                Blocked = outcome.IsHit && outcome.AbsorbedDamage > 0.0 && outcome.AppliedDamage <= 0.0,
                Critical = false,
                Killed = outcome.KilledTarget,
                TargetHealthAfter = target.Health.CurrentHealth,
            });

            if (outcome.KilledTarget)
            {
                if (_actions.TryGetValue(targetId, out ActiveAction interrupted))
                {
                    _actions.Remove(targetId);
                    _events.Publish(new ActionResolved
                    {
                        TimeMs = nowMs,
                        Entity = targetId,
                        Action = interrupted.Action,
                        Outcome = ActionOutcome.Interrupted,
                        Reason = "performer died",
                    });
                }

                _events.Publish(new EntityDied { TimeMs = nowMs, Entity = targetId, Killer = attackerId });
            }

            return outcome;
        }

        /// <summary>True when <paramref name="target"/> is inside the attacker's frontal melee sector.</summary>
        public bool InMeleeReach(Combatant attacker, Combatant target)
        {
            double dx = target.Position.X - attacker.Position.X;
            double dz = target.Position.Z - attacker.Position.Z;
            double dy = target.Position.Y - attacker.Position.Y;
            double horizontal = Math.Sqrt(dx * dx + dz * dz);
            if (horizontal > Rules.MeleeReach || Math.Abs(dy) > Rules.MeleeReach)
            {
                return false;
            }

            if (horizontal < 0.25)
            {
                return true;
            }

            double yaw = attacker.Yaw * Math.PI / 180.0;
            double facingX = Math.Sin(yaw);
            double facingZ = Math.Cos(yaw);
            double cos = (dx * facingX + dz * facingZ) / horizontal;
            return cos >= Math.Cos(Rules.MeleeHalfArcDegrees * Math.PI / 180.0);
        }

        private void ResolveMelee(ActiveAction action, long nowMs)
        {
            if (!_combatants.TryGetValue(action.Attacker, out Combatant attacker))
            {
                return;
            }

            // Snapshot: a kill inside the loop must not modify the collection being enumerated.
            var targets = new List<Combatant>(_combatants.Values);
            foreach (Combatant target in targets)
            {
                if (target.Id.Equals(attacker.Id) || target.IsDead || target.Hostile == attacker.Hostile)
                {
                    continue;
                }

                if (!InMeleeReach(attacker, target))
                {
                    continue;
                }

                ResolveHit(attacker.Id, target.Id, nowMs, action.Serial);
                action.HitCount++;
            }
        }

        private void SetPhase(ActiveAction action, ActionPhase phase, long timeMs)
        {
            action.Phase = phase;
            _events.Publish(new ActionPhaseChanged { TimeMs = timeMs, Entity = action.Attacker, Action = action.Action, Phase = phase });
        }

        /// <summary>Random seed of one strike (world seed, attacker, target, action serial) — public for independent verification.</summary>
        public int StrikeSeed(StableEntityId attacker, StableEntityId target, long serial)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                h = (h ^ (ulong)(uint)_seed) * 1099511628211UL;
                h = (h ^ attacker.Value) * 1099511628211UL;
                h = (h ^ target.Value) * 1099511628211UL;
                h = (h ^ (ulong)serial) * 1099511628211UL;
                return (int)(h ^ (h >> 32));
            }
        }

        private bool Reject(string reason)
        {
            LastRejection = reason;
            return false;
        }

        private sealed class ActiveAction
        {
            public ActiveAction(StableEntityId attacker, ActionId action, long startMs, ActionTiming timing, long serial)
            {
                Attacker = attacker;
                Action = action;
                StartMs = startMs;
                Timing = timing;
                Serial = serial;
                Phase = ActionPhase.Windup;
            }

            public StableEntityId Attacker { get; }

            public ActionId Action { get; }

            public long StartMs { get; }

            public ActionTiming Timing { get; }

            public long Serial { get; }

            public ActionPhase Phase { get; set; }

            public int HitCount { get; set; }
        }
    }
}
