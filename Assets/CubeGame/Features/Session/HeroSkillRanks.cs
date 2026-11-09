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

        // Cube.exe 0x47B8D9: two points per level after level 1. The mana-cube reward is pending.
        public long UnspentLevelPoints(int level) => UnspentLevelPoints(level, _ranks);

        public bool CanRaise(HeroSkill skill, int level)
        {
            int index = (int)skill;
            return (uint)index < Count && _ranks[index] < int.MaxValue && UnspentLevelPoints(level) > 0
                && HasPrerequisite(_ranks, index);
        }

        public bool TryRaise(HeroSkill skill, int level)
        {
            if (!CanRaise(skill, level))
            {
                return false;
            }

            _ranks[(int)skill]++;
            return true;
        }

        internal bool TryApplyIncreases(HeroSkillRanks draft, int level)
        {
            if (draft == null)
            {
                return false;
            }

            int[] proposed = draft.ToArray();
            if (UnspentLevelPoints(level, proposed) < 0)
            {
                return false;
            }

            for (int index = 0; index < Count; index++)
            {
                if (proposed[index] < _ranks[index]
                    || (proposed[index] > _ranks[index] && !HasPrerequisite(proposed, index)))
                {
                    return false;
                }
            }

            _ranks = proposed;
            return true;
        }

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

        private static long UnspentLevelPoints(int level, int[] ranks)
        {
            long points = 2L * (level - 1L);
            foreach (int rank in ranks)
            {
                points -= rank;
            }

            return points;
        }

        // Cube.exe 0x47B944…0x47B9B4: the listed skills require five ranks in the preceding slot.
        private static bool HasPrerequisite(int[] ranks, int index)
        {
            switch ((HeroSkill)index)
            {
                case HeroSkill.Riding:
                case HeroSkill.HangGliding:
                case HeroSkill.Boat:
                case HeroSkill.Ability2:
                case HeroSkill.Ability3:
                case HeroSkill.Ability4:
                    return ranks[index - 1] >= 5;
                default:
                    return true;
            }
        }
    }
}
