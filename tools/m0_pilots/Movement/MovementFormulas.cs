using System;

namespace CubeGame.M0Pilots.Movement
{
    /// <summary>
    /// Movement formulas proven in I_movement.md and P0 evidence §B.3-B.4.
    ///
    /// IMPORTANT: the skill values below are the *stat description* formulas (percentages shown
    /// by the game). Applying them to physical speed was not proven (PARTIAL: B-U5).
    /// No function here converts Alpha speed units to blocks/second (OQ-23 is OPEN).
    /// </summary>
    public static class MovementFormulas
    {
        public const double Gravity = 30.0;
        public const double GlideGravity = 3.0;
        public const double PlayerBase = 20.0;
        public const double BaseSpeed = 6.0;
        public const double BossSpeed = 8.0;
        public const double NpcSpeed = 12.0;
        public const double SprintMultiplier = 2.0;
        public const double DashImpulse = 40.0;
        public const double DashUpImpulse = 5.0;
        public const int DashDurationMs = 600;
        public const double DashStaminaCost = 0.25;
        public const double StaminaDrainPerMs = 0.0002;
        public const double StaminaRegenPerMs = 0.0001;

        public const int ContactSolid = 1;
        public const int ContactWater = 2;
        public const int ContactPush = 4;
        public const int ContactAirAbove = 0x10;

        public const int ActionClimbing = 0x1;
        public const int ActionAirOrGlider = 0x10;
        public const int ActionSprint = 0x40;
        public const int ActionTorch = 0x200;
        public const int ActionAim = 0x400;

        /// <summary>@ridingspeed = 100 * (1 - 1/(0.1n+1) + 1); n &lt; 1 yields 0.</summary>
        public static double RidingPercent(int rank)
        {
            return rank < 1 ? 0.0 : 100.0 * (1.0 - 1.0 / (0.1 * rank + 1.0) + 1.0);
        }

        /// <summary>@climbingpower = 100 * (1 - 1/(0.1(n+1)+1)); no guard at n &lt; 1 (rank 0 = 9.09%).</summary>
        public static double ClimbingPercent(int rank)
        {
            return 100.0 * (1.0 - 1.0 / (0.1 * (rank + 1.0) + 1.0));
        }

        /// <summary>@flyingspeed = 100 * ((1 - 1/(0.1n+1))*3 + 1.5); n &lt; 1 yields 0.</summary>
        public static double FlyingPercent(int rank)
        {
            return rank < 1 ? 0.0 : 100.0 * ((1.0 - 1.0 / (0.1 * rank + 1.0)) * 3.0 + 1.5);
        }

        /// <summary>@swimmingspeed = 100 * ((1 - 1/(0.1n+1))*0.5 + 0.5); n &lt; 1 yields 50%.</summary>
        public static double SwimmingPercent(int rank)
        {
            return rank < 1 ? 50.0 : 100.0 * ((1.0 - 1.0 / (0.1 * rank + 1.0)) * 0.5 + 0.5);
        }

        /// <summary>@boatspeed = 100 * ((1 - 1/(0.1n+1))*3 + 1); n &lt; 1 yields 0.</summary>
        public static double BoatPercent(int rank)
        {
            return rank < 1 ? 0.0 : 100.0 * ((1.0 - 1.0 / (0.1 * rank + 1.0)) * 3.0 + 1.0);
        }

        /// <summary>Vertical velocity after dt: velY -= dt * (glider ? 3 : 30).</summary>
        public static double VerticalVelocity(double initialVelocity, int elapsedMs, bool gliding)
        {
            if (elapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            }

            double gravity = gliding ? GlideGravity : Gravity;
            return initialVelocity - (elapsedMs / 1000.0) * gravity;
        }

        /// <summary>Player horizontal multiplier including sprint; still in Alpha units, not m/s.</summary>
        public static double PlayerHorizontalMultiplier(bool sprinting)
        {
            return PlayerBase * (sprinting ? SprintMultiplier : 1.0);
        }
    }

    public sealed class StaminaModel
    {
        private double _value = 1.0;

        public double Value => _value;

