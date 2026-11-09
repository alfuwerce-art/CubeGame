// CubeGame.Features.Creatures — the seam between a creature entity and its behaviour (D86 M0-06C).
//
// Features.AI implements ICreatureAi and Feature.Creatures implements ICreatureAiWorld, so the AI
// module depends on the creature contracts and never the other way round (no asmdef cycle).
namespace CubeGame.Features.Creatures
{
    public enum CreatureAiState
    {
        Idle = 0,
        Wander = 1,
        Chase = 2,
        Attack = 3,
        Flee = 4,
        Dead = 5,
    }

    public struct CreatureTarget
    {
        public string Id;
        public double X;
        public double Z;
        public bool Reachable;
    }

    /// <summary>Everything the AI may read from or ask of the game world. No damage, no HP writes.</summary>
    public interface ICreatureAiWorld
    {
        bool TryGetTarget(string id, out CreatureTarget target);
        string FindNearestHostile(CreatureState self, double radius);
        /// <summary>Ask the game to begin an Alpha action; the game owns resources/hit resolution.</summary>
        bool TryBeginAction(CreatureState self, int actionCode, out string reason);
        /// <summary>Move the creature for one tick; ignored when the creature is not a mover.</summary>
        void MoveTowards(CreatureState self, double targetX, double targetZ, double speed, int elapsedMs);
        /// <summary>Consume a LifePotion (1,1) if the game's inventory really has one.</summary>
        bool TryConsumeLifePotion(CreatureState self);
        /// <summary>The game's inventory says a LifePotion (1,1) is available.</summary>
        bool HasLifePotion(CreatureState self);
        /// <summary>Attack timing of the currently running action (combat_getAttackTiming); 0 when idle.</summary>
        int CurrentActionDurationMs(CreatureState self);
        double AggroRadius { get; }
        double AttackRange { get; }
        double WalkSpeed { get; }
        /// <summary>
        /// Alpha action codes this creature may request: the actor's evidenced action library when the
        /// visual is present, otherwise the kind's proven profile. Empty means "no action evidence".
        /// </summary>
        int[] AvailableActions(CreatureState self);
        /// <summary>Deterministic roll of the game (CRT-like), never System.Random.</summary>
        uint NextRoll();
    }

    public interface ICreatureAi
    {
        CreatureAiState State { get; }
        string TargetId { get; }
        string LastReason { get; }
        void Tick(CreatureState self, int elapsedMs, ICreatureAiWorld world);
        /// <summary>Incoming melee attack: Alpha dodge rule (rand()%10==0, non-boss, distance &lt; 2).</summary>
        bool TryDodge(CreatureState self, double distance, ICreatureAiWorld world);
    }
}
