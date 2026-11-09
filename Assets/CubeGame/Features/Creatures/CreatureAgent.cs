// CubeGame.Features.Creatures — one living creature: state + AI + Alpha visual (D86 M0-06C/06D).
//
// The agent is the world side of ICreatureAiWorld: it moves the transform, resolves targets through
// the target registry and translates Alpha action codes to the visual layer. HP, damage, loot and
// inventory live in the game (Core/Features); the agent only reads the state it is given.
using System;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using UnityEngine;

namespace CubeGame.Features.Creatures
{
    /// <summary>Anything the AI may target: the player, an NPC, a creature. Registered by the game.</summary>
    public interface ICreatureTargetSource
    {
        string TargetId { get; }
        Vector3 Position { get; }
        int Faction { get; }
        bool Alive { get; }
    }

    public static class CreatureTargets
    {
        static readonly List<ICreatureTargetSource> Sources = new List<ICreatureTargetSource>();

        public static void Register(ICreatureTargetSource source)
        {
            if (source == null || string.IsNullOrEmpty(source.TargetId)) return;
            Unregister(source.TargetId);
            Sources.Add(source);
        }

        public static void Unregister(string targetId)
        {
            for (int i = Sources.Count - 1; i >= 0; i--)
                if (Sources[i].TargetId == targetId) Sources.RemoveAt(i);
        }

        public static void Clear() => Sources.Clear();

        public static bool TryGet(string targetId, out ICreatureTargetSource source)
        {
            foreach (var s in Sources)
                if (s.TargetId == targetId) { source = s; return true; }
            source = null;
            return false;
        }

        public static string NearestHostile(int faction, Vector3 position, float radius)
        {
            string best = null;
            float bestDistance = radius;
            foreach (var s in Sources)
            {
                if (!s.Alive || s.Faction == faction) continue;
                float distance = Vector3.Distance(position, s.Position);
                if (distance <= bestDistance) { best = s.TargetId; bestDistance = distance; }
            }
            return best;
        }
    }

    public sealed class CreatureAgent : MonoBehaviour, ICreatureAiWorld
    {
        public static readonly Dictionary<string, CreatureAgent> Live = new Dictionary<string, CreatureAgent>(StringComparer.Ordinal);

        public CreatureState State { get; private set; }
        public ICreatureAi Ai { get; private set; }
        public AlphaActorView View { get; private set; }
        public string VisualStatus { get; private set; } = "NOT_RESOLVED";
        public bool VisualAvailable => View != null && View.Ready;

        /// <summary>Alpha action timing (windup/actionEnd/recovery) for combat scheduling; false if unknown.</summary>
        public bool TryGetActionTiming(int actionCode, out CubeGame.AlphaTransfer.AlphaActionTiming timing)
        {
            if (View != null) return View.TryGetActionTiming(actionCode, out timing);
            timing = default;
            return false;
        }

        public double WalkSpeed { get; set; } = CreatureMovement.WalkSpeed;   // ALPHA_CODE 0x5A7EB0 (C2 scale)
        public double AggroRadius { get; set; } = 16.0;  // CUBEGAME_CONTRACT
        public double AttackRange { get; set; } = 2.0;   // proven dodge distance
        public int LastMoveFrames { get; private set; }

        /// <summary>The game subscribes to apply combat consequences (damage, resources). AI never does.</summary>
        public event Action<CreatureAgent, int, string> ActionRequested;

        // Inventory/action seam: wired by the game (Features/Items, Features/Combat). Null = not available.
        public Func<CreatureState, bool> LifePotionQuery;
        public Func<CreatureState, bool> LifePotionConsume;
        public Func<CreatureState, int> ActionDurationQuery;

        uint _rng = 1u;
        float _lastDistance;

        /// <summary>
        /// Floating origin (absolute world voxels). The state keeps absolute `double` coordinates; the
        /// Transform holds only the local difference so a 2^24-voxel world does not lose float precision.
        /// </summary>
        public double WorldOriginX { get; private set; }
        public double WorldOriginZ { get; private set; }

        public bool Initialize(CreatureState state, ICreatureAi ai, AlphaActorDefinition actor, uint seed = 1u,
            double originX = 0.0, double originZ = 0.0)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Ai = ai;
            _rng = seed == 0 ? 1u : seed;
            WorldOriginX = originX;
            WorldOriginZ = originZ;
            ApplyPosition();
            if (actor != null)
            {
                var problems = new List<string>();
                View = AlphaActorView.Spawn(actor, transform, problems);
                VisualStatus = View != null && View.Ready ? "ALPHA_VISUAL " + actor.Id : "VISUAL_PROBLEMS " + string.Join(",", problems);
                if (View != null && View.Ready) View.transform.localPosition = Vector3.zero;
            }
            else
            {
                VisualStatus = "MISSING_VISUAL_CONTENT";
                Debug.LogWarning($"[CreatureAgent] {state.StableId} has no Alpha visual; AI/movement still work.");
            }
            Live[state.StableId] = this;
            return true;
        }

