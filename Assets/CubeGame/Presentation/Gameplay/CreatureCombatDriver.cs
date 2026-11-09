using System;
using System.Collections.Generic;
using CubeGame.Features.AI;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Session;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Runs the Alpha creature AI (<see cref="CreatureAi"/>, M0-06C) against the game: the AI decides
    /// wander/chase/attack, this driver is its <see cref="ICreatureAiWorld"/>. Movement, facing and attack starts
    /// go through gameplay; damage and HP stay in <see cref="CombatSystem"/> (Core S03/S04). Ticked by the game
    /// clock, so pause stops creatures and the result does not depend on frame rate.
    /// </summary>
    public sealed class CreatureCombatDriver : ICreatureAiWorld
    {
        public const string HeroTargetId = "hero";

        private readonly GameSession _session;
        private readonly Func<double, double, double> _groundAt;
        private readonly Func<KindId, int[]> _actionsOf;
        private readonly Dictionary<string, Enemy> _enemies = new Dictionary<string, Enemy>(StringComparer.Ordinal);
        private uint _roll;
        private long _lastTimeMs = -1;

        public CreatureCombatDriver(GameSession session, Func<double, double, double> groundAt, Func<KindId, int[]> actionsOf, uint seed)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _groundAt = groundAt;
            _actionsOf = actionsOf;
            _roll = seed == 0 ? 0x9E3779B9u : seed;
        }

        public double AggroRadius => CreatureAi.DefaultAggroRadius;

        public double AttackRange => CreatureAi.DefaultAttackRange;

        public double WalkSpeed => CreatureAi.DefaultWalkSpeed;

        public int Count => _enemies.Count;

        public bool TryGetAi(StableEntityId id, out CreatureAi ai)
        {
            ai = _enemies.TryGetValue(id.ToString(), out Enemy enemy) ? enemy.Ai : null;
            return ai != null;
        }

        /// <summary>Regeneration of faction 5 (Alpha N-12, DIRECT): HP += MaxHP · 0.10 per second.</summary>
        public const double Faction5RegenPerSecond = 0.10;

        public void Add(Combatant combatant, int level, int stars)
        {
            Add(combatant, level, stars, (int)CreatureFaction.Monster, -1, -1);
        }

        /// <summary>
        /// World spawn (C6): faction, class, specialisation and main-hand weapon from the spawn record
        /// drive Alpha's attack selection (C-29/R-2).
        /// </summary>
        public void Add(Combatant combatant, int level, int stars, int faction, int classId, int specialisationId,
            RawAlphaItemId? mainHandWeapon = null)
        {
            RawAlphaItemId? weapon = mainHandWeapon?.Type == AlphaNpcWeaponActions.WeaponItemType ? mainHandWeapon : null;
            var state = new CreatureState
            {
                StableId = combatant.Id.ToString(),
                Kind = combatant.Kind.Value,
                Faction = faction,
                ClassId = classId,
                SpecialisationId = specialisationId,
                WeaponItemType = weapon?.Type ?? -1,
                WeaponItemSubtype = weapon?.Subtype ?? -1,
                Level = level,
                Stars = stars,
                Health = combatant.Health.CurrentHealth,
                MaxHealth = combatant.Health.MaxHealth,
                X = combatant.Position.X,
                Y = combatant.Position.Y,
                Z = combatant.Position.Z,
            };
            _enemies[state.StableId] = new Enemy { Combatant = combatant, State = state, Ai = new CreatureAi() };
        }

        public void Remove(StableEntityId id) => _enemies.Remove(id.ToString());

        /// <summary>Advances every creature to the clock's game time (one call per simulation tick).</summary>
        public void Tick(GameClock clock)
        {
            long now = clock.TimeMs;
            int elapsed = _lastTimeMs < 0 ? (int)Math.Round(clock.TickSeconds * 1000.0) : (int)(now - _lastTimeMs);
            _lastTimeMs = now;
            if (elapsed <= 0)
            {
                return;
            }

            foreach (Enemy enemy in new List<Enemy>(_enemies.Values))
            {
                Combatant c = enemy.Combatant;
                enemy.State.Health = c.Health.CurrentHealth;
                enemy.State.X = c.Position.X;
                enemy.State.Y = c.Position.Y;
                enemy.State.Z = c.Position.Z;
                if (c.IsDead)
                {
                    continue;
                }

                if (enemy.State.Faction == (int)CreatureFaction.Npc && c.Health.CurrentHealth < c.Health.MaxHealth)
                {
                    c.Health.Heal(c.Health.MaxHealth * Faction5RegenPerSecond * (elapsed / 1000.0));
                    enemy.State.Health = c.Health.CurrentHealth;
                }

                enemy.Moved = 0.0;
                enemy.State.AdvanceTime(elapsed);
                enemy.Ai.Tick(enemy.State, elapsed, this);
                double speed = enemy.Moved / (elapsed / 1000.0);
                _session.Events.Publish(new EntityMoved
                {
                    TimeMs = now,
                    Entity = c.Id,
                    Position = c.Position,
                    Yaw = c.Yaw,
                    Mode = speed > 0.05 ? LocomotionMode.Walk : LocomotionMode.Idle,
                    Speed = (float)speed,
                });
            }
        }

        public bool TryGetTarget(string id, out CreatureTarget target)
        {
            target = default;
            if (id != HeroTargetId || _session.Hero == null || _session.Hero.Health.IsDead)
            {
                return false;
            }

            target = new CreatureTarget { Id = id, X = _session.Hero.Position.X, Z = _session.Hero.Position.Z, Reachable = true };
            return true;
        }

        public string FindNearestHostile(CreatureState self, double radius)
        {
            if (!TryGetTarget(HeroTargetId, out CreatureTarget hero))
            {
                return null;
            }

            double dx = hero.X - self.X;
            double dz = hero.Z - self.Z;
            return dx * dx + dz * dz <= radius * radius ? HeroTargetId : null;
        }

        public bool TryBeginAction(CreatureState self, int actionCode, out string reason)
        {
            reason = string.Empty;
            if (!_enemies.TryGetValue(self.StableId, out Enemy enemy) || _session.Combat == null)
            {
                reason = "UNKNOWN_CREATURE";
                return false;
            }

            if (TryGetTarget(HeroTargetId, out CreatureTarget hero))
            {
                enemy.Combatant.Yaw = YawTowards(enemy.Combatant.Position.X, enemy.Combatant.Position.Z, hero.X, hero.Z);
            }

            if (!_session.Combat.TryStartAttack(enemy.Combatant.Id, new ActionId(actionCode), _session.Clock.TimeMs, $"action_{actionCode:000}"))
            {
                reason = _session.Combat.LastRejection;
                return false;
            }

            return true;
        }

        public void MoveTowards(CreatureState self, double targetX, double targetZ, double speed, int elapsedMs)
        {
            if (!_enemies.TryGetValue(self.StableId, out Enemy enemy) || _session.Combat == null || _session.Combat.IsActing(enemy.Combatant.Id))
            {
                return;
            }

            Combatant c = enemy.Combatant;
            double dx = targetX - c.Position.X;
            double dz = targetZ - c.Position.Z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            if (distance < 1e-6)
            {
                return;
            }

            double step = Math.Min(distance, speed * elapsedMs / 1000.0);
            double x = c.Position.X + dx / distance * step;
            double z = c.Position.Z + dz / distance * step;
            double y = _groundAt != null ? _groundAt(x, z) : c.Position.Y;
            c.Position = new WorldPosition(x, y, z);
            c.Yaw = YawTowards(c.Position.X - dx, c.Position.Z - dz, c.Position.X, c.Position.Z);
            self.X = x;
            self.Y = y;
            self.Z = z;
            enemy.Moved += step;
        }

        public bool TryConsumeLifePotion(CreatureState self) => false;

        public bool HasLifePotion(CreatureState self) => false;

        public int CurrentActionDurationMs(CreatureState self) => 0;

        public int[] AvailableActions(CreatureState self)
        {
            return _actionsOf != null ? _actionsOf(new KindId(self.Kind)) ?? Array.Empty<int>() : Array.Empty<int>();
        }

        public uint NextRoll()
        {
            _roll ^= _roll << 13;
            _roll ^= _roll >> 17;
            _roll ^= _roll << 5;
            return _roll;
        }

        private static float YawTowards(double fromX, double fromZ, double toX, double toZ)
        {
            return (float)(Math.Atan2(toX - fromX, toZ - fromZ) * 180.0 / Math.PI);
        }

        private sealed class Enemy
        {
            public Combatant Combatant;
            public CreatureState State;
            public CreatureAi Ai;
            public double Moved;
        }
    }
}
