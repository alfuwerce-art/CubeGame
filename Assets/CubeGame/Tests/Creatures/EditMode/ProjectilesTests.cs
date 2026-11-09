// D5 — EditMode checks of the projectile data and motion (models are Alpha's, flight is CubeGame's).
using CubeGame.Presentation.Visual;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class ProjectilesTests
    {
        [Test]
        public void ProjectileModelsComeFromTheAlphaItemLayer()
        {
            Assert.AreEqual("arrow.cub", Projectiles.ModelOf(Projectiles.ArrowSubtype));
            Assert.AreEqual("wood-boomerang1.cub", Projectiles.ModelOf(Projectiles.BoomerangSubtype));
            Assert.IsTrue(Projectiles.IsProjectileSubtype(Projectiles.ArrowSubtype));
            Assert.IsFalse(Projectiles.IsProjectileSubtype(0), "a sword is not a projectile");
            StringAssert.Contains("ALPHA_DATA", Projectiles.Provenance);
            StringAssert.Contains("CUBEGAME_CONTRACT", Projectiles.Provenance);
        }

        [Test]
        public void LaunchFliesTowardTheTargetAtTheGivenSpeed()
        {
            var state = Projectiles.Launch(0, 0, 0, 10, 0, 0, 20.0, 0.0, 1000);
            Projectiles.Step(ref state, 250);
            Assert.AreEqual(5.0, state.X, 1e-6, "20 blocks/s for 0.25 s");
            Assert.AreEqual(0.0, state.Y, 1e-6);
            Assert.AreEqual(250, state.AgeMs);
            Assert.IsFalse(state.Expired);

            // Reaches the target in half a second and expires at the lifetime.
            Projectiles.Step(ref state, 250);
            Assert.AreEqual(10.0, state.X, 1e-6);
            Projectiles.Step(ref state, 500);
            Assert.IsTrue(state.Expired, "lifetime 1000 ms is over");
        }

        [Test]
        public void GravityPullsTheProjectileDown()
        {
            var state = Projectiles.Launch(0, 5, 0, 10, 5, 0, 10.0, gravity: 9.81, lifetimeMs: 2000);
            Projectiles.Step(ref state, 500);
            Assert.Less(state.Y, 5.0, "with gravity the arrow drops");
            var flat = Projectiles.Launch(0, 5, 0, 10, 5, 0, 10.0, gravity: 0.0, lifetimeMs: 2000);
            Projectiles.Step(ref flat, 500);
            Assert.AreEqual(5.0, flat.Y, 1e-6, "without gravity it stays level");
        }

        [Test]
        public void InvalidLaunchIsRefused()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Projectiles.Launch(0, 0, 0, 1, 0, 0, 0.0));
            Assert.Throws<System.ArgumentException>(() => Projectiles.Launch(1, 1, 1, 1, 1, 1, 10.0));
        }
    }
}
