// D6 — EditMode checks of the Alpha sound inventory and its event mapping (no audio playback, no cache needed).
using System.IO;
using CubeGame.Content;
using CubeGame.Presentation.Visual;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaSoundLibraryTests
    {
        [Test]
        public void InventoryHasTheAlphaSounds()
        {
            Assert.AreEqual(93, AlphaSoundCatalog.Names.Length, "data2.db holds 93 .wav entries");
            CollectionAssert.Contains(AlphaSoundCatalog.Names, AlphaSoundCatalog.MissionComplete);
            CollectionAssert.Contains(AlphaSoundCatalog.Names, "absorb.wav");
            CollectionAssert.AllItemsAreUnique(AlphaSoundCatalog.Names);
        }

        [Test]
        public void NamesAreUniqueIgnoringCaseAndResolvableOnlyWhenStaged()
        {
            var library = new AlphaSoundLibrary(Path.GetTempPath());
            Assert.AreEqual(93, library.Count);
            Assert.IsTrue(library.HasSound("MissionComplete.wav"), "lookup ignores case");
            Assert.IsFalse(library.HasSound("no-such-sound.wav"));
            Assert.IsFalse(library.TryResolve("no-such-sound.wav", out _));

            string dir = Path.Combine(Path.GetTempPath(), "cubegame-sound-probe");
            Directory.CreateDirectory(Path.Combine(dir, AlphaSoundLibrary.DirectoryName));
            string file = Path.Combine(dir, AlphaSoundLibrary.DirectoryName, AlphaSoundCatalog.MissionComplete);
            File.WriteAllBytes(file, new byte[4]);
            try
            {
                var staged = new AlphaSoundLibrary(dir);
                Assert.IsTrue(staged.TryResolve(AlphaSoundCatalog.MissionComplete, out string path), "staged file resolves");
                Assert.AreEqual(file, path);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void OnlyTheProvenEventIsMapped()
        {
            var library = new AlphaSoundLibrary(null);
            Assert.IsFalse(library.TryResolveEvent("hit", out _, out string reason));
            StringAssert.Contains("EVENT_SOUND_UNKNOWN", reason);
            Assert.IsFalse(library.TryResolveEvent(AlphaSoundLibrary.MissionCompleteEvent, out _, out string notStaged));
            StringAssert.Contains("SOUND_NOT_STAGED", notStaged, "mapped event without staged file says so");
        }
    }
}
