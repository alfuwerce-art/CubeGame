// CubeGame.Features.World.Foundation — G2-B: data-driven biome table.
//
// Before G2 a "biome" was a side effect of the Alpha landmark rule (rand()&7 → landmark type): there was no way to
// add a CubeGame biome, and every biome question had to be answered by reading the Alpha port. This file makes the
// biome a first-class data record: climate envelope + surface + vegetation + spawn + structure rules + weather.
//
// Deliberately not done here: dozens of final biomes and their art. These are the *shells* the pipeline can already
// resolve and that G9/G11 will fill with production content. Numeric envelopes are CubeGame design data, not Alpha
// numbers, so they are versioned in WorldGenerationProfile.BiomeCatalogVersion.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>Which part of the world surface a biome may cover.</summary>
    public enum EnvelopeSurface
    {
        Any = 0,
        Land = 1,
        Underwater = 2,
    }

    /// <summary>Climate box a biome accepts. Every bound inclusive; unused axes are left wide open.</summary>
    public sealed class ClimateEnvelope
    {
        public float MinTemperature;
        public float MaxTemperature = 1f;
        public float MinHumidity;
        public float MaxHumidity = 1f;
        public float MinRelief;
        public float MaxRelief = 1f;
        public float MaxRareness = 1f;
        public EnvelopeSurface Surface = EnvelopeSurface.Any;

        public bool Matches(ClimateAxes axes)
        {
            if (axes.Temperature < MinTemperature || axes.Temperature > MaxTemperature) return false;
            if (axes.Humidity < MinHumidity || axes.Humidity > MaxHumidity) return false;
            if (axes.Relief < MinRelief || axes.Relief > MaxRelief) return false;
            if (axes.Rareness > MaxRareness) return false;
            if (Surface == EnvelopeSurface.Land && axes.IsUnderwater) return false;
            if (Surface == EnvelopeSurface.Underwater && !axes.IsUnderwater) return false;
            return true;
        }

        /// <summary>Smaller is more specific: a biome with a narrow box wins a tie against a wide one.</summary>
        public float Specificity =>
            (MaxTemperature - MinTemperature) + (MaxHumidity - MinHumidity) + (MaxRelief - MinRelief);

        public List<string> Validate(string biomeId)
        {
            var problems = new List<string>();
            if (MinTemperature < 0f || MaxTemperature > 1f || MinTemperature > MaxTemperature)
            {
                problems.Add($"BIOME_ENVELOPE_TEMPERATURE {biomeId}");
            }

            if (MinHumidity < 0f || MaxHumidity > 1f || MinHumidity > MaxHumidity)
            {
                problems.Add($"BIOME_ENVELOPE_HUMIDITY {biomeId}");
            }

            if (MinRelief < 0f || MaxRelief > 1f || MinRelief > MaxRelief)
            {
                problems.Add($"BIOME_ENVELOPE_RELIEF {biomeId}");
            }

            if (MaxRareness < 0f || MaxRareness > 1f) problems.Add($"BIOME_ENVELOPE_RARENESS {biomeId}");
            if (!Enum.IsDefined(typeof(EnvelopeSurface), Surface)) problems.Add($"BIOME_ENVELOPE_SURFACE {biomeId}");
            return problems;
        }

        public override string ToString() =>
            $"t[{MinTemperature:0.##},{MaxTemperature:0.##}] h[{MinHumidity:0.##},{MaxHumidity:0.##}] " +
            $"r[{MinRelief:0.##},{MaxRelief:0.##}] rare<={MaxRareness:0.##} {Surface}";
    }

    /// <summary>
    /// What the ground looks like. The material id of the current Alpha-coloured renderer is intentionally not
    /// claimed here: binding a biome to real art is G9, so the field carries an explicit unbound marker.
    /// </summary>
    public sealed class SurfaceProfile
    {
        public const int UnboundMaterial = -1;

        public string Id = string.Empty;
        public string Family = string.Empty;

        /// <summary>Alpha material id when bound; <see cref="UnboundMaterial"/> while the visual binding is pending.</summary>
        public int AlphaMaterialId = UnboundMaterial;

        /// <summary>0…1 share of rock in the blended surface colour (renderer hint, not a world rule).</summary>
        public float RockBlend;

        public string Status = "VISUAL_BINDING_PENDING";

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(Id)) problems.Add("SURFACE_ID_EMPTY");
            if (string.IsNullOrEmpty(Family)) problems.Add($"SURFACE_FAMILY_EMPTY {Id}");
            if (AlphaMaterialId < UnboundMaterial || AlphaMaterialId > 0xFF) problems.Add($"SURFACE_MATERIAL {Id}");
            if (RockBlend < 0f || RockBlend > 1f) problems.Add($"SURFACE_ROCK_BLEND {Id}");
            return problems;
        }
    }

    /// <summary>Vegetation intent of a biome: which rule set and how much of it. Models are bound separately (G2-E).</summary>
    public sealed class VegetationProfile
    {
        public string Id = string.Empty;

        /// <summary>Id of a <see cref="VegetationRuleSet"/> rule, not of an asset.</summary>
        public string RuleSetId = string.Empty;

        /// <summary>Multiplier on the rule set's density (0 = bare biome).</summary>
        public float DensityScale = 1f;

        public VegetationKind[] Kinds = Array.Empty<VegetationKind>();

        public int MaxPerZone = 64;

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(Id)) problems.Add("VEGETATION_PROFILE_ID_EMPTY");
            if (string.IsNullOrEmpty(RuleSetId)) problems.Add($"VEGETATION_PROFILE_RULES {Id}");
            if (DensityScale < 0f || DensityScale > 4f) problems.Add($"VEGETATION_PROFILE_DENSITY {Id}");
            if (MaxPerZone < 0) problems.Add($"VEGETATION_PROFILE_MAX {Id}");
            if (Kinds == null) problems.Add($"VEGETATION_PROFILE_KINDS {Id}");
            return problems;
        }
    }

    /// <summary>Who lives in a biome and at what strength. Spawn *rules* only — the creature factory is G3.</summary>
    public sealed class SpawnProfile
    {
        public string Id = string.Empty;
        public int MinLevel = 1;
        public int MaxLevel = 5;
        public int MaxCreaturesPerZone = 3;
        public int MaxObjectsPerZone = 4;

        /// <summary>0…1 share of hostile creatures among planned spawns.</summary>
        public float HostileShare = 0.5f;

        public string[] Tags = Array.Empty<string>();

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(Id)) problems.Add("SPAWN_PROFILE_ID_EMPTY");
            if (MinLevel < 1 || MaxLevel < MinLevel) problems.Add($"SPAWN_PROFILE_LEVEL {Id}");
            if (MaxCreaturesPerZone < 0 || MaxObjectsPerZone < 0) problems.Add($"SPAWN_PROFILE_BUDGET {Id}");
            if (HostileShare < 0f || HostileShare > 1f) problems.Add($"SPAWN_PROFILE_HOSTILE {Id}");
            if (Tags == null) problems.Add($"SPAWN_PROFILE_TAGS {Id}");
            return problems;
        }
    }

    /// <summary>Which weather a biome can show. Weather itself is a later stage; the compatibility data lives now.</summary>
    public sealed class WeatherCompatibility
    {
        public bool Rain;
        public bool Snow;
        public bool Fog = true;
        public bool Storm;

        public List<string> Validate(string biomeId)
        {
            var problems = new List<string>();
            if (Snow && Rain) problems.Add($"WEATHER_BOTH_RAIN_AND_SNOW {biomeId}");
            if (Storm && !Rain) problems.Add($"WEATHER_STORM_WITHOUT_RAIN {biomeId}");
            return problems;
        }

        public override string ToString()
        {
            string parts = (Rain ? "rain " : string.Empty) + (Snow ? "snow " : string.Empty) +
                           (Fog ? "fog " : string.Empty) + (Storm ? "storm" : string.Empty);
            return parts.Length == 0 ? "weather(none)" : "weather(" + parts + ")";
        }
    }

    /// <summary>One biome of the world: pure data, resolved from climate.</summary>
    public sealed class BiomeDefinition
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;

        /// <summary>Higher wins when two envelopes match; the fallback biome uses the lowest priority.</summary>
        public int Priority;

        public ClimateEnvelope Climate = new ClimateEnvelope();
        public string VegetationProfileId = string.Empty;
        public string SurfaceProfileId = string.Empty;
        public string SpawnProfileId = string.Empty;

        /// <summary>Structure rule ids this biome allows (see StructureRuleSet).</summary>
        public string[] StructureRuleIds = Array.Empty<string>();

        public WeatherCompatibility Weather = new WeatherCompatibility();

        /// <summary>True for the catch-all biome used when no envelope matches.</summary>
        public bool IsFallback;

        public string Provenance = "CUBEGAME_NATIVE: biome table (G2-B)";

        public override string ToString() => $"{Id} p={Priority} {Climate}";
    }

    /// <summary>Data-driven biome table with a deterministic resolution rule.</summary>
    public sealed class BiomeCatalog
    {
        public const string FallbackBiomeId = "biome.temperate";

        private readonly Dictionary<string, BiomeDefinition> _byId =
            new Dictionary<string, BiomeDefinition>(StringComparer.Ordinal);

        private readonly Dictionary<string, SurfaceProfile> _surfaces =
            new Dictionary<string, SurfaceProfile>(StringComparer.Ordinal);

        private readonly Dictionary<string, VegetationProfile> _vegetation =
            new Dictionary<string, VegetationProfile>(StringComparer.Ordinal);

        private readonly Dictionary<string, SpawnProfile> _spawns =
            new Dictionary<string, SpawnProfile>(StringComparer.Ordinal);

        public BiomeCatalog(IEnumerable<BiomeDefinition> biomes, IEnumerable<SurfaceProfile> surfaces,
            IEnumerable<VegetationProfile> vegetation, IEnumerable<SpawnProfile> spawns)
        {
            Biomes = new List<BiomeDefinition>(biomes ?? Array.Empty<BiomeDefinition>());
            Surfaces = new List<SurfaceProfile>(surfaces ?? Array.Empty<SurfaceProfile>());
            Vegetation = new List<VegetationProfile>(vegetation ?? Array.Empty<VegetationProfile>());
            Spawns = new List<SpawnProfile>(spawns ?? Array.Empty<SpawnProfile>());
            foreach (BiomeDefinition biome in Biomes) _byId[biome.Id] = biome;
            foreach (SurfaceProfile surface in Surfaces) _surfaces[surface.Id] = surface;
            foreach (VegetationProfile profile in Vegetation) _vegetation[profile.Id] = profile;
            foreach (SpawnProfile spawn in Spawns) _spawns[spawn.Id] = spawn;
        }

        public IReadOnlyList<BiomeDefinition> Biomes { get; }

        public IReadOnlyList<SurfaceProfile> Surfaces { get; }

        public IReadOnlyList<VegetationProfile> Vegetation { get; }

        public IReadOnlyList<SpawnProfile> Spawns { get; }

        public bool TryGetBiome(string id, out BiomeDefinition biome) => _byId.TryGetValue(id ?? string.Empty, out biome);

        public bool TryGetSurface(string id, out SurfaceProfile profile) =>
            _surfaces.TryGetValue(id ?? string.Empty, out profile);

        public bool TryGetVegetation(string id, out VegetationProfile profile) =>
            _vegetation.TryGetValue(id ?? string.Empty, out profile);

        public bool TryGetSpawn(string id, out SpawnProfile profile) =>
            _spawns.TryGetValue(id ?? string.Empty, out profile);

        /// <summary>
        /// Deterministic biome of a climate sample: highest priority among matches, then the narrowest envelope,
        /// then the ordinal id. Never returns null for a valid catalog (falls back to the catch-all biome).
        /// </summary>
        public BiomeDefinition Resolve(ClimateAxes axes)
        {
            BiomeDefinition best = null;
            foreach (BiomeDefinition biome in Biomes)
            {
                if (biome?.Climate == null || !biome.Climate.Matches(axes)) continue;
                if (best == null || Beats(biome, best)) best = biome;
            }

            if (best != null) return best;
            foreach (BiomeDefinition biome in Biomes)
            {
                if (biome != null && biome.IsFallback) return biome;
            }

            return null;
        }

        private static bool Beats(BiomeDefinition candidate, BiomeDefinition current)
        {
            if (candidate.Priority != current.Priority) return candidate.Priority > current.Priority;
            float candidateSpecificity = candidate.Climate.Specificity;
            float currentSpecificity = current.Climate.Specificity;
            if (Math.Abs(candidateSpecificity - currentSpecificity) > 1e-6f)
            {
                return candidateSpecificity < currentSpecificity;
            }

            return string.CompareOrdinal(candidate.Id, current.Id) < 0;
        }

        /// <summary>Structural validation: ids, cross-references, envelope sanity.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (BiomeDefinition biome in Biomes)
            {
                if (biome == null)
                {
                    problems.Add("BIOME_NULL");
                    continue;
                }

                if (string.IsNullOrEmpty(biome.Id)) problems.Add("BIOME_ID_EMPTY");
                else if (!seen.Add(biome.Id)) problems.Add($"BIOME_DUPLICATE {biome.Id}");
                if (string.IsNullOrEmpty(biome.DisplayName)) problems.Add($"BIOME_NAME_EMPTY {biome.Id}");
                problems.AddRange(biome.Climate.Validate(biome.Id));
                problems.AddRange(biome.Weather.Validate(biome.Id));
                if (!_surfaces.ContainsKey(biome.SurfaceProfileId)) problems.Add($"BIOME_SURFACE_MISSING {biome.Id}");
                if (!_vegetation.ContainsKey(biome.VegetationProfileId))
                {
                    problems.Add($"BIOME_VEGETATION_MISSING {biome.Id}");
                }

                if (!_spawns.ContainsKey(biome.SpawnProfileId)) problems.Add($"BIOME_SPAWN_MISSING {biome.Id}");
                if (biome.StructureRuleIds == null) problems.Add($"BIOME_STRUCTURE_RULES_NULL {biome.Id}");
                else
                {
                    foreach (string rule in biome.StructureRuleIds)
                    {
                        if (string.IsNullOrEmpty(rule) || !rule.StartsWith("structure.", StringComparison.Ordinal))
                        {
                            problems.Add($"BIOME_STRUCTURE_RULE_BAD {biome.Id} '{rule}'");
                        }
                    }
                }
            }

            if (!seen.Contains(FallbackBiomeId)) problems.Add($"BIOME_FALLBACK_MISSING {FallbackBiomeId}");
            if (_byId.Count != Biomes.Count) problems.Add("BIOME_INDEX_MISMATCH");
            foreach (SurfaceProfile surface in Surfaces) problems.AddRange(surface.Validate());
            foreach (VegetationProfile profile in Vegetation) problems.AddRange(profile.Validate());
            foreach (SpawnProfile spawn in Spawns) problems.AddRange(spawn.Validate());
            if (!HasCompleteCoverage(problems)) problems.Add("BIOME_COVERAGE_INCOMPLETE");
            return problems;
        }

        /// <summary>A few climate probes must each resolve to a biome from this catalog.</summary>
        private bool HasCompleteCoverage(List<string> problems)
        {
            bool ok = true;
            var probes = new[]
            {
                new ClimateAxes(0.5f, 0.5f, 0f, 0.1f, 1, 10, 0),
                new ClimateAxes(0.1f, 0.5f, 0f, 0.2f, 1, 10, 0),
                new ClimateAxes(0.9f, 0.2f, 0f, 0.3f, 1, 10, 0),
                new ClimateAxes(0.7f, 0.9f, 0f, 0.2f, 1, 10, 0),
                new ClimateAxes(0.5f, 0.5f, 0f, 0.9f, 1, 40, 0),
                new ClimateAxes(0.5f, 0.9f, 0f, 0.1f, 1, -20, 0),
                new ClimateAxes(0.9f, 0.5f, 0.8f, 0.2f, 3, 10, 0),
            };
            foreach (ClimateAxes probe in probes)
            {
                if (Resolve(probe) == null)
                {
                    ok = false;
                    problems.Add($"BIOME_PROBE_UNRESOLVED {probe.Describe()}");
                }
            }

            // Underwater must never resolve to a land-only biome.
            BiomeDefinition deep = Resolve(new ClimateAxes(0.5f, 0.9f, 0f, 0f, 1, -30, 0));
            if (deep == null || deep.Climate.Surface == EnvelopeSurface.Land) problems.Add("BIOME_UNDERWATER_LAND");
            return ok;
        }

        public string Describe() =>
            $"biomes={Biomes.Count} surfaces={Surfaces.Count} vegetation={Vegetation.Count} spawns={Spawns.Count} " +
            $"v={WorldGenerationProfile.BiomeCatalogVersion}";

        /// <summary>
        /// The CubeGame default table. Envelopes are design data (CubeGame, not Alpha). Twelve shells are enough to
        /// prove the pipeline; G9/G11 replace/extend them without touching the resolver.
        /// </summary>
        public static BiomeCatalog Default()
        {
            var surfaces = new List<SurfaceProfile>
            {
                new SurfaceProfile { Id = "surface.grass", Family = "grass" },
                new SurfaceProfile { Id = "surface.forest_litter", Family = "grass", RockBlend = 0.05f },
                new SurfaceProfile { Id = "surface.sand", Family = "sand" },
                new SurfaceProfile { Id = "surface.rock", Family = "rock", RockBlend = 0.8f },
                new SurfaceProfile { Id = "surface.snow", Family = "snow" },
                new SurfaceProfile { Id = "surface.ash", Family = "ash", RockBlend = 0.6f },
                new SurfaceProfile { Id = "surface.water_bed", Family = "water_bed", RockBlend = 0.2f },
                new SurfaceProfile { Id = "surface.highland", Family = "rock", RockBlend = 0.5f },
            };

            var vegetation = new List<VegetationProfile>
            {
                new VegetationProfile
                {
                    Id = "vegetation.sparse", RuleSetId = VegetationRuleSet.SparseId, DensityScale = 0.25f,
                    Kinds = new[] { VegetationKind.Grass, VegetationKind.Rock }, MaxPerZone = 24,
                },
                new VegetationProfile
                {
                    Id = "vegetation.grassland", RuleSetId = VegetationRuleSet.GrasslandId, DensityScale = 1f,
                    Kinds = new[] { VegetationKind.Grass, VegetationKind.Flower, VegetationKind.Rock },
                    MaxPerZone = 96,
                },
                new VegetationProfile
                {
                    Id = "vegetation.forest", RuleSetId = VegetationRuleSet.ForestId, DensityScale = 1.2f,
                    Kinds = new[]
                    {
                        VegetationKind.Sapling, VegetationKind.Grass, VegetationKind.Flower,
                        VegetationKind.Mushroom, VegetationKind.Rock,
                    },
                    MaxPerZone = 160,
                },
                new VegetationProfile
                {
                    Id = "vegetation.jungle", RuleSetId = VegetationRuleSet.ForestId, DensityScale = 1.8f,
                    Kinds = new[]
                    {
                        VegetationKind.Sapling, VegetationKind.Grass, VegetationKind.Flower, VegetationKind.Mushroom,
                    },
                    MaxPerZone = 200,
                },
                new VegetationProfile
                {
                    Id = "vegetation.desert", RuleSetId = VegetationRuleSet.DesertId, DensityScale = 0.5f,
                    Kinds = new[] { VegetationKind.DesertPlant, VegetationKind.Rock }, MaxPerZone = 32,
                },
                new VegetationProfile
                {
                    Id = "vegetation.snow", RuleSetId = VegetationRuleSet.SparseId, DensityScale = 0.3f,
                    Kinds = new[] { VegetationKind.AlpineShrub, VegetationKind.Rock }, MaxPerZone = 24,
                },
                new VegetationProfile
                {
                    Id = "vegetation.water", RuleSetId = VegetationRuleSet.WaterId, DensityScale = 0.8f,
                    Kinds = new[] { VegetationKind.WaterPlant, VegetationKind.Coral }, MaxPerZone = 48,
                },
                new VegetationProfile
                {
                    Id = "vegetation.volcanic", RuleSetId = VegetationRuleSet.SparseId, DensityScale = 0.2f,
                    Kinds = new[] { VegetationKind.Rock, VegetationKind.AlpineShrub }, MaxPerZone = 16,
                },
            };

            var spawns = new List<SpawnProfile>
            {
                new SpawnProfile
                {
                    Id = "spawn.grassland", MaxCreaturesPerZone = 3, MaxObjectsPerZone = 4, HostileShare = 0.4f,
                    Tags = new[] { "grazer", "scavenger" },
                },
                new SpawnProfile
                {
                    Id = "spawn.forest", MaxCreaturesPerZone = 4, MaxObjectsPerZone = 6, HostileShare = 0.6f,
                    Tags = new[] { "beast", "ambusher" },
                },
                new SpawnProfile
                {
                    Id = "spawn.desert", MaxCreaturesPerZone = 2, MaxObjectsPerZone = 2, HostileShare = 0.7f,
                    MinLevel = 2, MaxLevel = 8, Tags = new[] { "scorpion", "nomad" },
                },
                new SpawnProfile
                {
                    Id = "spawn.snow", MaxCreaturesPerZone = 2, MaxObjectsPerZone = 2, HostileShare = 0.6f,
                    MinLevel = 2, MaxLevel = 9, Tags = new[] { "predator" },
                },
                new SpawnProfile
                {
                    Id = "spawn.highlands", MaxCreaturesPerZone = 3, MaxObjectsPerZone = 3, HostileShare = 0.75f,
                    MinLevel = 3, MaxLevel = 10, Tags = new[] { "flyer", "mountain_beast" },
                },
                new SpawnProfile
                {
                    Id = "spawn.water", MaxCreaturesPerZone = 2, MaxObjectsPerZone = 2, HostileShare = 0.3f,
                    Tags = new[] { "fish", "water_plant" },
                },
                new SpawnProfile
                {
                    Id = "spawn.volcanic", MaxCreaturesPerZone = 2, MaxObjectsPerZone = 1, HostileShare = 0.9f,
                    MinLevel = 4, MaxLevel = 12, Tags = new[] { "elemental" },
                },
            };

            var biomes = new List<BiomeDefinition>
            {
                new BiomeDefinition
                {
                    Id = "biome.water", DisplayName = "Open water", Priority = 90,
                    Climate = new ClimateEnvelope { Surface = EnvelopeSurface.Underwater },
                    SurfaceProfileId = "surface.water_bed", VegetationProfileId = "vegetation.water",
                    SpawnProfileId = "spawn.water",
                    StructureRuleIds = new[] { "structure.landmark" },
                    Weather = new WeatherCompatibility { Fog = true, Rain = true, Storm = true },
                },
                new BiomeDefinition
                {
                    Id = "biome.volcanic", DisplayName = "Ashen waste", Priority = 80,
                    Climate = new ClimateEnvelope
                    {
                        MinTemperature = 0.7f, MaxTemperature = 1f, MaxRareness = 1f, MinRelief = 0.3f,
                        Surface = EnvelopeSurface.Land,
                    },
                    SurfaceProfileId = "surface.ash", VegetationProfileId = "vegetation.volcanic",
                    SpawnProfileId = "spawn.volcanic",
                    StructureRuleIds = new[] { "structure.ruin", "structure.camp" },
                    Weather = new WeatherCompatibility { Fog = true },
                },
                new BiomeDefinition
                {
                    Id = "biome.highlands", DisplayName = "Highlands", Priority = 70,
                    Climate = new ClimateEnvelope { MinRelief = 0.55f, Surface = EnvelopeSurface.Land },
                    SurfaceProfileId = "surface.highland", VegetationProfileId = "vegetation.sparse",
                    SpawnProfileId = "spawn.highlands",
                    StructureRuleIds = new[] { "structure.tower", "structure.cave_entrance", "structure.ruin" },
                    Weather = new WeatherCompatibility { Rain = true, Snow = false, Fog = true, Storm = true },
                },
                new BiomeDefinition
                {
                    Id = "biome.snow", DisplayName = "Snowfield", Priority = 60,
                    Climate = new ClimateEnvelope { MaxTemperature = 0.25f, Surface = EnvelopeSurface.Land },
                    SurfaceProfileId = "surface.snow", VegetationProfileId = "vegetation.snow",
                    SpawnProfileId = "spawn.snow",
                    StructureRuleIds = new[] { "structure.camp", "structure.ruin" },
                    Weather = new WeatherCompatibility { Snow = true, Fog = true },
                },
                new BiomeDefinition
                {
                    Id = "biome.tundra", DisplayName = "Tundra", Priority = 50,
                    Climate = new ClimateEnvelope
                    {
                        MaxTemperature = 0.4f, MaxHumidity = 0.55f, Surface = EnvelopeSurface.Land,
                    },
                    SurfaceProfileId = "surface.grass", VegetationProfileId = "vegetation.sparse",
                    SpawnProfileId = "spawn.grassland",
                    StructureRuleIds = new[] { "structure.camp", "structure.landmark" },
                    Weather = new WeatherCompatibility { Snow = true, Fog = true },
                },
                new BiomeDefinition
                {
                    Id = "biome.desert", DisplayName = "Desert", Priority = 65,
                    Climate = new ClimateEnvelope
                    {
                        MinTemperature = 0.55f, MaxHumidity = 0.35f, Surface = EnvelopeSurface.Land,
                    },
                    SurfaceProfileId = "surface.sand", VegetationProfileId = "vegetation.desert",
                    SpawnProfileId = "spawn.desert",
                    StructureRuleIds = new[] { "structure.ruin", "structure.camp", "structure.landmark" },
                    Weather = new WeatherCompatibility { Fog = true, Storm = true, Rain = true },
                },
                new BiomeDefinition
                {
                    Id = "biome.rainforest", DisplayName = "Rainforest", Priority = 62,
                    Climate = new ClimateEnvelope
                    {
                        MinTemperature = 0.6f, MinHumidity = 0.75f, Surface = EnvelopeSurface.Land,
                    },
                    SurfaceProfileId = "surface.forest_litter", VegetationProfileId = "vegetation.jungle",
                    SpawnProfileId = "spawn.forest",
                    StructureRuleIds = new[] { "structure.village", "structure.ruin", "structure.cave_entrance" },
                    Weather = new WeatherCompatibility { Rain = true, Fog = true, Storm = true },
                },
                new BiomeDefinition
                {
                    Id = "biome.forest", DisplayName = "Forest", Priority = 45,
                    Climate = new ClimateEnvelope { MinHumidity = 0.55f, Surface = EnvelopeSurface.Land },
                    SurfaceProfileId = "surface.forest_litter", VegetationProfileId = "vegetation.forest",
                    SpawnProfileId = "spawn.forest",
                    StructureRuleIds = new[]
                    {
                        "structure.village", "structure.ruin", "structure.camp", "structure.cave_entrance",
                    },
                    Weather = new WeatherCompatibility { Rain = true, Fog = true },
                },
                new BiomeDefinition
                {
                    Id = "biome.grassland", DisplayName = "Grassland", Priority = 40,
                    Climate = new ClimateEnvelope { Surface = EnvelopeSurface.Land },
                    SurfaceProfileId = "surface.grass", VegetationProfileId = "vegetation.grassland",
                    SpawnProfileId = "spawn.grassland",
                    StructureRuleIds = new[]
                    {
                        "structure.village", "structure.camp", "structure.landmark", "structure.dungeon_entrance",
                    },
                    Weather = new WeatherCompatibility { Rain = true, Fog = true },
                },
                new BiomeDefinition
                {
                    Id = "biome.rock_barrens", DisplayName = "Rock barrens", Priority = 35,
                    Climate = new ClimateEnvelope
                    {
                        MinRelief = 0.4f, MaxHumidity = 0.5f, Surface = EnvelopeSurface.Land,
                    },
                    SurfaceProfileId = "surface.rock", VegetationProfileId = "vegetation.sparse",
                    SpawnProfileId = "spawn.highlands",
                    StructureRuleIds = new[] { "structure.ruin", "structure.tower" },
                    Weather = new WeatherCompatibility { Fog = true, Storm = true, Rain = true },
                },
                new BiomeDefinition
                {
                    Id = FallbackBiomeId, DisplayName = "Temperate lowland", Priority = 0, IsFallback = true,
                    Climate = new ClimateEnvelope { Surface = EnvelopeSurface.Land },
                    SurfaceProfileId = "surface.grass", VegetationProfileId = "vegetation.grassland",
                    SpawnProfileId = "spawn.grassland",
                    StructureRuleIds = new[] { "structure.village", "structure.camp", "structure.landmark" },
                    Weather = new WeatherCompatibility { Rain = true, Fog = true },
                },
            };

            return new BiomeCatalog(biomes, surfaces, vegetation, spawns);
        }
    }
}