        /// <summary>Move the floating origin and put the transform at state − origin.</summary>
        public void SetWorldOrigin(double originX, double originZ)
        {
            WorldOriginX = originX;
            WorldOriginZ = originZ;
            ApplyPosition();
        }

        void ApplyPosition()
        {
            if (State == null) return;
            transform.position = new Vector3(
                (float)(State.X - WorldOriginX), (float)State.Y, (float)(State.Z - WorldOriginZ));
        }

        public void Tick(float deltaSeconds)
        {
            if (State == null || deltaSeconds < 0f) return;
            int elapsedMs = Mathf.RoundToInt(deltaSeconds * 1000f);
            State.AdvanceTime(elapsedMs);
            Ai?.Tick(State, elapsedMs, this);
            UpdateVisual(deltaSeconds);
        }

        void UpdateVisual(float deltaSeconds)
        {
            if (View == null || !View.Ready) return;
            var position = transform.position;
            float moved = Vector3.Distance(position, new Vector3(
                (float)(State.X - WorldOriginX), (float)State.Y, (float)(State.Z - WorldOriginZ)));
            _lastDistance = moved;
            var state = moved > 1e-4f
                ? new AlphaLocomotionState { SpeedXY = (float)WalkSpeed, Amplitude = 0.19f, OnGround = true }
                : AlphaLocomotionState.Standing;
            View.ApplyLocomotion(state);
            View.Tick(deltaSeconds);
        }

        // ---------------------------------------------------------------- ICreatureAiWorld
        public bool TryGetTarget(string id, out CreatureTarget target)
        {
            if (!string.IsNullOrEmpty(id) && CreatureTargets.TryGet(id, out var source) && source.Alive)
            {
                var p = source.Position;
                target = new CreatureTarget { Id = id, X = p.x, Z = p.z, Reachable = true };
                return true;
            }
            target = default;
            return false;
        }

        public string FindNearestHostile(CreatureState self, double radius) =>
            CreatureTargets.NearestHostile(self.Faction, transform.position, (float)radius);

        public bool TryBeginAction(CreatureState self, int actionCode, out string reason)
        {
            reason = null;
            if (View != null && View.Ready)
            {
                if (View.TryPlayAction(actionCode, out reason))
                {
                    ActionRequested?.Invoke(this, actionCode, "visual");
                    return true;
                }
            }
            if (ActionRequested != null)
            {
                ActionRequested(this, actionCode, "game");
                reason = "GAME_HANDLED";
                return true;
            }
            reason = reason ?? "NO_ACTION_VISUAL";
            return false;
        }

        public void MoveTowards(CreatureState self, double targetX, double targetZ, double speed, int elapsedMs)
        {
            var p = transform.position;
            var target = new Vector3((float)(targetX - WorldOriginX), p.y, (float)(targetZ - WorldOriginZ));
            float step = (float)(speed * (elapsedMs / 1000.0));
            var next = Vector3.MoveTowards(p, target, step);
            transform.position = next;
            self.X = next.x + WorldOriginX;
            self.Y = next.y;
            self.Z = next.z + WorldOriginZ;
            LastMoveFrames++;
        }

        public bool HasLifePotion(CreatureState self) => LifePotionQuery != null && LifePotionQuery(self);
        public bool TryConsumeLifePotion(CreatureState self) => LifePotionConsume != null && LifePotionConsume(self);
        public int CurrentActionDurationMs(CreatureState self) => ActionDurationQuery != null ? ActionDurationQuery(self) : 0;

        public int[] AvailableActions(CreatureState self)
        {
            if (View != null && View.Ready && View.AvailableActions.Length > 0)
            {
                var codes = new List<int>(View.AvailableActions.Length);
                foreach (var clip in View.AvailableActions)
                {
                    int code = AlphaActorView.ActionCodeOf(clip);
                    if (code >= 0) codes.Add(code);
                }
                if (codes.Count > 0) return codes.ToArray();
            }
            return CreatureKinds.ActionsOf(self.Kind);
        }

        public uint NextRoll()
        {
            // Deterministic xorshift-style roll of this agent; CRT-like, never System.Random.
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return _rng;
        }

        void OnDestroy()
        {
            if (State != null) Live.Remove(State.StableId);
        }
    }
}
