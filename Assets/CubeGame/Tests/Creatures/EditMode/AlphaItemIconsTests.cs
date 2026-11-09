// D6 — EditMode checks of the Alpha item icon lookup (held items proven, weapons/armour refused).
using CubeGame.Features.Contracts;
using CubeGame.Presentation.Visual;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaItemIconsTests
    {
        [Test]
        public void HeldItemsResolveToTheirAlphaModelEntry()
        {
            Assert.IsTrue(AlphaItemIcons.TryEntry(new RawAlphaItemId(1, 1), out string potion));
            Assert.AreEqual("life-potion", potion);
            Assert.AreEqual("cookie", AlphaItemIcons.EntryOf("cookie.cub"));
            Assert.AreEqual("plain", AlphaItemIcons.EntryOf("plain"));
            Assert.IsNull(AlphaItemIcons.EntryOf(null));
        }

        [Test]
        public void UnprovenItemsAreRefusedWithAReason()
        {
            Assert.IsFalse(AlphaItemIcons.TryEntry(new RawAlphaItemId(3, 0), out string sword),
                "weapon icon needs the material mapping, which is not recovered");
            Assert.IsNull(sword);
            Assert.IsNull(AlphaItemIcons.TryGetSprite(null, new RawAlphaItemId(1, 1), out string noAtlas));
            Assert.AreEqual("NO_ICON_ATLAS", noAtlas);
            Assert.IsNull(AlphaItemIcons.TryGetSprite(null, new RawAlphaItemId(99, 99), out string unknown));
            Assert.AreEqual("NO_ICON_ATLAS", unknown);
        }

        [Test]
        public void StatusNamesTheUnknownPart()
        {
            StringAssert.Contains("UNKNOWN", AlphaItemIcons.Status);
            StringAssert.Contains("ALPHA_DATA", AlphaItemIcons.Status);
        }
    }
}