        public void DrainForSprint(int elapsedMs)
        {
            if (elapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            }

            _value = Clamp(_value - MovementFormulas.StaminaDrainPerMs * elapsedMs);
        }

        public void Regenerate(int elapsedMs)
        {
            if (elapsedMs < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time cannot be negative.");
            }

            _value = Clamp(_value + MovementFormulas.StaminaRegenPerMs * elapsedMs);
        }

        /// <summary>
        /// CUBEGAME_CONTRACT (not an Alpha constant): dash requires the full 0.25 cost.
        /// The cost and duration themselves are DIRECT.
        /// </summary>
        public bool TrySpendDash()
        {
            if (_value + 1e-12 < MovementFormulas.DashStaminaCost)
            {
                return false;
            }

            _value = Clamp(_value - MovementFormulas.DashStaminaCost);
            return true;
        }

        public void SetValue(double value)
        {
            _value = Clamp(value);
        }

        private static double Clamp(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            return value > 1.0 ? 1.0 : value;
        }
    }

    public sealed class DashState
    {
        public bool IsActive { get; private set; }

        public int RemainingMs { get; private set; }

        public bool TryStart(StaminaModel stamina)
        {
            if (IsActive)
            {
                return false;
            }

            if (!stamina.TrySpendDash())
            {
                return false;
            }

            IsActive = true;
            RemainingMs = MovementFormulas.DashDurationMs;
            return true;
        }

        public void Tick(int elapsedMs)
        {
            if (!IsActive)
            {
                return;
            }

            RemainingMs -= elapsedMs;
            if (RemainingMs <= 0)
            {
                RemainingMs = 0;
                IsActive = false;
            }
        }

        public void Reset()
        {
            IsActive = false;
            RemainingMs = 0;
        }
    }

    public sealed class ContactFlags
    {
        public ContactFlags(int bits)
        {
            Bits = bits;
        }

        public int Bits { get; }

        public bool OnSolid => (Bits & MovementFormulas.ContactSolid) != 0;

        public bool InWater => (Bits & MovementFormulas.ContactWater) != 0;

        public bool Pushed => (Bits & MovementFormulas.ContactPush) != 0;

        public bool AirAbove => (Bits & MovementFormulas.ContactAirAbove) != 0;
    }

    public sealed class ActionFlags
    {
        public ActionFlags(int bits)
        {
            Bits = bits;
        }

        public int Bits { get; }

        public bool Climbing => (Bits & MovementFormulas.ActionClimbing) != 0;

        public bool AirOrGlider => (Bits & MovementFormulas.ActionAirOrGlider) != 0;

        public bool Sprinting => (Bits & MovementFormulas.ActionSprint) != 0;

        public bool Torch => (Bits & MovementFormulas.ActionTorch) != 0;

        public bool Aiming => (Bits & MovementFormulas.ActionAim) != 0;
    }

    public enum MovementMode
    {
        Grounded = 0,
        Sprinting = 1,
        Swimming = 2,
        Climbing = 3,
        Gliding = 4,
        Falling = 5,
    }

    /// <summary>
    /// Deterministic mode selection for the pilot. STATUS: CUBEGAME_CONTRACT.
    /// Alpha exposes the flags, but the precedence between them was not recovered; this order
    /// is a pilot integration decision, not an Alpha fact.
    /// </summary>
    public static class MovementModeSelector
    {
        public static MovementMode Select(ContactFlags contact, ActionFlags actions, bool hasGlider)
        {
            if (actions.Climbing)
            {
                return MovementMode.Climbing;
            }

            if (contact.InWater)
            {
                return MovementMode.Swimming;
            }

            if (!contact.OnSolid)
            {
                return actions.AirOrGlider && hasGlider ? MovementMode.Gliding : MovementMode.Falling;
            }

            return actions.Sprinting ? MovementMode.Sprinting : MovementMode.Grounded;
        }
    }

    public static class MovementScale
    {
        /// <summary>OQ-23 remains OPEN: Alpha speed numbers are not proven to be blocks/second.</summary>
        public const bool IsMetersPerSecondProven = false;

        /// <summary>The physical use of stat-description multipliers is not proven (B-U5).</summary>
        public const bool SkillMultiplierAppliedToPhysics = false;
    }
}
