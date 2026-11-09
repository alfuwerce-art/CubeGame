using System;

namespace CubeGame.Core.Movement
{
    /// <summary>Keys of one frame (Alpha DirectInput table, WinMain 0x4C91F2).</summary>
    public struct MovementKeys
    {
        public bool Forward;   // W, GC+0x0B
        public bool Back;      // S, GC+0x0C
        public bool Left;      // A, GC+0x0D
        public bool Right;     // D, GC+0x0E
        public bool Jump;      // Space, GC+0x12
        public bool Climb;     // left Ctrl, GC+0x13 → action flag 0x1
        public bool Walk;      // left Shift, GC+0x14 → action flag 0x40 cleared
    }

    /// <summary>Camera axes, Unity space (Y up): the 3D look direction and the horizontal right vector.</summary>
    public struct MovementView
    {
        public double LookX;
        public double LookY;
        public double LookZ;
        public double RightX;
        public double RightZ;

        public static MovementView FromYaw(double forwardX, double forwardZ)
        {
            double len = Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
            if (len < 1e-9)
            {
                forwardX = 0.0;
                forwardZ = 1.0;
                len = 1.0;
            }

            forwardX /= len;
            forwardZ /= len;
            return new MovementView { LookX = forwardX, LookY = 0.0, LookZ = forwardZ, RightX = forwardZ, RightZ = -forwardX };
        }
    }

    /// <summary>Contacts measured by the caller's collision (Alpha contact flags +0x5C).</summary>
    public struct MovementContact
    {
        /// <summary>Flag 1: standing on a solid block.</summary>
        public bool OnGround;

        /// <summary>Flag 2: a water block overlaps the body.</summary>
        public bool InWater;

        /// <summary>Flag 4: a wall blocks the body horizontally.</summary>
        public bool AtWall;

        /// <summary>The upward move was blocked.</summary>
        public bool HitCeiling;

        /// <summary>Top of the highest water block overlapping the body minus the body centre, blocks (0x61D0DC).</summary>
        public double WaterDepth;

        /// <summary>Body height, blocks (Alpha <c>+0x80[2]</c>).</summary>
        public double BodyHeight;

        /// <summary>Current vertical position, blocks (for the glider apex rule).</summary>
        public double PositionY;
    }

    /// <summary>
    /// Hero locomotion of Alpha 2013 (C2). Every number is read from the bytes of <c>Cube.exe</c>
    /// (evidence: docs/research/alpha2013/C2_ALPHA_MOVEMENT_PHYSICS.md); status DIRECT unless marked.
    ///
    /// Units: Alpha integrates <c>position += (velocity + extra) · dt · 65536</c> (0x61AEB4 → 0x42C580 → 0x42C9B0) and
    /// 65536 position units are one terrain block (0x42F040). One Alpha speed unit is one block per second, and one
    /// CubeGame block is one Unity unit — OQ-23 is closed. Axes are Unity axes: Y is up (Alpha Z).
    ///
    /// Alpha steps time in whole milliseconds (<c>dt = ms · 0.001</c>, 0x60C852; drag loops run once per ms).
    /// One frame = <see cref="BeginFrame"/> (input handler 0x4A6B50 + velocity part of the physics tick 0x60C510)
    /// → the caller moves the body by the returned displacement with its collision → <see cref="EndFrame"/>
    /// (buoyancy, ground friction, water drag, acceleration reset).
    ///
    /// Modes: ground/air, swimming (water contact), climbing (Ctrl at a wall), hang glider (G with the glider item).
    /// Skill ranks are the Alpha rank array <c>+0x1138</c>: climbing +0x1140, hang gliding +0x1144, swimming +0x1148.
    /// Not here: boat (needs the Boat item and rank ≥ 1, state 0x6B), glider wind field (0x5A6270, PARTIAL),
    /// glider crash stun, attack-state speeds and the dash (C4).
    /// </summary>
    public sealed class AlphaMovement
    {
        /// <summary>Input acceleration per pressed direction key, blocks/s² (0x4A6B8B, f80).</summary>
        public const double InputAcceleration = 80.0;

        /// <summary>Input acceleration while gliding (0x4A6BAA, f8; <c>isGliding</c> 0x444650).</summary>
        public const double GlideInputAcceleration = 8.0;

        /// <summary>Climbing input factor: 80 · 0.2 = 16 blocks/s² up/down/sideways (0x4A6EAE, 0x4A7295).</summary>
        public const double ClimbInputFactor = 0.2;

        /// <summary>Base horizontal speed limit, blocks/s (0x619234: <c>max = 6.0</c>).</summary>
        public const double BaseMaxSpeed = 6.0;

