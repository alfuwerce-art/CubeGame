using System.Collections.Generic;
using System.Linq;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// P5: Alpha vendors — stock generators 0x5C9B30 / 0x5CA0A0 / 0x5C8390, buy/sell at the full value, the ten-entry
    /// buyback list, identification (type 0x0E, max(1, value/2) copper) and adaptation (0x40F4F0, platinum).
    /// </summary>
    public sealed class FullM0AlphaShopTests
    {
        private static AlphaCrtRandom Rng(int seed) => new AlphaCrtRandom(seed);

        [Test]
        public void TierLevelsFollowTheInverseCurve()
        {
            var rng = Rng(3);
            for (int round = 0; round < 50; round++)
            {
                Assert.AreEqual(1, AlphaShop.TierLevel(rng, 0));
                for (int tier = 1; tier < 20; tier++)
                {
                    int low = (int)AlphaLoot.CurveInverse20P1(tier / 30f);
                    int high = (int)AlphaLoot.CurveInverse20P1((tier + 0.99999f) / 30f);
                    Assert.That(AlphaShop.TierLevel(rng, tier), Is.InRange(System.Math.Max(1, low), high));
                }
            }
        }

        [Test]
        public void ArmourAndWeaponStockHoldOneToThreeItemsPerTier()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                List<AlphaItemRecord> armour = AlphaShop.Stock(Rng(seed), AlphaShop.ArmorVendor);
                Assert.That(armour.Count, Is.InRange(30, 90));
                foreach (AlphaItemRecord item in armour)
                {
                    Assert.That(item.Type, Is.InRange(4, 9));
                    if (item.Type <= 7) Assert.That(new[] { 0x01, 0x19, 0x1A, 0x1B }, Does.Contain(item.Modifier));
                    else Assert.That(item.Modifier, Is.EqualTo(0x0B).Or.EqualTo(0x0C));
                }

                List<AlphaItemRecord> weapons = AlphaShop.Stock(Rng(seed), AlphaShop.WeaponVendor);
                Assert.That(weapons.Count, Is.InRange(30, 90));
                Assert.IsTrue(weapons.All(w => w.Type == 3 && w.Subtype != 7), "no crossbow in the Alpha pool");
                CollectionAssert.AreEqual(weapons, AlphaShop.Stock(Rng(seed), AlphaShop.WeaponVendor2), "0x81 and 0x83 share 0x5CA0A0");
                Assert.IsEmpty(AlphaShop.Stock(Rng(seed), 0x84), "the innkeeper has no stock");
            }
        }

        [Test]
        public void ItemVendorStartsWithTheTierZeroGoods()
        {
            List<AlphaItemRecord> stock = AlphaShop.ItemStock(Rng(11));
            Assert.AreEqual(new AlphaItemRecord(0x0B, 0x0C, modifier: 0x18, level: 1), stock[0]);
            Assert.AreEqual(new AlphaItemRecord(0x18, 0, modifier: 0x01, level: 1), stock[1]);
            Assert.IsTrue(stock.Any(r => r.Type == 0x17 && r.Subtype == 0), "hang glider");
            Assert.IsTrue(stock.Any(r => r.Type == 0x17 && r.Subtype == 1), "boat");
            Assert.AreEqual(1, stock.Count(r => r.Type == AlphaLoot.TypeFood));
            foreach (AlphaItemRecord r in stock.Where(r => r.Type == AlphaLoot.TypeFormula))
            {
                Assert.That(r.Variant, Is.InRange(3, 9), "formula keeps its target type");
            }

            Assert.IsTrue(stock.All(r => r.Type != 1 || r.Subtype == 7), "the only consumable is the bomb");
            Assert.IsFalse(stock.Any(r => r.Type == 1 && r.Subtype == 1), "Alpha vendors do not sell life potions");
        }

        [TestCase(1, 0, 3, 4)]
        [TestCase(5, 2, 6, 5)]
        [TestCase(7, 0, 7, 0)]
        public void AdaptPriceFollowsComputeLevelDiffBonus(int itemLevel, int rarity, int heroLevel, int price)
        {
            Assert.AreEqual(price, AlphaShop.AdaptPrice(itemLevel, rarity, heroLevel));
        }

        [Test]
        public void SellingFillsTheBuybackListAndBuybackRestoresTheItem()
        {
            GameSession session = Running();
            HeroState hero = session.Hero;
            var potion = InventoryEntry.FromRaw(LootRules.LifePotion, 1);
            for (int i = 0; i < 12; i++)
            {
                Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromAlpha(new AlphaItemRecord(0x0B, 0, modifier: 0x01, level: 1), 1)));
                int slot = hero.Inventory.FindFirst(new RawAlphaItemId(0x0B, 0));
                Assert.IsTrue(session.TrySell(slot), session.LastRejection);
            }

            Assert.AreEqual(AlphaShop.BuybackCapacity, session.Buyback.Count, "oldest entries drop after ten");
            int copper = hero.Copper;
            int price = AlphaTradeValue.Of(session.Buyback[0]);
            Assert.IsTrue(session.TryBuyBack(0), session.LastRejection);
            Assert.AreEqual(copper - price, hero.Copper);
            Assert.AreEqual(AlphaShop.BuybackCapacity - 1, session.Buyback.Count);
            Assert.AreEqual(1, hero.Inventory.CountOf(new RawAlphaItemId(0x0B, 0)));
            Assert.AreEqual(2, AlphaTradeValue.Of(potion));
        }

        [Test]
        public void IdentificationTurnsAnUnidentifiedItemIntoAClassItem()
        {
            GameSession session = Running();
            HeroState hero = session.Hero;
            Assert.IsTrue(hero.TryCollect(InventoryEntry.FromAlpha(new AlphaItemRecord(0x0C, 0, modifier: 0x0A, level: 100))));
            var unknown = new AlphaItemRecord(AlphaLoot.TypeLeftovers, 0, 1234, 0, 2, 0, 4);
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromAlpha(unknown)));
            int slot = hero.Inventory.FindFirst(new RawAlphaItemId(AlphaLoot.TypeLeftovers, 0));
            int price = AlphaShop.IdentifyPrice(hero.Inventory[slot]);
            var before = hero.Inventory.AllCoreItems().ToList();
            Assert.IsTrue(session.TryIdentify(slot), session.LastRejection);
            Assert.AreEqual(100 - price, hero.Copper);
            Assert.AreEqual(-1, hero.Inventory.FindFirst(new RawAlphaItemId(AlphaLoot.TypeLeftovers, 0)));
            ItemInstance made = hero.Inventory.AllCoreItems().Single(i => !before.Contains(i));
            Assert.AreEqual(4, made.Level);
            Assert.AreEqual(2, made.Rarity);
        }

        [Test]
        public void AdaptationRaisesTheItemLevelForPlatinum()
        {
            GameSession session = Running();
            HeroState hero = session.Hero;
            hero.Sheet.AddExperience(2000);
            int heroLevel = hero.Sheet.Progression.Level;
            Assert.Greater(heroLevel, 1);
            var sword = new ItemInstance(ItemType.Weapon, 0, 1, 0, 5);
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromCore(sword)));
            int slot = Enumerable.Range(0, HeroInventory.Capacity).First(i => ReferenceEquals(hero.Inventory[i]?.Core, sword));
            int price = AlphaShop.AdaptPrice(1, 0, heroLevel);
            Assert.IsFalse(session.TryAdapt(slot), "no platinum yet");
            Assert.IsTrue(hero.TryCollect(InventoryEntry.FromAlpha(new AlphaItemRecord(0x0D, 0, level: price))));
            Assert.IsTrue(session.TryAdapt(slot), session.LastRejection);
            Assert.AreEqual(heroLevel, hero.Inventory[slot].Core.Level);
            Assert.AreEqual(0, hero.Platinum);
        }

        private static GameSession Running()
        {
            var session = new GameSession();
            Assert.IsTrue(session.EnterMainMenu());
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { GeneratorId = "test" }, 0));
            Assert.IsTrue(session.CompleteLoading());
            return session;
        }
    }
}
