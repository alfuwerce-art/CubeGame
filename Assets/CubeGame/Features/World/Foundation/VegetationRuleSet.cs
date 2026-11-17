// CubeGame.Features.World.Foundation — G2-E: "where vegetation is allowed" separated from "what model is drawn".
//
// The runtime already draws Alpha plant models (AlphaVegetationMeshes) from placements produced by the ported
// Alpha surface scan (AlphaGroundVegetation) and the W-18 scatter (AlphaVegetationPlanner). Those stay as the
// temporary visual/content path. What was missing is the CubeGame rule layer: a biome-aware statement of where a
// plant may stand (slope, height above water, moisture, temperature, distance to water, spacing, clustering,
// density) that does not mention a model, a material or an Alpha kind at all.
//
// Model binding is a separate table (VegetationModelBinding) whose values are TEMPORARY_RUNTIME: Alpha plant
// models are placeholders until G3/G9 supply CubeGame art. Replacing the table must not change a single rule.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>What kind of plant/decoration CubeGame wants. Not an Alpha model id.</summary>
    public enum VegetationKind
    {
        Grass = 0,
        Flower = 1,
        Reed = 2,
        WaterPlant = 3,
        Coral = 4,
        AlpineShrub = 5,
        DesertPlant = 6,
        Mushroom = 7,
        Sapling = 8,
        Rock = 9,
    }

    /// <summary>What the world says about one candidate site. Slope and water distance are supplied by the caller.</summary>
    public readonly struct VegetationSiteSample
    {
        public VegetationSiteSample(string biomeId, int x, int z, float slope, int heightAboveWater,
            float moisture, float temperature, int waterDistanceVoxels, bool underwater)
        {
            BiomeId = biomeId ?? string.Empty;
            X = x;
            Z = z;
            Slope = slope;
            HeightAboveWater = heightAboveWater;
            Moisture = moisture;
            Temperature = temperature;
            WaterDistanceVoxels = waterDistanceVoxels;
            Underwater = underwater;
        }

        public string BiomeId { get; }

        public int X { get; }

        public int Z { get; }

        /// <summary>0 = flat, 1 = vertical.</summary>
        public float Slope { get; }

        public int HeightAboveWater { get; }

        public float Moisture { get; }

        public float Temperature { get; }

        /// <summary>Voxels to the nearest water column; <see cref="int.MaxValue"/> when unknown/none.</summary>
        public int WaterDistanceVoxels { get; }

        public bool Underwater { get; }

        public string Describe() =>
            $"biome={BiomeId} ({X},{Z}) slope={Slope.ToString("0.##", CultureInfo.InvariantCulture)} " +
            $"aboveWater={HeightAboveWater} wet={Moisture.ToString("0.##", CultureInfo.InvariantCulture)} " +
            $"t={Temperature.ToString("0.##", CultureInfo.InvariantCulture)} waterDist={WaterDistanceVoxels} " +
            $"underwater={Underwater}";
    }

    /// <summary>Why a site was accepted or rejected. Machine-readable for the debug overlay.</summary>
    public enum VegetationRejection
    {
        None = 0,
        NoRuleForBiome = 1,
        BiomeNotAllowed = 2,
        SlopeOutOfRange = 3,
        HeightOutOfRange = 4,
        MoistureOutOfRange = 5,
        TemperatureOutOfRange = 6,
        WrongWaterSide = 7,
        TooFarFromWater = 8,
        DensityRollFailed = 9,
        SpacingViolation = 10,
        ClusterFull = 11,
        ZoneBudgetReached = 12,
    }

    public readonly struct VegetationDecision
    {
        public VegetationDecision(bool allowed, VegetationRejection rejection, string ruleId, VegetationKind kind)
        {
            Allowed = allowed;
            Rejection = rejection;
            RuleId = ruleId ?? string.Empty;
            Kind = kind;
        }

        public bool Allowed { get; }

        public VegetationRejection Rejection { get; }

        public string RuleId { get; }

        public VegetationKind Kind { get; }

        public override string ToString() =>
            Allowed ? $"allow {RuleId} {Kind}" : $"deny {Rejection} ({RuleId})";
    }

    /// <summary>One CubeGame vegetation rule: a climate/relief/water envelope plus density and spacing.</summary>
    public sealed class VegetationRule
    {
        public string Id = string.Empty;

        /// <summary>Biome ids this rule serves. Empty = any biome.</summary>
        public string[] BiomeIds = Array.Empty<string>();

        public VegetationKind[] Kinds = Array.Empty<VegetationKind>();

        public float MinSlope;
        public float MaxSlope = 1f;
        public int MinHeightAboveWater = -1000;
        public int MaxHeightAboveWater = 1000;
        public float MinMoisture;
        public float MaxMoisture = 1f;
        public float MinTemperature;
        public float MaxTemperature = 1f;

        /// <summary>Maximum distance to water in voxels where the rule applies; 0 = ignore the water distance.</summary>
        public int MaxWaterDistanceVoxels;

        /// <summary>0 = dry land only, 1 = underwater only, −1/other = both sides allowed.</summary>
        public int UnderwaterRequirement = -1;

        /// <summary>Chance (0…1) that a site passing every gate receives a plant.</summary>
        public float Density = 0.5f;

        /// <summary>Minimum distance between two plants of this rule.</summary>
        public int SpacingVoxels;

        /// <summary>Plants allowed inside <see cref="ClusterRadiusVoxels"/>; 0 = no clustering limit.</summary>
        public int ClusterSize;

        public int ClusterRadiusVoxels = 8;

        public string Provenance = "CUBEGAME_NATIVE: vegetation rule (G2-E)";

        public bool AllowsBiome(string biomeId)
        {
            if (BiomeIds == null || BiomeIds.Length == 0) return true;
            foreach (string allowed in BiomeIds)
            {
                if (string.Equals(allowed, biomeId ?? string.Empty, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        public VegetationRejection Check(in VegetationSiteSample site)
        {
            if (!AllowsBiome(site.BiomeId)) return VegetationRejection.BiomeNotAllowed;
            if (site.Slope < MinSlope || site.Slope > MaxSlope) return VegetationRejection.SlopeOutOfRange;
            if (site.HeightAboveWater < MinHeightAboveWater || site.HeightAboveWater > MaxHeightAboveWater)
            {
                return VegetationRejection.HeightOutOfRange;
            }

            if (site.Moisture < MinMoisture || site.Moisture > MaxMoisture)
            {
                return VegetationRejection.MoistureOutOfRange;
            }

            if (site.Temperature < MinTemperature || site.Temperature > MaxTemperature)
            {
                return VegetationRejection.TemperatureOutOfRange;
            }

            if (UnderwaterRequirement == 0 && site.Underwater) return VegetationRejection.WrongWaterSide;
            if (UnderwaterRequirement == 1 && !site.Underwater) return VegetationRejection.WrongWaterSide;
            if (MaxWaterDistanceVoxels > 0 && site.WaterDistanceVoxels > MaxWaterDistanceVoxels)
            {
                return VegetationRejection.TooFarFromWater;
            }

            return VegetationRejection.None;
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(Id)) problems.Add("VEGETATION_RULE_ID_EMPTY");
            if (Kinds == null || Kinds.Length == 0) problems.Add($"VEGETATION_RULE_KINDS {Id}");
            else
            {
                foreach (VegetationKind kind in Kinds)
                {
                    if (!Enum.IsDefined(typeof(VegetationKind), kind)) problems.Add($"VEGETATION_RULE_KIND {Id}");
                }
            }

            if (MinSlope < 0f || MaxSlope > 1f || MinSlope > MaxSlope) problems.Add($"VEGETATION_RULE_SLOPE {Id}");
            if (MinHeightAboveWater > MaxHeightAboveWater) problems.Add($"VEGETATION_RULE_HEIGHT {Id}");
            if (MinMoisture < 0f || MaxMoisture > 1f || MinMoisture > MaxMoisture)
            {
                problems.Add($"VEGETATION_RULE_MOISTURE {Id}");
            }

            if (MinTemperature < 0f || MaxTemperature > 1f || MinTemperature > MaxTemperature)
            {
                problems.Add($"VEGETATION_RULE_TEMPERATURE {Id}");
            }

            if (Density < 0f || Density > 1f) problems.Add($"VEGETATION_RULE_DENSITY {Id}");
            if (SpacingVoxels < 0) problems.Add($"VEGETATION_RULE_SPACING {Id}");
            if (ClusterSize < 0) problems.Add($"VEGETATION_RULE_CLUSTER {Id}");
            if (ClusterRadiusVoxels < 1) problems.Add($"VEGETATION_RULE_CLUSTER_RADIUS {Id}");
            if (MaxWaterDistanceVoxels < 0) problems.Add($"VEGETATION_RULE_WATER_DISTANCE {Id}");
            if (UnderwaterRequirement < -1 || UnderwaterRequirement > 1)
            {
                problems.Add($"VEGETATION_RULE_UNDERWATER {Id}");
            }

            return problems;
        }

        public override string ToString() =>
            $"{Id} biomes={(BiomeIds.Length == 0 ? "any" : string.Join(",", BiomeIds))} " +
            $"density={Density.ToString("0.##", CultureInfo.InvariantCulture)} spacing={SpacingVoxels}";
    }

    /// <summary>Named set of rules; the first matching rule in declared order wins (deterministic).</summary>
    public sealed class VegetationRuleSet
    {
        public const string SparseId = "vegetation.rules.sparse";
        public const string GrasslandId = "vegetation.rules.grassland";
        public const string ForestId = "vegetation.rules.forest";
        public const string DesertId = "vegetation.rules.desert";
        public const string WaterId = "vegetation.rules.water";

        private readonly List<VegetationRule> _rules;

        public VegetationRuleSet(string id, IEnumerable<VegetationRule> rules)
        {
            Id = id ?? string.Empty;
            _rules = new List<VegetationRule>(rules ?? Array.Empty<VegetationRule>());
        }

        public string Id { get; }

        public IReadOnlyList<VegetationRule> Rules => _rules;

        /// <summary>First rule whose biome filter matches the site; null when the biome has no rule.</summary>
        public VegetationRule ResolveRule(in VegetationSiteSample site)
        {
            foreach (VegetationRule rule in _rules)
            {
                if (rule.AllowsBiome(site.BiomeId)) return rule;
            }

            return null;
        }

        /// <summary>
        /// First rule that both serves the biome and accepts the site. Several rules may serve one biome (a shore
        /// rule and a deep-water rule share the water biome), so a site is allowed when *any* of them accepts it.
        /// </summary>
        public VegetationRule ResolveAllowedRule(in VegetationSiteSample site)
        {
            foreach (VegetationRule rule in _rules)
            {
                if (!rule.AllowsBiome(site.BiomeId)) continue;
                if (rule.Check(site) == VegetationRejection.None) return rule;
            }

            return null;
        }

        /// <summary>Gate only: is this site inside every allowed range of at least one matching rule?</summary>
        public VegetationDecision Evaluate(in VegetationSiteSample site)
        {
            VegetationRule allowed = ResolveAllowedRule(site);
            if (allowed != null)
            {
                return new VegetationDecision(true, VegetationRejection.None, allowed.Id, allowed.Kinds[0]);
            }

            VegetationRule rule = ResolveRule(site);
            if (rule == null) return new VegetationDecision(false, VegetationRejection.NoRuleForBiome, Id, default);
            return new VegetationDecision(false, rule.Check(site), rule.Id, default);
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(Id)) problems.Add("VEGETATION_RULESET_ID_EMPTY");
            if (_rules.Count == 0) problems.Add($"VEGETATION_RULESET_EMPTY {Id}");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (VegetationRule rule in _rules)
            {
                problems.AddRange(rule.Validate());
                if (rule != null && !ids.Add(rule.Id)) problems.Add($"VEGETATION_RULE_DUPLICATE {rule.Id}");
            }

            return problems;
        }

        /// <summary>Sparse rule set: bare biomes, rocks and a little grass.</summary>
        public static VegetationRuleSet Sparse() => new VegetationRuleSet(SparseId, new[]
        {
            new VegetationRule
            {
                Id = "vegetation.rule.sparse_grass", Kinds = new[] { VegetationKind.Grass }, MaxSlope = 0.5f,
                Density = 0.12f, SpacingVoxels = 3, MinHeightAboveWater = 1, UnderwaterRequirement = 0,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.rock", Kinds = new[] { VegetationKind.Rock }, MaxSlope = 1f, Density = 0.15f,
                SpacingVoxels = 6, MinHeightAboveWater = 0,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.shrub", Kinds = new[] { VegetationKind.AlpineShrub },
                MaxSlope = 0.6f, Density = 0.1f, SpacingVoxels = 4, MinHeightAboveWater = 1,
            },
        });

        /// <summary>Grassland: grass everywhere flat, flowers clustered, occasional rocks.</summary>
        public static VegetationRuleSet Grassland() => new VegetationRuleSet(GrasslandId, new[]
        {
            new VegetationRule
            {
                Id = "vegetation.rule.grass", Kinds = new[] { VegetationKind.Grass }, MaxSlope = 0.35f,
                Density = 0.7f, SpacingVoxels = 1, MinHeightAboveWater = 1, UnderwaterRequirement = 0,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.flowers", Kinds = new[] { VegetationKind.Flower }, MaxSlope = 0.3f,
                Density = 0.25f, SpacingVoxels = 3, ClusterSize = 6, ClusterRadiusVoxels = 10,
                MinMoisture = 0.2f, MinHeightAboveWater = 1, UnderwaterRequirement = 0,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.rock", Kinds = new[] { VegetationKind.Rock }, MaxSlope = 1f, Density = 0.05f,
                SpacingVoxels = 8, MinHeightAboveWater = 0,
            },
        });

        /// <summary>Forest: dense saplings and mushrooms, ferns in shade (moisture high).</summary>
        public static VegetationRuleSet Forest() => new VegetationRuleSet(ForestId, new[]
        {
            new VegetationRule
            {
                Id = "vegetation.rule.sapling", Kinds = new[] { VegetationKind.Sapling }, MaxSlope = 0.4f,
                Density = 0.35f, SpacingVoxels = 4, ClusterSize = 5, ClusterRadiusVoxels = 14,
                MinMoisture = 0.35f, MinHeightAboveWater = 1, UnderwaterRequirement = 0,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.grass_shade", Kinds = new[] { VegetationKind.Grass }, MaxSlope = 0.5f,
                Density = 0.55f, SpacingVoxels = 1, MinMoisture = 0.3f, MinHeightAboveWater = 1,
                UnderwaterRequirement = 0,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.mushroom", Kinds = new[] { VegetationKind.Mushroom }, MaxSlope = 0.45f,
                Density = 0.12f, SpacingVoxels = 3, MinMoisture = 0.55f, MaxTemperature = 0.8f,
                MinHeightAboveWater = 1, UnderwaterRequirement = 0,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.flowers", Kinds = new[] { VegetationKind.Flower }, MaxSlope = 0.35f,
                Density = 0.15f, SpacingVoxels = 3, ClusterSize = 4, ClusterRadiusVoxels = 8,
                MinMoisture = 0.3f, MinHeightAboveWater = 1, UnderwaterRequirement = 0,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.rock", Kinds = new[] { VegetationKind.Rock }, MaxSlope = 1f, Density = 0.06f,
                SpacingVoxels = 6,
            },
        });

        /// <summary>Desert: rare plants near water and rocks on slopes.</summary>
        public static VegetationRuleSet Desert() => new VegetationRuleSet(DesertId, new[]
        {
            new VegetationRule
            {
                Id = "vegetation.rule.desert_plant", Kinds = new[] { VegetationKind.DesertPlant }, MaxSlope = 0.35f,
                Density = 0.2f, SpacingVoxels = 6, MaxMoisture = 0.4f, MinHeightAboveWater = 1,
                UnderwaterRequirement = 0, MaxWaterDistanceVoxels = 48,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.rock", Kinds = new[] { VegetationKind.Rock }, MaxSlope = 1f, Density = 0.1f,
                SpacingVoxels = 5,
            },
        });

        /// <summary>Water: reeds at the shore, water plants and coral below the surface.</summary>
        public static VegetationRuleSet Water() => new VegetationRuleSet(WaterId, new[]
        {
            new VegetationRule
            {
                Id = "vegetation.rule.reed", Kinds = new[] { VegetationKind.Reed }, MaxSlope = 0.3f, Density = 0.4f,
                SpacingVoxels = 2, MinMoisture = 0.6f, MinHeightAboveWater = 1, UnderwaterRequirement = 0,
                MaxWaterDistanceVoxels = 6,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.water_plant", Kinds = new[] { VegetationKind.WaterPlant },
                Density = 0.3f, SpacingVoxels = 3, UnderwaterRequirement = 1,
            },
            new VegetationRule
            {
                Id = "vegetation.rule.coral", Kinds = new[] { VegetationKind.Coral }, Density = 0.15f,
                SpacingVoxels = 4, UnderwaterRequirement = 1,
            },
        });

        /// <summary>All CubeGame default rule sets, keyed by id.</summary>
        public static IReadOnlyDictionary<string, VegetationRuleSet> Defaults()
        {
            var sets = new Dictionary<string, VegetationRuleSet>(StringComparer.Ordinal);
            foreach (VegetationRuleSet set in new[] { Sparse(), Grassland(), Forest(), Desert(), Water() })
            {
                sets[set.Id] = set;
            }

            return sets;
        }
    }

    /// <summary>
    /// Deterministic gate for a single site: rule ranges, then density roll, then spacing and cluster limits.
    /// Pure function of (seed, rule, site, already accepted plants): same inputs → same answer.
    /// </summary>
    public static class VegetationPolicy
    {
        public static bool Allows(VegetationRule rule, in VegetationSiteSample site) =>
            rule != null && rule.Check(site) == VegetationRejection.None;

        /// <summary>Density roll in 1/10000 units — integer only, so no float platform drift.</summary>
        public static bool PassesDensityRoll(VegetationRule rule, in VegetationSiteSample site, uint worldSeed,
            int siteSlot)
        {
            if (rule == null) return false;
            int threshold = (int)Math.Round(Math.Max(0f, Math.Min(1f, rule.Density)) * 10000f);
            if (threshold <= 0) return false;
            if (threshold >= 10000) return true;
            uint hash = CubeGame.Core.Generation.DeterministicHash.Combine(unchecked((int)worldSeed), site.X, site.Z,
                siteSlot, rule.Id.Length, KindSalt(rule.Kinds));
            return hash % 10000u < (uint)threshold;
        }

        /// <summary>True when the site respects spacing and cluster limits against already accepted plants.</summary>
        public static bool RespectsSpacing(VegetationRule rule, in VegetationSiteSample site,
            IReadOnlyList<VegetationSiteSample> accepted)
        {
            if (rule == null) return false;
            if (accepted == null || accepted.Count == 0) return true;

            long spacing = (long)rule.SpacingVoxels * rule.SpacingVoxels;
            long clusterRadius = (long)rule.ClusterRadiusVoxels * rule.ClusterRadiusVoxels;
            int inCluster = 0;
            for (int i = 0; i < accepted.Count; i++)
            {
                VegetationSiteSample other = accepted[i];
                long dx = other.X - site.X;
                long dz = other.Z - site.Z;
                long distance = dx * dx + dz * dz;
                if (spacing > 0 && distance < spacing) return false;
                if (rule.ClusterSize > 0 && distance <= clusterRadius)
                {
                    inCluster++;
                    if (inCluster >= rule.ClusterSize) return false;
                }
            }

            return true;
        }

        /// <summary>Deterministic kind for an accepted site (stable per seed, coordinate and slot).</summary>
        public static VegetationKind ChooseKind(VegetationRule rule, in VegetationSiteSample site, uint worldSeed,
            int siteSlot)
        {
            if (rule == null || rule.Kinds == null || rule.Kinds.Length == 0) return default;
            if (rule.Kinds.Length == 1) return rule.Kinds[0];
            uint hash = CubeGame.Core.Generation.DeterministicHash.Combine(unchecked((int)worldSeed), site.X, site.Z,
                siteSlot, 0x4B1D);
            return rule.Kinds[(int)(hash % (uint)rule.Kinds.Length)];
        }

        /// <summary>Full deterministic decision for one site.</summary>
        public static VegetationDecision Decide(VegetationRuleSet set, in VegetationSiteSample site, uint worldSeed,
            int siteSlot, IReadOnlyList<VegetationSiteSample> accepted, int placedInZone, int zoneBudget)
        {
            if (set == null) return new VegetationDecision(false, VegetationRejection.NoRuleForBiome, "", default);

            VegetationRule firstMatching = null;
            VegetationRejection firstRejection = VegetationRejection.None;
            foreach (VegetationRule rule in set.Rules)
            {
                if (!rule.AllowsBiome(site.BiomeId)) continue;
                if (firstMatching == null) firstMatching = rule;

                VegetationRejection gate = rule.Check(site);
                if (gate != VegetationRejection.None)
                {
                    if (firstRejection == VegetationRejection.None) firstRejection = gate;
                    continue;
                }

                if (zoneBudget > 0 && placedInZone >= zoneBudget)
                {
                    return new VegetationDecision(false, VegetationRejection.ZoneBudgetReached, rule.Id, default);
                }

                if (!PassesDensityRoll(rule, site, worldSeed, siteSlot))
                {
                    if (firstRejection == VegetationRejection.None) firstRejection = VegetationRejection.DensityRollFailed;
                    continue;
                }

                if (!RespectsSpacing(rule, site, accepted))
                {
                    if (firstRejection == VegetationRejection.None) firstRejection = VegetationRejection.SpacingViolation;
                    continue;
                }

                return new VegetationDecision(true, VegetationRejection.None, rule.Id,
                    ChooseKind(rule, site, worldSeed, siteSlot));
            }

            if (firstMatching == null)
            {
                return new VegetationDecision(false, VegetationRejection.NoRuleForBiome, set.Id, default);
            }

            return new VegetationDecision(false,
                firstRejection == VegetationRejection.None ? VegetationRejection.DensityRollFailed : firstRejection,
                firstMatching.Id, default);
        }

        private static int KindSalt(VegetationKind[] kinds) => kinds == null ? 0 : kinds.Length * 0x9E37;
    }

    /// <summary>
    /// TERMPORARY_RUNTIME binding: CubeGame vegetation kind → the Alpha model that currently represents it.
    /// The rule layer never reads this table; G3/G9 replace it with CubeGame assets without touching a rule.
    /// </summary>
    public static class VegetationModelBinding
    {
        public const string Status =
            "TEMPORARY_RUNTIME: Alpha plant models stand in for CubeGame vegetation kinds until G3/G9";

        private static readonly Dictionary<VegetationKind, AlphaVegetationModel> Bindings =
            new Dictionary<VegetationKind, AlphaVegetationModel>
            {
                { VegetationKind.Grass, AlphaVegetationModel.Grass },
                { VegetationKind.Flower, AlphaVegetationModel.Flowers },
                { VegetationKind.Reed, AlphaVegetationModel.Reed },
                { VegetationKind.WaterPlant, AlphaVegetationModel.UnderwaterPlant },
                { VegetationKind.Coral, AlphaVegetationModel.Coral },
                { VegetationKind.AlpineShrub, AlphaVegetationModel.ThornPlant },
                { VegetationKind.DesertPlant, AlphaVegetationModel.DesertFlower1 },
                { VegetationKind.Mushroom, AlphaVegetationModel.Echinacea },
                { VegetationKind.Sapling, AlphaVegetationModel.Leaf },
                { VegetationKind.Rock, AlphaVegetationModel.Grass2 },
            };

        public static bool TryGetModel(VegetationKind kind, out AlphaVegetationModel model) =>
            Bindings.TryGetValue(kind, out model);

        public static AlphaVegetationModel ModelOf(VegetationKind kind) =>
            Bindings.TryGetValue(kind, out AlphaVegetationModel model) ? model : AlphaVegetationModel.Grass;

        public static IReadOnlyDictionary<VegetationKind, AlphaVegetationModel> All => Bindings;

        /// <summary>Every kind must be bound; a kind without a placeholder is a data hole, not a runtime error.</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            foreach (VegetationKind kind in (VegetationKind[])Enum.GetValues(typeof(VegetationKind)))
            {
                if (!Bindings.ContainsKey(kind)) problems.Add($"VEGETATION_BINDING_MISSING {kind}");
            }

            return problems;
        }
    }
}
