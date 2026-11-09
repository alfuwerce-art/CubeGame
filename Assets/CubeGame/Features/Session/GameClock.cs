using System;

namespace CubeGame.Features.Session
{
    /// <summary>
    /// The single source of simulation time (M0-00C §2, R-TIME). Game time advances in fixed ticks;
    /// the presentation layer feeds real frame time exactly once per frame through <see cref="Advance"/>.
    /// Time is kept as an integer tick counter, so 30/60/120 Hz frame rates produce identical tick
    /// sequences and identical game-millisecond timestamps (Alpha formulas are expressed in ms).
    ///
    /// Tick rate 30 Hz: CubeGame decision (M0-00C A-2), PLACEHOLDER_NOT_ALPHA — Alpha's own update rate
    /// is not used as a simulation constant anywhere in CubeGame.
    /// </summary>
    public sealed class GameClock
    {
        public const int DefaultTicksPerSecond = 30;

        /// <summary>Upper bound of ticks executed for one frame (protects against spiral of death).</summary>
        public const int MaxTicksPerFrame = 8;

        private readonly int _ticksPerSecond;
        private double _accumulatorSeconds;

        public GameClock(int ticksPerSecond = DefaultTicksPerSecond, long startTick = 0)
        {
            if (ticksPerSecond <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            }

            if (startTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startTick));
            }

            _ticksPerSecond = ticksPerSecond;
            Tick = startTick;
        }

        /// <summary>Raised once per simulation tick, after <see cref="Tick"/> was incremented.</summary>
        public event Action<GameClock> Ticked;

        public int TicksPerSecond => _ticksPerSecond;

        public long Tick { get; private set; }

        public bool Paused { get; set; }

        /// <summary>Game time in milliseconds (exact: tick * 1000 / rate, rounded down).</summary>
        public long TimeMs => Tick * 1000L / _ticksPerSecond;

        public double TickSeconds => 1.0 / _ticksPerSecond;

        /// <summary>Fraction of the next tick already accumulated, for render interpolation [0,1).</summary>
        public double Alpha => _accumulatorSeconds * _ticksPerSecond;

        /// <summary>Real seconds dropped because a frame needed more than <see cref="MaxTicksPerFrame"/>.</summary>
        public double DroppedSeconds { get; private set; }

        /// <summary>Feeds real elapsed time; returns the number of ticks executed.</summary>
        public int Advance(double realSeconds)
        {
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds <= 0.0 || Paused)
            {
                return 0;
            }

            _accumulatorSeconds += realSeconds;
            double step = TickSeconds;
            int executed = 0;
            while (_accumulatorSeconds + 1e-9 >= step)
            {
                if (executed >= MaxTicksPerFrame)
                {
                    DroppedSeconds += _accumulatorSeconds;
                    _accumulatorSeconds = 0.0;
                    break;
                }

                _accumulatorSeconds -= step;
                if (_accumulatorSeconds < 0.0)
                {
                    _accumulatorSeconds = 0.0;
                }

                StepOnce();
                executed++;
            }

            return executed;
        }

        /// <summary>Executes exactly <paramref name="ticks"/> ticks regardless of pause (tests, loading).</summary>
        public void StepTicks(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                StepOnce();
            }
        }

        public void Restore(long tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            Tick = tick;
            _accumulatorSeconds = 0.0;
        }

        private void StepOnce()
        {
            Tick++;
            Ticked?.Invoke(this);
        }
    }
}
