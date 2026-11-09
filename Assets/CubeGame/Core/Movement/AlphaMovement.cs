using System;

namespace CubeGame.Core.Movement
{
    /// <summary>
    /// Hero locomotion of Alpha 2013 on the ground and in the air (C2, wave 1). Every number is read from the
    /// bytes of <c>Cube.exe</c> (evidence: docs/research/alpha2013/C2_ALPHA_MOVEMENT_PHYSICS.md); status DIRECT.
    ///
    /// Units: Alpha integrates <c>position += (velocity + extra) · dt · 65536</c> (0x61AEB4 → 0x42C580 → 0x42C9B0) and
    /// 65536 position units are one terrain block (<c>vec3i64_toIntFloor</c> 0x42F040). So one Alpha speed unit is one
    /// block per second, and in CubeGame one block is one Unity unit (WorldLayout) — OQ-23 is closed.
    ///
    /// Alpha steps time in whole milliseconds (<c>dt = ms · 0.001</c>, 0x60C852; friction loops once per ms), so this
    /// rule does too. Axes are Unity axes: Y is up (Alpha Z).
    ///
    /// One frame = <see cref="BeginFrame"/> (input handler 0x4A6B50 + velocity part of the physics tick
    /// 0x60C510) → the caller moves the body by the returned displacement with its collision → <see cref="EndFrame"/>
    /// (contact, ground friction, acceleration reset).
    ///
    /// Not in this rule yet (later C2 steps): swimming, climbing, hang glider, boat/riding, attack-state speeds,
    /// slow/haste buffs, the "hard landing" input factor 8 (<c>physics_isLandingHard</c> 0x444650 — condition not
    /// recovered, PARTIAL).
    /// </summary>
    public sealed class AlphaMovement
    {
        /// <summary>Input acceleration per pressed direction key, blocks/s² (0x4A6B8B, f80).</summary>
        public const double InputAcceleration = 80.0;

        /// <summary>Base horizontal speed limit, blocks/s (0x619234: <c>max = 6.0</c>).</summary>
        public const double BaseMaxSpeed = 6.0;

        /// <summary>Run multiplier of action flag 0x40 (0x619DB8: <c>max *= 2.0</c>).</summary>
        public const double RunMultiplier = 2.0;

        /// <summary>Gravity, blocks/s² (0x61AC33: <c>vz -= dt · 30</c>).</summary>
        public const double Gravity = 30.0;

        /// <summary>Ground friction per millisecond without input acceleration (0x61D19E, f0.005).</summary>
        public const double GroundFrictionIdle = 0.005;

        /// <summary>Ground friction per millisecond while input accelerates (0x61D1CF, f0.0025).</summary>
        public const double GroundFrictionMoving = 0.0025;

        /// <summary>Jump: the key starts a jump only during the first 200 ms of holding it (0x4A8CAB, 0xC8).</summary>
        public const int JumpPressWindowMs = 200;

        /// <summary>Jump start speed = horizontal speed + 2 (0x4A8D03), capped at 10 (0x4A8CF7).</summary>
        public const double JumpBaseSpeed = 2.0;

        /// <summary>Vertical speed cap of the jump and its boost, blocks/s (0x4A8DCD, 0x41200000).</summary>
        public const double JumpMaxSpeed = 10.0;

        /// <summary>Boost time after the jump start while the key stays held (0x4A8D42, 0x64 = 100 ms).</summary>
        public const int JumpBoostMs = 100;

        /// <summary>Boost: vertical speed += 0.08 per held millisecond (0x4A8DB7, f0.08 → 80 blocks/s²).</summary>
        public const double JumpBoostPerMs = 0.08;

        private const double Epsilon = 1e-12;

        private int _jumpHeldMs;
        private int _jumpBoostLeftMs;
        private double _accelX;
        private double _accelZ;

        public double VelocityX { get; private set; }

        public double VelocityY { get; private set; }

        public double VelocityZ { get; private set; }

        /// <summary>Alpha action flag 0x40: set while the walk key (left Shift, DIK 0x2A) is NOT held (0x498303).</summary>
        public bool Running { get; private set; } = true;

        /// <summary>Alpha contact flag 1 (standing on a solid block) after the last <see cref="EndFrame"/>.</summary>
        public bool OnGround { get; private set; }

        public double HorizontalSpeed => Math.Sqrt(VelocityX * VelocityX + VelocityZ * VelocityZ);

