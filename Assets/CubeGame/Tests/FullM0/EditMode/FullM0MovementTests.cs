using System;
using CubeGame.Core.Movement;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>C2: hero locomotion numbers of Alpha (Cube.exe bytes, see C2_ALPHA_MOVEMENT_PHYSICS.md).</summary>
    public sealed class FullM0MovementTests
    {
        private const double Tolerance = 1e-9;

        private static readonly MovementView East = MovementView.FromYaw(1.0, 0.0);

        private static MovementContact Ground => new MovementContact { OnGround = true, BodyHeight = 1.8 };

        private static MovementContact Air => new MovementContact { BodyHeight = 1.8 };

        private static MovementContact Wall => new MovementContact { AtWall = true, BodyHeight = 1.8 };

        private static MovementContact Water(double depth) => new MovementContact { InWater = true, WaterDepth = depth, BodyHeight = 1.8 };

        private static MovementKeys Keys(bool forward = false, bool walk = false, bool jump = false, bool climb = false, bool back = false)
        {
            return new MovementKeys { Forward = forward, Walk = walk, Jump = jump, Climb = climb, Back = back };
        }

        private static void Frame(AlphaMovement move, int ms, MovementKeys keys, MovementContact before, MovementContact after)
        {
            move.BeginFrame(ms, keys, East, before);
            move.EndFrame(ms, after);
        }

        /// <summary>Flat-ground run: the body is always on the ground, no walls.</summary>
        private static void RunOnGround(AlphaMovement move, int frames, int ms, bool walk, out double distance)
        {
            distance = 0.0;
            for (int i = 0; i < frames; i++)
            {
                (double x, double _, double z) = move.BeginFrame(ms, Keys(forward: true, walk: walk), East, Ground);
                move.EndFrame(ms, Ground);
                distance += Math.Sqrt(x * x + z * z);
            }
        }

        [Test]
        public void ConstantsAreAlphaBytes()
        {
            Assert.AreEqual(80.0, AlphaMovement.InputAcceleration);
            Assert.AreEqual(8.0, AlphaMovement.GlideInputAcceleration);
            Assert.AreEqual(0.2, AlphaMovement.ClimbInputFactor);
            Assert.AreEqual(6.0, AlphaMovement.BaseMaxSpeed);
            Assert.AreEqual(2.0, AlphaMovement.RunMultiplier);
            Assert.AreEqual(30.0, AlphaMovement.Gravity);
            Assert.AreEqual(0.1, AlphaMovement.GlideGravityFactor);
            Assert.AreEqual(0.005, AlphaMovement.GroundFrictionIdle);
            Assert.AreEqual(0.0025, AlphaMovement.GroundFrictionMoving);
            Assert.AreEqual(0.005, AlphaMovement.WaterDrag);
            Assert.AreEqual(0.0025, AlphaMovement.ClimbDrag);
            Assert.AreEqual(200, AlphaMovement.JumpPressWindowMs);
            Assert.AreEqual(10.0, AlphaMovement.JumpMaxSpeed);
            Assert.AreEqual(100, AlphaMovement.JumpBoostMs);
            Assert.AreEqual(0.08, AlphaMovement.JumpBoostPerMs);
            Assert.AreEqual(0.001, AlphaMovement.WaterRisePerMs);
            Assert.AreEqual(2.0, AlphaMovement.GlideOpenMaxDrop);
        }

        [Test]
        public void SkillCurvesMatchAlphaFunctions()
        {
            // Same formulas as the stat descriptions (PILOT_MOVEMENT), here proven to drive the physics.
            Assert.AreEqual(0.5, AlphaMovement.SwimmingCurve(0), 1e-6);
            Assert.AreEqual((1.0 - 1.0 / 1.5) * 0.5 + 0.5, AlphaMovement.SwimmingCurve(5), 1e-6);
            Assert.AreEqual(1.0 - 1.0 / 1.1, AlphaMovement.ClimbingCurve(0), 1e-6);
            Assert.AreEqual(1.0 - 1.0 / 1.6, AlphaMovement.ClimbingCurve(5), 1e-6);
            Assert.AreEqual(0.0, AlphaMovement.GlidingCurve(0), Tolerance);
            Assert.AreEqual((1.0 - 1.0 / 1.1) * 3.0 + 1.5, AlphaMovement.GlidingCurve(1), 1e-6);
        }

        [Test]
        public void RunIsDefaultAndShiftWalks()
        {
            var run = new AlphaMovement();
            RunOnGround(run, 120, 16, false, out double runDistance);
            Assert.IsTrue(run.Running);
            Assert.AreEqual(12.0, run.MaxHorizontalSpeed, Tolerance);

            var walk = new AlphaMovement();
            RunOnGround(walk, 120, 16, true, out double walkDistance);
            Assert.IsFalse(walk.Running);
            Assert.AreEqual(6.0, walk.MaxHorizontalSpeed, Tolerance);

            Assert.AreEqual(12.0 * 1.92 - 0.9, runDistance, 0.3, "12 blocks/s after a 0.15 s start at 80 blocks/s²");
            Assert.AreEqual(6.0 * 1.92 - 0.225, walkDistance, 0.15, "6 blocks/s after a 0.075 s start");
        }

        [Test]
        public void SteadySpeedIsTheSameAt30_60_120Hz()
        {
            foreach (int ms in new[] { 33, 16, 8 })
            {
                var move = new AlphaMovement();
                RunOnGround(move, 2000 / ms, ms, false, out _);
                RunOnGround(move, 1000 / ms, ms, false, out double after);
                double seconds = (1000 / ms) * ms / 1000.0;
                Assert.AreEqual(12.0, after / seconds, 1e-6, $"steady run displacement at {ms} ms frames");
            }
        }

        [Test]
        public void GroundFrictionStopsWithAlphaFactorPerMillisecond()
        {
            var move = new AlphaMovement();
            RunOnGround(move, 60, 16, false, out _);
            Assert.AreEqual(12.0 * Math.Pow(1.0 - 0.0025, 16), move.HorizontalSpeed, 1e-9);

            move.BeginFrame(16, Keys(), East, Ground);
            double v = move.HorizontalSpeed;
            move.EndFrame(16, Ground);
            Assert.AreEqual(v * Math.Pow(1.0 - 0.005, 16), move.HorizontalSpeed, 1e-9, "idle friction 0.005 per ms");

            for (int i = 0; i < 200; i++)
            {
                Frame(move, 16, Keys(), Ground, Ground);
            }

            Assert.Less(move.HorizontalSpeed, 0.01, "the hero stops after ~3 s without input");
        }

        [Test]
        public void NoFrictionInTheAir()
        {
            var move = new AlphaMovement();
            RunOnGround(move, 60, 16, false, out _);
            double v = move.HorizontalSpeed;
            Frame(move, 16, Keys(), Air, Air);
            Assert.AreEqual(v, move.HorizontalSpeed, Tolerance);
        }

        [Test]
        public void GravityIsThirtyBlocksPerSecondSquared()
        {
            var move = new AlphaMovement();
            (double _, double y, double _) = move.BeginFrame(100, Keys(), East, Air);
            Assert.AreEqual(-3.0, move.VelocityY, Tolerance);
            Assert.AreEqual(-0.3, y, Tolerance);
        }

        [Test]
        public void StandingJumpStartsAtTwoAndBoostsToTen()
        {
            var move = new AlphaMovement();
            Frame(move, 10, Keys(jump: true), Ground, Air);
            Assert.AreEqual(2.0 + 0.8 - 0.3, move.VelocityY, Tolerance);

            for (int i = 0; i < 9; i++)
            {
                Frame(move, 10, Keys(jump: true), Air, Air);
            }

            double expected = 2.0;
            for (int i = 0; i < 10; i++)
            {
                expected = Math.Min(expected + 0.8, 10.0) - 0.3;
            }

            Assert.AreEqual(expected, move.VelocityY, 1e-9);
            move.BeginFrame(10, Keys(jump: true), East, Air);
            Assert.AreEqual(expected - 0.3, move.VelocityY, 1e-9, "after 100 ms the key no longer lifts");
        }

        [Test]
        public void RunningJumpStartsFromHorizontalSpeedAndCapsAtTen()
        {
            var move = new AlphaMovement();
            RunOnGround(move, 60, 16, true, out _);
            double h = move.HorizontalSpeed;
            move.BeginFrame(1, Keys(forward: true, walk: true, jump: true), East, Ground);
            Assert.AreEqual(Math.Min(h + 2.0, 10.0) + 0.08 - 0.03, move.VelocityY, 1e-9, "start = horizontal speed + 2");

            var fast = new AlphaMovement();
            RunOnGround(fast, 60, 16, false, out _);
            fast.BeginFrame(1, Keys(forward: true, jump: true), East, Ground);
            Assert.AreEqual(10.0 - 0.03, fast.VelocityY, 1e-9, "start capped at 10, boost cannot pass the cap");
        }

        [Test]
        public void JumpKeyHeldTooLongOrInTheAirDoesNotJump()
        {
            var move = new AlphaMovement();
            move.BeginFrame(250, Keys(jump: true), East, Ground);
            Assert.Less(move.VelocityY, 0.0, "first press frame already past the 200 ms window");

            var air = new AlphaMovement();
            air.BeginFrame(10, Keys(jump: true), East, Air);
            Assert.Less(air.VelocityY, 0.0, "no jump without ground contact");
        }

        [Test]
        public void LandingAndCeilingStopVerticalSpeed()
        {
            var move = new AlphaMovement();
            Frame(move, 100, Keys(), Air, Ground);
            Assert.AreEqual(0.0, move.VelocityY, Tolerance);

            move.BeginFrame(10, Keys(jump: true), East, Ground);
            Assert.Greater(move.VelocityY, 0.0);
            move.EndFrame(10, new MovementContact { HitCeiling = true, BodyHeight = 1.8 });
            Assert.AreEqual(0.0, move.VelocityY, Tolerance);
        }

        // ---- Swimming ----

        [Test]
        public void SwimmingHalvesTheLimitAtRankZero()
        {
            var move = new AlphaMovement();
            for (int i = 0; i < 100; i++)
            {
                Frame(move, 16, Keys(forward: true), Water(2.0), Water(2.0));
            }

            Assert.AreEqual(12.0 * 0.5, move.MaxHorizontalSpeed, Tolerance, "run 12 × swimming curve 0.5");
            var walk = new AlphaMovement();
            walk.BeginFrame(16, Keys(forward: true, walk: true), East, Water(2.0));
            Assert.AreEqual(3.0, walk.MaxHorizontalSpeed, Tolerance);

            var skilled = new AlphaMovement { SwimmingRank = 5 };
            skilled.BeginFrame(16, Keys(forward: true), East, Water(2.0));
            Assert.AreEqual(12.0 * AlphaMovement.SwimmingCurve(5), skilled.MaxHorizontalSpeed, Tolerance);
        }

        [Test]
        public void BuoyancyFactorFollowsSubmersion()
        {
            // f = depth / (height / 2): f ≤ 0 → 0, 0 < f < 1 → f + 1, f ≥ 1 → 1 (0x61D121…0x61D184).
            foreach ((double depth, double factor) in new[] { (-0.2, 0.0), (0.45, 1.5), (0.9, 1.0), (3.0, 1.0) })
            {
                var move = new AlphaMovement();
                move.BeginFrame(10, Keys(), East, Air);
                double before = move.VelocityY;
                move.EndFrame(10, Water(depth));
                double expected = (before + 0.01 * 30.0 * factor) * Math.Pow(1.0 - 0.005, 10);
                Assert.AreEqual(expected, move.VelocityY, 1e-9, $"depth {depth}");
            }
        }

        [Test]
        public void WaterDragSlowsEveryAxis()
        {
            var move = new AlphaMovement();
            RunOnGround(move, 60, 16, false, out _);
            move.BeginFrame(10, Keys(), East, Water(3.0));
            move.EndFrame(10, Water(3.0));
            Assert.AreEqual(6.0 * Math.Pow(1.0 - 0.005, 10), move.HorizontalSpeed, 1e-9, "clamped to 12 × 0.5, then drag");
        }

        [Test]
        public void SwimmingFollowsTheCameraPitch()
        {
            var down = new MovementView { LookX = 0.0, LookY = -1.0, LookZ = 0.0, RightX = 1.0, RightZ = 0.0 };
            var move = new AlphaMovement();
            move.BeginFrame(10, Keys(forward: true), down, Water(3.0));
            Assert.AreEqual(-0.01 * 80.0 - 0.01 * 30.0, move.VelocityY, 1e-9, "W while looking down dives at 80 blocks/s²");
            Assert.AreEqual(0.0, move.HorizontalSpeed, Tolerance);
        }

        [Test]
        public void JumpKeyInWaterRises()
        {
            var move = new AlphaMovement();
            move.BeginFrame(10, Keys(jump: true), East, Water(3.0));
            Assert.AreEqual(10 * 0.001 + 10 * 0.08 - 0.3, move.VelocityY, 1e-9);
        }

        // ---- Climbing ----

        [Test]
        public void ClimbingCancelsGravityAndLiftsSixteen()
        {
            var move = new AlphaMovement();
            Frame(move, 10, Keys(forward: true, climb: true), Wall, Wall);
            Assert.IsTrue(move.Climbing);
            Assert.AreEqual(16.0 * 0.01 * Math.Pow(1.0 - 0.0025, 10), move.VelocityY, 1e-9, "no gravity, 16 blocks/s² up, drag 0.0025/ms");

            var hold = new AlphaMovement();
            Frame(hold, 10, Keys(climb: true), Wall, Wall);
            Assert.AreEqual(0.0, hold.VelocityY, Tolerance, "hanging on the wall does not fall");

            var down = new AlphaMovement();
            Frame(down, 10, Keys(back: true, climb: true), Wall, Wall);
            Assert.Less(down.VelocityY, 0.0);
        }

        [Test]
        public void ClimbingDrainsStaminaUntilTheHeroFalls()
        {
            var move = new AlphaMovement();
            double drainPerMs = (1.0 - AlphaMovement.ClimbingCurve(0)) * 0.0005;
            int frames = 0;
            while (frames < 1000)
            {
                Frame(move, 10, Keys(forward: true, climb: true), Wall, Wall);
                frames++;
                if (!move.Climbing)
                {
                    break;
                }
            }

            Assert.IsFalse(move.Climbing, "stamina runs out");
            // The flag is cleared before the stamina block (Alpha order), so the falling frame already regenerates.
            Assert.LessOrEqual(move.Stamina, 0.01 + 10 * 0.0001 + 1e-9);
            double expectedMs = (1.0 - 0.01) / drainPerMs;
            Assert.AreEqual(expectedMs, frames * 10.0, 30.0, "about 2.2 s of climbing at rank 0");

            double low = move.Stamina;
            Frame(move, 1000, Keys(), Ground, Ground);
            Assert.AreEqual(low + 0.1, move.Stamina, 1e-9, "regenerates 0.0001 per ms on the ground");
        }

        [Test]
        public void ClimbKeyWithoutWallIsNormalMovement()
        {
            var move = new AlphaMovement();
            move.BeginFrame(10, Keys(forward: true, climb: true), East, Air);
            Assert.IsFalse(move.Climbing);
            Assert.AreEqual(-0.3, move.VelocityY, Tolerance);
        }

        // ---- Hang glider ----

        [Test]
        public void GliderNeedsTheItemAndARank()
        {
            var move = new AlphaMovement();
            move.ToggleGlider(0.0);
            Assert.IsFalse(move.GliderOpen, "no glider item");

            var noRank = new AlphaMovement { HasGlider = true };
            noRank.ToggleGlider(0.0);
            Assert.IsTrue(noRank.GliderOpen);
            noRank.BeginFrame(10, Keys(), East, Air);
            Assert.IsFalse(noRank.GliderOpen, "hang gliding rank 0 → curve 0 → flag cleared (0x6194BA)");
        }

        [Test]
        public void GlidingFallsWithGravityThreeAndSteersWithEight()
        {
            var move = new AlphaMovement { HasGlider = true, HangGlidingRank = 1 };
            move.ToggleGlider(0.0);
            Frame(move, 10, Keys(), Air, Air);
            Assert.IsTrue(move.Gliding);
            double vy = move.VelocityY;
            double vx = move.VelocityX;
            move.BeginFrame(10, Keys(forward: true), East, Air);
            Assert.AreEqual(vy - 0.01 * 3.0, move.VelocityY, 1e-9, "gravity 3 while gliding");
            Assert.AreEqual(vx + 0.01 * 8.0, move.VelocityX, 1e-9, "input 8 while gliding");
            Assert.AreEqual(6.0 * AlphaMovement.GlidingCurve(1), move.MaxHorizontalSpeed, 1e-9, "limit × hang gliding curve, no run ×2");
        }

        [Test]
        public void GliderOpensOnlyNearTheApexAndWaterClosesIt()
        {
            var move = new AlphaMovement { HasGlider = true, HangGlidingRank = 1 };
            move.BeginFrame(10, Keys(), East, new MovementContact { OnGround = true, PositionY = 50.0, BodyHeight = 1.8 });
            move.ToggleGlider(47.5);
            Assert.IsFalse(move.GliderOpen, "2.5 blocks below the apex");
            move.ToggleGlider(48.5);
            Assert.IsTrue(move.GliderOpen);
            move.ToggleGlider(48.5);
            Assert.IsFalse(move.GliderOpen, "second press closes");

            move.ToggleGlider(48.5);
            move.BeginFrame(10, Keys(), East, Water(1.0));
            Assert.IsFalse(move.GliderOpen);
        }

        [Test]
        public void GliderBoostCancelsGravityAndDrainsStamina()
        {
            var move = new AlphaMovement { HasGlider = true, HangGlidingRank = 1 };
            move.ToggleGlider(0.0);
            Frame(move, 10, Keys(), Air, Air);
            double vy = move.VelocityY;
            double stamina = move.Stamina;
            move.BeginFrame(10, Keys(jump: true), East, Air);
            Assert.IsTrue(move.Running, "Space with stamina sets flag 0x40 while gliding");
            Assert.AreEqual(vy - 0.01 * 3.0 + 0.01 * 30.0, move.VelocityY, 1e-9);
            Assert.AreEqual(stamina - 10 * 0.0002, move.Stamina, 1e-9);
        }
    }
}
