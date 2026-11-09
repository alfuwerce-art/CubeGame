using System;
using CubeGame.Core.Movement;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>C2: hero locomotion numbers of Alpha (Cube.exe bytes, see C2_ALPHA_MOVEMENT_PHYSICS.md).</summary>
    public sealed class FullM0MovementTests
    {
        private const double Tolerance = 1e-9;

        /// <summary>Runs a flat-ground simulation: the body is always on the ground, no walls.</summary>
        private static double RunOnGround(AlphaMovement move, int frames, int ms, double inputX, bool walk, out double distance)
        {
            distance = 0.0;
            for (int i = 0; i < frames; i++)
            {
                (double x, double _, double z) = move.BeginFrame(ms, inputX, 0.0, walk, false, true);
                distance += Math.Sqrt(x * x + z * z);
                move.EndFrame(ms, true, false);
            }

            return move.HorizontalSpeed;
        }

        [Test]
        public void ConstantsAreAlphaBytes()
        {
            Assert.AreEqual(80.0, AlphaMovement.InputAcceleration);
            Assert.AreEqual(6.0, AlphaMovement.BaseMaxSpeed);
            Assert.AreEqual(2.0, AlphaMovement.RunMultiplier);
            Assert.AreEqual(30.0, AlphaMovement.Gravity);
            Assert.AreEqual(0.005, AlphaMovement.GroundFrictionIdle);
            Assert.AreEqual(0.0025, AlphaMovement.GroundFrictionMoving);
            Assert.AreEqual(200, AlphaMovement.JumpPressWindowMs);
            Assert.AreEqual(10.0, AlphaMovement.JumpMaxSpeed);
            Assert.AreEqual(100, AlphaMovement.JumpBoostMs);
            Assert.AreEqual(0.08, AlphaMovement.JumpBoostPerMs);
        }

        [Test]
        public void RunIsDefaultAndShiftWalks()
        {
            var run = new AlphaMovement();
            RunOnGround(run, 120, 16, 1.0, false, out double runDistance);
            Assert.IsTrue(run.Running);
            Assert.AreEqual(12.0, run.MaxHorizontalSpeed, Tolerance);

            var walk = new AlphaMovement();
            RunOnGround(walk, 120, 16, 1.0, true, out double walkDistance);
            Assert.IsFalse(walk.Running);
            Assert.AreEqual(6.0, walk.MaxHorizontalSpeed, Tolerance);

            // Clamp before the move: after the first frames the body covers max·dt per frame.
            Assert.AreEqual(12.0 * 1.92 - 0.9, runDistance, 0.3, "12 blocks/s after a 0.15 s start at 80 blocks/s²");
            Assert.AreEqual(6.0 * 1.92 - 0.225, walkDistance, 0.15, "6 blocks/s after a 0.075 s start");
        }

        [Test]
        public void SteadySpeedIsTheSameAt30_60_120Hz()
        {
            foreach (int ms in new[] { 33, 16, 8 })
            {
                var move = new AlphaMovement();
                int frames = 2000 / ms;
                double before = 0.0;
                RunOnGround(move, frames, ms, 1.0, false, out before);
                double after;
                RunOnGround(move, 1000 / ms, ms, 1.0, false, out after);
                double seconds = (1000 / ms) * ms / 1000.0;
                Assert.AreEqual(12.0, after / seconds, 1e-6, $"steady run displacement at {ms} ms frames");
            }
        }

        [Test]
        public void GroundFrictionStopsWithAlphaFactorPerMillisecond()
        {
            var move = new AlphaMovement();
            RunOnGround(move, 60, 16, 1.0, false, out _);
            // Friction with input (0.0025/ms) was applied after the last clamped move.
            Assert.AreEqual(12.0 * Math.Pow(1.0 - 0.0025, 16), move.HorizontalSpeed, 1e-9);

            move.BeginFrame(16, 0.0, 0.0, false, false, true);
            double v = move.HorizontalSpeed;
            move.EndFrame(16, true, false);
            Assert.AreEqual(v * Math.Pow(1.0 - 0.005, 16), move.HorizontalSpeed, 1e-9, "idle friction 0.005 per ms");

            for (int i = 0; i < 200; i++)
            {
                move.BeginFrame(16, 0.0, 0.0, false, false, true);
                move.EndFrame(16, true, false);
            }

            Assert.Less(move.HorizontalSpeed, 0.01, "the hero stops after ~3 s without input");
        }

        [Test]
        public void NoFrictionInTheAir()
        {
            var move = new AlphaMovement();
            RunOnGround(move, 60, 16, 1.0, false, out _);
            double v = move.HorizontalSpeed;
            move.BeginFrame(16, 0.0, 0.0, false, false, false);
            move.EndFrame(16, false, false);
            Assert.AreEqual(v, move.HorizontalSpeed, Tolerance);
        }

        [Test]
        public void GravityIsThirtyBlocksPerSecondSquared()
        {
            var move = new AlphaMovement();
            (double _, double y, double _) = move.BeginFrame(100, 0.0, 0.0, false, false, false);
            Assert.AreEqual(-3.0, move.VelocityY, Tolerance);
            Assert.AreEqual(-0.3, y, Tolerance);
        }

        [Test]
        public void StandingJumpStartsAtTwoAndBoostsToTen()
        {
            var move = new AlphaMovement();
            move.EndFrame(1, true, false);

            // Frame 1 (10 ms): start vz = 0 + 2, boost +0.08·10 = 2.8, gravity −0.3.
            move.BeginFrame(10, 0.0, 0.0, false, true, true);
            Assert.AreEqual(2.0 + 0.8 - 0.3, move.VelocityY, Tolerance);
            move.EndFrame(10, false, false);

            // Hold for the rest of the 100 ms boost: +0.08/ms, capped at 10 before gravity.
            for (int i = 0; i < 9; i++)
            {
                move.BeginFrame(10, 0.0, 0.0, false, true, false);
                move.EndFrame(10, false, false);
            }

            double expected = 2.0;
            for (int i = 0; i < 10; i++)
            {
                expected = Math.Min(expected + 0.8, 10.0) - 0.3;
            }

            Assert.AreEqual(expected, move.VelocityY, 1e-9);

            // After the boost the key no longer lifts.
            move.BeginFrame(10, 0.0, 0.0, false, true, false);
            Assert.AreEqual(expected - 0.3, move.VelocityY, 1e-9);
        }

        [Test]
        public void RunningJumpStartsFromHorizontalSpeedAndCapsAtTen()
        {
            var move = new AlphaMovement();
            RunOnGround(move, 60, 16, 1.0, true, out _);
            double h = move.HorizontalSpeed;
            Assert.Less(h, 6.0 + Tolerance);

            move.BeginFrame(1, 1.0, 0.0, true, true, true);
            Assert.AreEqual(Math.Min(h + 2.0, 10.0) + 0.08 - 0.03, move.VelocityY, 1e-9, "start = horizontal speed + 2");

            var fast = new AlphaMovement();
            RunOnGround(fast, 60, 16, 1.0, false, out _);
            fast.BeginFrame(1, 1.0, 0.0, false, true, true);
            Assert.AreEqual(10.0 - 0.03, fast.VelocityY, 1e-9, "start capped at 10, boost cannot pass the cap");
        }

        [Test]
        public void JumpKeyHeldTooLongOrInTheAirDoesNotJump()
        {
            var move = new AlphaMovement();
            move.BeginFrame(250, 0.0, 0.0, false, true, true);
            Assert.Less(move.VelocityY, 0.0, "first press frame already past the 200 ms window");

            var air = new AlphaMovement();
            air.BeginFrame(10, 0.0, 0.0, false, true, false);
            Assert.Less(air.VelocityY, 0.0, "no jump without ground contact");
        }

        [Test]
        public void LandingAndCeilingStopVerticalSpeed()
        {
            var move = new AlphaMovement();
            move.BeginFrame(100, 0.0, 0.0, false, false, false);
            move.EndFrame(100, true, false);
            Assert.AreEqual(0.0, move.VelocityY, Tolerance);

            move.BeginFrame(10, 0.0, 0.0, false, true, true);
            Assert.Greater(move.VelocityY, 0.0);
            move.EndFrame(10, false, true);
            Assert.AreEqual(0.0, move.VelocityY, Tolerance);
        }
    }
}