        /// <summary>Current horizontal speed limit, blocks/s.</summary>
        public double MaxHorizontalSpeed => BaseMaxSpeed * (Running ? RunMultiplier : 1.0);

        /// <summary>
        /// Input handler and the velocity half of the physics tick.
        /// <paramref name="inputX"/>/<paramref name="inputZ"/> is the sum of the unit world directions of the pressed
        /// direction keys (Alpha adds 80 per key, so a diagonal is not normalised); <paramref name="onGround"/> is the
        /// contact of the previous frame. Returns the displacement in blocks for this frame.
        /// </summary>
        public (double X, double Y, double Z) BeginFrame(int elapsedMs, double inputX, double inputZ, bool walkHeld, bool jumpHeld, bool onGround)
        {
            if (elapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            }

            OnGround = onGround;
            Running = !walkHeld;
            _accelX += inputX * InputAcceleration;
            _accelZ += inputZ * InputAcceleration;
            ApplyJumpKey(elapsedMs, jumpHeld);

            double dt = elapsedMs * 0.001;

            // 0x618F44: delta = acceleration · dt, added to the velocity, then the horizontal clamp (0x61AA49…0x61AAFC).
            VelocityX += _accelX * dt;
            VelocityZ += _accelZ * dt;
            double max = MaxHorizontalSpeed;
            double lengthSq = VelocityX * VelocityX + VelocityZ * VelocityZ;
            if (lengthSq > max * max)
            {
                double scale = max / Math.Sqrt(lengthSq);
                VelocityX *= scale;
                VelocityZ *= scale;
            }

            // 0x61AC2B: gravity.
            VelocityY -= dt * Gravity;

            // 0x61AEB4: displacement = velocity · dt.
            return (VelocityX * dt, VelocityY * dt, VelocityZ * dt);
        }

        /// <summary>
        /// After the caller's collision: <paramref name="onGround"/> is the new contact, <paramref name="hitCeiling"/>
        /// a blocked upward move. Applies ground friction once per millisecond (0x61D73D → 0x605950) and resets the
        /// input acceleration (0x61EBDA).
        /// </summary>
        public void EndFrame(int elapsedMs, bool onGround, bool hitCeiling)
        {
            OnGround = onGround;
            if (onGround && VelocityY < 0.0)
            {
                VelocityY = 0.0;
            }

            if (hitCeiling && VelocityY > 0.0)
            {
                VelocityY = 0.0;
            }

            if (onGround && elapsedMs > 0)
            {
                bool accelerating = _accelX * _accelX + _accelZ * _accelZ > Epsilon;
                double k = accelerating ? GroundFrictionMoving : GroundFrictionIdle;
                double keep = Math.Pow(1.0 - k, elapsedMs);
                VelocityX *= keep;
                VelocityY *= keep;
                VelocityZ *= keep;
            }

            _accelX = 0.0;
            _accelZ = 0.0;
        }

        public void Stop()
        {
            VelocityX = 0.0;
            VelocityY = 0.0;
            VelocityZ = 0.0;
            _accelX = 0.0;
            _accelZ = 0.0;
            _jumpHeldMs = 0;
            _jumpBoostLeftMs = 0;
        }

        /// <summary>Jump key handling of the input handler (0x4A8C8A…0x4A8DDE), without the water branch.</summary>
        private void ApplyJumpKey(int elapsedMs, bool jumpHeld)
        {
            int boost = _jumpBoostLeftMs;
            if (jumpHeld)
            {
                _jumpHeldMs += elapsedMs;
                if (_jumpHeldMs > 0 && _jumpHeldMs < JumpPressWindowMs && OnGround && VelocityY <= 0.0)
                {
                    double start = HorizontalSpeed + JumpBaseSpeed;
                    VelocityY = start < JumpMaxSpeed ? start : JumpMaxSpeed;
                    boost = JumpBoostMs;
                }

                if (boost > 0 && VelocityY < JumpMaxSpeed)
                {
                    VelocityY += elapsedMs * JumpBoostPerMs;
                    if (VelocityY >= JumpMaxSpeed)
                    {
                        VelocityY = JumpMaxSpeed;
                    }
                }
            }
            else
            {
                _jumpHeldMs = 0;
            }

            boost -= elapsedMs;
            _jumpBoostLeftMs = boost < 0 ? 0 : boost;
        }
    }
}
