namespace CubeGame.Features.Session
{
    /// <summary>Indices in the Alpha creature's rank array at +0x1138 (P-13).</summary>
    public enum HeroSkill
    {
        Taming = 0,
        Riding = 1,
        Climbing = 2,
        HangGliding = 3,
        Swimming = 4,
        Boat = 5,
        Ability1 = 6,
        Ability2 = 7,
        Ability3 = 8,
        Ability4 = 9,
        Ability5 = 10,
    }

    public sealed class HeroSkillRanks
    {
        public const int Count = 11;

        private int[] _ranks = new int[Count];

        public int this[HeroSkill skill] => _ranks[(int)skill];

        public int[] ToArray() => (int[])_ranks.Clone();

        /// <summary>Applies a complete set of ranks. A rejected set leaves the previous ranks intact.</summary>
        public bool TryReplace(int[] ranks)
        {
            if (ranks == null || ranks.Length != Count)
            {
                return false;
            }

            int[] copy = (int[])ranks.Clone();
            if (!IsValid(copy))
            {
                return false;
            }

            _ranks = copy;
            return true;
        }

        internal static bool IsValid(int[] ranks)
        {
            if (ranks.Length != Count)
            {
                return false;
            }

            foreach (int rank in ranks)
            {
                if (rank < 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
