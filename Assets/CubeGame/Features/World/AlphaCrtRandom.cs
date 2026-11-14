using System;
namespace CubeGame.Features.World
{
    /// <summary>
    /// MSVC CRT <c>srand/rand</c> used by Alpha world generation (B_world §B7, DIRECT):
    /// <c>state = state·214013 + 2531011; rand = (state &gt;&gt; 16) &amp; 0x7FFF</c>.
    /// One instance per generation stream — Alpha reseeds the global CRT state per region / climate cell / zone.
    /// </summary>
    public sealed class AlphaCrtRandom
    {
        public const int RandMax = 0x7FFF;

        private uint _state;

        /// <summary>Current CRT state (srand seed after the last draw).</summary>
        public int State => unchecked((int)_state);

        /// <summary>Diagnostics: called after every draw (tests compare the stream with executed Alpha code).</summary>
        public Action<AlphaCrtRandom> AfterDraw { get; set; }

        /// <summary>
        /// Diagnostics: the Alpha code the following draws stand for, as an address range of the return sites of
        /// its <c>rand()</c> calls (<see cref="SiteOutside"/>: a callee outside the range of the tagging function).
        /// Tests compare it with the call sites of the executed Alpha draw log; generation ignores it.
        /// </summary>
        public int SiteFrom { get; set; }

        public int SiteTo { get; set; }

        public const int SiteOutside = -1;

        /// <summary>Sets <see cref="SiteFrom"/>…<see cref="SiteTo"/>.</summary>
        public void At(int from, int to)
        {
            SiteFrom = from;
            SiteTo = to;
        }

        /// <summary>Runs a callee with the tag <see cref="SiteOutside"/> and restores the caller's tag.</summary>
        public T Callee<T>(Func<T> call)
        {
            int from = SiteFrom, to = SiteTo;
            At(SiteOutside, SiteOutside);
            try
            {
                return call();
            }
            finally
            {
                At(from, to);
            }
        }

        public AlphaCrtRandom(int seed)
        {
            Seed(seed);
        }

        public void Seed(int seed)
        {
            _state = unchecked((uint)seed);
        }

        public int Next()
        {
            unchecked
            {
                _state = _state * 214013u + 2531011u;
            }

            AfterDraw?.Invoke(this);
            return (int)((_state >> 16) & 0x7FFF);
        }
    }
}
