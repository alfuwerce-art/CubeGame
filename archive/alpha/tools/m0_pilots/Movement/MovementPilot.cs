using System;

namespace CubeGame.M0Pilots.Movement
{
    public static class MovementPilot
    {
        // Percent table from M0_00B_P0_MECHANICS_EVIDENCE §B.3 (ranks 0..10).
        private static readonly double[] Ride = { 0.0, 109.1, 116.7, 123.1, 128.6, 133.3, 137.5, 141.2, 144.4, 147.4, 150.0 };
        private static readonly double[] Climb = { 9.1, 16.7, 23.1, 28.6, 33.3, 37.5, 41.2, 44.4, 47.4, 50.0, 52.4 };
        private static readonly double[] Fly = { 0.0, 177.3, 200.0, 219.2, 235.7, 250.0, 262.5, 273.5, 283.3, 292.1, 300.0 };
        private static readonly double[] Swim = { 50.0, 54.5, 58.3, 61.5, 64.3, 66.7, 68.8, 70.6, 72.2, 73.7, 75.0 };
        private static readonly double[] Boat = { 0.0, 127.3, 150.0, 169.2, 185.7, 200.0, 212.5, 223.5, 233.3, 242.1, 250.0 };

        public static PilotResult Run()
        {
            Check.Start("PILOT_MOVEMENT");
            SkillTables();
            VerticalMotion();
            Stamina();
            Dash();
            FlagsAndModes();
            HonestLimits();
            return Check.Finish();
        }

        private static void SkillTables()
        {
            for (int rank = 0; rank <= 10; rank++)
            {
                Check.Near("ride rank " + rank + " = " + Ride[rank] + "%", Ride[rank], MovementFormulas.RidingPercent(rank), 0.05);
                Check.Near("climb rank " + rank + " = " + Climb[rank] + "%", Climb[rank], MovementFormulas.ClimbingPercent(rank), 0.05);
                Check.Near("fly rank " + rank + " = " + Fly[rank] + "%", Fly[rank], MovementFormulas.FlyingPercent(rank), 0.05);
                Check.Near("swim rank " + rank + " = " + Swim[rank] + "%", Swim[rank], MovementFormulas.SwimmingPercent(rank), 0.05);
                Check.Near("boat rank " + rank + " = " + Boat[rank] + "%", Boat[rank], MovementFormulas.BoatPercent(rank), 0.05);
            }

            Check.True("riding has a rank<1 guard", MovementFormulas.RidingPercent(0) == 0.0);
            Check.True("flying has a rank<1 guard", MovementFormulas.FlyingPercent(0) == 0.0);
            Check.True("boat has a rank<1 guard", MovementFormulas.BoatPercent(0) == 0.0);
            Check.True("swimming has a 50% rank<1 floor", Math.Abs(MovementFormulas.SwimmingPercent(0) - 50.0) < 1e-9);
            Check.True("climbing has no rank<1 guard", MovementFormulas.ClimbingPercent(0) > 9.0);
        }

        private static void VerticalMotion()
        {
            Check.Near("gravity 30 over 1 s changes velY by -30", -30.0,
                MovementFormulas.VerticalVelocity(0.0, 1000, false), 1e-9);
            Check.Near("glide gravity 3 over 1 s changes velY by -3", -3.0,
                MovementFormulas.VerticalVelocity(0.0, 1000, true), 1e-9);
            Check.Near("glide velocity equals falling velocity / 10", -3.0,
                MovementFormulas.VerticalVelocity(0.0, 1000, false) / 10.0, 1e-9);
            Check.Near("upward velocity integrates with the same gravity", 5.0,
                MovementFormulas.VerticalVelocity(20.0, 500, false), 1e-9);
            Check.Throws<ArgumentOutOfRangeException>("negative elapsed time is rejected",
                () => MovementFormulas.VerticalVelocity(0.0, -1, false));
        }

        private static void Stamina()
        {
            var stamina = new StaminaModel();
            Check.Near("stamina starts full", 1.0, stamina.Value, 1e-12);

            stamina.DrainForSprint(2500);
            Check.Near("sprint drains 0.0002/ms: 2500 ms -> 0.5", 0.5, stamina.Value, 1e-9);

            stamina.DrainForSprint(2500);
            Check.Near("sprint reaches zero after 5000 ms", 0.0, stamina.Value, 1e-9);

            stamina.DrainForSprint(1000);
            Check.Near("stamina never goes below zero", 0.0, stamina.Value, 1e-12);

            stamina.Regenerate(5000);
            Check.Near("regen adds 0.0001/ms: 5000 ms -> 0.5", 0.5, stamina.Value, 1e-9);

            stamina.Regenerate(10000);
            Check.Near("stamina never exceeds one", 1.0, stamina.Value, 1e-12);

            stamina.SetValue(0.2);
            Check.True("dash at 0.2 stamina is refused", !stamina.TrySpendDash());
            Check.Near("refused dash does not change stamina", 0.2, stamina.Value, 1e-12);

            stamina.SetValue(0.25);
            Check.True("dash at exactly 0.25 is allowed", stamina.TrySpendDash());
            Check.Near("dash spends exactly 0.25", 0.0, stamina.Value, 1e-12);

            Check.Throws<ArgumentOutOfRangeException>("negative drain time is rejected",
                () => stamina.DrainForSprint(-1));
        }