        /// <summary>Run multiplier of action flag 0x40 (0x619DB8: <c>max *= 2.0</c>).</summary>
        public const double RunMultiplier = 2.0;

        /// <summary>Gravity, blocks/s² (0x61AC2B: <c>vz -= dt · 30</c>).</summary>
        public const double Gravity = 30.0;

        /// <summary>Gravity factor while gliding (0x61ABBB: <c>dt · 0.1 · 30</c>).</summary>
        public const double GlideGravityFactor = 0.1;

        /// <summary>Ground friction per millisecond without input acceleration (0x61D19E, f0.005).</summary>
        public const double GroundFrictionIdle = 0.005;

        /// <summary>Ground friction per millisecond while input accelerates (0x61D1CF, f0.0025).</summary>
        public const double GroundFrictionMoving = 0.0025;

        /// <summary>Water drag per millisecond on all axes (0x61E6A5, 0x3BA3D70A = 0.005).</summary>
        public const double WaterDrag = 0.005;

        /// <summary>Climbing drag per millisecond toward rest (0x61AB2C, 0x3B23D70A = 0.0025).</summary>
        public const double ClimbDrag = 0.0025;

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

        /// <summary>In water the jump key lifts by 0.001 per ms and keeps the boost alive (0x4A8D66…0x4A8D7E).</summary>
        public const double WaterRisePerMs = 0.001;

        /// <summary>Stamina regeneration per ms (0x619D49, f0.0001).</summary>
        public const double StaminaRegenPerMs = 0.0001;

        /// <summary>Stamina drain per ms of the glider boost (0x619D1C, f0.0002).</summary>
        public const double GlideBoostDrainPerMs = 0.0002;

        /// <summary>Climbing drain per ms = (1 − climbing curve) · 0.0005 (decomp 100183).</summary>
        public const double ClimbDrainPerMs = 0.0005;

        /// <summary>Climbing stops when stamina falls to 0.01 or below (decomp 100187).</summary>
        public const double ClimbMinStamina = 0.01;

        /// <summary>The glider opens only within 2 blocks below the last apex (0x47E88C → 0x42C860).</summary>
        public const double GlideOpenMaxDrop = 2.0;

        private const double Epsilon = 1e-12;

        private int _jumpHeldMs;
        private int _jumpBoostLeftMs;
        private double _accelX;
        private double _accelY;
        private double _accelZ;
        private double _apexY;
        private bool _climbFlag;

        public double VelocityX { get; private set; }

        public double VelocityY { get; private set; }

        public double VelocityZ { get; private set; }

        /// <summary>Alpha action flag 0x40. On foot: set while left Shift is NOT held (0x498303). Gliding: Space with stamina.</summary>
        public bool Running { get; private set; } = true;

        /// <summary>Stamina +0x1194, 0…1.</summary>
        public double Stamina { get; private set; } = 1.0;

        /// <summary>Action flag 0x10: the glider is open.</summary>
        public bool GliderOpen { get; private set; }

        public bool OnGround { get; private set; }

        public bool InWater { get; private set; }

        /// <summary>Climbing on a wall this frame (flag 0x1, contact 4, not gliding).</summary>
        public bool Climbing { get; private set; }

        /// <summary><c>isGliding</c> (0x444650): glider open, falling, not on the ground.</summary>
        public bool Gliding => GliderOpen && VelocityY < 0.0 && !OnGround;

        /// <summary>The hero carries a hang glider (special slot, item 0x17/0). Set by the item system.</summary>
        public bool HasGlider { get; set; }

        public int ClimbingRank { get; set; }

        public int HangGlidingRank { get; set; }

        public int SwimmingRank { get; set; }

        public double HorizontalSpeed => Math.Sqrt(VelocityX * VelocityX + VelocityZ * VelocityZ);

        /// <summary>Horizontal speed limit of the last frame, blocks/s.</summary>
        public double MaxHorizontalSpeed { get; private set; } = BaseMaxSpeed * RunMultiplier;

        /// <summary>curve_diminishingHalf 0x4476A0: rank &lt; 1 → 0.5, else (1 − 1/(0.1r + 1)) · 0.5 + 0.5.</summary>
        public static double SwimmingCurve(int rank) => rank < 1 ? 0.5f : (1f - 1f / (rank * 0.1f + 1f)) * 0.5f + 0.5f;

        /// <summary>curve_diminishingN1 0x43E660: 1 − 1/(0.1(r + 1) + 1), no guard.</summary>
        public static double ClimbingCurve(int rank) => 1f - 1f / ((rank + 1) * 0.1f + 1f);

