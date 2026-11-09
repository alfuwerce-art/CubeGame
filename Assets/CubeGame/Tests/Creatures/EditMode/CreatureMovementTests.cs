// D2 / C2 scale — EditMode checks of the Alpha movement numbers shared by creatures (walk 6, run 12).
using System.Collections.Generic;
using CubeGame.Features.AI;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class CreatureMovementTests
    {
        sealed class ProbeWorld : ICreatureAiWorld
        {
            public double LastStep;
            public double LastSpeed;
            public bool TryGetTarget(string id, out CreatureTarget target)
            {
                target = new CreatureTarget { Id = id, X = 10.0, Z = 0.0, Reachable = true };
                return true;
            }

            public string FindNearestHostile(CreatureState self, double radius) => "player";
            public bool TryBeginAction(CreatureState self, int actionCode, out string reason) { reason = "ACCEPTED"; return true; }
            public void MoveTowards(CreatureState self, double x, double z, double speed, int elapsedMs)
            {
                LastSpeed = speed;
                LastStep = speed * (elapsedMs / 1000.0);
            }

            public bool TryConsumeLifePotion(CreatureState self) => false;
            public bool HasLifePotion(CreatureState self) => false;
            public int CurrentActionDurationMs(CreatureState self) => 0;
            public double AggroRadius => 16.0;
            public double AttackRange => 2.0;
            public double WalkSpeed => 0;   // 0 → the AI must fall back to its Alpha constant
            public int[] AvailableActions(CreatureState self) => new int[0];
            public uint NextRoll() => 3;
        }

        [Test]
        public void MovementNumbersAreTheAlphaOnes()
        {
            Assert.AreEqual(6.0, CreatureMovement.WalkSpeed, 1e-9, "path walk speed of 0x5A7EB0");
            Assert.AreEqual(12.0, CreatureMovement.RunSpeed, 1e-9, "run speed 6 × 2 (0x619DB8)");
            Assert.AreEqual(80.0, CreatureMovement.CombatAcceleration, 1e-9);
            Assert.AreEqual(40.0, CreatureMovement.RetreatAcceleration, 1e-9);
            Assert.AreEqual(CreatureMovement.WalkSpeed, CreatureAi.DefaultWalkSpeed, 1e-9);
            Assert.AreEqual(CreatureMovement.RunSpeed, CreatureAi.DefaultRunSpeed, 1e-9);
            var probe = new UnityEngine.GameObject("movement-probe");
            try
            {
                var agent = probe.AddComponent<CreatureAgent>();
                Assert.AreEqual(CreatureMovement.WalkSpeed, agent.WalkSpeed, 1e-9,
                    "the world agent walks at the Alpha speed, not at the old 3.7 placeholder");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void ChaseStepIsSpeedTimesElapsedTime()
        {
            var ai = new CreatureAi();
            var world = new ProbeWorld();
            var self = new CreatureState
            {
                StableId = "spawn:0:0:0:0:0", Kind = 0x1c, Faction = (int)CreatureFaction.Monster,
                Level = 1, MaxHealth = 100, Health = 100,
            };
            ai.Tick(self, 100, world);
            // One 100 ms tick with the Alpha walk speed moves exactly 0.6 blocks toward the target.
            Assert.AreEqual(CreatureMovement.WalkSpeed, world.LastSpeed, 1e-9, "the AI uses the Alpha speed when the world reports none");
            Assert.AreEqual(0.6, world.LastStep, 1e-6);
        }
    }
}
