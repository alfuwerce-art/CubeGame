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

        /// <summary>Optional world object name. Its table ID is separate from the creature kind namespace.</summary>
        public string WorldObjectName { get; set; }

        public int? WorldObjectId { get; set; }

        public WorldPosition Position { get; set; }

        public float Yaw { get; set; }

        /// <summary>Visual appearance seed (hair colour etc.; Alpha: server rand() per NPC).</summary>
        public uint AppearanceSeed { get; set; }

        /// <summary>Hero editor choice (C1): race, gender, face, haircut, hair colour. Null for creatures.</summary>
        public HeroAppearance? Appearance { get; set; }
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

        /// <summary>Alpha material byte +0x0D of the item (selects the model, item_icon_id 0x4EC400).</summary>
        public int Material { get; set; }

        /// <summary>Alpha byte +0x0C of the item (rarity; read by the model selector).</summary>
        public int Rarity { get; set; }

        /// <summary>Alpha item seed +0x04 (picks the random model variant).</summary>
        public int Seed { get; set; }
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

        /// <summary>Serial of the attack (swing or released projectile) that produced the hit (M0-F2B).</summary>
        public long AttackSerial { get; set; }
    }

    /// <summary>
    /// A projectile (or the staff hit volume) entered the game (M0-F2B). Game-space position and velocity in
    /// blocks / blocks per second; <see cref="Kind"/> is the Alpha projectile type byte (0 arrow, 1 magic,
    /// 2 boomerang; 100 = CubeGame staff volume).
    /// </summary>
    public sealed class ProjectileSpawned : IGameEvent
    {
        public long TimeMs { get; set; }

        public long Projectile { get; set; }

        public StableEntityId Owner { get; set; }

        public int Kind { get; set; }

        public ActionId Action { get; set; }

        public long AttackSerial { get; set; }

        public WorldPosition Position { get; set; }

        public double VelocityX { get; set; }

        public double VelocityY { get; set; }

        public double VelocityZ { get; set; }

        public double Scale { get; set; }
    }

    /// <summary>
    /// A hitscan attack was resolved (M0-F2B): the ray from <see cref="Origin"/> to <see cref="End"/> (stopped by a wall
    /// or at the range). <see cref="Trail"/> is true only for the action ids whose muzzle/trail path is byte-verified.
    /// </summary>
    public sealed class HitscanFired : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Owner { get; set; }

        public ActionId Action { get; set; }

        public long AttackSerial { get; set; }

        public WorldPosition Origin { get; set; }

        public WorldPosition End { get; set; }

        public bool HitWall { get; set; }

        public int Hits { get; set; }

        public bool Trail { get; set; }
    }

    /// <summary>A projectile left the game; published exactly once per projectile.</summary>
    public sealed class ProjectileRemoved : IGameEvent
    {
        public long TimeMs { get; set; }

        public long Projectile { get; set; }

        public StableEntityId Owner { get; set; }

        public int Kind { get; set; }

        /// <summary><c>CubeGame.Features.Combat.ProjectileEndReason</c> as an int.</summary>
        public int Reason { get; set; }

        /// <summary>The creature hit when <see cref="Reason"/> is a target hit; None otherwise.</summary>
        public StableEntityId Target { get; set; }

        public WorldPosition Position { get; set; }
    }

    /// <summary>The hero gained experience (C3, Alpha kill reward 0x595A60) and possibly levels.</summary>
    public sealed class ExperienceGained : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Source { get; set; }

        public long Amount { get; set; }

        public int LevelsGained { get; set; }

        public int Level { get; set; }
    }

    public sealed class EntityDied : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Entity { get; set; }

        public StableEntityId Killer { get; set; }
    }

    /// <summary>An item lies in the world (loot drop). Visual layer shows the item model; gameplay owns pickup.</summary>
    public sealed class WorldItemDropped : IGameEvent
    {
        public long TimeMs { get; set; }

        /// <summary>Runtime id of the item on the ground.</summary>
        public StableEntityId Item { get; set; }

        public RawAlphaItemId Raw { get; set; }

        public int Count { get; set; } = 1;

        public WorldPosition Position { get; set; }

        /// <summary>Alpha material byte +0x0D (0 for raw-only items).</summary>
        public int Material { get; set; }

        /// <summary>Alpha byte +0x0C (rarity).</summary>
        public int Rarity { get; set; }

        /// <summary>Alpha item seed +0x04.</summary>
        public int Seed { get; set; }
    }

    /// <summary>An item left the world (picked up or expired).</summary>
    public sealed class WorldItemRemoved : IGameEvent
    {
        public long TimeMs { get; set; }

        public StableEntityId Item { get; set; }

        /// <summary>Who picked it up; None when it expired.</summary>
        public StableEntityId PickedBy { get; set; }

        public RawAlphaItemId Raw { get; set; }
        public int Count { get; set; }
    }

    public sealed class HeroItemUsed : IGameEvent
    {
        public long TimeMs { get; set; }
        public RawAlphaItemId Raw { get; set; }
        public double HealthRestored { get; set; }
    }

    /// <summary>Live hunt transition. Rehydrating a saved journal emits no transition.</summary>
    public sealed class HuntChanged : IGameEvent
    {
        public long TimeMs { get; set; }
        public KindId Kind { get; set; }
        public bool Complete { get; set; }
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
