// CubeGame.Features.Creatures — pets of a creature/NPC (D2, M0-06E).
//
// Proven in Alpha (M0-00B FINAL / M0-06E): a character can have at most TWO summoned pets, and summoning is
// ability 0x5C (CreatureRules.PetSummonLimit / PetSummonAbilityId). Explicitly NOT proven: taming, riding,
// pet AI, and whether a pet teleports when it falls behind — the negative result for teleport is recorded as
// CQ-09 "нет" in the research notes, so nothing here teleports a pet.
//
// This roster is the bookkeeping only: which entities are pets of one owner and whether another summon is
// allowed. Damage, HP and movement stay with gameplay and Features/AI.
using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Creatures
{
    /// <summary>Pets of one owner; the proven limit and ability id are enforced, the rest is not invented.</summary>
    public sealed class CreaturePetRoster
    {
        public const string Status =
            "PROVEN: summon limit 2 and ability 0x5C; UNKNOWN: taming, riding, pet AI, teleport (CQ-09: no teleport)";

        readonly List<StableEntityId> _pets = new List<StableEntityId>();

        /// <summary>Alpha limit: two pets per owner (PROVEN).</summary>
        public int Limit { get; }

        public CreaturePetRoster(int limit = CreaturePets.SummonLimit)
        {
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            Limit = limit;
        }

        public int Count => _pets.Count;

        public IReadOnlyList<StableEntityId> Pets => _pets;

        public bool IsPet(StableEntityId entity) => _pets.Contains(entity);

        /// <summary>Summon ability must be Alpha's 0x5C and a free pet slot must exist.</summary>
        public bool CanSummon(int abilityId) => abilityId == CreaturePets.SummonAbilityId && _pets.Count < Limit;

        /// <summary>Registers a summoned pet; false for a duplicate, a full roster or an empty id.</summary>
        public bool TryAdd(StableEntityId pet)
        {
            if (pet.IsNone) return false;
            if (_pets.Contains(pet)) return false;
            if (_pets.Count >= Limit) return false;
            _pets.Add(pet);
            return true;
        }

        public bool Remove(StableEntityId pet) => _pets.Remove(pet);

        public void Clear() => _pets.Clear();

        /// <summary>Why a summon was refused (diagnostics; empty when <see cref="CanSummon"/> is true).</summary>
        public string ExplainSummon(int abilityId)
        {
            if (abilityId != CreaturePets.SummonAbilityId)
                return $"NOT_SUMMON_ABILITY {abilityId} (Alpha summon ability is 0x{CreaturePets.SummonAbilityId:x})";
            if (_pets.Count >= Limit) return $"PET_LIMIT {_pets.Count}/{Limit}";
            return string.Empty;
        }
    }
}
