using System.Linq;
using CubeGame.Core.Characters;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// P5: Alpha crafting — recipe builder 0x59CFF0, craftable count 0x4A19D0, craft 0x4709C0, stations 0x42F910,
    /// recipes learned at creation and per level 0x47FAE0, formulas 0x47D740, persistence of the learned list.
    /// </summary>
    public sealed class FullM0AlphaCraftingTests
    {
        private static AlphaItemRecord M(int sub, int mod = 0, int rarity = 0) => new AlphaItemRecord(0x0B, sub, rarity: rarity, modifier: mod, level: 1);

        [Test]
        public void RecipesHaveTheAlphaIngredientsAndStations()
        {
            AlphaRecipe potion = AlphaCrafting.Build(new AlphaItemRecord(1, 1, level: 1));
            Assert.AreEqual(AlphaCraftStation.None, potion.Station);
            CollectionAssert.AreEqual(new[] { (M(0x16), 1), (M(0x1A), 1) }, potion.Ingredients.ToArray());

            AlphaRecipe sword = AlphaCrafting.Build(new AlphaItemRecord(3, 0, 5, 0, 0, 1, 7));
            Assert.AreEqual(AlphaCraftStation.Anvil, sword.Station);
            CollectionAssert.AreEqual(new[] { (M(0x0A, 1), 8) }, sword.Ingredients.ToArray());

            AlphaRecipe bow = AlphaCrafting.Build(new AlphaItemRecord(3, 6, 0, 0, 2, 2, 7));
            Assert.AreEqual(AlphaCraftStation.Workbench, bow.Station);
            CollectionAssert.AreEqual(new[] { (M(0x0A, 2), 15), (M(0x09, 0x1A), 1), (M(0, 0x0E, 2), 2) }, bow.Ingredients.ToArray(),
                "rarity 2 adds two rare nuggets");

            AlphaRecipe leather = AlphaCrafting.Build(new AlphaItemRecord(4, 0, 0, 0, 0, 0x1A, 3));
            Assert.AreEqual(AlphaCraftStation.Loom, leather.Station);
            CollectionAssert.AreEqual(new[] { (M(0x09, 0x1A), 20) }, leather.Ingredients.ToArray());

            Assert.AreEqual(AlphaCraftStation.Furnace, AlphaCrafting.Build(M(0x0A, 1)).Station);
            CollectionAssert.AreEqual(new[] { (M(0, 1), 1) }, AlphaCrafting.Build(M(0x0A, 1)).Ingredients.ToArray());
            Assert.AreEqual(AlphaCraftStation.Saw, AlphaCrafting.Build(M(0x0A, 2)).Station);
            CollectionAssert.AreEqual(new[] { (M(1, 2), 1) }, AlphaCrafting.Build(M(0x0A, 2)).Ingredients.ToArray());
            Assert.AreEqual(AlphaCraftStation.Water, AlphaCrafting.Build(M(0x1A)).Station);
            Assert.AreEqual(AlphaCraftStation.SpinningWheel, AlphaCrafting.Build(M(9, 0x19)).Station);
            Assert.AreEqual(AlphaCraftStation.Campfire, AlphaCrafting.Build(new AlphaItemRecord(1, 6, level: 1)).Station);

            Assert.AreEqual(0, AlphaCrafting.Category(new AlphaItemRecord(3, 0)));
            Assert.AreEqual(1, AlphaCrafting.Category(new AlphaItemRecord(6, 0)));
            Assert.AreEqual(2, AlphaCrafting.Category(new AlphaItemRecord(9, 0)));
            Assert.AreEqual(4, AlphaCrafting.Category(new AlphaItemRecord(1, 2)));
            Assert.AreEqual(3, AlphaCrafting.Category(new AlphaItemRecord(1, 4)));
            Assert.AreEqual(5, AlphaCrafting.Category(M(0x0A)));
        }

        [Test]
        public void LevelOneTeachesTheAlphaStarterRecipes()
        {
            var warrior = AlphaCrafting.LearnedAtLevel(new AlphaCrtRandom(1), 1, 1);
            Assert.Contains(new AlphaItemRecord(1, 1, level: 1), warrior);
            Assert.Contains(new AlphaItemRecord(1, 4, level: 1), warrior);
            Assert.Contains(new AlphaItemRecord(1, 8, level: 1), warrior);
            Assert.Contains(M(0x1A), warrior);
            Assert.Contains(M(0x16), warrior);
            foreach (int mod in new[] { 1, 2, 0x0B, 0x0C }) Assert.Contains(M(0x0A, mod), warrior);
            Assert.IsFalse(warrior.Any(r => r.Type == 0x0B && r.Subtype == 9), "warriors learn no yarn");
            AlphaItemRecord chest = warrior.Single(r => r.Type == 4);
            Assert.AreEqual(1, chest.Modifier, "level 1 % 4 = 1 teaches a chest of the class material");
            AlphaItemRecord weapon = warrior.Single(r => r.Type == 3);
            Assert.That(weapon.Subtype, Is.InRange(0, 2), "level 1 % 3 = 1 teaches a one-hander");
            Assert.That(chest.Seed, Is.InRange(0, 10));

            var mage = AlphaCrafting.LearnedAtLevel(new AlphaCrtRandom(9), 2, 3);
            Assert.Contains(new AlphaItemRecord(1, 9, level: 2), mage);
            Assert.AreEqual(0x19, mage.Single(r => r.Type == 6).Modifier, "level 2 % 4 = 2 teaches boots");
            AlphaItemRecord bracelet = mage.Single(r => r.Type == 3);
            Assert.AreEqual(0x0C, bracelet.Subtype);
            Assert.That(bracelet.Modifier, Is.EqualTo(0x0B).Or.EqualTo(0x0C));
        }

        [Test]
        public void SessionCraftsWithIngredientsAndStations()
        {
            GameSession session = Running();
            HeroState hero = session.Hero;
            Assert.IsTrue(hero.KnownRecipes.Contains(new AlphaItemRecord(1, 1, level: 1)), "new heroes know the Life Potion");
            int potions = hero.Inventory.CountOf(LootRules.LifePotion);
            Assert.IsFalse(session.TryBrewLifePotion());
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromAlpha(M(0x16), 1)));
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromAlpha(M(0x1A), 1)));
            Assert.IsTrue(session.TryBrewLifePotion(), session.LastRejection);
            Assert.AreEqual(potions + 1, hero.Inventory.CountOf(LootRules.LifePotion));

            int iron = hero.KnownRecipes.IndexOf(M(0x0A, 1));
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromAlpha(M(0, 1), 2)));
            Assert.IsFalse(session.TryCraft(iron), "no furnace in reach");
            StringAssert.Contains("Furnace", session.LastRejection);
            session.StationNear = s => s == AlphaCraftStation.Furnace;
            Assert.IsTrue(session.TryCraft(iron), session.LastRejection);
            Assert.AreEqual(1, AlphaCrafting.CountOf(hero.Inventory, M(0, 1)), "one nugget used");
            Assert.That(AlphaCrafting.CountOf(hero.Inventory, M(0x0A, 1)), Is.InRange(1, 4), "Alpha material bonus rolls");
        }

        [Test]
        public void FormulasTeachTheirTargetOnce()
        {
            GameSession session = Running();
            HeroState hero = session.Hero;
            var formula = new AlphaItemRecord(AlphaLoot.TypeFormula, 0x0F, 33, 3, 1, 1, 12);
            Assert.IsTrue(hero.Inventory.TryAdd(InventoryEntry.FromAlpha(formula, 2)));
            int slot = hero.Inventory.FindFirst(new RawAlphaItemId(AlphaLoot.TypeFormula, 0x0F));
            Assert.IsTrue(session.TryLearnFormula(slot), session.LastRejection);
            Assert.Contains(new AlphaItemRecord(3, 0x0F, 33, 0, 1, 1, 12), hero.KnownRecipes);
            Assert.AreEqual(1, hero.Inventory[slot].Count);
            Assert.IsFalse(session.TryLearnFormula(slot), "already known");
            Assert.AreEqual(1, hero.Inventory[slot].Count);
        }

        [Test]
        public void LearnedRecipesSurviveTheSessionFileAndLevelUps()
        {
            GameSession session = Running();
            HeroState hero = session.Hero;
            int known = hero.KnownRecipes.Count;
            hero.LearnRecipesForLevel(2);
            Assert.Greater(hero.KnownRecipes.Count, known);

            var data = new SessionSaveData();
            GameSlotStore.WriteRecipes(hero, data);
            var copy = new HeroState("Copy", 0, new CharacterSheet());
            GameSlotStore.RestoreRecipes(copy, data);
            CollectionAssert.AreEqual(hero.KnownRecipes, copy.KnownRecipes);

            var legacy = new HeroState("Old", 0, new CharacterSheet());
            GameSlotStore.RestoreRecipes(legacy, new SessionSaveData());
            Assert.IsTrue(legacy.KnownRecipes.Contains(new AlphaItemRecord(1, 1, level: 1)), "pre-P5 slots relearn levels 1…L");
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
