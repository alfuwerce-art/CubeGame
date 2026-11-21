// CubeGame.Features.Creatures — Alpha movement numbers of creatures (D2, N-04/N-05 scale of C2).
//
// The player movement physics were ported by Claude in Core/Movement (C2, docs/research/alpha2013/
// C2_ALPHA_MOVEMENT_PHYSICS.md): 1 speed unit = 1 block/s, dt in milliseconds, position in 1/65536 blocks.
// Creatures use the same scale: Creature_findPathToTarget (0x5A7EB0) turns a direction into a velocity of
// 6 blocks/s (path walk) and the run cap of the same scale is 12 blocks/s (max_speed 6.0 × 2.0, 0x619DB8).
// CombatBehavior accelerations 80/40 belong to that scale as well (acceleration in blocks/s²).
//
// Status: ALPHA_CODE for the numbers (addresses above), CUBEGAME_CONTRACT for which of them an AI chooses
// when: Alpha decides walk/run inside the creature tick that is not ported yet (N-05 RandomWalkBehavior
// is only partially reconstructed), so the AI walks at the path speed and keeps the run speed available.
namespace CubeGame.Features.Creatures
{
    /// <summary>Movement constants of Alpha creatures, shared by the AI and the world agent.</summary>
    public static class CreatureMovement
    {
        /// <summary>Path walk speed: 6 blocks/s (Cube.exe 0x5A7EB0, same scale as C2 player movement).</summary>
        public const double WalkSpeed = 6.0;

        /// <summary>Run speed: base max 6.0 × 2.0 = 12 blocks/s (Cube.exe 0x619DB8, C2 report).</summary>
        public const double RunSpeed = 12.0;

        /// <summary>CombatBehaviour acceleration toward its move target, blocks/s² (Cube.exe 0x42CB20 area).</summary>
        public const double CombatAcceleration = 80.0;

        /// <summary>Flee/retreat acceleration of the same behaviour, blocks/s².</summary>
        public const double RetreatAcceleration = 40.0;

        /// <summary>PLACEHOLDER: which of walk/run a creature picks per tick is not ported (N-05).</summary>
        public const string Status =
            "ALPHA_CODE: walk 6 / run 12 blocks/s (C2 scale); ALPHA_CODE: accelerations 80/40; " +
            "walk-vs-run choice CUBEGAME_CONTRACT (N-05 RandomWalkBehaviour not fully reconstructed)";
    }
}
