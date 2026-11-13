using System;
using System.Collections.Generic;
using CubeGame.Core.Combat;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
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
        private readonly Dictionary<StableEntityId, AuthoritativeActionState> _actions = new Dictionary<StableEntityId, AuthoritativeActionState>();
        private long _nowMs;
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
            Projectiles = new ProjectileSystem(this, events);
        }

        /// <summary>Projectiles and hit volumes released by ranged/magic attacks (M0-F2B); advanced by <see cref="Update"/>.</summary>
        public ProjectileSystem Projectiles { get; }

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

            if (_combatants.TryGetValue(combatant.Id, out Combatant previous) && previous != combatant)
            {
                Unregister(combatant.Id);
            }

            _combatants[combatant.Id] = combatant;
            return combatant;
        }

        public bool Unregister(StableEntityId id)
        {
            bool removed = _combatants.Remove(id);
            Interrupt(id, _nowMs, "performer unloaded");
            Projectiles.RemoveOwnedBy(id, _nowMs);
            return removed;
        }

        public bool TryGet(StableEntityId id, out Combatant combatant) => _combatants.TryGetValue(id, out combatant);

        public bool IsActing(StableEntityId id) => _actions.ContainsKey(id);

        public bool TryGetAction(StableEntityId id, out AuthoritativeActionState action) => _actions.TryGetValue(id, out action);

        public bool TryGetPhase(StableEntityId id, out ActionPhase phase)
        {
            bool active = TryGetAction(id, out AuthoritativeActionState action);
            phase = active ? action.Phase : ActionPhase.Finished;
            return active;
        }

        public long ActionSerialOf(StableEntityId id) => TryGetAction(id, out AuthoritativeActionState action) ? action.Serial : -1;

        public bool TryStartAttack(StableEntityId attackerId, ActionId action, long nowMs, string animationKey = "") =>
            TryStart(attackerId, AlphaAttackRoute.ForAction(action.Value), nowMs, animationKey, StableEntityId.None, true);

        public bool TryStartAttack(StableEntityId attackerId, AttackPlan plan, long nowMs, StableEntityId target = default)
        {
            AttackPlan route = AlphaAttackRoute.ForAction(plan.Action.Value);
            if (!plan.IsSupported || !route.IsSupported || plan.Path != route.Path || plan.Projectile != route.Projectile)
            {
                return Reject(!plan.IsSupported ? plan.Reason : $"unsupported route for Alpha action 0x{plan.Action.Value:X}");
            }

            return TryStart(attackerId, plan, nowMs, string.Empty, target, true);
        }

        public bool TryStartAction(StableEntityId attackerId, ActionId action, long nowMs, string animationKey, bool isAttack) =>
            TryStart(attackerId, AlphaAttackRoute.ForAction(action.Value), nowMs, animationKey, StableEntityId.None, isAttack);

        private bool TryStart(StableEntityId attackerId, AttackPlan plan, long nowMs, string animationKey,
            StableEntityId target, bool isAttack)
        {
            if (!plan.IsSupported) return Reject(plan.Reason);
            if (isAttack == (plan.Path == AttackPath.ItemUse)) return Reject($"invalid intent for {plan.Action.Value}");
            if (!_combatants.TryGetValue(attackerId, out Combatant attacker)) return Reject($"unknown attacker {attackerId}");
            if (attacker.IsDead) return Reject($"{attacker.Name} is dead");
            if (IsActing(attackerId)) return Reject($"{attacker.Name} is still performing an action");

            ItemInstance main = attacker.Loadout.GetEquipped(EquipmentSlot.MainHand);
            ItemInstance off = attacker.Loadout.GetEquipped(EquipmentSlot.OffHand);
            int mainSubtype = attacker.AlphaMainHandSubtype ?? main?.Subtype ?? -1;
            if ((_timings == null || !_timings.TryGet(attacker.Kind, plan.Action, out ActionTiming timing)) &&
                !AlphaDispatcherTiming.TryGet(plan.Action, mainSubtype, attacker.TimingRate, out timing))
            {
                return Reject($"UNKNOWN Alpha timing for action 0x{plan.Action.Value:X}");
            }

            CombatAttack attack = isAttack ? _adapter.BuildAttack(attacker.Sheet, attacker.Loadout, Rules.HitChance, 0, 0) : null;
            var action = new AuthoritativeActionState(attacker, plan, timing, nowMs, ++_actionSerial,
                target, main, mainSubtype, off, attack, string.IsNullOrEmpty(animationKey) ? $"action_{plan.Action.Value:000}" : animationKey);
            _actions.Add(attackerId, action);
            _nowMs = nowMs;
            LastRejection = string.Empty;
            _events.Publish(new ActionStarted
            {
                TimeMs = nowMs, Entity = attackerId, Action = action.Action, Target = target, Serial = action.Serial,
                PhaseDurationsMs = timing.PhaseDurations, AnimationKey = action.AnimationKey,
            });
            if (IsCurrent(action)) SetPhase(action, ActionPhase.Windup, nowMs);
            return true;
        }

        /// <summary>Advance action boundaries in timestamp order, including across a coarse simulation tick.</summary>
        public void Update(long nowMs)
        {
            _nowMs = nowMs;
            var running = new List<AuthoritativeActionState>(_actions.Values);
            foreach (AuthoritativeActionState action in running)
            {
                if (!action.Actor.IsDead) continue;
                Interrupt(action.Actor.Id, nowMs, "performer died");
                Projectiles.RemoveOwnedBy(action.Actor.Id, nowMs);
            }

            while (true)
            {
                AuthoritativeActionState next = null;
                long time = nowMs;
                foreach (AuthoritativeActionState action in running)
                {
                    if (!IsCurrent(action)) continue;
                    long boundary = action.StartMs + action.NextBoundaryMs;
                    if (boundary <= time && (next == null || boundary < time || action.Serial < next.Serial))
                    {
                        next = action;
                        time = boundary;
                    }
                }

                if (next == null) break;
                _nowMs = time;
                Projectiles.Update(time);
                if (IsCurrent(next)) Advance(next, time);
            }

            foreach (AuthoritativeActionState action in running)
            {
                if (IsCurrent(action)) action.ElapsedMs = Math.Max(0, nowMs - action.StartMs);
            }

            _nowMs = nowMs;
            Projectiles.Update(nowMs);
        }

        private void Advance(AuthoritativeActionState action, long timeMs)
        {
            action.ElapsedMs = timeMs - action.StartMs;
            if (action.Phase == ActionPhase.Windup)
            {
                SetPhase(action, ActionPhase.Strike, timeMs);
            }

            if (action.Phase == ActionPhase.Strike && action.ElapsedMs == action.NextStrikeMs)
            {
                if (!IsCurrent(action)) return;
                long following = action.NextStrikeMs + action.Timing.RepeatMs;
                action.NextStrikeMs = action.Timing.RepeatMs > 0 && following < action.Timing.StrikeEndMs
                    ? following : long.MaxValue;
                action.ReleaseCount++;
                Strike(action, timeMs);
                return;
            }

            if (action.Phase == ActionPhase.Strike)
            {
                SetPhase(action, ActionPhase.Recover, timeMs);
                return;
            }

            Finish(action, timeMs, ActionOutcome.Completed, $"windows={action.ReleaseCount} hits={action.HitCount}");
        }

        private void Strike(AuthoritativeActionState action, long nowMs)
        {
            if (action.Actor.IsDead) return;
            switch (action.Plan.Path)
            {
                case AttackPath.Projectile:
                    Projectiles.Launch(action.Actor, action.Plan.Projectile, action.Action, action.Serial, nowMs, action);
                    break;
                case AttackPath.Hitscan:
                    action.HitCount += Projectiles.FireRay(action.Actor, action.Action, action.Serial, nowMs, action);
                    break;
                case AttackPath.Melee:
                case AttackPath.AreaEffect:
                    ResolveMelee(action, nowMs);
                    break;
            }
        }

        /// <summary>
        /// One strike against one target through Core S03/S04 (deterministic per world seed and action serial).
        /// Public so that systems with their own hit detection (projectiles, AI) use the same damage path.
        /// </summary>
        public CombatOutcome ResolveHit(StableEntityId attackerId, StableEntityId targetId, long nowMs, long actionSerial) =>
            ResolveHit(attackerId, targetId, nowMs, actionSerial, null);

        internal CombatOutcome ResolveHit(StableEntityId attackerId, StableEntityId targetId, long nowMs, long actionSerial,
            AuthoritativeActionState action)
        {
            Combatant attacker = _combatants[attackerId];
            Combatant target = _combatants[targetId];
            if (target.IsDead)
            {
                // A dead target gets no further hits, events or effects (M0-F2B).
                return CombatOutcome.TargetAlreadyDead(target.Health.CurrentHealth);
            }

            double armor = EquipmentCombatAdapter.ComputeTargetArmor(target.Loadout);
            CombatAttack attack = action?.DamageInput ?? _adapter.BuildAttack(attacker.Sheet, attacker.Loadout, Rules.HitChance, 0, 0);
            if (action != null)
            {
                // 0x43EC90, used by 0x6144CF / 0x617483 / 0x617D4D before the damage roll.
                double multiplier = action.Action.Value switch
                {
                    0x05 or 0x1A => 0.5,
                    0x1E or 0x20 => 0.4,
                    0x1F or 0x21 => 0.6,
                    0x25 or 0x2B or 0x39 or 0x3A or 0x44 => 2.0,
                    _ => 1.0,
                };
                if (multiplier != 1.0)
                {
                    // These multipliers apply to the whole input, including items and upgrades.
                    double baseDamage = CombatFormulas.WeaponDamage(attack.WeaponBase, attack.Level, attack.Upgrades, attack.Stars);
                    attack = new CombatAttack((baseDamage + attack.ItemWeaponDamageBonus) * multiplier, 1, attack.HitChance);
                }
            }

            attack.TargetArmor = armor;
            attack.BlockFraction = Rules.BlockFraction;
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
                AttackSerial = actionSerial,
                Action = action?.Action ?? default,
            });

            if (outcome.KilledTarget)
            {
                Interrupt(targetId, nowMs, "performer died");
                Projectiles.RemoveOwnedBy(targetId, nowMs);

                _events.Publish(new EntityDied { TimeMs = nowMs, Entity = targetId, Killer = attackerId });
            }

            return outcome;
        }

        // 0x6137F7..0x614411: yaw-relative centre, horizontal radius, vertical tolerance and LOS.
        private bool InMeleeReach(AuthoritativeActionState action, Combatant target)
        {
            Combatant actor = action.Actor;
            int id = action.Action.Value;
            double width = actor.HitWidth;
            double radius = width * 1.5;
            double forward = 1.5;
            double yaw = action.Yaw * Math.PI / 180.0;
            double x = actor.Position.X;
            double y = actor.Position.Y + actor.HitHeight * 0.5;
            double z = actor.Position.Z;
            switch (id)
            {
                case 0x01: case 0x02: case 0x03: case 0x04: case 0x06: case 0x07:
                case 0x09: case 0x0D: case 0x0E: case 0x12: case 0x13:
                    radius = width * 2.0;
                    forward = 1.0;
                    break;
                case 0x1E: case 0x1F: case 0x20: case 0x21:
                    radius = id == 0x1E || id == 0x20 ? 3.0 : 5.0;
                    forward = 0.0;
                    x += action.Aim.X;
                    y += action.Aim.Y;
                    z += action.Aim.Z;
                    break;
                case 0x4C: case 0x4D: case 0x4E:
                    radius *= 1.2;
                    forward = 0.0;
                    break;
            }

            x += Math.Sin(yaw) * width * forward;
            z += Math.Cos(yaw) * width * forward;
            double dx = target.Position.X - x;
            double dy = target.Position.Y + target.HitHeight * 0.5 - y;
            double dz = target.Position.Z - z;
            double reach = radius + target.HitWidth * 0.7;
            return dx * dx + dz * dz < reach * reach &&
                Math.Abs(dy) < target.HitHeight * 0.5 + actor.HitHeight + radius &&
                Projectiles.HasLineOfSight(new WorldPosition(x, y, z), dx, dy, dz);
        }

        private void ResolveMelee(AuthoritativeActionState action, long nowMs)
        {
            foreach (Combatant target in new List<Combatant>(_combatants.Values))
            {
                if (!IsCurrent(action) || action.Actor.IsDead) break;
                if (!CanHit(action.Actor, target) || !InMeleeReach(action, target)) continue;

                ResolveHit(action.Actor.Id, target.Id, nowMs, action.Serial, action);
                action.HitCount++;
            }
        }

        internal bool CanHit(Combatant attacker, Combatant target) =>
            !attacker.IsDead && !target.IsDead && target.Hostile != attacker.Hostile &&
            _combatants.TryGetValue(target.Id, out Combatant current) && current == target;

        private bool IsCurrent(AuthoritativeActionState action) =>
            _actions.TryGetValue(action.Actor.Id, out AuthoritativeActionState current) && current == action;

        private void SetPhase(AuthoritativeActionState action, ActionPhase phase, long timeMs)
        {
            action.Phase = phase;
            _events.Publish(new ActionPhaseChanged
            {
                TimeMs = timeMs, Entity = action.Actor.Id, Action = action.Action, Phase = phase, Serial = action.Serial,
            });
        }

        private void Interrupt(StableEntityId id, long nowMs, string reason)
        {
            if (TryGetAction(id, out AuthoritativeActionState action)) Finish(action, nowMs, ActionOutcome.Interrupted, reason);
        }

        private void Finish(AuthoritativeActionState action, long timeMs, ActionOutcome outcome, string reason)
        {
            _actions.Remove(action.Actor.Id);
            action.ElapsedMs = Math.Max(0, timeMs - action.StartMs);
            action.Outcome = outcome;
            SetPhase(action, ActionPhase.Finished, timeMs);
            _events.Publish(new ActionResolved
            {
                TimeMs = timeMs, Entity = action.Actor.Id, Action = action.Action, Serial = action.Serial,
                Outcome = outcome, Reason = reason,
            });
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

    }

    public sealed class AuthoritativeActionState
    {
        internal AuthoritativeActionState(Combatant actor, AttackPlan plan, ActionTiming timing, long startMs,
            long serial, StableEntityId target, ItemInstance mainHand, int mainHandSubtype, ItemInstance offHand,
            CombatAttack damageInput, string animationKey)
        {
            Actor = actor;
            Plan = plan;
            Timing = timing;
            StartMs = startMs;
            Serial = serial;
            Target = target;
            MainHand = mainHand;
            MainHandSubtype = mainHandSubtype;
            DamageInput = damageInput;
            OffHand = offHand;
            AnimationKey = animationKey;
            Yaw = actor.Yaw;
            actor.GetAim(out double x, out double y, out double z);
            Aim = new WorldPosition(x, y, z);
            NextStrikeMs = timing.StrikeAtMs;
        }

        public Combatant Actor { get; }
        public AttackPlan Plan { get; }
        public ActionId Action => Plan.Action;
        public ActionTiming Timing { get; }
        public long Serial { get; }
        public long StartMs { get; }
        public long ElapsedMs { get; internal set; }
        public ActionPhase Phase { get; internal set; }
        public ActionOutcome? Outcome { get; internal set; }
        public StableEntityId Target { get; }
        public WorldPosition Aim { get; }
        public float Yaw { get; }
        public ItemInstance MainHand { get; }
        public int MainHandSubtype { get; }
        public ItemInstance OffHand { get; }
        public string AnimationKey { get; }
        public int ReleaseCount { get; internal set; }
        public int HitCount { get; internal set; }
        internal CombatAttack DamageInput { get; }
        internal long NextStrikeMs { get; set; }

        internal long NextBoundaryMs => Phase switch
        {
            ActionPhase.Windup => Timing.StrikeAtMs,
            ActionPhase.Strike => Math.Min(NextStrikeMs, Timing.StrikeEndMs),
            _ => Timing.EndMs,
        };
    }
}
