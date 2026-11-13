using System;
using System.Collections.Generic;
using System.Linq;
using CubeGame.Core.Characters;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// P5: Alpha item generation, death/chest/mission loot and wallet semantics (Cube.exe 0x59B180, 0x5F51E0,
    /// 0x5F8AD0, 0x5F8750, 0x5C9F90, 0x5F8530, 0x4C7E80, 0x596919, 0x46EBE0). Golden values for seed 1 are worked by
    /// hand from the MSVC rand sequence 41, 18467, 6334, 26500, 19169, 15724, 11478, … and the disassembly.
    /// </summary>
    public sealed class FullM0AlphaLootTests
    {
        private static AlphaCrtRandom Rng(int seed) => new AlphaCrtRandom(seed);

        [Test]
        public void ChestSlotsFollowTheAlphaRollOrderForSeedOne()
        {
            var rng = Rng(1);
            var expected = new[]
            {
                (0x0C, 0, AlphaLoot.CoinCopper, 2), (0, 0, 0, 1), (0x0C, 0, AlphaLoot.CoinSilver, 1),
                (0x0C, 0, AlphaLoot.CoinCopper, 3), (0x0C, 0, AlphaLoot.CoinCopper, 3), (0, 0, 0, 1),
                (0x0C, 0, AlphaLoot.CoinCopper, 2), (0x0C, 0, AlphaLoot.CoinCopper, 2), (0x0C, 0, AlphaLoot.CoinCopper, 2),
            };
            foreach (var (type, subtype, modifier, level) in expected)
            {
                AlphaItemRecord slot = AlphaLoot.ChestSlot(rng, 1);
                Assert.AreEqual(type, slot.Type, slot.ToString());
                Assert.AreEqual(subtype, slot.Subtype, slot.ToString());
                if (type != 0)
                {
                    Assert.AreEqual(modifier, slot.Modifier, slot.ToString());
                    Assert.AreEqual(level, slot.Level, slot.ToString());
                }
            }
        }

        [Test]
        public void MissionRewardForSeedOneIsPlatinumAndAWarriorMace()
        {
            List<AlphaLootDrop> reward = AlphaLoot.MissionReward(Rng(1), 1, 0, 1);
            Assert.AreEqual(2, reward.Count);
            Assert.AreEqual(AlphaLoot.TypePlatinum, reward[0].Record.Type);
            Assert.AreEqual(1, reward[0].Record.Level, "int(curve_level05(1)*50 + 1) = 1");
            Assert.AreEqual(new AlphaItemRecord(3, 2, 34, 0, 0, 0x01, 1), reward[1].Record);
        }

        [TestCase(1, 0, 1)]
        [TestCase(10, 2, 18)]
        [TestCase(50, 4, 37)]
        public void MissionPlatinumUsesTheAlphaCurve(int level, int stars, int platinum)
        {
            Assert.AreEqual(platinum, AlphaLoot.MissionReward(Rng(7), level, stars, 2)[0].Record.Level);
        }

        [Test]
        public void ClassPoolsOnlyContainTheAlphaItemsOfThatClass()
        {
            var weapons = new Dictionary<int, HashSet<int>>
            {
                [1] = new HashSet<int> { 0, 1, 2, 0x0D, 0x0F, 0x10, 0x11 },
                [2] = new HashSet<int> { 6, 8 },
                [3] = new HashSet<int> { 0x0A, 0x0B, 0x0C },
                [4] = new HashSet<int> { 3, 4, 5 },
            };
            var armourMaterial = new Dictionary<int, int> { [1] = 0x01, [2] = 0x1A, [3] = 0x19, [4] = 0x1B };
            for (int alphaClass = 1; alphaClass <= 4; alphaClass++)
            {
                var seenWeapons = new HashSet<int>();
                var seenArmour = new HashSet<int>();
                for (int seed = 0; seed < 2000; seed++)
                {
                    AlphaItemRecord weapon = AlphaLoot.GenerateWeapon(Rng(seed), 5, 1, alphaClass);
                    Assert.AreEqual(3, weapon.Type);
                    Assert.IsTrue(weapons[alphaClass].Contains(weapon.Subtype), $"class {alphaClass}: {weapon}");
                    Assert.That(weapon.Seed, Is.InRange(0, 99));
                    seenWeapons.Add(weapon.Subtype);

                    AlphaItemRecord armour = AlphaLoot.GenerateArmor(Rng(seed), 5, 1, alphaClass);
                    seenArmour.Add(armour.Type);
                    if (armour.Type >= 4 && armour.Type <= 7)
                    {
                        Assert.AreEqual(armourMaterial[alphaClass], armour.Modifier, armour.ToString());
                    }
                    else
                    {
                        Assert.That(armour.Type, Is.EqualTo(8).Or.EqualTo(9));
                        Assert.That(armour.Modifier, Is.EqualTo(0x0B).Or.EqualTo(0x0C));
                    }
                }

                CollectionAssert.AreEquivalent(weapons[alphaClass], seenWeapons, $"class {alphaClass} weapon pool");
                CollectionAssert.AreEquivalent(new[] { 4, 5, 6, 7, 8, 9 }, seenArmour, $"class {alphaClass} armour pool");
            }
        }

        [Test]
        public void ChestSlotDistributionMatchesTheAlphaBranches()
        {
            var rng = Rng(12345);
            int copper = 0, empty = 0, candle = 0, special = 0, potion = 0;
            const int samples = 24000;
            for (int i = 0; i < samples; i++)
            {
                AlphaItemRecord slot = AlphaLoot.ChestSlot(rng, 5);
                if (slot.IsEmpty) empty++;
                else if (slot.Type == AlphaLoot.TypeCandle) { candle++; Assert.That(slot.Seed, Is.InRange(0, 2)); }
                else if (slot.Type == AlphaLoot.TypeCoin && slot.Modifier == AlphaLoot.CoinCopper) copper++;
                else
                {
                    special++;
                    Assert.IsFalse(slot.Type == 1 && slot.Subtype == 4, "case 1 falls through into silver coins");
                    if (slot.Type == 1 && slot.Subtype == 1) potion++;
                }
            }

            Assert.That(copper, Is.InRange(samples * 0.47, samples * 0.53), "1/2 copper");
            Assert.That(empty, Is.InRange(samples * 0.18, samples * 0.24), "5/24 empty");
            Assert.That(candle, Is.InRange(samples * 0.18, samples * 0.24), "5/24 candle");
            Assert.That(special, Is.InRange(samples * 0.07, samples * 0.10), "1/12 consumable or silver");
            Assert.That(potion, Is.InRange(samples * 0.010, samples * 0.024), "1/60 life potion");
        }

        [Test]
        public void RarityRollStaysWithinAlphaBounds()
        {
            var rng = Rng(99);
            int above = 0;
            for (int i = 0; i < 20000; i++)
            {
                int r = AlphaLoot.RollRarity(rng, 0);
                Assert.That(r, Is.InRange(0, 4));
                if (r > 0) above++;
            }

            Assert.That(above, Is.InRange(120, 300), "about 1.1 % above zero for max 0");
            Assert.AreEqual(3, AlphaLoot.RollRarity(Rng(1), 2, true));
            Assert.AreEqual(4, AlphaLoot.RollRarity(Rng(1), 6, true));
        }

        [Test]
        public void LevelRerollStaysNearTheSourceLevel()
        {
            var rng = Rng(5);
            Assert.AreEqual(1, AlphaLoot.RerollLevel(rng, new AlphaItemRecord(3, 0, level: 1), 0.05f, true).Level);
            for (int i = 0; i < 2000; i++)
            {
                int level = AlphaLoot.RerollLevel(rng, new AlphaItemRecord(3, 0, level: 20), 0.05f, true).Level;
                Assert.That(level, Is.InRange(15, 26));
            }
        }

        [Test]
        public void DeathDropsAreDeterministicAndFollowRoleAndFlags()
        {
            var hostile = new AlphaDeathInput { Role = 1, Kind = 0x1C, Level = 6, Stars = 1 };
            string a = string.Join("|", AlphaLoot.DeathDrops(Rng(77), hostile));
            string b = string.Join("|", AlphaLoot.DeathDrops(Rng(77), hostile));
            Assert.AreEqual(a, b);

            Assert.IsEmpty(AlphaLoot.DeathDrops(Rng(77), new AlphaDeathInput { Role = 0, Level = 6 }));
            Assert.IsEmpty(AlphaLoot.DeathDrops(Rng(77), new AlphaDeathInput { Role = 3, Level = 6 }));
            Assert.IsEmpty(AlphaLoot.DeathDrops(Rng(77), new AlphaDeathInput { Role = 1, Level = 6, Flags = 0x800 }));

            float scale = AlphaLoot.Pow2Mul(6, 1) * 10f;
            for (int seed = 0; seed < 500; seed++)
            {
                List<AlphaLootDrop> drops = AlphaLoot.DeathDrops(Rng(seed), hostile);
                long copper = drops.Where(d => d.Record.Type == AlphaLoot.TypeCoin).Sum(d => AlphaWallet.CopperValue(d.Record, d.Count));
                Assert.That(copper, Is.InRange((long)scale - 1, (long)(scale * 3f) + 1), "pow2Mul·10·(1…3)");
                foreach (AlphaLootDrop drop in drops)
                {
                    Assert.That(drop.OffsetX, Is.InRange(-1.0, 1.0));
                    Assert.That(drop.OffsetZ, Is.InRange(-1.0, 1.0));
                    Assert.AreNotEqual(AlphaLoot.TypeMaterial, drop.Record.Type, "kind 0x1C has no material entry");
                }

                List<AlphaLootDrop> peaceful = AlphaLoot.DeathDrops(Rng(seed), new AlphaDeathInput { Role = 2, Kind = 0x1C, Level = 6 });
                Assert.IsFalse(peaceful.Any(d => d.Record.Type == AlphaLoot.TypeCoin), "coins only for hostile actors and chests");
            }

            List<AlphaLootDrop> leftovers = Enumerable.Range(0, 3000)
                .SelectMany(s => AlphaLoot.DeathDrops(Rng(s), new AlphaDeathInput { Role = 1, Kind = 0x1C, Level = 4, Stars = 20, Flags = 0x10 }))
                .ToList();
            Assert.IsFalse(leftovers.Any(d => d.Record.Type == AlphaLoot.TypeCoin), "flags 0x18 replace coins");
            Assert.IsTrue(leftovers.Any(d => d.Record.Type == AlphaLoot.TypeLeftovers));
        }

        [Test]
        public void KindMaterialsDropOneToThreeTimes()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var materials = AlphaLoot.DeathDrops(Rng(seed), new AlphaDeathInput { Role = 2, Kind = 0x84, Level = 3 })
                    .Where(d => d.Record.Type == AlphaLoot.TypeMaterial).ToList();
                Assert.That(materials.Count, Is.InRange(1, 3));
                Assert.IsTrue(materials.All(m => m.Record.Subtype == 0 && m.Record.Modifier == 0x01 && m.Record.Level == 1));
            }

            Assert.AreEqual(new AlphaItemRecord(0x0B, 0, rarity: 4, modifier: 0x10), AlphaLoot.KindMaterial(Rng(1), 0x8A));
            Assert.IsTrue(AlphaLoot.KindMaterial(Rng(1), 0x1C).IsEmpty);
        }

        [Test]
        public void EquippedItemsAndFormulasComeFromTheirAlphaBranches()
        {
            var sword = new AlphaItemRecord(3, 0, 12, 0, 1, 0x01, 6);
            int equipped = 0, formulas = 0;
            for (int seed = 0; seed < 4000; seed++)
            {
                var input = new AlphaDeathInput { Role = 1, Kind = 0x1C, Level = 6, Stars = 0 };
                input.Equipped.Add(sword);
                foreach (AlphaLootDrop drop in AlphaLoot.DeathDrops(Rng(seed), input))
                {
                    if (drop.Source == "death.equipped") { equipped++; Assert.AreEqual(sword, drop.Record); }
                    if (drop.Record.Type == AlphaLoot.TypeFormula)
                    {
                        formulas++;
                        Assert.That(drop.Record.Variant, Is.InRange(3, 9), "formula +0x08 keeps the target type");
                    }
                }
            }

            Assert.That(equipped, Is.InRange(850, 1150), "rand%4 == 0");
            Assert.That(formulas, Is.InRange(50, 115), "rand%50 == 0");
        }

        [Test]
        public void WalletAppliesAlphaCoinMultipliersAndPlatinum()
        {
            var hero = new HeroState("Wallet", 0, new CharacterSheet());
            Assert.IsTrue(hero.TryCollect(InventoryEntry.FromAlpha(new AlphaItemRecord(0x0C, 0, modifier: 0x0A, level: 7))));
            Assert.IsTrue(hero.TryCollect(InventoryEntry.FromAlpha(new AlphaItemRecord(0x0C, 0, modifier: 0x0C, level: 3))));
            Assert.IsTrue(hero.TryCollect(InventoryEntry.FromAlpha(new AlphaItemRecord(0x0C, 0, modifier: 0x0B, level: 2))));
            Assert.IsTrue(hero.TryCollect(InventoryEntry.FromAlpha(new AlphaItemRecord(0x0D, 0, level: 5))));
            Assert.AreEqual(7 + 300 + 20000, hero.Copper);
            Assert.AreEqual(5, hero.Platinum);
            Assert.AreEqual(0, hero.Inventory.UsedSlots, "coins never occupy bag slots");
        }

        [Test]
        public void StacksMergeOnlyWithTheWholeAlphaRecord()
        {
            var bags = new HeroInventory(new EquipmentLoadout());
            var iron = new AlphaItemRecord(0x0B, 0, modifier: 0x01, level: 1);
            var gold = new AlphaItemRecord(0x0B, 0, modifier: 0x0B, level: 1);
            Assert.IsTrue(bags.TryAdd(InventoryEntry.FromAlpha(iron, 3)));
            Assert.IsTrue(bags.TryAdd(InventoryEntry.FromAlpha(iron, 2)));
            Assert.IsTrue(bags.TryAdd(InventoryEntry.FromAlpha(gold, 1)));
            Assert.AreEqual(2, bags.UsedSlots);
            Assert.AreEqual(5, bags[0].Count);
            InventoryEntry taken = bags.TakeFrom(0, 2);
            Assert.AreEqual(iron, taken.Record, "splitting keeps the record");
        }

        [Test]
        public void RawRecordsAndPlatinumSurviveTheSessionArrays()
        {
            var hero = new HeroState("Saver", 0, new CharacterSheet());
            var formula = new AlphaItemRecord(AlphaLoot.TypeFormula, 0x0F, 42, 3, 2, 0x01, 9);
            var material = new AlphaItemRecord(0x0B, 0, rarity: 3, modifier: 0x0F, level: 1);
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromAlpha(formula)));
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromAlpha(material, 4)));
            var data = new SessionSaveData();
            GameSlotStore.WriteRawItems(hero, data);
            var restored = Enumerable.Range(0, data.rawItemTypes.Length).Select(i => GameSlotStore.RawEntryFromSave(data, i)).ToList();
            Assert.IsTrue(restored.Any(e => e.Record.Equals(formula) && e.Count == 1));
            Assert.IsTrue(restored.Any(e => e.Record.Equals(material) && e.Count == 4));

            data.rawItemSeeds = null;
            InventoryEntry legacy = GameSlotStore.RawEntryFromSave(data, Array.IndexOf(data.rawItemTypes, 0x0B));
            Assert.AreEqual(new AlphaItemRecord(0x0B, 0), legacy.Record, "pre-P5 arrays restore the Alpha defaults");
        }

        [Test]
        public void SessionKillDropsAlphaCoinsAndPickupFillsTheWallet()
        {
            var session = new GameSession();
            Assert.IsTrue(session.EnterMainMenu());
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { GeneratorId = "test" }, 0));
            Assert.IsTrue(session.CompleteLoading());
            HeroState hero = session.Hero;
            var wolf = Combatant.CreateCreature(StableEntityId.ForZoneSpawn(new ZoneKey(3, 3), 0, 9), new KindId(0x1C), "Wolf", 4, 0);
            wolf.Position = hero.Position;
            session.Combat.Register(wolf);
            int copper = hero.Copper;
            wolf.Health.ApplyDamage(wolf.Health.CurrentHealth);
            session.Events.Publish(new EntityDied { Entity = wolf.Id, Killer = hero.Id, TimeMs = session.Clock.TimeMs });
            Assert.Greater(session.WorldItems.Count, 0, "Alpha 0x59B180 scatters coins for a hostile kill");
            Assert.IsTrue(session.WorldItems.All.All(g => g.Entry.Raw.Type != 0));
            session.Clock.StepTicks(1);
            Assert.Greater(hero.Copper, copper);
            Assert.AreEqual(0, session.WorldItems.All.Count(g => g.Entry.Raw.Type == AlphaLoot.TypeCoin));
        }
    }
}
