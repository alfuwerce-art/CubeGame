// CubeGame.Features.World.Foundation — G2-F: structure placement foundation.
//
// The runtime already has a content layer that can produce a house footprint (AlphaStructurePlacementPlanner, W-17
// size evidence) and the world delta store can remember that an object was removed. What was missing is a CubeGame
// pipeline that every future structure goes through and whose rules belong to this project:
//
//   candidate (deterministic per zone) → validation (rule + world facts) → placement → persistent id → runtime state
//
// Kinds are already the ones G5/G11 need: city, village, camp, ruin, tower, cave entrance, dungeon entrance,
// landmark. No production city generator, dungeon layout or art is built here — this is the seam they will plug into.
//
// Persistence: a structure's persistent id is derived from (world seed, zone, rule, slot), so unloading and
// reloading a zone yields the same structure. "Destroyed" is stored in the existing world delta channel for zone
// spawns (WorldDeltaKind.ObjectRemoved on the structure channel), not in a new save format.
using System;
using System.Collections.Generic;
using System.Globalization;
using CubeGame.Content;
using CubeGame.Core.Generation;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World.Foundation
{
    public enum StructureKind
    {
        City = 0,
        Village = 1,
        Camp = 2,
        Ruin = 3,
        Tower = 4,
        CaveEntrance = 5,
        DungeonEntrance = 6,
        Landmark = 7,
    }

    /// <summary>What the world must be like for a structure rule to place anything.</summary>
    public sealed class StructureRule
    {
        public string Id = string.Empty;
        public StructureKind Kind;

        /// <summary>Template whose footprint size is used (Alpha templates carry evidence sizes only).</summary>
        public string TemplateId = AlphaStructureTemplates.HouseId;

        /// <summary>Biomes the rule applies to; empty = any biome.</summary>
        public string[] BiomeIds = Array.Empty<string>();

        public int MinRegionLevel = 1;
        public int MaxRegionLevel = int.MaxValue;

        /// <summary>How many candidates a zone may produce for this rule.</summary>
        public int CandidatesPerZone = 1;

        /// <summary>Only place when the footprint is flat and free.</summary>
        public bool RequiresFlat = true;

        public int MinHeightAboveWater = 1;

        /// <summary>Minimum distance (in zones) to another structure of the same rule; 0 = no spacing.</summary>
        public int MinSpacingZones;

        public string Provenance = "CUBEGAME_NATIVE: structure rule (G2-F)";

        public bool AllowsBiome(string biomeId)
        {
            if (BiomeIds == null || BiomeIds.Length == 0) return true;
            foreach (string allowed in BiomeIds)
            {
                if (string.Equals(allowed, biomeId ?? string.Empty, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(Id) || !Id.StartsWith("structure.", StringComparison.Ordinal))
            {
                problems.Add($"STRUCTURE_RULE_ID {Id}");
            }

            if (!Enum.IsDefined(typeof(StructureKind), Kind)) problems.Add($"STRUCTURE_RULE_KIND {Id}");
            if (!AlphaStructureTemplates.TryGet(TemplateId, out _)) problems.Add($"STRUCTURE_RULE_TEMPLATE {Id}");
            if (CandidatesPerZone < 0 || CandidatesPerZone > 8) problems.Add($"STRUCTURE_RULE_CANDIDATES {Id}");
            if (MinRegionLevel < 0 || MaxRegionLevel < MinRegionLevel) problems.Add($"STRUCTURE_RULE_LEVEL {Id}");
            if (MinSpacingZones < 0) problems.Add($"STRUCTURE_RULE_SPACING {Id}");
            if (MinHeightAboveWater < -1000) problems.Add($"STRUCTURE_RULE_HEIGHT {Id}");
            return problems;
        }

        public override string ToString() => $"{Id} {Kind} template={TemplateId} perZone={CandidatesPerZone}";
    }

    /// <summary>Named set of structure rules (data; the pipeline owns no rules of its own).</summary>
    public sealed class StructureRuleSet
    {
        private readonly Dictionary<string, StructureRule> _byId =
            new Dictionary<string, StructureRule>(StringComparer.Ordinal);

        public StructureRuleSet(IEnumerable<StructureRule> rules)
        {
            Rules = new List<StructureRule>(rules ?? Array.Empty<StructureRule>());
            foreach (StructureRule rule in Rules)
            {
                if (rule != null && !string.IsNullOrEmpty(rule.Id)) _byId[rule.Id] = rule;
            }
        }

        public IReadOnlyList<StructureRule> Rules { get; }

        public bool TryGet(string id, out StructureRule rule) => _byId.TryGetValue(id ?? string.Empty, out rule);

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (Rules.Count == 0) problems.Add("STRUCTURE_RULES_EMPTY");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (StructureRule rule in Rules)
            {
                if (rule == null)
                {
                    problems.Add("STRUCTURE_RULE_NULL");
                    continue;
                }

                problems.AddRange(rule.Validate());
                if (!ids.Add(rule.Id)) problems.Add($"STRUCTURE_RULE_DUPLICATE {rule.Id}");
            }

            return problems;
        }

        /// <summary>Default CubeGame rules: the seams exist for every kind the roadmap needs.</summary>
        public static StructureRuleSet Default() => new StructureRuleSet(new[]
        {
            new StructureRule
            {
                Id = "structure.village", Kind = StructureKind.Village,
                BiomeIds = new[] { "biome.grassland", "biome.forest", "biome.rainforest", "biome.temperate" },
                MinRegionLevel = 1, MaxRegionLevel = 6, CandidatesPerZone = 1, RequiresFlat = true,
                MinSpacingZones = 4, Provenance = "CUBEGAME_CONTRACT: village site reserved by G2-G seams",
            },
            new StructureRule
            {
                Id = "structure.camp", Kind = StructureKind.Camp,
                BiomeIds = new[]
                {
                    "biome.grassland", "biome.forest", "biome.snow", "biome.tundra", "biome.volcanic",
                    "biome.temperate",
                },
                MinRegionLevel = 1, CandidatesPerZone = 1, RequiresFlat = true, MinSpacingZones = 2,
                Provenance = "CUBEGAME_NATIVE: camps are the first destructible structures (D02/D18)",
            },
            new StructureRule
            {
                Id = "structure.ruin", Kind = StructureKind.Ruin,
                BiomeIds = new string[0], MinRegionLevel = 1, CandidatesPerZone = 1, RequiresFlat = false,
                MinSpacingZones = 3, Provenance = "CUBEGAME_NATIVE: exploration reward site",
            },
            new StructureRule
            {
                Id = "structure.tower", Kind = StructureKind.Tower,
                BiomeIds = new[] { "biome.highlands", "biome.rock_barrens", "biome.forest" },
                MinRegionLevel = 2, CandidatesPerZone = 1, RequiresFlat = true, MinSpacingZones = 6,
                Provenance = "CUBEGAME_NATIVE: vantage/POI structure",
            },
            new StructureRule
            {
                Id = "structure.dungeon_entrance", Kind = StructureKind.DungeonEntrance,
                BiomeIds = new[] { "biome.grassland", "biome.temperate", "biome.forest" },
                MinRegionLevel = 2, CandidatesPerZone = 1, RequiresFlat = true, MinSpacingZones = 5,
                Provenance = "CUBEGAME_CONTRACT: dungeon entrance seam (G2-G)",
            },
            new StructureRule
            {
                Id = "structure.cave_entrance", Kind = StructureKind.CaveEntrance,
                BiomeIds = new[] { "biome.highlands", "biome.rock_barrens", "biome.forest" },
                MinRegionLevel = 1, CandidatesPerZone = 1, RequiresFlat = false, MinSpacingZones = 3,
                Provenance = "CUBEGAME_CONTRACT: cave entrance seam (G2-G)",
            },
            new StructureRule
            {
                Id = "structure.landmark", Kind = StructureKind.Landmark,
                BiomeIds = new string[0], MinRegionLevel = 1, CandidatesPerZone = 1, RequiresFlat = false,
                MinSpacingZones = 8, Provenance = "CUBEGAME_NATIVE: landmark monument seam",
            },
        });
    }

    /// <summary>World facts about a possible footprint. Supplied by the world, never invented by a rule.</summary>
    public readonly struct StructureFootprint
    {
        public StructureFootprint(bool free, int minSurface, int maxSurface, string biomeId)
        {
            Free = free;
            MinSurface = minSurface;
            MaxSurface = maxSurface;
            BiomeId = biomeId ?? string.Empty;
        }

        public bool Free { get; }

        public int MinSurface { get; }

        public int MaxSurface { get; }

        public string BiomeId { get; }

        public int Flatness => MaxSurface - MinSurface;
    }

    /// <summary>The read-only view of the world a structure planner needs.</summary>
    public interface IStructureProbe
    {
        StructureFootprint Probe(int x, int z, int width, int depth);
    }

    public enum StructureRejection
    {
        None = 0,
        ZoneOutsideWorld = 1,
        RuleLevel = 2,
        RuleBiome = 3,
        TemplateUnknown = 4,
        FootprintUnavailable = 5,
        FootprintNotEmpty = 6,
        NotFlat = 7,
        Underwater = 8,
        TooCloseToSameRule = 9,
        InvalidSlot = 10,
    }

    /// <summary>A structure the rules allowed and the world can hold.</summary>
    public sealed class StructurePlacement
    {
        public string PersistentId = string.Empty;
        public string RuleId = string.Empty;
        public StructureKind Kind;
        public string TemplateId = string.Empty;
        public int ZoneX;
        public int ZoneZ;
        public int X;
        public int Z;
        public int SurfaceY;
        public int Width;
        public int Depth;
        public int Height;
        public int Rotation;
        public string BiomeId = string.Empty;
        public string State = StructureRuntimeState.Generated;

        /// <summary>
        /// Index of this structure inside the zone's spawn channel (assigned by the content provider). It is the
        /// removal key of the structure: <c>WorldDeltaKind.ObjectRemoved</c> on <c>StructureChannel</c>.
        /// </summary>
        public int SpawnIndex = -1;

        public override string ToString() =>
            $"{Kind} {PersistentId} at ({X},{SurfaceY},{Z}) {Width}x{Depth}x{Height} rot={Rotation} {State}";
    }

    /// <summary>Runtime life cycle of a placed structure. Text values survive a save without an enum map.</summary>
    public static class StructureRuntimeState
    {
        public const string Candidate = "candidate";
        public const string Generated = "generated";
        public const string Discovered = "discovered";
        public const string Destroyed = "destroyed";
        public const string Cleared = "cleared";

        public static bool IsKnown(string state) =>
            state == Candidate || state == Generated || state == Discovered || state == Destroyed || state == Cleared;

        /// <summary>Allowed transitions: a template is generated, optionally discovered, then destroyed/cleared.</summary>
        public static bool CanTransition(string from, string to)
        {
            if (!IsKnown(from) || !IsKnown(to)) return false;
            if (from == to) return true;
            return from switch
            {
                Candidate => to == Generated || to == Cleared,
                Generated => to == Discovered || to == Destroyed || to == Cleared,
                Discovered => to == Destroyed || to == Cleared,
                Destroyed => to == Cleared,
                Cleared => false,
                _ => false,
            };
        }
    }

    /// <summary>Persistent identity of a structure: stable across unload, reload and save.</summary>
    public static class StructurePersistentIds
    {
        /// <summary>World-delta channel for structure state (zone spawn channels 0…3 are taken).</summary>
        public const int Channel = 7;

        public static string Of(uint worldSeed, int zoneX, int zoneZ, string ruleId, int slot) =>
            "structure:" + worldSeed.ToString(CultureInfo.InvariantCulture) + ":" +
            zoneX.ToString(CultureInfo.InvariantCulture) + ":" + zoneZ.ToString(CultureInfo.InvariantCulture) + ":" +
            (ruleId ?? string.Empty) + ":" + slot.ToString(CultureInfo.InvariantCulture);

        public static bool TryParse(string id, out uint worldSeed, out int zoneX, out int zoneZ, out string ruleId,
            out int slot)
        {
            worldSeed = 0u;
            zoneX = zoneZ = 0;
            ruleId = string.Empty;
            slot = 0;
            if (string.IsNullOrEmpty(id)) return false;
            string[] parts = id.Split(':');
            if (parts.Length != 6 || parts[0] != "structure") return false;
            return uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out worldSeed) &&
                   int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out zoneX) &&
                   int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out zoneZ) &&
                   (ruleId = parts[4]).Length > 0 &&
                   int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out slot);
        }

        /// <summary>Conversion to the world delta key (zone spawn channel + slot), so removals persist.</summary>
        public static bool TryGetDeltaKey(string persistentId, out ZoneKey zone, out int channel, out int slot)
        {
            zone = default;
            channel = Channel;
            slot = 0;
            if (!TryParse(persistentId, out _, out int zoneX, out int zoneZ, out string ruleId, out int ruleSlot))
            {
                return false;
            }

            zone = new ZoneKey(zoneX, zoneZ);
            slot = IndexWithinZone(ruleId, ruleSlot);
            return true;
        }

        /// <summary>
        /// Index of a structure inside the zone-spawn channel (0…4095, the range of the delta key). Rules place one or
        /// two candidates per zone, so a 12-bit hash space gives each structure its own removal key in practice; a
        /// collision inside one zone is a data error the planner reports.
        /// </summary>
        public static int IndexWithinZone(string ruleId, int ruleSlot)
        {
            int salt = 0x5731;
            if (!string.IsNullOrEmpty(ruleId))
            {
                foreach (char c in ruleId) salt = unchecked(salt * 31 + c);
            }

            uint hash = DeterministicHash.Combine(salt, ruleSlot, 0x51DE);
            return (int)(hash % 4096u);
        }
    }

    /// <summary>Deterministic pipeline: candidate → validation → placement.</summary>
    public static class StructurePlacementPipeline
    {
        public const string Provenance =
            "CUBEGAME_NATIVE: deterministic structure candidates over a CubeGame rule set (G2-F)";

        /// <summary>
        /// Plans the structures of one zone. Same (seed, zone, rules, world facts) → same placements and ids.
        /// Rejections are returned for the debug overlay; nothing is forced when the world says no.
        /// </summary>
        public static List<StructurePlacement> PlanZone(StructureRuleSet rules, BiomeCatalog biomes, uint worldSeed,
            int zoneX, int zoneZ, int regionLevel, IStructureProbe probe, List<StructureRejection> rejections,
            List<string> problems)
        {
            var placements = new List<StructurePlacement>();
            if (rules == null || probe == null)
            {
                problems?.Add("STRUCTURE_PLAN_MISSING_INPUT");
                return placements;
            }

            var zone = new WorldZoneKey(zoneX, zoneZ);
            if (!AlphaWorldScale.IsInsideWorld(zone))
            {
                rejections?.Add(StructureRejection.ZoneOutsideWorld);
                problems?.Add($"STRUCTURE_ZONE_OUTSIDE_WORLD {zone}");
                return placements;
            }

            int originX = AlphaWorldScale.ZoneOriginVoxel(zoneX);
            int originZ = AlphaWorldScale.ZoneOriginVoxel(zoneZ);
            var usedIndices = new HashSet<int>();
            var planned = new List<StructurePlacement>();
            foreach (StructureRule rule in rules.Rules)
            {
                if (rule == null || rule.CandidatesPerZone <= 0) continue;
                if (regionLevel < rule.MinRegionLevel || regionLevel > rule.MaxRegionLevel)
                {
                    rejections?.Add(StructureRejection.RuleLevel);
                    continue;
                }

                if (!AlphaStructureTemplates.TryGet(rule.TemplateId, out AlphaStructureTemplateDef template))
                {
                    rejections?.Add(StructureRejection.TemplateUnknown);
                    problems?.Add($"STRUCTURE_TEMPLATE_UNKNOWN {rule.Id} {rule.TemplateId}");
                    continue;
                }

                int maxX = AlphaWorldScale.ZoneVoxels - template.Width;
                int maxZ = AlphaWorldScale.ZoneVoxels - template.Depth;
                if (maxX < 0 || maxZ < 0)
                {
                    rejections?.Add(StructureRejection.InvalidSlot);
                    problems?.Add($"STRUCTURE_TEMPLATE_TOO_LARGE {rule.Id}");
                    continue;
                }

                for (int slot = 0; slot < rule.CandidatesPerZone; slot++)
                {
                    uint hash = DeterministicHash.Combine(unchecked((int)worldSeed), zoneX, zoneZ, RuleSalt(rule),
                        slot, template.Width, template.Depth);
                    int offsetX = (int)((hash >> 3) % (uint)(maxX + 1));
                    int offsetZ = (int)((hash >> 13) % (uint)(maxZ + 1));
                    int x = originX + offsetX;
                    int z = originZ + offsetZ;
                    StructureFootprint footprint = probe.Probe(x, z, template.Width, template.Depth);
                    if (!rule.AllowsBiome(footprint.BiomeId))
                    {
                        rejections?.Add(StructureRejection.RuleBiome);
                        continue;
                    }

                    if (footprint.MinSurface <= int.MinValue / 4)
                    {
                        rejections?.Add(StructureRejection.FootprintUnavailable);
                        problems?.Add($"STRUCTURE_NO_SURFACE {rule.Id} {x}:{z}");
                        continue;
                    }

                    int minSurface = footprint.MinSurface;
                    if (minSurface < rule.MinHeightAboveWater)
                    {
                        rejections?.Add(StructureRejection.Underwater);
                        continue;
                    }

                    if (rule.RequiresFlat)
                    {
                        if (!footprint.Free)
                        {
                            rejections?.Add(StructureRejection.FootprintNotEmpty);
                            continue;
                        }

                        if (footprint.Flatness > 1)
                        {
                            rejections?.Add(StructureRejection.NotFlat);
                            continue;
                        }
                    }

                    if (rule.MinSpacingZones > 0 && IsTooClose(placements, zoneX, zoneZ, rule))
                    {
                        rejections?.Add(StructureRejection.TooCloseToSameRule);
                        continue;
                    }

                    // Every structure of a zone needs its own removal key inside the delta channel.
                    int deltaIndex = StructurePersistentIds.IndexWithinZone(rule.Id, slot);
                    if (!usedIndices.Add(deltaIndex))
                    {
                        rejections?.Add(StructureRejection.InvalidSlot);
                        problems?.Add($"STRUCTURE_DELTA_INDEX_COLLISION {rule.Id} {zoneX}:{zoneZ} {deltaIndex}");
                        continue;
                    }

                    var placement = new StructurePlacement
                    {
                        PersistentId = StructurePersistentIds.Of(worldSeed, zoneX, zoneZ, rule.Id, slot),
                        RuleId = rule.Id,
                        Kind = rule.Kind,
                        TemplateId = rule.TemplateId,
                        ZoneX = zoneX,
                        ZoneZ = zoneZ,
                        X = x + template.Width / 2,
                        Z = z + template.Depth / 2,
                        SurfaceY = footprint.Flatness > 1 ? (footprint.MinSurface + footprint.MaxSurface) / 2
                            : footprint.MinSurface,
                        Width = template.Width,
                        Depth = template.Depth,
                        Height = template.Height,
                        Rotation = (int)((hash >> 24) & 3u) * 90,
                        BiomeId = footprint.BiomeId,
                    };
                    planned.Add(placement);
                    placements.Add(placement);
                }
            }

            _ = biomes;
            return placements;
        }

        private static bool IsTooClose(List<StructurePlacement> accepted, int zoneX, int zoneZ, StructureRule rule)
        {
            foreach (StructurePlacement other in accepted)
            {
                if (!string.Equals(other.RuleId, rule.Id, StringComparison.Ordinal)) continue;
                int dx = other.ZoneX - zoneX;
                int dz = other.ZoneZ - zoneZ;
                if (dx * dx + dz * dz < rule.MinSpacingZones * rule.MinSpacingZones) return true;
            }

            return false;
        }

        private static int RuleSalt(StructureRule rule)
        {
            int salt = 0x51ED;
            foreach (char c in rule.Id) salt = unchecked(salt * 31 + c);
            return salt;
        }

        /// <summary>
        /// Applies the world delta layer: a structure recorded as removed in its zone never comes back. The same
        /// channel/slot key is used for containers and other zone content, so the mapping is explicit.
        /// </summary>
        public static List<StructurePlacement> ApplyRemovals(List<StructurePlacement> placements,
            WorldDeltaStore deltas)
        {
            var result = new List<StructurePlacement>();
            if (placements == null) return result;
            foreach (StructurePlacement placement in placements)
            {
                if (deltas != null && StructurePersistentIds.TryGetDeltaKey(placement.PersistentId, out ZoneKey zone,
                        out int channel, out int slot) && deltas.IsObjectRemoved(zone, channel, slot))
                {
                    placement.State = StructureRuntimeState.Destroyed;
                    continue;
                }

                result.Add(placement);
            }

            return result;
        }
    }

    /// <summary>World-backed probe used by the runtime: climate biome + surface flatness of a footprint.</summary>
    public sealed class WorldStructureProbe : IStructureProbe
    {
        private readonly IWorldClimateSource _climate;
        private readonly Func<int, int, int> _surfaceAt;
        private readonly BiomeCatalog _biomes;

        public WorldStructureProbe(IWorldClimateSource climate, Func<int, int, int> surfaceAt, BiomeCatalog biomes)
        {
            _climate = climate ?? throw new ArgumentNullException(nameof(climate));
            _surfaceAt = surfaceAt ?? throw new ArgumentNullException(nameof(surfaceAt));
            _biomes = biomes;
        }

        public StructureFootprint Probe(int x, int z, int width, int depth)
        {
            int min = int.MaxValue;
            int max = int.MinValue;
            for (int i = 0; i < 4; i++)
            {
                int px = x + (i == 1 || i == 3 ? Math.Max(0, width - 1) : 0);
                int pz = z + (i >= 2 ? Math.Max(0, depth - 1) : 0);
                int surface = _surfaceAt(px, pz);
                if (surface <= int.MinValue / 4) return new StructureFootprint(false, int.MinValue, int.MinValue, "");
                min = Math.Min(min, surface);
                max = Math.Max(max, surface);
            }

            ClimateAxes axes = _climate.Sample(x, z);
            string biome = _biomes?.Resolve(axes)?.Id ?? string.Empty;
            // "Free" is decided by the world (blockers/objects); flatness and water rules are the planner's own gates.
            return new StructureFootprint(true, min, max, biome);
        }
    }
}
