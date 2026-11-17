// CubeGame.Features.World.Foundation — one place that proves the world foundation is internally consistent.
//
// Every G2 part validates itself; this aggregator checks the cross-references a single part cannot see (a biome
// pointing at a structure rule that does not exist, a vegetation profile pointing at a missing rule set). Tests and
// the debug overlay call exactly this, so "green" means the same thing everywhere.
using System;
using System.Collections.Generic;

namespace CubeGame.Features.World.Foundation
{
    public static class WorldFoundationValidation
    {
        /// <summary>All structural problems of the world foundation (empty = consistent).</summary>
        public static List<string> Check()
        {
            var problems = new List<string>();
            problems.AddRange(WorldGenerationProfile.Validate());
            problems.AddRange(WorldSeedTable.Validate());
            problems.AddRange(ClimateMath.Validate());
            problems.AddRange(WorldDistanceStrategy.Validate());
            problems.AddRange(WorldStreamingBudget.Validate());

            BiomeCatalog biomes = BiomeCatalog.Default();
            problems.AddRange(biomes.Validate());

            StructureRuleSet structures = StructureRuleSet.Default();
            problems.AddRange(structures.Validate());

            IReadOnlyDictionary<string, VegetationRuleSet> vegetation = VegetationRuleSet.Defaults();
            foreach (VegetationRuleSet set in vegetation.Values) problems.AddRange(set.Validate());
            problems.AddRange(VegetationModelBinding.Validate());

            // Cross-references: biome → structure rule → known rule.
            foreach (BiomeDefinition biome in biomes.Biomes)
            {
                foreach (string ruleId in biome.StructureRuleIds ?? Array.Empty<string>())
                {
                    if (!structures.TryGet(ruleId, out _))
                    {
                        problems.Add($"BIOME_STRUCTURE_RULE_UNKNOWN {biome.Id} {ruleId}");
                    }
                }
            }

            // Cross-references: vegetation profile → rule set → known set, and its kinds must exist in the rules.
            foreach (VegetationProfile profile in biomes.Vegetation)
            {
                if (!vegetation.TryGetValue(profile.RuleSetId, out VegetationRuleSet set))
                {
                    problems.Add($"BIOME_VEGETATION_RULESET_UNKNOWN {profile.Id} {profile.RuleSetId}");
                    continue;
                }

                foreach (VegetationKind kind in profile.Kinds ?? Array.Empty<VegetationKind>())
                {
                    bool used = false;
                    foreach (VegetationRule rule in set.Rules)
                    {
                        foreach (VegetationKind ruleKind in rule.Kinds ?? Array.Empty<VegetationKind>())
                        {
                            if (ruleKind == kind)
                            {
                                used = true;
                                break;
                            }
                        }

                        if (used) break;
                    }

                    if (!used) problems.Add($"BIOME_VEGETATION_KIND_UNRULED {profile.Id} {kind}");
                }
            }

            problems.AddRange(CheckSeamsAreDeterministic());
            return problems;
        }

        /// <summary>The seams must give the same answer twice, and a different one for another seed.</summary>
        private static List<string> CheckSeamsAreDeterministic()
        {
            var problems = new List<string>();
            var biomes = BiomeCatalog.Default();
            var rules = StructureRuleSet.Default();
            ClimateAxes axis = new ClimateAxes(0.5f, 0.6f, 0f, 0.1f, 2, 30, 0);
            var probe = new FixedSeamProbe(axis, biomes, 30);
            SettlementSite first = SettlementSeamPlanner.Plan(12345u, 512, 512, probe, rules);
            SettlementSite second = SettlementSeamPlanner.Plan(12345u, 512, 512, probe, rules);
            if (first == null || second == null)
            {
                problems.Add("SEAM_SETTLEMENT_NOT_PLANNED");
                return problems;
            }

            if (first.PersistentId != second.PersistentId || first.CenterX != second.CenterX ||
                first.CenterZ != second.CenterZ)
            {
                problems.Add("SEAM_SETTLEMENT_NON_DETERMINISTIC");
            }

            SettlementSite other = SettlementSeamPlanner.Plan(54321u, 512, 512, probe, rules);
            if (other != null && other.PersistentId == first.PersistentId)
            {
                problems.Add("SEAM_SETTLEMENT_SEED_INSENSITIVE");
            }

            if (first.Anchors.Count < 8) problems.Add($"SEAM_SERVICE_ANCHORS {first.Anchors.Count}");
            if (first.Roads.Count != 4) problems.Add($"SEAM_ROAD_STUBS {first.Roads.Count}");

            DungeonEntranceSite entrance = DungeonSeamPlanner.Plan(12345u, first.ZoneX, first.ZoneZ, probe);
            if (entrance == null) problems.Add("SEAM_DUNGEON_NOT_PLANNED");
            else if (string.IsNullOrEmpty(entrance.DungeonInstanceId) || entrance.Level < 1)
            {
                problems.Add("SEAM_DUNGEON_INVALID");
            }

            return problems;
        }

        /// <summary>Synthetic probe: constant climate and surface (used by validation and tests).</summary>
        public sealed class FixedSeamProbe : IWorldSeamProbe
        {
            private readonly ClimateAxes _axes;
            private readonly BiomeCatalog _biomes;
            private readonly int _surface;

            public FixedSeamProbe(ClimateAxes axes, BiomeCatalog biomes, int surface)
            {
                _axes = axes;
                _biomes = biomes;
                _surface = surface;
            }

            public string BiomeOf(int x, int z) => _biomes?.Resolve(_axes)?.Id ?? string.Empty;

            public int SurfaceAt(int x, int z) => _surface;

            public int RegionLevel(int regionX, int regionZ) => _axes.RegionLevel;
        }

        /// <summary>Compact description for logs and reports.</summary>
        public static string Describe()
        {
            BiomeCatalog biomes = BiomeCatalog.Default();
            StructureRuleSet structures = StructureRuleSet.Default();
            IReadOnlyDictionary<string, VegetationRuleSet> vegetation = VegetationRuleSet.Defaults();
            int rules = 0;
            foreach (VegetationRuleSet set in vegetation.Values) rules += set.Rules.Count;
            return $"profile={WorldGenerationProfile.Describe()} | biomes={biomes.Biomes.Count} " +
                   $"surfaces={biomes.Surfaces.Count} vegetationProfiles={biomes.Vegetation.Count} " +
                   $"spawnProfiles={biomes.Spawns.Count} structureRules={structures.Rules.Count} " +
                   $"vegetationRuleSets={vegetation.Count} vegetationRules={rules} | " +
                   WorldStreamingBudget.Default().Describe();
        }
    }
}
