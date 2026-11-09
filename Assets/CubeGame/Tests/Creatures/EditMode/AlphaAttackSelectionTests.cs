// M0-06C / C-29 — EditMode checks of Alpha's attack-variant selection and of its use by CreatureAi.
//
// The numbers under test come from AlphaAttackSelectionTable, generated and byte-verified by
// tools/alpha_content/attack_selection.py from Cube.exe 0x4452A0 (see the evidence JSON). These tests
// do not re-derive the table from the format — they pin the control flow and the AI integration.
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.AI;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaAttackSelectionTests
    {
        const int NoWeapon = -1;

        [Test]
        public void WeaponThreeSixIsTwoHandedAttackBeforeAnyClassCheck()
        {
            // 0x4452A0..0x4452B8 — checked before class 3 and before kind 0x75/0x56.
            Assert.AreEqual(0x17, AlphaAttackSelection.SelectAttackId(0x75, 3, 0, 3, 6, false));
            Assert.AreEqual(0x17, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, 6, true));
        }

        [Test]
        public void WeaponThreeFiveIsAlwaysAttackFive()
        {
            Assert.AreEqual(5, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, 5, false));
            Assert.AreEqual(5, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, 5, true));
        }

        [Test]
        public void ClassThreeTakesTheClassSpecialisationBranch()
        {
            // No weapon, class 3: default branch 0x25, specialist 0x2B (0x445397..0x4453AB).
            Assert.AreEqual(0x25, AlphaAttackSelection.SelectAttackId(0x02, 3, -1, NoWeapon, NoWeapon, false));
            Assert.AreEqual(0x2B, AlphaAttackSelection.SelectAttackId(0x02, 3, 1, NoWeapon, NoWeapon, false));
        }

        [Test]
        public void Kinds0x75And0x56TakeTheClassSpecialisationBranch()
        {
            Assert.AreEqual(0x25, AlphaAttackSelection.SelectAttackId(0x75, 0, 0, NoWeapon, NoWeapon, false));
            Assert.AreEqual(0x2B, AlphaAttackSelection.SelectAttackId(0x56, 0, 1, NoWeapon, NoWeapon, false));
            // Another kind with no weapon is a plain unarmed creature.
            Assert.AreEqual(3, AlphaAttackSelection.SelectAttackId(0x1c, 0, 0, NoWeapon, NoWeapon, false));
        }

        [Test]
        public void ClassBranchSubtypesChooseAttackBySpecialisation()
        {
            // Subtype 0x0B -> 0x2D, specialist 0x2E.
            Assert.AreEqual(0x2D, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, 0x0B, false));
            Assert.AreEqual(0x2E, AlphaAttackSelection.SelectAttackId(0x02, 0, 1, 3, 0x0B, false));
            // Subtype 0x0A -> 0x1F, specialist 0x21.
            Assert.AreEqual(0x1F, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, 0x0A, false));
            Assert.AreEqual(0x21, AlphaAttackSelection.SelectAttackId(0x02, 0, 1, 3, 0x0A, false));
            // Subtype 0x0C -> default branch.
            Assert.AreEqual(0x25, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, 0x0C, false));
            Assert.AreEqual(0x2B, AlphaAttackSelection.SelectAttackId(0x02, 0, 1, 3, 0x0C, false));
        }

        [Test]
        public void ParityWeaponSubtypesDifferOnlyByTheParityBit()
        {
            // Alpha's byte sequence compares all ten subtypes with bit0, but four of them never reach that
            // comparison: (3,6) is answered first with 0x17, subtype 5 with attack 5, and subtypes 0x0A/0x0B
            // jump to the class/specialisation branch. The table keeps Alpha's list; the order is Alpha's too.
            foreach (int subtype in AlphaAttackSelectionTable.ParitySubtypes)
            {
                int even;
                int odd;
                if (subtype == AlphaAttackSelectionTable.TwoHandedSubtype)
                {
                    even = odd = AlphaAttackSelectionTable.TwoHandedAttack;
                }
                else if (subtype == AlphaAttackSelectionTable.ModeSwitchSubtype)
                {
                    even = odd = AlphaAttackSelectionTable.ModeSwitchAttack;
                }
                else if (subtype == AlphaAttackSelectionTable.CastSubtype)
                {
                    even = AlphaAttackSelectionTable.CastAttackDefault;
                    odd = AlphaAttackSelectionTable.CastAttackDefault;
                }
                else if (subtype == AlphaAttackSelectionTable.BleedSubtype)
                {
                    even = AlphaAttackSelectionTable.BleedAttackDefault;
                    odd = AlphaAttackSelectionTable.BleedAttackDefault;
                }
                else
                {
                    even = AlphaAttackSelectionTable.ParityAttackEven;
                    odd = AlphaAttackSelectionTable.ParityAttackOdd;
                }

                Assert.AreEqual(even, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, subtype, false), $"subtype 0x{subtype:X2} parity 0");
                Assert.AreEqual(odd, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, subtype, true), $"subtype 0x{subtype:X2} parity 1");
            }

            // The parity values actually reachable in Alpha: bit0 decides 0x42/0x41.
            Assert.AreEqual(0x42, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, 0x0F, false));
            Assert.AreEqual(0x41, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, 0x0F, true));
        }

        [Test]
        public void UnarmedAndUnknownWeaponsFallBackToAttackThree()
        {
            Assert.AreEqual(3, AlphaAttackSelection.SelectAttackId(0x1c, 0, -1, NoWeapon, NoWeapon, false));
            Assert.AreEqual(4, AlphaAttackSelection.SelectAttackId(0x1c, 0, -1, NoWeapon, NoWeapon, true));
            // A non-weapon item in the hand (type != 3) is the unarmed branch as well.
            Assert.AreEqual(3, AlphaAttackSelection.SelectAttackId(0x1c, 0, -1, 1, 4, false));
            // An unrecognised weapon subtype is neither parity nor class branch.
            Assert.AreEqual(3, AlphaAttackSelection.SelectAttackId(0x02, 0, 0, 3, 0x01, false));
        }

        [Test]
        public void GeneratedTableKeepsItsProvenance()
        {
            Assert.AreEqual("ALPHA_CODE_STATIC", AlphaAttackSelectionTable.Status);
            Assert.AreEqual(64, AlphaAttackSelectionTable.CubeSha256.Length);
            Assert.AreEqual(3, AlphaAttackSelectionTable.WeaponItemType);
            CollectionAssert.AreEquivalent(new[] { 0x75, 0x56 }, AlphaAttackSelectionTable.SpecialKinds);
            CollectionAssert.AreEquivalent(new[] { 0x0A, 0x0B, 0x0C }, AlphaAttackSelectionTable.ClassBranchSubtypes);
            Assert.AreEqual(10, AlphaAttackSelectionTable.ParitySubtypes.Length);
        }
    }

    public class CreatureAttackChoiceTests
    {
        sealed class ScriptedWorld : ICreatureAiWorld
        {
            public readonly List<int> Requested = new List<int>();
            public int[] Actions = { 3, 4 };

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
            public double WalkSpeed => 3.7;
            public int[] AvailableActions(CreatureState self) => Actions;
            public uint NextRoll() => 7;
        }

        static CreatureState Creature(int kind, int weaponType = -1, int weaponSubtype = -1)
        {
            return new CreatureState
            {
                StableId = "spawn:0:0:0:0:0", Kind = kind, Faction = (int)CreatureFaction.Monster,
                Level = 1, MaxHealth = 100, Health = 100,
                WeaponItemType = weaponType, WeaponItemSubtype = weaponSubtype,
            };
        }

        [Test]
        public void AiRequestsTheC29AttackFirstEvenWhenTheLibraryStartsWithAnotherAction()
        {
            var ai = new CreatureAi();
            var world = new ScriptedWorld { Actions = new[] { 0x0F, 3, 4 } };
            ai.Tick(Creature(0x1c), 100, world);
            Assert.AreEqual(3, ai.LastSelectedAttack, "unarmed wolf -> Alpha attack 3");
            Assert.AreEqual(3, ai.LastRequestedAction);
            CollectionAssert.AreEqual(new[] { 3 }, world.Requested);
            StringAssert.Contains("ACTION_C29", ai.LastReason);
        }

        [Test]
        public void WeaponChangesTheSelectedAttack()
        {
            var ai = new CreatureAi();
            // Weapon (3, 6) = heavy weapon: Alpha launches action 0x17.
            var world = new ScriptedWorld { Actions = new[] { 0x17, 3 } };
            ai.Tick(Creature(0x02, 3, 6), 100, world);
            Assert.AreEqual(0x17, ai.LastSelectedAttack);
            CollectionAssert.AreEqual(new[] { 0x17 }, world.Requested);
        }

        [Test]
        public void MissingC29ActionFallsBackToTheLibraryAndSaysSo()
        {
            var ai = new CreatureAi();
            var world = new ScriptedWorld { Actions = new[] { 54, 6 } };
            ai.Tick(Creature(0x1c), 100, world);
            Assert.AreEqual(3, ai.LastSelectedAttack);
            CollectionAssert.AreEqual(new[] { 54 }, world.Requested);
            StringAssert.Contains("C29_NOT_IN_LIBRARY", ai.LastReason);
        }
    }
}
