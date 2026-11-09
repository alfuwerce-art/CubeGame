using System;
using System.Collections.Generic;

namespace CubeGame.Features.Contracts
{
    /// <summary>Marker for immutable events published by the gameplay features.</summary>
    public interface IGameEvent
    {
        /// <summary>Game time (GameClock ms) when the event happened.</summary>
        long TimeMs { get; }
    }

    public enum LocomotionMode
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Jump = 3,
        Fall = 4,
        Swim = 5,
        Climb = 6,
        Glide = 7,
        Boat = 8,
        Sit = 9,
        Dead = 10,
    }

    public enum ActionPhase
    {
        Windup = 0,
        Strike = 1,
        Recover = 2,
        Finished = 3,
    }

    public enum ActionOutcome
    {
        Completed = 0,
        Cancelled = 1,
        Interrupted = 2,
        Rejected = 3,
    }

    /// <summary>Equipment slot as seen by the visual layer (Core S04 has 6 domain slots; mapping is in the item adapter).</summary>
    public enum VisualEquipmentSlot
    {
        MainHand = 0,
        OffHand = 1,
        Chest = 2,
        Hands = 3,
        Feet = 4,
        Shoulders = 5,
        Neck = 6,
        Ring1 = 7,
        Ring2 = 8,
        Lamp = 9,
        Pet = 10,
        Special = 11,
        Back = 12,
    }

    public sealed class EntitySpawned : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Entity { get; set; }

        public KindId Kind { get; set; }

        public WorldPosition Position { get; set; }

        public float Yaw { get; set; }

        /// <summary>Visual appearance seed (hair colour etc.; Alpha: server rand() per NPC).</summary>
        public uint AppearanceSeed { get; set; }
    }

    public sealed class EntityDespawned : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Entity { get; set; }
    }

    public sealed class EntityMoved : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Entity { get; set; }

        public WorldPosition Position { get; set; }

        public float Yaw { get; set; }

        public LocomotionMode Mode { get; set; }

        /// <summary>Horizontal speed in blocks/s, used by parametric walk amplitude.</summary>
        public float Speed { get; set; }
    }

    public sealed class EquipmentVisualChanged : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Entity { get; set; }

        public VisualEquipmentSlot Slot { get; set; }

        /// <summary>Item shown in the slot; null when the slot became empty.</summary>
        public RawAlphaItemId? Item { get; set; }
    }

    public sealed class ActionStarted : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Entity { get; set; }

        public ActionId Action { get; set; }

        public StableEntityId Target { get; set; }

        /// <summary>Phase durations in game ms (windup, strike, recover). Visual playback must follow them.</summary>
        public IReadOnlyList<long> PhaseDurationsMs { get; set; } = Array.Empty<long>();

        /// <summary>Animation key for the visual layer; empty when no clip is known (ANIM_MISSING is not an error).</summary>
        public string AnimationKey { get; set; } = string.Empty;
    }

    public sealed class ActionPhaseChanged : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Entity { get; set; }

        public ActionId Action { get; set; }

        public ActionPhase Phase { get; set; }
    }

    public sealed class ActionResolved : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Entity { get; set; }

        public ActionId Action { get; set; }

        public ActionOutcome Outcome { get; set; }

        public string Reason { get; set; } = string.Empty;
    }

    public sealed class HitResolved : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Attacker { get; set; }

        public StableEntityId Target { get; set; }

        public double Damage { get; set; }

        public bool Blocked { get; set; }

        public bool Critical { get; set; }

        public bool Killed { get; set; }

        public double TargetHealthAfter { get; set; }
    }

    public sealed class EntityDied : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Entity { get; set; }

        public StableEntityId Killer { get; set; }
    }

    public sealed class SessionStateChanged : IGameEvent
    {
        public long TimeMs { get; set; }

        public string From { get; set; } = string.Empty;

        public string To { get; set; } = string.Empty;
    }

    /// <summary>
    /// Boundary for the visual runtime (DeepSeek, Presentation/Visual): receives events, never changes
    /// gameplay state (HP, money, inventory, saves). Implementations must tolerate unknown kinds/actions
    /// (log DATA_MISSING / ANIM_MISSING, keep the game running).
    /// </summary>
    public interface IEntityVisualSink
    {
        void OnEntitySpawned(EntitySpawned e);

        void OnEntityDespawned(EntityDespawned e);

        void OnEntityMoved(EntityMoved e);

        void OnEquipmentChanged(EquipmentVisualChanged e);

        void OnActionStarted(ActionStarted e);

        void OnActionPhaseChanged(ActionPhaseChanged e);

        void OnActionResolved(ActionResolved e);

        void OnHitResolved(HitResolved e);

        void OnEntityDied(EntityDied e);

        /// <summary>Called by the presentation loop every rendered frame with the current game time.</summary>
        void OnFrame(long gameTimeMs, double interpolationAlpha);
    }
}
