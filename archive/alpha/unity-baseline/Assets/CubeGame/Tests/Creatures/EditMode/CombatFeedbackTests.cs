// D4/D5 — EditMode checks of the combat feedback curves used by the visual runtime.
using CubeGame.Presentation.Visual;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class CombatFeedbackTests
    {
        [Test]
        public void HitFlashIsFullAtTheMomentAndFadesToZero()
        {
            Assert.AreEqual(1f, CombatFeedback.FlashIntensity(0), 1e-6);
            float middle = CombatFeedback.FlashIntensity((long)(CombatFeedback.FlashSeconds * 1000 / 2));
            Assert.Greater(middle, 0f);
            Assert.Less(middle, 1f);
            Assert.AreEqual(0f, CombatFeedback.FlashIntensity((long)(CombatFeedback.FlashSeconds * 1000) + 1), 1e-6);
            Assert.AreEqual(1f, CombatFeedback.FlashIntensity(-5), 1e-6, "a negative age means 'just hit'");
        }

        [Test]
        public void CorpseSinksMonotonicallyAndFinishes()
        {
            Assert.AreEqual(0f, CombatFeedback.DeathSink(0), 1e-6);
            float quarter = CombatFeedback.DeathSink((long)(CombatFeedback.DeathSinkSeconds * 1000 / 4));
            float half = CombatFeedback.DeathSink((long)(CombatFeedback.DeathSinkSeconds * 1000 / 2));
            Assert.Greater(half, quarter);
            Assert.Less(half, CombatFeedback.DeathSinkBlocks);
            Assert.AreEqual(CombatFeedback.DeathSinkBlocks,
                CombatFeedback.DeathSink((long)(CombatFeedback.DeathSinkSeconds * 1000)), 1e-6);
            Assert.IsTrue(CombatFeedback.CorpseFinished((long)(CombatFeedback.DeathSinkSeconds * 1000)));
            Assert.IsFalse(CombatFeedback.CorpseFinished(0));
        }

        [Test]
        public void ConstantsAndProvenanceAreHonest()
        {
            CombatFeedback.Validate();
            Assert.AreEqual(90f, CombatFeedback.DeathTiltDegrees, 1e-6);
            StringAssert.Contains("CUBEGAME_CONTRACT", CombatFeedback.Provenance);
            StringAssert.Contains("not restored", CombatFeedback.Provenance);
        }
    }
}
