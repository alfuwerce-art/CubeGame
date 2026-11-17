// CubeGame.Features.World.Foundation — G2-1A: CubeGame owns vegetation placement.
//
// Before G2-1 the plants of a world came from the ported Alpha surface scan (AlphaGroundVegetation) and the Alpha tree
// pass (AlphaZoneTrees). From the CubeGame content world id, the decision is made here instead:
//
//     seed -> biome (BiomeCatalog) -> vegetation profile -> rule set -> VegetationPolicy -> placements -> identity
//
// The Alpha plant models are still what the renderer draws (VegetationModelBinding, TEMPORARY_VISUAL_PLACEHOLDER):
// the *visual* is temporary, the *world rule* is CubeGame. Zone content is deterministic, so unloading and reloading
// a zone, or saving and loading a world, produces the same plants with the same persistent ids.
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>One CubeGame vegetation object: identity, rule, biome and the temporary visual binding.</summary>
    public sealed class VegetationPlacement
    {
        public string PersistentId = string.Empty;
        public string RuleId = string.Empty;
        public string BiomeId = string.Empty;
        public VegetationKind Kind;
        public AlphaVegetationModel Model;
        public int StableIndex;
        public int X;
        public int Y;
        public int Z;
        public string State = "generated";

        public override string ToString() =>
            $"{PersistentId} {Kind}->{Model} at ({X},{Y},{Z}) rule={RuleId} biome={BiomeId}";
    }

    /// <summary>
    /// CubeGame vegetation stage of zone generation. Runs once per zone on a worker thread; no Unity API, no shared
    /// mutable state, no dependency on render distance (the zone is the unit of generation).
    /// </summary>
    public sealed class CubeGameVegetationProvider : IZoneContentProvider
    {
        public const string ProviderKey = "cubegame.vegetation.v1";

        /// <summary>Candidate grid step: one site every N voxels (density is the rule's job, not the grid's).</summary>
        public const int SiteStep = 3;

        /// <summary>Water-proximity search radius in voxels (rule checks compare against it).</summary>
        public const int MaxWaterSearch = 64;

        private readonly BiomeCatalog _biomes;
        private readonly IReadOnlyDictionary<string, VegetationRuleSet> _ruleSets;

        public CubeGameVegetationProvider(BiomeCatalog biomes = null,
            IReadOnlyDictionary<string, VegetationRuleSet> ruleSets = null)
        {
            _biomes = biomes ?? BiomeCatalog.Default();
            _ruleSets = ruleSets ?? VegetationRuleSet.Defaults();
        }

        public string Key => ProviderKey;

        /// <summary>Statistics of the most recent zone (diagnostics, overlay, tests).</summary>
        public ZoneVegetationStats LastZoneStats { get; private set; }

        public void Populate(ZoneContentContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            int zoneX = context.Zone.Zx;
            int zoneZ = context.Zone.Zz;
            int originX = zoneX * AlphaZone.Size;
            int originZ = zoneZ * AlphaZone.Size;
            uint seed = context.Terrain.MasterSeed;
            int waterLevel = context.Terrain.WaterLevel;

            int size = AlphaZone.Size;
            var heights = new int[size * size];
            for (int lx = 0; lx < size; lx++)
            {
                for (int lz = 0; lz < size; lz++)
                {
                    heights[lx + lz * size] = context.Zone.Column(originX + lx, originZ + lz).TopSolid();
                }
            }

            int[] waterDistance = WaterDistance(heights, size, waterLevel);
            ClimateAxes axes = ZoneAxes(context, originX + size / 2, originZ + size / 2);
            BiomeDefinition biome = _biomes.Resolve(axes);
            if (biome == null)
            {
                LastZoneStats = new ZoneVegetationStats(zoneX, zoneZ, string.Empty, string.Empty, 0, 0, 0, "BIOME_UNRESOLVED");
                return;
            }

            if (!_biomes.TryGetVegetation(biome.VegetationProfileId, out VegetationProfile profile) ||
                !_ruleSets.TryGetValue(profile.RuleSetId, out VegetationRuleSet rules))
            {
                LastZoneStats = new ZoneVegetationStats(zoneX, zoneZ, biome.Id, profile?.RuleSetId ?? string.Empty, 0, 0,
                    0, "VEGETATION_PROFILE_MISSING");
                return;
            }

            var probe = new ZoneSeamProbe(context, _biomes);
            var problems = new List<string>();
            SettlementBlueprint settlement = CubeGameContentPlans.SettlementForZone(seed, zoneX, zoneZ, probe,
                StructureRuleSet.Default(), problems);
            DungeonPrototype dungeon = CubeGameContentPlans.DungeonForZone(seed, zoneX, zoneZ, probe, problems);

            int candidates = 0;
            int rejected = 0;
            int acceptLimit = profile.MaxPerZone;
            var acceptedSamples = new List<VegetationSiteSample>();
            for (int lx = 1; lx < size - 1; lx += SiteStep)
            {
                for (int lz = 1; lz < size - 1; lz += SiteStep)
                {
                    int x = originX + lx;
                    int z = originZ + lz;
                    int index = lx + lz * size;
                    int top = heights[index];
                    if (top <= int.MinValue / 4) continue;
                    if (IsReserved(x, z, settlement, dungeon)) continue;

                    int slopeRaw = Slope(heights, lx, lz, size);
                    var site = new VegetationSiteSample(biome.Id, x, z, slopeRaw / 8f, top - waterLevel,
                        context.Zone.Column(x, z).Humidity, context.Zone.Column(x, z).Temperature,
                        waterDistance[index], top < waterLevel);
                    candidates++;

                    int siteSlot = lx * size + lz;
                    VegetationDecision decision = VegetationPolicy.Decide(rules, site, seed, siteSlot,
                        acceptedSamples, acceptedSamples.Count, acceptLimit);
                    if (!decision.Allowed)
                    {
                        rejected++;
                        continue;
                    }

                    // The biome profile scales the rule set: a jungle asks for more than a tundra.
                    if (profile.DensityScale > 0f && profile.DensityScale != 1f &&
                        !WithinProfileScale(decision.RuleId, site, seed, siteSlot, profile.DensityScale))
                    {
                        rejected++;
                        continue;
                    }

                    AlphaVegetationModel model = VegetationModelBinding.ModelOf(decision.Kind);
                    int y = top + 1;
                    int surfaceMaterial = Voxel.Material(context.Zone.Column(x, z).Read(top));
                    int aboveMaterial = Voxel.Material(context.Zone.Column(x, z).Read(y));
                    float yaw = (uint)(siteSlot * 2654435761u) % 360u;
                    float scale = decision.Kind == VegetationKind.Rock ? 0.075f : 0.075f;
                    int stableIndex = acceptedSamples.Count;
                    string id = CubeGameContentIds.Vegetation(seed, zoneX, zoneZ, stableIndex, decision.RuleId);
                    context.Zone.AddPlant(new AlphaPlantPlacement(model, x, y, z, surfaceMaterial, aboveMaterial, 0f,
                        yaw, scale, stableIndex));
                    context.Zone.Vegetation.Add(new VegetationPlacement
                    {
                        PersistentId = id,
                        RuleId = decision.RuleId,
                        BiomeId = biome.Id,
                        Kind = decision.Kind,
                        Model = model,
                        StableIndex = stableIndex,
                        X = x,
                        Y = y,
                        Z = z,
                    });
                    acceptedSamples.Add(site);
                }
            }

            LastZoneStats = new ZoneVegetationStats(zoneX, zoneZ, biome.Id, profile.RuleSetId, candidates,
                acceptedSamples.Count, rejected, string.Empty);
            context.Zone.VegetationCandidates = candidates;
            context.Zone.VegetationProfileId = profile.Id;
            context.Zone.VegetationRuleSetId = profile.RuleSetId;
            context.Zone.BiomeId = biome.Id;
        }

        /// <summary>Biome of a zone centre from the same climate functions the terrain uses.</summary>
        private static ClimateAxes ZoneAxes(ZoneContentContext context, int x, int z)
        {
            AlphaTerrainGenerator terrain = context.Terrain;
            VoxelColumn column = context.Zone.Column(x, z);
            return new ClimateAxes(column.Temperature, column.Humidity, terrain.RareCellTerm(x, z),
                terrain.Mountain(x, z), AlphaTerrainGenerator.Level(x >> 14, z >> 14), column.TopSolid(),
                terrain.WaterLevel);
        }

        private static int Slope(int[] heights, int lx, int lz, int size)
        {
            int centre = heights[lx + lz * size];
            int max = 0;
            max = Math.Max(max, Math.Abs(centre - heights[lx + 1 + lz * size]));
            max = Math.Max(max, Math.Abs(centre - heights[lx - 1 + lz * size]));
            max = Math.Max(max, Math.Abs(centre - heights[lx + (lz + 1) * size]));
            max = Math.Max(max, Math.Abs(centre - heights[lx + (lz - 1) * size]));
            return max;
        }

        /// <summary>BFS distance (voxels) from every column to the nearest water column inside the zone.</summary>
        private static int[] WaterDistance(int[] heights, int size, int waterLevel)
        {
            int[] distance = new int[size * size];
            var queue = new Queue<int>();
            for (int i = 0; i < distance.Length; i++)
            {
                distance[i] = -1;
                if (heights[i] < waterLevel)
                {
                    distance[i] = 0;
                    queue.Enqueue(i);
                }
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int lx = index % size;
                int lz = index / size;
                int next = distance[index] + 1;
                if (next > MaxWaterSearch) continue;
                if (lx > 0) Visit(index - 1);
                if (lx < size - 1) Visit(index + 1);
                if (lz > 0) Visit(index - size);
                if (lz < size - 1) Visit(index + size);

                void Visit(int neighbour)
                {
                    if (distance[neighbour] >= 0) return;
                    distance[neighbour] = next;
                    queue.Enqueue(neighbour);
                }
            }

            for (int i = 0; i < distance.Length; i++)
            {
                if (distance[i] < 0) distance[i] = MaxWaterSearch;
            }

            return distance;
        }

        private static bool IsReserved(int x, int z, SettlementBlueprint settlement, DungeonPrototype dungeon)
        {
            if (settlement != null)
            {
                if (settlement.Contains(x, z)) return true;
                foreach (SettlementRoadCell cell in settlement.RoadCells)
                {
                    if (Math.Abs(cell.X - x) <= 1 && Math.Abs(cell.Z - z) <= 1) return true;
                }

                foreach (SettlementBuildingSlot slot in settlement.Slots)
                {
                    if (Math.Abs(slot.X - x) <= 3 && Math.Abs(slot.Z - z) <= 3) return true;
                }
            }

            if (dungeon != null && Math.Abs(dungeon.EntranceX - x) <= 6 && Math.Abs(dungeon.EntranceZ - z) <= 6)
            {
                return true;
            }

            return false;
        }

        /// <summary>Profile scale applied as an extra deterministic roll (integer only).</summary>
        private static bool WithinProfileScale(string ruleId, in VegetationSiteSample site, uint seed, int slot,
            float scale)
        {
            int threshold = (int)Math.Round(Math.Min(1f, scale) * 10000f);
            if (threshold >= 10000) return true;
            uint hash = CubeGame.Core.Generation.DeterministicHash.Combine(unchecked((int)seed), site.X, site.Z, slot,
                0x5CA1E);
            return hash % 10000u < (uint)threshold;
        }

        /// <summary>Read-only seam probe over the zone being generated.</summary>
        private sealed class ZoneSeamProbe : IWorldSeamProbe
        {
            private readonly ZoneContentContext _context;
            private readonly BiomeCatalog _biomes;

            public ZoneSeamProbe(ZoneContentContext context, BiomeCatalog biomes)
            {
                _context = context;
                _biomes = biomes;
            }

            public string BiomeOf(int x, int z)
            {
                if (!_context.Contains(x, z)) return string.Empty;
                VoxelColumn column = _context.Zone.Column(x, z);
                return _biomes.Resolve(new ClimateAxes(column.Temperature, column.Humidity,
                    _context.Terrain.RareCellTerm(x, z), _context.Terrain.Mountain(x, z),
                    AlphaTerrainGenerator.Level(x >> 14, z >> 14), column.TopSolid(),
                    _context.Terrain.WaterLevel))?.Id ?? string.Empty;
            }

            public int SurfaceAt(int x, int z) =>
                _context.Contains(x, z) ? _context.Zone.Column(x, z).TopSolid() : 0;

            public int RegionLevel(int regionX, int regionZ) => AlphaTerrainGenerator.Level(regionX, regionZ);
        }
    }

    /// <summary>Deterministic counters of one zone's vegetation stage (overlay, metrics, tests).</summary>
    public readonly struct ZoneVegetationStats
    {
        public ZoneVegetationStats(int zoneX, int zoneZ, string biomeId, string ruleSetId, int candidates, int accepted,
            int rejected, string problem)
        {
            ZoneX = zoneX;
            ZoneZ = zoneZ;
            BiomeId = biomeId ?? string.Empty;
            RuleSetId = ruleSetId ?? string.Empty;
            Candidates = candidates;
            Accepted = accepted;
            Rejected = rejected;
            Problem = problem ?? string.Empty;
        }

        public int ZoneX { get; }
        public int ZoneZ { get; }
        public string BiomeId { get; }
        public string RuleSetId { get; }
        public int Candidates { get; }
        public int Accepted { get; }
        public int Rejected { get; }
        public string Problem { get; }

        public override string ToString() =>
            $"zone({ZoneX},{ZoneZ}) biome={BiomeId} rules={RuleSetId} candidates={Candidates} accepted={Accepted} " +
            $"rejected={Rejected}" + (string.IsNullOrEmpty(Problem) ? string.Empty : " problem=" + Problem);
    }
}
