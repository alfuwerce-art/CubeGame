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

            return (int)((_state >> 16) & 0x7FFF);
        }
    }
}