        private static void Dash()
        {
            var stamina = new StaminaModel();
            var dash = new DashState();
            Check.True("dash starts with enough stamina", dash.TryStart(stamina));
            Check.True("dash is active after start", dash.IsActive);
            Check.Equal("dash timer starts at 600 ms", 600, dash.RemainingMs);

            Check.True("second dash cannot start while active", !dash.TryStart(stamina));

            dash.Tick(599);
            Check.True("dash is still active at 599 ms", dash.IsActive);
            Check.Equal("dash timer is 1 ms", 1, dash.RemainingMs);

            dash.Tick(1);
            Check.True("dash ends exactly at 600 ms", !dash.IsActive);
            Check.Equal("dash timer is clamped to zero", 0, dash.RemainingMs);

            var poor = new StaminaModel();
            poor.SetValue(0.24);
            Check.True("dash is refused below the 0.25 cost", !dash.TryStart(poor));

            Check.Near("dash impulse is 40 horizontal + 5 up", 45.0,
                MovementFormulas.DashImpulse + MovementFormulas.DashUpImpulse, 1e-12);
        }

        private static void FlagsAndModes()
        {
            var solid = new ContactFlags(MovementFormulas.ContactSolid);
            Check.True("contact flag 1 means solid", solid.OnSolid && !solid.InWater && !solid.AirAbove);

            var water = new ContactFlags(MovementFormulas.ContactWater | MovementFormulas.ContactSolid);
            Check.True("contact flag 2 means water", water.InWater && water.OnSolid);

            var air = new ContactFlags(0);
            Check.True("empty contact flags mean no solid ground", !air.OnSolid);

            var sprint = new ActionFlags(MovementFormulas.ActionSprint);
            Check.True("action flag 0x40 means sprint", sprint.Sprinting && !sprint.Aiming);

            var climbing = new ActionFlags(MovementFormulas.ActionClimbing | MovementFormulas.ActionAirOrGlider);
            Check.True("action flag 0x1 means climbing", climbing.Climbing && climbing.AirOrGlider);

            var aim = new ActionFlags(MovementFormulas.ActionAim | MovementFormulas.ActionTorch);
            Check.True("action flags 0x400/0x200 mean aim/torch", aim.Aiming && aim.Torch);

            Check.True("sprint doubles the player multiplier (still Alpha units)",
                Math.Abs(MovementFormulas.PlayerHorizontalMultiplier(true)
                         - 2.0 * MovementFormulas.PlayerHorizontalMultiplier(false)) < 1e-9);

            Check.Equal("climbing outranks water and ground", MovementMode.Climbing,
                MovementModeSelector.Select(water, climbing, false));
            Check.Equal("water outranks ground", MovementMode.Swimming,
                MovementModeSelector.Select(water, new ActionFlags(0), false));
            Check.Equal("air with a glider is gliding", MovementMode.Gliding,
                MovementModeSelector.Select(air, new ActionFlags(MovementFormulas.ActionAirOrGlider), true));
            Check.Equal("air without a glider is falling", MovementMode.Falling,
                MovementModeSelector.Select(air, new ActionFlags(MovementFormulas.ActionAirOrGlider), false));
            Check.Equal("solid ground with sprint flag is sprinting", MovementMode.Sprinting,
                MovementModeSelector.Select(solid, sprint, false));
            Check.Equal("solid ground without sprint is grounded", MovementMode.Grounded,
                MovementModeSelector.Select(solid, new ActionFlags(0), false));
        }

        private static void HonestLimits()
        {
            Check.True("OQ-23: speed scale is explicitly not proven as m/s", !MovementScale.IsMetersPerSecondProven);
            Check.True("B-U5: stat-description multipliers are not applied to physics",
                !MovementScale.SkillMultiplierAppliedToPhysics);
            Check.True("jump is not implemented from a UI hint; no jump constant is exported by this pilot",
                typeof(MovementFormulas).GetField("JumpSpeed") == null);
        }
    }
}
