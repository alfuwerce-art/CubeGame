// CubeGame.Presentation.Visual — playable races of Alpha for the character editor (D4, P-01…P-05).
//
// Alpha's character creation offers eight races with both sexes; every race is one creature kind of the S05
// catalogue (kind numbers and names from docs/research/alpha2013/S05B2_ALPHA_ORIGINAL_MODEL_ANIMATION_CATALOG.md,
// kinds 0x00…0x10 are humanoids with the shared humanoid rig alpha_creature/e66d96be5a). Non-playable humanoid
// kinds (skeleton 0x11, old man 0x12) are NPCs and are NOT offered here.
//
// The editor itself belongs to Claude (C1); this file only supplies the data and the visual request, so the
// editor cannot invent a race the content layer cannot draw: every offered kind is checked against the staged
// catalogue before it is returned (Validate/Available).
using System;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Features.Creatures;

namespace CubeGame.Presentation.Visual
{
    /// <summary>One playable race/sex of Alpha.</summary>
    public readonly struct AlphaRaceDef
    {
        /// <summary>Creature kind (alpha_creature id is shared by all humanoids).</summary>
        public readonly int Kind;
        public readonly string Race;
        public readonly bool Female;

        public AlphaRaceDef(int kind, string race, bool female)
        {
            Kind = kind;
            Race = race;
            Female = female;
        }

        public string DisplayName => Female ? Race + "Female" : Race + "Male";

        public override string ToString() => $"0x{Kind:x2} {DisplayName}";
    }

    /// <summary>Playable races of Alpha + character preview request (pure, no scene objects).</summary>
    public static class AlphaRaceCatalog
    {
        public const string Evidence =
            "S05B2 catalogue: humanoid kinds 0x00…0x10 = eight races × two sexes; 0x11 skeleton and 0x12 old man are NPC-only";

        public const int DefaultHairSeed = 0;

        /// <summary>Eight races × two sexes, in the Alpha kind order.</summary>
        public static readonly AlphaRaceDef[] All =
        {
            new AlphaRaceDef(0x00, "Elf", false),
            new AlphaRaceDef(0x01, "Elf", true),
            new AlphaRaceDef(0x02, "Human", false),
            new AlphaRaceDef(0x03, "Human", true),
            new AlphaRaceDef(0x04, "Goblin", false),
            new AlphaRaceDef(0x05, "Goblin", true),
            new AlphaRaceDef(0x07, "Lizardman", false),
            new AlphaRaceDef(0x08, "Lizardman", true),
            new AlphaRaceDef(0x09, "Dwarf", false),
            new AlphaRaceDef(0x0A, "Dwarf", true),
            new AlphaRaceDef(0x0B, "Orc", false),
            new AlphaRaceDef(0x0C, "Orc", true),
            new AlphaRaceDef(0x0D, "Frogman", false),
            new AlphaRaceDef(0x0E, "Frogman", true),
            new AlphaRaceDef(0x0F, "Undead", false),
            new AlphaRaceDef(0x10, "Undead", true),
        };

        /// <summary>Race names in the order of the editor list (one entry per race, sex-agnostic).</summary>
        public static readonly string[] RaceNames =
            { "Elf", "Human", "Goblin", "Lizardman", "Dwarf", "Orc", "Frogman", "Undead" };

        public static bool TryByKind(int kind, out AlphaRaceDef def)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Kind == kind)
                {
                    def = All[i];
                    return true;
                }
            }

            def = default;
            return false;
        }

        public static bool TryFind(string race, bool female, out AlphaRaceDef def)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Female == female && string.Equals(All[i].Race, race, StringComparison.OrdinalIgnoreCase))
                {
                    def = All[i];
                    return true;
                }
            }

            def = default;
            return false;
        }

        /// <summary>
        /// Races whose visual really exists in the staged content. An editor must offer only these; the
        /// catalogue itself stays complete so a missing race is reported, not silently dropped.
        /// </summary>
        public static List<AlphaRaceDef> Available(AlphaActorLibrary library, List<string> problems = null)
        {
            var result = new List<AlphaRaceDef>(All.Length);
            for (int i = 0; i < All.Length; i++)
            {
                if (library != null && CreatureVisualCatalog.TryResolve(All[i].Kind, library, out _, out _))
                {
                    result.Add(All[i]);
                }
                else if (problems != null)
                {
                    problems.Add($"RACE_NOT_STAGED {All[i]} " + (library == null ? "NO_ACTOR_LIBRARY" : "MISSING_VISUAL"));
                }
            }

            return result;
        }

        /// <summary>Every playable kind exists in the committed creature catalogue (data check, no content cache).</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            var kinds = new HashSet<int>();
            for (int i = 0; i < All.Length; i++)
            {
                if (!kinds.Add(All[i].Kind)) problems.Add($"RACE_DUPLICATE_KIND 0x{All[i].Kind:x2}");
                if (!AlphaContentDatabase.TryCreatureKind(All[i].Kind, out _))
                    problems.Add($"RACE_KIND_NOT_IN_CATALOGUE 0x{All[i].Kind:x2}");
            }

            if (All.Length != 16) problems.Add($"RACE_COUNT {All.Length}");
            if (RaceNames.Length != 8) problems.Add($"RACE_NAME_COUNT {RaceNames.Length}");
            for (int r = 0; r < RaceNames.Length; r++)
            {
                bool male = false, female = false;
                for (int i = 0; i < All.Length; i++)
                {
                    if (!string.Equals(All[i].Race, RaceNames[r], StringComparison.Ordinal)) continue;
                    if (All[i].Female) female = true; else male = true;
                }

                if (!male || !female) problems.Add($"RACE_SEX_PAIR {RaceNames[r]}");
            }

            return problems;
        }

        /// <summary>
        /// Character visual request of one race. Hair colour is free for a player (D61: vertices × hair_rgb/255);
        /// equipment starts disabled and is filled by <see cref="HeroLoadout"/> from the loadout events.
        /// </summary>
        public static CharacterVisualRequest PreviewRequest(AlphaRaceDef race, UnityEngine.Color hair, bool hasHairColour)
        {
            CharacterVisualRequest request = CharacterVisualRequest.Default;
            request.Kind = race.Kind;
            request.HairColour = hair;
            request.HasHairColour = hasHairColour;
            return request;
        }
    }
}
