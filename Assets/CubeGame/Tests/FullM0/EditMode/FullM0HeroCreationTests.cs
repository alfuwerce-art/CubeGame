using System.Collections.Generic;
using System.Linq;
using CubeGame.Core.Characters;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using CubeGame.Persistence;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>C1: hero editor and start kit of Alpha (Cube.exe bytes, see C1_ALPHA_HERO_CREATION.md).</summary>
    public sealed class FullM0HeroCreationTests
    {
        private sealed class MemoryStore : ISaveStore
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

            public bool Exists(string key) => _files.ContainsKey(key);

            public string Read(string key) => _files.TryGetValue(key, out string value) ? value : null;

            public void Write(string key, string contents) => _files[key] = contents;

            public void Delete(string key) => _files.Remove(key);
        }

        [Test]
        public void RaceAndGenderGiveTheAlphaSpecies()
        {
            // CharCreation_refresh_model 0x42C080: kind = base[race] + gender.
            int[] male = { 0x02, 0x00, 0x09, 0x0B, 0x04, 0x07, 0x0F, 0x0D };
            CollectionAssert.AreEqual(new[] { "Human", "Elf", "Dwarf", "Orc", "Goblin", "Lizard", "Undead", "Frogman" }, AlphaHeroCreation.Races);
            for (int race = 0; race < 8; race++)
            {
                Assert.AreEqual(male[race], AlphaHeroCreation.KindOf(race, 0).Value, AlphaHeroCreation.Races[race]);
                Assert.AreEqual(male[race] + 1, AlphaHeroCreation.KindOf(race, 1).Value, AlphaHeroCreation.Races[race]);
            }
        }

        [Test]
        public void FaceAndHaircutRangesFollowTheSwitchTable()
        {
            // Table 0x42C41C: HumanMale (default) heads 0x4DE…0x4E3, hair 0x4E4…0x4F2.
            KindId human = AlphaHeroCreation.KindOf(0, 0);
            Assert.AreEqual(6, AlphaHeroCreation.FaceCount(human));
            Assert.AreEqual(15, AlphaHeroCreation.HaircutCount(human));
            var r = AlphaHeroCreation.ModelRanges(human);
            Assert.AreEqual((0x4DE, 0x4E3, 0x4E4, 0x4F2), r);
            Assert.AreEqual(2, AlphaHeroCreation.FaceCount(AlphaHeroCreation.KindOf(5, 0)), "LizardmanMale heads 0x62…0x63");
            Assert.AreEqual(3, AlphaHeroCreation.HaircutCount(AlphaHeroCreation.KindOf(2, 0)), "DwarfMale hair 0x11F…0x121");
            var lizardF = AlphaHeroCreation.ModelRanges(AlphaHeroCreation.KindOf(5, 1));
            Assert.AreEqual((0x064, 0x069), (lizardF.HairFirst, lizardF.HairLast), "LizardmanFemale shares the male hair range");
        }

        [Test]
        public void IndicesWrapLikeTheEditor()
        {
            Assert.AreEqual(5, AlphaHeroCreation.Wrap(-1, 6));
            Assert.AreEqual(0, AlphaHeroCreation.Wrap(6, 6));
            Assert.AreEqual(3, AlphaHeroCreation.Wrap(3, 6));

            // Face 5 of a human is out of range for a male lizard (2 heads) → wrapped to 0.
            HeroAppearance a = AlphaHeroCreation.Normalize(new HeroAppearance(5, 0, 5, 1, 1, 2, 3));
            Assert.AreEqual(0, a.Face);
            Assert.AreEqual(1, a.Haircut);
            Assert.AreEqual(0x62, AlphaHeroCreation.HeadModel(a));
            Assert.AreEqual(0x65, AlphaHeroCreation.HairModel(a));
        }

        [Test]
        public void HairPaletteIsSixteenHuesBySevenSteps()
        {
            Assert.AreEqual(((byte)0xFF, (byte)0xC8, (byte)0x32), AlphaHeroCreation.DefaultHairColor);
            Assert.AreEqual(((byte)255, (byte)0, (byte)0), AlphaHeroCreation.PaletteColor(0, 3), "row 3 = hue");
            Assert.AreEqual(((byte)51, (byte)0, (byte)0), AlphaHeroCreation.PaletteColor(0, 0), "×0.2");
            Assert.AreEqual(((byte)178, (byte)0, (byte)0), AlphaHeroCreation.PaletteColor(0, 2), "×0.7");
            Assert.AreEqual(((byte)255, (byte)191, (byte)191), AlphaHeroCreation.PaletteColor(0, 6), "+(1−c)·0.75");
            Assert.AreEqual(((byte)127, (byte)127, (byte)127), AlphaHeroCreation.PaletteColor(15, 3), "grey column");
            var all = new HashSet<(byte, byte, byte)>();
            for (int c = 0; c < 16; c++)
            {
                for (int row = 0; row < 7; row++)
                {
                    all.Add(AlphaHeroCreation.PaletteColor(c, row));
                }
            }

            Assert.Greater(all.Count, 100, "112 cells, almost all distinct");
        }

        [Test]
        public void NameKeepsFifteenCharacters()
        {
            Assert.AreEqual("Abcdefghijklmno", AlphaHeroCreation.NormalizeName("  Abcdefghijklmnopq  "));
            Assert.AreEqual("Hero", AlphaHeroCreation.NormalizeName("   "));
        }

        [TestCase(CharacterClass.Warrior, WeaponSubtype.Greatmace, -1)]
        [TestCase(CharacterClass.Ranger, WeaponSubtype.Bow, -1)]
        [TestCase(CharacterClass.Mage, WeaponSubtype.Staff, -1)]
        [TestCase(CharacterClass.Rogue, WeaponSubtype.Dagger, (int)WeaponSubtype.Dagger)]
        public void NewHeroGetsTheAlphaStartKit(CharacterClass heroClass, WeaponSubtype main, int offHand)
        {
            GameSession session = New(heroClass, new HeroAppearance(3, 1, 2, 3, 10, 20, 30));
            HeroState hero = session.Hero;
            Assert.AreEqual((byte)main, hero.Loadout.GetEquipped(EquipmentSlot.MainHand).Subtype);
            ItemInstance off = hero.Loadout.GetEquipped(EquipmentSlot.OffHand);
            Assert.AreEqual(offHand, off == null ? -1 : off.Subtype);
            Assert.AreEqual(5, hero.Inventory.CountOf(new RawAlphaItemId(1, 1)), "five (1,1) consumables");
            Assert.AreEqual(1, hero.Inventory.CountOf(new RawAlphaItemId(0x18, 0)), "lamp");
            Assert.AreEqual(2, hero.Items.Count(i => i.Type == ItemType.Ring), "two rings (bags until C5 ring slots)");
            int kitCore = AlphaHeroCreation.StarterKit(heroClass).Count(i => i.Core != null);
            Assert.AreEqual(kitCore, hero.Items.Count);
            Assert.AreEqual(0x0C, hero.Kind.Value, "Orc female");
        }

        [Test]
        public void StartKitMaterialsAndLevelsAreAlphaBytes()
        {
            IReadOnlyList<StarterKitItem> warrior = AlphaHeroCreation.StarterKit(CharacterClass.Warrior);
            Assert.AreEqual(2, warrior[0].AlphaMaterial, "greatmace material 2 (0x477455)");
            Assert.AreEqual(ItemModifier.None, warrior[0].Core.Modifier, "Core S04 has no material 2 yet");
            Assert.AreEqual(ItemModifier.M01, warrior[1].Core.Modifier, "swords material 1");
            Assert.AreEqual((byte)WeaponSubtype.Shield, warrior[3].Core.Subtype);
            IReadOnlyList<StarterKitItem> mage = AlphaHeroCreation.StarterKit(CharacterClass.Mage);
            Assert.AreEqual(ItemModifier.M0B, mage[1].Core.Modifier, "bracelets material 0x0B (0x4775BD)");
            foreach (StarterKitItem item in warrior.Where(i => i.Core != null))
            {
                Assert.AreEqual(1, item.Core.Level);
                Assert.AreEqual(0, item.Core.Rarity);
            }
        }

        [Test]
        public void AppearanceSurvivesACheckpointAndOldSlotsReadAsHuman()
        {
            var store = new MemoryStore();
            var slots = new GameSlotStore(store, new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());
            var look = new HeroAppearance(7, 1, 3, 2, 12, 34, 56);
            GameSession session = New(CharacterClass.Mage, look);
            Assert.IsTrue(slots.Save(session, 1L).IsSuccess);
            SlotLoadResult loaded = slots.Load("slot0", "test");
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            Assert.AreEqual(look.ToString(), loaded.Hero.Appearance.ToString());
            Assert.AreEqual(0x0E, loaded.Hero.Kind.Value, "FrogmanFemale");

            // A session payload written before C1 has no appearance fields.
            var serializer = new JsonSessionSerializer();
            SessionSaveData old = serializer.Deserialize(serializer.Serialize(new SessionSaveData()).Replace("\"heroGender\":-1,", string.Empty));
            Assert.AreEqual(-1, old.heroGender);
            var legacyHero = new HeroState("Old", -1, new CharacterSheet(CharacterClass.Warrior, CharacterSpecialization.Primary));
            Assert.AreEqual(0x02, legacyHero.Kind.Value, "old heroes stay HumanMale");
        }

        private static GameSession New(CharacterClass heroClass, HeroAppearance look)
        {
            var session = new GameSession();
            session.EnterMainMenu();
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { Seed = 5, GeneratorId = "test", Class = heroClass, Appearance = look }, 0));
            Assert.IsTrue(session.CompleteLoading());
            return session;
        }
    }
}
