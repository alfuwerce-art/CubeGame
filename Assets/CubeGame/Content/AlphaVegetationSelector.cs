// CubeGame.Content — Alpha vegetation kind selection (B_vegetation_research.md).
//
// The W-18 scatter table (WorldInfo_scatterObjectsInArea 0x5F56C0) selects CREATURE templates, so the
// existing "vegetation" that routed through it was wrong. Real Alpha vegetation is two functions:
//   * "trees": World_generateTreeRoot 0x5D9AD0 -> dtor_listBuffer 0x5F7820. P7 byte trace corrects this:
//     0x5D9AD0 is the region mission pass and 0x5F7820 picks a BOSS creature kind (0x6C Troll … 0x77), with the
//     climate range below as the boss level (docs/research/alpha2013/P7_MISSIONS_TRACE.md). Not used by the game
//     runtime; kept for the editor/import tools until P2 replaces it;
//   * decor: World_serializeCellData 0x5F6E20 (kind + scale).
// Everything here is ALPHA_DECOMPILED (byte-level), not live-verified against a running Alpha scene.
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    public static class AlphaVegetationSelector
    {
        public const string TreeProvenance = "ALPHA_DECOMPILED: World_generateTreeRoot 0x5D9AD0 + dtor_listBuffer 0x5F7820";
        public const string DecorProvenance = "ALPHA_DECOMPILED: World_serializeCellData 0x5F6E20";

        /// <summary>Tree "kind" range by climate (generateTreeRoot). cellFlag = cell+0x08 (mountain).</summary>
        public static (int min, int max) TreeKindRange(double humidity, double temperature, int cellFlag)
        {
            int min = 1, max = 10;
            if (humidity < 0.2) { min = 10; max = 20; }
            if (temperature < 0.2 && humidity > 0.8) { min = 15; max = 25; }
            if (temperature > 0.8 && humidity > 0.8) { min = 10; max = 20; }
            if (cellFlag == 1) { min = 20; max = 30; }
            return (min, max);
        }

        public static int TreeKind(double humidity, double temperature, int cellFlag, uint rng)
        {
            var (min, max) = TreeKindRange(humidity, temperature, cellFlag);
            return (int)(rng % (uint)(max - min + 1)) + min;
        }

        /// <summary>dtor_listBuffer: tree kind -> candidate model ids (0x6C..0x77). cold = cell+0x10 ≤ 0.2.</summary>
        public static List<int> TreeModelIds(int treeKind, bool cold)
        {
            var ids = new List<int> { cold ? 0x72 : 0x6c, cold ? 0x74 : 0x77 };
            if (treeKind > 10) ids.Add(0x73);
            if (treeKind > 20) ids.Add(0x75);
            if (treeKind > 50)
            {
                ids.Add(cold ? 0x71 : 0x70);
                ids.Add(0x6d);
            }

            if (treeKind > 70) ids.Add(0x6e);
            return ids;
        }

        public static int TreeModel(int treeKind, bool cold, uint rng)
        {
            var ids = TreeModelIds(treeKind, cold);
            return ids[(int)(rng % (uint)ids.Count)];
        }

        /// <summary>serializeCellData: decor kind + scale. biomeType = 3/4/5 or other.</summary>
        public static (int kind, float sx, float sy, float sz) Decor(uint rng, int biomeType)
        {
            if (rng % 50 == 0) return (10, 1.5f, 1.0f, 1.0f);
            uint branch = (rng >> 8) % 3;
            uint sub = (rng >> 16) % 3;
            if (branch == 0)
            {
                int b = biomeType == 3 ? 0x20 : biomeType == 4 ? 0x26 : biomeType == 5 ? 0x29 : 0x23;
                return (b + (int)sub, 2.0f, 1.0f, 1.57f);
            }

            if (branch == 1)
            {
                int k = biomeType == 4 ? 0xd : (biomeType == 5 ? 0xe : 0xc);
                return (k, 3.0f, 3.0f, 1.0f);
            }

            int r = biomeType == 4 ? 0xf : (biomeType == 5 ? 0x11 : 0x10);
            return (r, 1.0f, 1.0f, 0.5f);
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            // Tree kind ranges never inverted, and every range maps to at least one staged-model id.
            for (int flag = 0; flag <= 1; flag++)
            {
                var (min, max) = TreeKindRange(0.5, 0.5, flag);
                if (min < 1 || max < min) p.Add($"TREE_RANGE_BAD {min}..{max}");
                for (int kind = min; kind <= max; kind += Math.Max(1, (max - min) / 3))
                {
                    var ids = TreeModelIds(kind, false);
                    if (ids.Count == 0) p.Add($"TREE_MODEL_EMPTY kind={kind}");
                }
            }

            // Decor is deterministic and returns in-range ids.
            var a = Decor(0x12345678u, 0);
            var b = Decor(0x12345678u, 0);
            if (a.kind != b.kind || a.sx != b.sx) p.Add("DECOR_NON_DETERMINISTIC");
            return p;
        }
    }
}
