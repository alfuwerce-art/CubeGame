// CubeGame.Content — which Alpha model draws a head, a haircut or an item (M0-F2A).
//
// Both answers are lookups into generated evidence tables (AlphaCharacterModels.g.cs, written by
// tools/alpha_content/gen_character_models.py):
//   * head/hair: the hero editor keeps face/haircut as an index inside the species range of switch table
//     0x42C41C (AlphaHeroCreation.HeadModel/HairModel give the registry index); the registry loader call sites
//     of the asset catalog name the model behind that index (ALPHA_CODE_STATIC);
//   * items: the executed selector GameController_item_icon_id 0x4EC400 over type/subtype/material(+0x0D)/
//     rarity(+0x0C) and seed (EMULATED_ALPHA_CODE, 310k runs, 0 errors). models[seed mod period].
// Nothing is substituted: an index or item outside the tables returns false with a reason.
using System;

namespace CubeGame.Content
{
    public static partial class AlphaCharacterModels
    {
        public const int SubtypeDomain = 64;
        public const int MaterialDomain = 64;
        public const int RarityDomain = 8;

        /// <summary>Head/hair model behind a registry index of the hero editor ranges.</summary>
        public static bool TryAppearanceModel(int registryIndex, out AlphaAppearanceModelDef def) =>
            AppearanceModels.TryGetValue(registryIndex, out def);

        /// <summary>
        /// Model Alpha draws for an item (type, subtype, material byte +0x0D, rarity byte +0x0C, seed +0x04).
        /// Negative seeds are read as the unsigned 32-bit value (the sign handling of Alpha is UNKNOWN; every
        /// CubeGame seed is non-negative).
        /// </summary>
        public static bool TrySelectItemModel(int type, int subtype, int material, int rarity, int seed, out string model,
            out string problem)
        {
            model = null;
            problem = null;
            if (subtype < 0 || subtype >= SubtypeDomain || material < 0 || material >= MaterialDomain || rarity < 0 ||
                rarity >= RarityDomain)
            {
                problem = $"ITEM_OUTSIDE_SELECTOR_DOMAIN {type}/{subtype} material {material} rarity {rarity}";
                return false;
            }

            ulong subBit = 1UL << subtype;
            ulong matBit = 1UL << material;
            int rarBit = 1 << rarity;
            AlphaItemModelRule[] rules = ItemModelRules;
            for (int i = 0; i < rules.Length; i++)
            {
                AlphaItemModelRule rule = rules[i];
                if (rule.Type != type || (rule.Subtypes & subBit) == 0 || (rule.Materials & matBit) == 0 ||
                    (rule.Rarities & rarBit) == 0)
                {
                    continue;
                }

                int pick = (int)((uint)seed % (uint)rule.Models.Length);
                model = ItemModelNames[rule.Models[pick]];
                if (string.IsNullOrEmpty(model))
                {
                    problem = $"ITEM_SELECTOR_EMPTY_SLOT {type}/{subtype} material {material}";
                    model = null;
                    return false;
                }

                return true;
            }

            problem = $"ITEM_NOT_IN_SELECTOR {type}/{subtype} material {material} rarity {rarity}";
            return false;
        }

        /// <summary>Staged payload name of an Alpha model ("iron-sword1.cub" → "iron-sword1.cubdec").</summary>
        public static string PayloadName(string alphaModel)
        {
            if (string.IsNullOrEmpty(alphaModel)) return null;
            return alphaModel.EndsWith(".cub", StringComparison.OrdinalIgnoreCase)
                ? alphaModel.Substring(0, alphaModel.Length - 4) + ".cubdec"
                : alphaModel;
        }
    }
}
