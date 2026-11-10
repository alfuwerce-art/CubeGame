using System;
using CubeGame.Core.Progression;

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

        // Cube.exe 0x47B8D9: two points per level after level 1, plus one per four mana cubes.
        public long UnspentLevelPoints(int level, int manaCubes = 0) => UnspentLevelPoints(level, manaCubes, _ranks);

        public bool CanRaise(HeroSkill skill, int level, int manaCubes = 0)
        {
            int index = (int)skill;
            return (uint)index < Count && _ranks[index] < int.MaxValue && UnspentLevelPoints(level, manaCubes) > 0
                && HasPrerequisite(_ranks, index);
        }

        public bool TryRaise(HeroSkill skill, int level, int manaCubes = 0)
        {
            if (!CanRaise(skill, level, manaCubes))
            {
                return false;
            }

            _ranks[(int)skill]++;
            return true;
        }

        public bool TryLower(HeroSkill skill)
        {
            int index = (int)skill;
            if ((uint)index >= Count || _ranks[index] == 0)
            {
                return false;
            }

            _ranks[index]--;
            int next = index + 1;
            if (next < Count && _ranks[next] > 0 && !HasPrerequisite(_ranks, next))
            {
                _ranks[index]++;
                return false;
            }

            return true;
        }

        internal bool TryApplyIncreases(HeroSkillRanks draft, int level, int manaCubes)
        {
            if (!TryPriceChange(draft, level, manaCubes, false, out long price) || price != 0)
            {
                return false;
            }

            return TryReplace(draft._ranks);
        }

        /// <summary>Validates the whole draft before applying the paid change (Cube.exe 0x4DF9C0).</summary>
        public bool TryPriceChange(HeroSkillRanks draft, int level, int manaCubes, bool switchSpec, out long price)
        {
            price = 0;
            if (draft == null || draft.UnspentLevelPoints(level, manaCubes) < 0)
            {
                return false;
            }

            float levelFactor = (float)AlphaMath.LevelFactor(level);
            float total = 0f;
            int[] proposed = draft._ranks;
            for (int index = 0; index < Count; index++)
            {
                if (proposed[index] > 0 && !HasPrerequisite(proposed, index)
                    && (proposed[index] > _ranks[index] || HasPrerequisite(_ranks, index)))
                {
                    return false;
                }

                int removed = Math.Max(0, _ranks[index] - proposed[index]);
                total += levelFactor * removed * 10f;
            }

            if (switchSpec && level > 1)
            {
                total += levelFactor * 100f;
            }
            price = (long)total;
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

        private static long UnspentLevelPoints(int level, int manaCubes, int[] ranks)
        {
            long points = 2L * (level - 1L) + manaCubes / 4;
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
