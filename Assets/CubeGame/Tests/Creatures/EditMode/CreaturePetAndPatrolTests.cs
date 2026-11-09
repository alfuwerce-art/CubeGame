// D2 — EditMode checks of pets (proven limit/ability) and patrol (CUBEGAME_CONTRACT, N-14 unknown).
using CubeGame.Features.AI;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class CreaturePetRosterTests
    {
        static StableEntityId Pet(int n) => StableEntityId.ForRuntime((ulong)n);

        [Test]
        public void TwoPetsAreTheProvenLimit()
        {
            var roster = new CreaturePetRoster();
            Assert.AreEqual(2, roster.Limit);
            Assert.IsTrue(roster.CanSummon(CreaturePets.SummonAbilityId));
            Assert.IsTrue(roster.TryAdd(Pet(1)));
            Assert.IsTrue(roster.TryAdd(Pet(2)));
            Assert.IsFalse(roster.TryAdd(Pet(3)));
            Assert.AreEqual(2, roster.Count);
            Assert.IsFalse(roster.CanSummon(CreaturePets.SummonAbilityId));
            StringAssert.Contains("PET_LIMIT", roster.ExplainSummon(CreaturePets.SummonAbilityId));
        }

        [Test]
        public void OnlyTheAlphaSummonAbilityAndUniquePetsAreAccepted()
        {
            var roster = new CreaturePetRoster();
            Assert.IsFalse(roster.CanSummon(0x50), "0x50 is the potion action, not a summon");
            StringAssert.Contains("NOT_SUMMON_ABILITY", roster.ExplainSummon(0x50));
            Assert.IsTrue(roster.TryAdd(Pet(7)));
            Assert.IsFalse(roster.TryAdd(Pet(7)), "the same pet cannot be added twice");
            Assert.IsFalse(roster.TryAdd(StableEntityId.None));
            Assert.IsTrue(roster.IsPet(Pet(7)));
            Assert.IsTrue(roster.Remove(Pet(7)));
            Assert.IsFalse(roster.IsPet(Pet(7)));
        }

        [Test]
        public void StatusNamesWhatIsUnknown()
        {
            StringAssert.Contains("UNKNOWN", CreaturePetRoster.Status);
            StringAssert.Contains("no teleport", CreaturePetRoster.Status);
        }
    }

    public class PatrolRouteTests
    {
        sealed class Probe : ICreatureAiWorld
        {
            public int Moves;
            public double LastX, LastZ;
            public bool TryGetTarget(string id, out CreatureTarget target) { target = default; return false; }
            public string FindNearestHostile(CreatureState self, double radius) => null;
            public bool TryBeginAction(CreatureState self, int actionCode, out string reason) { reason = "ACCEPTED"; return true; }
            public void MoveTowards(CreatureState self, double x, double z, double speed, int elapsedMs)
            {
                Moves++;
                LastX = x;
                LastZ = z;
            }

            public bool TryConsumeLifePotion(CreatureState self) => false;
            public bool HasLifePotion(CreatureState self) => false;
            public int CurrentActionDurationMs(CreatureState self) => 0;
            public double AggroRadius => 16.0;
            public double AttackRange => 2.0;
            public double WalkSpeed => 0;
            public int[] AvailableActions(CreatureState self) => new int[0];
            public uint NextRoll() => 5;
        }

        [Test]
        public void RouteIsDeterministicAndCycles()
        {
            var a = new PatrolRoute(10, 20, 4, 4, 42UL);
            var b = new PatrolRoute(10, 20, 4, 4, 42UL);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.Waypoint(i), b.Waypoint(i));
            }

            a.AdvanceIfArrived(a.X, a.Z);
            Assert.AreEqual(1, a.Index, "arriving advances the waypoint");
            for (int i = 0; i < 4; i++) a.AdvanceIfArrived(a.X, a.Z);
            Assert.AreEqual(1, a.Index, "the route cycles back to the same index");
        }

        [Test]
        public void WaypointsStayInsideThePatrolRadius()
        {
            var route = new PatrolRoute(0, 0, 5, 6, 7UL);
            for (int i = 0; i < route.Count; i++)
            {
                var (x, z) = route.Waypoint(i);
                double distance = System.Math.Sqrt(x * x + z * z);
                Assert.LessOrEqual(distance, 5.0 + 1e-9, "jitter never exceeds the radius");
                Assert.Greater(distance, 0.0);
            }
        }

        [Test]
        public void AiFollowsThePatrolWhenItHasNoTarget()
        {
            var ai = new CreatureAi();
            var route = new PatrolRoute(0, 0, 3, 4, 99UL);
            ai.AttachPatrol(route);
            var self = new CreatureState
            {
                StableId = "spawn:0:0:0:0:0", Kind = 0x1c, Faction = (int)CreatureFaction.Monster,
                Level = 1, MaxHealth = 50, Health = 50, X = 0, Z = 0,
            };
            var world = new Probe();
            ai.Tick(self, 100, world);
            Assert.Greater(world.Moves, 0);
            StringAssert.StartsWith("PATROL", ai.LastReason);
            Assert.AreEqual(route.X, world.LastX, 1e-9);
            Assert.AreEqual(route.Z, world.LastZ, 1e-9);
        }
    }
}
