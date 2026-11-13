using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Features.Combat;
using CubeGame.Features.Session;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// P5: Alpha class/specialization action context — hotbar 0x48B8AF, rank slots 0x43C9C0, power 0x43ED60,
    /// cooldowns 0x43E6A0, mana 0x444AE0, gate 0x43E5A0 and mana regeneration.
    /// </summary>
    public sealed class FullM0ClassAbilityTests
    {
        private static ItemInstance W(WeaponSubtype s) => new ItemInstance(ItemType.Weapon, (byte)s, 1, 0, 1);

        private static int[] Ranks(int a1, int a2, int a3)
        {
            var r = new int[HeroSkillRanks.Count];
            r[6] = a1; r[7] = a2; r[8] = a3;
            return r;
        }

        [TestCase(CharacterClass.Warrior, CharacterSpecialization.Primary, 0x36, 0x56, 0x66)]
        [TestCase(CharacterClass.Warrior, CharacterSpecialization.Secondary, 0x36, 0x56, 0x65)]
        [TestCase(CharacterClass.Ranger, CharacterSpecialization.Primary, 0x15, 0x32, 0x63)]
        [TestCase(CharacterClass.Ranger, CharacterSpecialization.Secondary, 0x15, 0x32, 0x64)]
        [TestCase(CharacterClass.Mage, CharacterSpecialization.Primary, 0x58, 0x67, 0x31)]
        [TestCase(CharacterClass.Mage, CharacterSpecialization.Secondary, 0x22, 0x67, 0x31)]
        [TestCase(CharacterClass.Rogue, CharacterSpecialization.Primary, 0x30, 0x4F, 0x61)]
        [TestCase(CharacterClass.Rogue, CharacterSpecialization.Secondary, 0x30, 0x4F, 0x60)]
        public void ClassAndSpecializationChooseTheAlphaAbilities(CharacterClass c, CharacterSpecialization s, int a1, int a2, int a3)
        {
            int[] bar = AlphaClassAbilities.Hotbar(c, s, W(WeaponSubtype.Sword), null, Ranks(1, 1, 1));
            CollectionAssert.AreEqual(new[] { a1, a2, a3 }, new[] { bar[2], bar[3], bar[4] });
            for (int slot = 2; slot < 5; slot++)
            {
                Assert.AreEqual(slot + 4, AlphaClassAbilities.RankSlot(bar[slot]), "the hotbar slot is powered by its own rank");
            }

            int[] locked = AlphaClassAbilities.Hotbar(c, s, W(WeaponSubtype.Sword), null, Ranks(0, 2, 0));
            CollectionAssert.AreEqual(new[] { 0, a2, 0 }, new[] { locked[2], locked[3], locked[4] }, "rank 0 locks the slot");
        }

        [Test]
        public void WeaponsChooseTheAlphaPrimaryAndSecondary()
        {
            var none = Ranks(0, 0, 0);
            int[] Bar(CharacterSpecialization s, ItemInstance main, ItemInstance off = null) =>
                AlphaClassAbilities.Hotbar(CharacterClass.Warrior, s, main, off, none);

            CollectionAssert.AreEqual(new[] { 0x1E, 0x1F }, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Staff))[..2]);
            CollectionAssert.AreEqual(new[] { 0x20, 0x21 }, Bar(CharacterSpecialization.Secondary, W(WeaponSubtype.Staff))[..2]);
            CollectionAssert.AreEqual(new[] { 0x28, 0x25 }, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Bracelet))[..2]);
            CollectionAssert.AreEqual(new[] { 0x2A, 0x2B }, Bar(CharacterSpecialization.Secondary, W(WeaponSubtype.Bracelet))[..2]);
            CollectionAssert.AreEqual(new[] { 0x26, 0x2E }, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Wand))[..2]);
            CollectionAssert.AreEqual(new[] { 0x2C, 0x2D }, Bar(CharacterSpecialization.Secondary, W(WeaponSubtype.Wand))[..2]);
            Assert.AreEqual(0x05, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Longsword))[1]);
            Assert.AreEqual(0x11, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Dagger))[1]);
            Assert.AreEqual(0x14, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Fist))[1]);
            Assert.AreEqual(0x19, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Bow))[1]);
            Assert.AreEqual(0x18, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Crossbow))[1]);
            Assert.AreEqual(0x1B, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Boomerang))[1]);
            Assert.AreEqual(0x3B, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Greatsword))[1]);
            Assert.AreEqual(0x08, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Sword), W(WeaponSubtype.Shield))[1]);
            Assert.AreEqual(0x3F, Bar(CharacterSpecialization.Primary, W(WeaponSubtype.Sword))[1]);
        }

        [Test]
        public void PowerCooldownAndManaFollowTheAlphaFormulas()
        {
            Assert.AreEqual(0f, AlphaClassAbilities.PowerFactor(0x36, Ranks(0, 0, 0)));
            Assert.AreEqual(0.5f, AlphaClassAbilities.PowerFactor(0x36, Ranks(10, 0, 0)), 1e-6f);
            Assert.AreEqual(0.5f, AlphaClassAbilities.PowerFactor(0x36, Ranks(0, 0, 0), player: false, level: 17, stars: 1), 1e-6f,
                "NPC rank fallback level/2 + stars + 1");
            Assert.AreEqual(20000, AlphaClassAbilities.CooldownMs(0x36, 0f));
            Assert.AreEqual(13000, AlphaClassAbilities.CooldownMs(0x36, 0.5f));
            Assert.AreEqual(45000, AlphaClassAbilities.CooldownMs(0x66, 0.5f));
            Assert.AreEqual(15000, AlphaClassAbilities.CooldownMs(0x48, 0.9f));
            Assert.AreEqual(0, AlphaClassAbilities.CooldownMs(0x1E, 0f));
            Assert.AreEqual(0.3f, AlphaClassAbilities.ManaCost(0x1F, 0f));
            Assert.AreEqual(0f, AlphaClassAbilities.ManaCost(0x1F, 0f, buff9Active: true));
            Assert.AreEqual(0.125f, AlphaClassAbilities.ManaCost(0x22, 0f), 1e-6f);
            Assert.IsFalse(AlphaClassAbilities.IsUsable(0x1F, 0f, 0.2f, 0, 0), "not enough mana");
            Assert.IsFalse(AlphaClassAbilities.IsUsable(0x36, 0f, 1f, 0, 10), "cooldown");
            Assert.IsFalse(AlphaClassAbilities.IsUsable(0x36, 0f, 1f, 1, 0), "+0x12C blocks");
            Assert.IsTrue(AlphaClassAbilities.IsUsable(0x65, 0f, 1f, 1, 0), "Bulwark ignores +0x12C");
        }

        [Test]
        public void SessionHotbarGatesAbilitiesAndPaysMana()
        {
            var session = new GameSession();
            Assert.IsTrue(session.EnterMainMenu());
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { GeneratorId = "test", Class = CharacterClass.Mage }, 0));
            Assert.IsTrue(session.CompleteLoading());
            HeroState hero = session.Hero;
            int[] bar = session.HeroHotbar();
            Assert.AreEqual(0x1E, bar[0], "starter staff");
            Assert.AreEqual(0x1F, bar[1]);
            Assert.AreEqual(0, bar[2], "abilities are locked at rank 0");
            Assert.IsFalse(session.TryHeroSlot(3));
            StringAssert.Contains("locked", session.LastRejection);

            Assert.AreEqual(1f, hero.Mana);
            Assert.IsTrue(session.TryHeroSlot(2), session.LastRejection);
            Assert.AreEqual(0.7f, hero.Mana, 1e-5f, "0x1F costs 0.3 of the bar");
            session.Clock.StepTicks(session.Clock.TicksPerSecond * 10);
            Assert.Greater(hero.Mana, 0.7f, "mana regenerates once the cast is over");
            Assert.LessOrEqual(hero.Mana, 1f);
        }
    }
}
