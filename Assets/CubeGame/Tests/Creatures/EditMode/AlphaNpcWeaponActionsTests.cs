// D2 / R-2 — EditMode checks of the weapon-gated Alpha actions.
using CubeGame.Features.AI;
using CubeGame.Features.Creatures;
using CubeGame.Features.Contracts;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaNpcWeaponActionsTests
    {
        [Test]
        public void WeaponOnlyActionsComeFromTheItemTable()
        {
            CollectionAssert.IsEmpty(AlphaNpcWeaponActions.Validate());
            Assert.IsNotEmpty(AlphaNpcWeaponActions.WeaponOnly);
            Assert.IsTrue(AlphaNpcWeaponActions.IsWeaponOnly(AlphaNpcWeaponActions.WeaponOnly[0]));
            // Action 3 (unarmed attack of C-29) must not be weapon-gated, otherwise no creature could attack bare-handed.
            Assert.IsFalse(AlphaNpcWeaponActions.IsWeaponOnly(3));
        }

        [Test]
        public void RuleDependsOnAWeaponInHandOnly()
        {
            var unarmed = new CreatureState();
            var armed = new CreatureState { WeaponItemType = 3, WeaponItemSubtype = 0 };
            Assert.IsFalse(AlphaNpcWeaponActions.HasWeapon(unarmed));
            Assert.IsTrue(AlphaNpcWeaponActions.HasWeapon(armed));
            int weaponAction = AlphaNpcWeaponActions.WeaponOnly[0];
            Assert.IsFalse(AlphaNpcWeaponActions.IsAllowed(weaponAction, unarmed));
            Assert.IsTrue(AlphaNpcWeaponActions.IsAllowed(weaponAction, armed));
            Assert.IsTrue(AlphaNpcWeaponActions.IsAllowed(3, unarmed), "unarmed actions stay available");
        }

        sealed class Probe : ICreatureAiWorld
        {
            public readonly System.Collections.Generic.List<int> Requested = new System.Collections.Generic.List<int>();
            public int[] Actions = { 5, 3 };
            public bool TryGetTarget(string id, out CreatureTarget target)
            {
                target = new CreatureTarget { Id = id, X = 0, Z = 0, Reachable = true };
                return true;
            }

            public string FindNearestHostile(CreatureState self, double radius) => "player";
            public bool TryBeginAction(CreatureState self, int actionCode, out string reason)
            {
                Requested.Add(actionCode);
                reason = "ACCEPTED";
                return true;
            }

            public void MoveTowards(CreatureState self, double x, double z, double speed, int elapsedMs) { }
            public bool TryConsumeLifePotion(CreatureState self) => false;
            public bool HasLifePotion(CreatureState self) => false;
            public int CurrentActionDurationMs(CreatureState self) => 0;
            public double AggroRadius => 16.0;
            public double AttackRange => 100.0;
            public double WalkSpeed => 0;
            public int[] AvailableActions(CreatureState self) => Actions;
            public uint NextRoll() => 1;
        }

        [Test]
        public void AiNeverLaunchesAWeaponActionWithoutAWeapon()
        {
            var ai = new CreatureAi();
            // Only a weapon-gated action is in the library: an unarmed creature must not launch it at all.
            var world = new Probe { Actions = new[] { 5 } };
            var self = new CreatureState
            {
                StableId = "spawn:0:0:0:0:0", Kind = 0x02, Faction = (int)CreatureFaction.Monster,
                Level = 1, MaxHealth = 100, Health = 100,
            };
            ai.Tick(self, 100, world);
            CollectionAssert.AreEqual(new[] { 3 }, world.Requested, "selection follows the unarmed Alpha branch");

            // With a weapon in hand the same action is allowed.
            var armedWorld = new Probe { Actions = new[] { 5 } };
            var armed = new CreatureState
            {
                StableId = "spawn:0:0:0:0:1", Kind = 0x02, Faction = (int)CreatureFaction.Monster,
                Level = 1, MaxHealth = 100, Health = 100, WeaponItemType = 3, WeaponItemSubtype = 5,
            };
            var armedAi = new CreatureAi();
            armedAi.Tick(armed, 100, armedWorld);
            CollectionAssert.AreEqual(new[] { 5 }, armedWorld.Requested);
        }
    }
}