        /// <summary>curve_diminishingx3p15 0x43F770: rank &lt; 1 → 0, else (1 − 1/(0.1r + 1)) · 3 + 1.5.</summary>
        public static double GlidingCurve(int rank) => rank < 1 ? 0f : (1f - 1f / (rank * 0.1f + 1f)) * 3f + 1.5f;

        /// <summary>
        /// Input handler and the velocity half of the physics tick. <paramref name="contact"/> is the contact of the
        /// previous frame plus the current position. Returns the displacement in blocks for this frame.
        /// </summary>
        public (double X, double Y, double Z) BeginFrame(int elapsedMs, MovementKeys keys, MovementView view, MovementContact contact)
        {
            if (elapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            }

            OnGround = contact.OnGround;
            InWater = contact.InWater;
            if (InWater)
            {
                GliderOpen = false;   // 0x61AC63: water closes the glider
            }

            // 0x4982D0…0x498313: action flags of the hero.
            bool glidingInAir = GliderOpen && !OnGround;
            Running = glidingInAir ? keys.Jump && Stamina > 0.0 : !keys.Walk;
            _climbFlag = keys.Climb;
            bool atWall = _climbFlag && contact.AtWall;

            ApplyDirectionKeys(keys, view, atWall);
            ApplyJumpKey(elapsedMs, keys.Jump);

            double dt = elapsedMs * 0.001;
            double ms = elapsedMs;

            // 0x619234…: horizontal speed limit.
            double max = BaseMaxSpeed;
            if (InWater)
            {
                max *= SwimmingCurve(SwimmingRank);
            }

            if (GliderOpen)
            {
                double glide = GlidingCurve(HangGlidingRank);
                if (glide <= 0.0)
                {
                    GliderOpen = false;   // 0x6194BA: no hang gliding rank → flag cleared
                }
                else if (!OnGround)
                {
                    max *= glide;
                }
            }

            // Climbing stamina (decomp 100177…100190): only at a wall, not gliding, in the air, while moving.
            double speedSq = VelocityX * VelocityX + VelocityY * VelocityY + VelocityZ * VelocityZ;
            if (_climbFlag && speedSq > 0.0)
            {
                double climb = ClimbingCurve(ClimbingRank);
                if (climb <= 0.0)
                {
                    _climbFlag = false;
                }
                else if (!GliderOpen && contact.AtWall && !OnGround)
                {
                    Stamina = Math.Max(0.0, Stamina - (1.0 - climb) * ClimbDrainPerMs * ms);
                    if (Stamina <= ClimbMinStamina)
                    {
                        _climbFlag = false;
                    }
                }
            }

            // Stamina regeneration / glider boost drain (0x619CF9…0x619D77).
            bool gliderInAir = GliderOpen && !OnGround;
            if (gliderInAir)
            {
                if (Running)
                {
                    Stamina -= GlideBoostDrainPerMs * ms;
                }
            }
            else if (!(_climbFlag && contact.AtWall && !OnGround))
            {
                Stamina += StaminaRegenPerMs * ms;
            }

            Stamina = Math.Min(1.0, Math.Max(0.0, Stamina));

            // 0x619D95: run doubles the limit unless climbing at a wall or gliding in the air.
            Climbing = _climbFlag && contact.AtWall && !GliderOpen;
            if (Running && !(_climbFlag && contact.AtWall) && !gliderInAir)
            {
                max *= RunMultiplier;
            }

            MaxHorizontalSpeed = max;

            // 0x618F44: delta = acceleration · dt; then the horizontal clamp (0x61AA49…0x61AAFC).
            VelocityX += _accelX * dt;
            VelocityY += _accelY * dt;
            VelocityZ += _accelZ * dt;
            double lengthSq = VelocityX * VelocityX + VelocityZ * VelocityZ;
            if (lengthSq > max * max)
            {
                double scale = max / Math.Sqrt(lengthSq);
                VelocityX *= scale;
                VelocityZ *= scale;
            }

            // 0x61AB04…0x61AC43: climbing drag (no gravity) or gravity (×0.1 while gliding).
            if (Climbing)
            {
                double keep = Math.Pow(1.0 - ClimbDrag, elapsedMs);
                VelocityX *= keep;
                VelocityY *= keep;
                VelocityZ *= keep;
            }
            else if (Gliding)
            {
                VelocityY -= dt * GlideGravityFactor * Gravity;
            }
            else
            {
                if (VelocityY >= 0.0)
                {
                    _apexY = contact.PositionY;   // 0x61AC1F: apex height +0x13BC
                }

                VelocityY -= dt * Gravity;
            }

            // 0x61AD36: glider boost — Space (flag 0x40) cancels gravity while falling.
            if (gliderInAir && !OnGround && Running && VelocityY < 0.0)
            {
                VelocityY += dt * Gravity;
            }

            // 0x61AEB4: displacement = velocity · dt.
            return (VelocityX * dt, VelocityY * dt, VelocityZ * dt);
        }

