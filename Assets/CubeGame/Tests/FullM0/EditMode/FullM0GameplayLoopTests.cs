using System.Linq;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Core.Progression;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.Session;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    public sealed class FullM0GameplayLoopTests
    {
        [TestCase(ItemType.Weapon, 6, 1, 0, 40)]
        [TestCase(ItemType.Weapon, 1, 1, 4, 40)]
        [TestCase(ItemType.Chest, 0, 11, 0, 40)]
        [TestCase(ItemType.Shoulders, 0, 1, 0, 15)]
        public void TradeUsesAlphaTypeLevelAndRarity(ItemType type, byte subtype, int level, int rarity, int price)
        {
            var item = InventoryEntry.FromCore(new ItemInstance(type, subtype, level, rarity, 1));
            Assert.AreEqual(price, AlphaTradeValue.Of(item));
        }

        [Test]
        public void TradeRefusalsDoNotChangeInventoryOrWallet()
        {
            GameSession session = StartSession();
            HeroState hero = session.Hero;
            var flask = InventoryEntry.FromRaw(new RawAlphaItemId(0x0B, 0x0C), 1);
            int potions = hero.Inventory.CountOf(LootRules.LifePotion);
            Assert.IsFalse(session.TryBuy(flask), "new hero has no money");
            Assert.AreEqual(0, hero.Inventory.CountOf(flask.Raw));

            Assert.IsTrue(session.TrySell(hero.Inventory.FindFirst(LootRules.LifePotion)));
            Assert.AreEqual(potions - 1, hero.Inventory.CountOf(LootRules.LifePotion));
            Assert.AreEqual(AlphaTradeValue.Of(flask), hero.Copper);
            for (int slot = 0; slot < HeroInventory.Capacity; slot++)
            {
                if (hero.Inventory[slot] == null)
                {
                    Assert.IsTrue(hero.Inventory.TryPlace(slot,
                        InventoryEntry.FromRaw(LootRules.LifePotion, HeroInventory.MaxStack)));
                }
            }

            Assert.IsFalse(session.TryBuy(flask), "full bags");
            Assert.AreEqual(AlphaTradeValue.Of(flask), hero.Copper);
            Assert.AreEqual(0, hero.Inventory.CountOf(flask.Raw));
            hero.Inventory.TakeFrom(HeroInventory.Capacity - 1);
            Assert.IsTrue(session.TryBuy(flask));
            Assert.AreEqual(1, hero.Inventory.CountOf(flask.Raw));
            Assert.AreEqual(0, hero.Copper);
        }

        [Test]
        public void PaidResetAndSpecializationCommitAsOneChange()
        {
            GameSession session = StartSession();
            HeroState hero = session.Hero;
            hero.Sheet.AddExperience(AlphaMath.ExperienceCostForNextLevel(1));
            var draft = new HeroSkillRanks();
            Assert.IsTrue(draft.TryRaise(HeroSkill.Climbing, 2));
            Assert.IsTrue(session.TryApplySkillRanks(draft));
            Assert.IsTrue(draft.TryLower(HeroSkill.Climbing));

            Assert.IsFalse(session.TryApplyProgression(draft, CharacterSpecialization.Secondary));
            Assert.AreEqual(CharacterSpecialization.Primary, hero.Sheet.Specialization);
            Assert.AreEqual(1, hero.SkillRanks[HeroSkill.Climbing]);

            var glider = new RawAlphaItemId(0x17, 0);
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromRaw(glider, 1)));
            Assert.IsTrue(session.TrySell(hero.Inventory.FindFirst(glider)));
            Assert.AreEqual(1000, hero.Copper);
            Assert.IsFalse(session.TryApplyProgression(draft, CharacterSpecialization.Secondary), "the matching trainer is required");
            Assert.AreEqual(1000, hero.Copper);
            double previousMax = hero.Health.MaxHealth;
            double previousHealth = hero.Health.CurrentHealth;
            Assert.IsTrue(session.TryApplyProgression(draft, CharacterSpecialization.Secondary, true), session.LastRejection);
            Assert.AreEqual(1000 - (int)(110 * AlphaMath.LevelFactor(2)), hero.Copper);
            Assert.AreEqual(0, hero.SkillRanks[HeroSkill.Climbing]);
            Assert.AreEqual(2, hero.Sheet.Progression.Level);
            Assert.Greater(hero.Health.MaxHealth, previousMax);
            Assert.AreEqual(previousHealth, hero.Health.CurrentHealth, 1e-8);
            Assert.IsTrue(session.Combat.TryGet(hero.Id, out Combatant fighter));
            Assert.AreSame(hero.Sheet, fighter.Sheet);
            Assert.AreSame(hero.Health, fighter.Health);
        }

        [Test]
        public void CraftingConsumesOneOfEachIngredientOnlyIfThePotionFits()
        {
            GameSession session = StartSession();
            HeroInventory bags = session.Hero.Inventory;
            Assert.IsFalse(session.TryBrewLifePotion());
            for (int slot = 0; slot < HeroInventory.Capacity; slot++)
            {
                bags.TakeFrom(slot);
                RawAlphaItemId raw = slot == 0 ? AlphaLifePotionRecipe.Heartflower
                    : slot == 1 ? AlphaLifePotionRecipe.WaterFlask : LootRules.LifePotion;
                int count = slot < 2 ? 2 : HeroInventory.MaxStack;
                Assert.IsTrue(bags.TryPlace(slot, InventoryEntry.FromRaw(raw, count)));
            }

            Assert.IsFalse(session.TryBrewLifePotion());
            Assert.AreEqual(2, bags[0].Count);
            Assert.AreEqual(2, bags[1].Count);
            bags.TakeFrom(0, 1);
            int potions = bags.CountOf(LootRules.LifePotion);
            Assert.IsTrue(session.TryBrewLifePotion(), session.LastRejection);
            Assert.AreEqual(potions + 1, bags.CountOf(LootRules.LifePotion));
            Assert.AreEqual(1, bags.CountOf(AlphaLifePotionRecipe.WaterFlask));
            Assert.AreEqual(0, bags.CountOf(AlphaLifePotionRecipe.Heartflower));
        }

        [Test]
        public void OnlyTheTrackedKillCompletesTheMissionAndRewardsOnce()
        {
            GameSession session = StartSession();
            HeroState hero = session.Hero;
            var home = new ZoneKey(10, 10);
            Combatant first = Spawn(StableEntityId.ForZoneSpawn(home, 0, 0), 8);
            Combatant second = Spawn(StableEntityId.ForZoneSpawn(home, 0, 1), 12);
            Combatant far = Spawn(StableEntityId.ForZoneSpawn(new ZoneKey(18, 18), 0, 0), 100);

            session.Clock.StepTicks(1);
            Assert.AreEqual(first.Id, hero.Missions.Hunts.Single().Target);
            Assert.IsFalse(hero.Missions.Hunts.Single().Complete);
            Kill(second);
            Kill(far);
            Assert.AreEqual(0, hero.Platinum);
            int bagsBefore = hero.Inventory.UsedSlots;
            Kill(first);
            Assert.IsTrue(hero.Missions.Hunts.Single().Complete);
            // Alpha 0x596919: platinum int(curve_level05(1+0)*50+1) = 1 and one class item into the bags.
            Assert.AreEqual(1, hero.Platinum);
            Assert.AreEqual(bagsBefore + 1, hero.Inventory.UsedSlots);
            Assert.AreEqual(0, hero.ManaCubes, "mana cubes are not a mission reward in Alpha");
            Kill(first);
            session.Clock.StepTicks(1);
            Assert.AreEqual(1, hero.Platinum);
            Assert.AreEqual(0L, new HeroSkillRanks().UnspentLevelPoints(1, 3));
            Assert.AreEqual(1L, new HeroSkillRanks().UnspentLevelPoints(1, 4));

            Combatant Spawn(StableEntityId id, int distance)
            {
                Combatant fighter = Combatant.CreateCreature(id, new KindId(0x1C), "Wolf", 1, 0);
                fighter.Position = new WorldPosition(hero.Position.X + distance, hero.Position.Y, hero.Position.Z);
                session.Combat.Register(fighter);
                return fighter;
            }

            void Kill(Combatant fighter)
            {
                fighter.Health.ApplyDamage(fighter.Health.CurrentHealth);
                session.Events.Publish(new EntityDied { Entity = fighter.Id, Killer = hero.Id, TimeMs = session.Clock.TimeMs });
            }
        }

        private static GameSession StartSession()
        {
            var session = new GameSession();
            Assert.IsTrue(session.EnterMainMenu());
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { GeneratorId = "test" }, 0));
            Assert.IsTrue(session.CompleteLoading());
            return session;
        }
    }
}
