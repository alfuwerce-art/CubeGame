// D6 — EditMode checks of the Alpha item icon lookup (held items proven, weapons/armour refused).
using System;
using System.IO;
using CubeGame.Features.Contracts;
using CubeGame.Presentation.Visual;
using NUnit.Framework;
using UnityEngine;

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
        public void MissingAtlasIsReported()
        {
            Assert.IsNull(AlphaItemIcons.TryGetSprite(null, new RawAlphaItemId(1, 1), out string noAtlas));
            Assert.AreEqual("NO_ICON_ATLAS", noAtlas);
        }

        [Test]
        public void LoadedAtlasReportsUnknownIconsWithoutGuessing()
        {
            string directory = Path.Combine(Path.GetTempPath(), "cubegame-icons-" + Guid.NewGuid().ToString("N"));
            string png = Path.Combine(directory, "atlas.png");
            string json = Path.Combine(directory, "atlas.json");
            Directory.CreateDirectory(directory);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            AlphaIconAtlas atlas = null;
            try
            {
                File.WriteAllBytes(png, texture.EncodeToPNG());
                File.WriteAllText(json, "{\"life-potion\":{\"x\":0,\"y\":0,\"w\":2,\"h\":2}}");
                Assert.IsTrue(AlphaIconAtlas.TryLoad(directory, out atlas, null));
                Assert.IsTrue(atlas.TryGetRect("life-potion", out _));

                var unknownItems = new[] { new RawAlphaItemId(99, 99), new RawAlphaItemId(3, 5), new RawAlphaItemId(4, 0) };
                foreach (RawAlphaItemId item in unknownItems)
                {
                    Assert.IsFalse(AlphaItemIcons.TryEntry(item, out string entry));
                    Assert.IsNull(entry);
                    Assert.IsNull(AlphaItemIcons.TryGetSprite(atlas, item, out string reason));
                    StringAssert.StartsWith("ICON_UNKNOWN ", reason);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(atlas?.Texture);
                UnityEngine.Object.DestroyImmediate(texture);
                File.Delete(png);
                File.Delete(json);
                Directory.Delete(directory);
            }
        }

        [Test]
        public void StatusNamesTheUnknownPart()
        {
            StringAssert.Contains("UNKNOWN", AlphaItemIcons.Status);
            StringAssert.Contains("ALPHA_DATA", AlphaItemIcons.Status);
        }
    }
}