        /// <summary>
        /// After the caller's collision: buoyancy (0x61D0D2), ground friction (0x61D73D), water drag (0x61E6D1) and the
        /// acceleration reset (0x61EBDA).
        /// </summary>
        public void EndFrame(int elapsedMs, MovementContact contact)
        {
            OnGround = contact.OnGround;
            InWater = contact.InWater;
            if (OnGround && VelocityY < 0.0)
            {
                VelocityY = 0.0;
            }

            if (contact.HitCeiling && VelocityY > 0.0)
            {
                VelocityY = 0.0;
            }

            double dt = elapsedMs * 0.001;
            if (InWater && contact.BodyHeight > 0.0)
            {
                double f = contact.WaterDepth / (contact.BodyHeight * 0.5);
                double factor = f <= 0.0 ? 0.0 : f < 1.0 ? f + 1.0 : 1.0;
                VelocityY += dt * Gravity * factor;
            }

            if (OnGround && elapsedMs > 0)
            {
                bool accelerating = _accelX * _accelX + _accelY * _accelY + _accelZ * _accelZ > Epsilon;
                double k = accelerating ? GroundFrictionMoving : GroundFrictionIdle;
                Damp(Math.Pow(1.0 - k, elapsedMs));
            }

            if (InWater && elapsedMs > 0)
            {
                Damp(Math.Pow(1.0 - WaterDrag, elapsedMs));
            }

            _accelX = 0.0;
            _accelY = 0.0;
            _accelZ = 0.0;
        }

        /// <summary>
        /// The G key (handleKeyPress 0x47E83C) with a hang glider: opens it only within 2 blocks below the last apex
        /// and when it is closed; otherwise closes it. Without a glider it closes.
        /// </summary>
        public void ToggleGlider(double positionY)
        {
            if (!HasGlider)
            {
                GliderOpen = false;
                return;
            }

            GliderOpen = _apexY - positionY < GlideOpenMaxDrop && !GliderOpen;
        }

        public void Stop()
        {
            VelocityX = 0.0;
            VelocityY = 0.0;
            VelocityZ = 0.0;
            _accelX = 0.0;
            _accelY = 0.0;
            _accelZ = 0.0;
            _jumpHeldMs = 0;
            _jumpBoostLeftMs = 0;
            GliderOpen = false;
        }

        /// <summary>Direction keys (0x4A6EC4…0x4A7769): at a wall, in water, or on foot/in the air.</summary>
        private void ApplyDirectionKeys(MovementKeys keys, MovementView view, bool atWall)
        {
            double forward = (keys.Forward ? 1.0 : 0.0) - (keys.Back ? 1.0 : 0.0);
            double strafe = (keys.Right ? 1.0 : 0.0) - (keys.Left ? 1.0 : 0.0);
            if (atWall && !GliderOpen)
            {
                double a = InputAcceleration * ClimbInputFactor;
                _accelY += forward * a;
                _accelX += strafe * a * view.RightX;
                _accelZ += strafe * a * view.RightZ;
                return;
            }

            double scale = Gliding ? GlideInputAcceleration : InputAcceleration;
            double fx;
            double fy;
            double fz;
            if (InWater)
            {
                // Swimming follows the full camera look, including its pitch (0x4A73F3…0x4A74D2).
                fx = view.LookX;
                fy = view.LookY;
                fz = view.LookZ;
            }
            else
            {
                double len = Math.Sqrt(view.LookX * view.LookX + view.LookZ * view.LookZ);
                fx = len > 1e-9 ? view.LookX / len : 0.0;
                fy = 0.0;
                fz = len > 1e-9 ? view.LookZ / len : 1.0;
            }

            _accelX += (forward * fx + strafe * view.RightX) * scale;
            _accelY += forward * fy * scale;
            _accelZ += (forward * fz + strafe * view.RightZ) * scale;
        }

        /// <summary>Jump key handling of the input handler (0x4A8C8A…0x4A8DDE).</summary>
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

                if (InWater && VelocityY < JumpMaxSpeed)
                {
                    VelocityY += elapsedMs * WaterRisePerMs;
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

        private void Damp(double keep)
        {
            VelocityX *= keep;
            VelocityY *= keep;
            VelocityZ *= keep;
        }
    }
}
