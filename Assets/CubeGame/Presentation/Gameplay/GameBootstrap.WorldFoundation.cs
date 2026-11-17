// CubeGame.Gameplay — G2: turns the running session into a CubeGame World Foundation context.
//
// What this adds to the existing runtime (nothing is replaced):
//   * biome table + climate sampler (G2-B) over the verified generator;
//   * an explicit streaming budget applied to StreamingWorldView (G2-C) and a distance strategy (G2-D);
//   * structure/seam candidates of the current zone, computed through the CubeGame pipeline (G2-F/G2-G);
//   * the debug overlay (G2-H) and the performance probe (G2-I);
//   * the generation identity of the world, logged at build and validated by the save layer (G2-J);
//   * the entity definition registry seam for G3.
using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Features.World.Foundation;
using CubeGame.Gameplay.Streaming;
using UnityEngine;

namespace CubeGame.Gameplay
{
    public sealed partial class GameBootstrap
    {
        private BiomeCatalog _worldBiomes;
        private WorldStreamingBudget _worldBudget;
        private WorldDistanceStrategy _worldDistance;
        private WorldPerformanceRecorder _worldMetrics;
        private AlphaWorldClimateSource _worldClimate;
        private IWorldClimateSource _worldClimateSource;
        private WorldStructureProbe _worldStructureProbe;
        private StructureRuleSet _worldStructureRules;
        private EntityDefinitionRegistry _entityDefinitions;
        private WorldDebugOverlay _worldDebugOverlay;
        private WorldMetricsProbe _worldMetricsProbe;
        private string _worldMetricsPath = string.Empty;
        private bool _debugOverlayFromStart;
        private List<string> _foundationProblems = new List<string>();

        // Debug sampling is expensive (climate reads 25 cells); cache it until the hero moves far enough.
        private int _debugSampleX = int.MinValue;
        private int _debugSampleZ = int.MinValue;
        private ClimateAxes _debugAxes;
        private string _debugBiomeId = string.Empty;
        private string _debugBiomeName = string.Empty;
        private int _debugZoneX = int.MinValue;
        private int _debugZoneZ = int.MinValue;
        private List<StructurePlacement> _debugStructures = new List<StructurePlacement>();
        private readonly List<StructureRejection> _debugRejections = new List<StructureRejection>();

        /// <summary>CubeGame biome table of the current session (null before a world is entered).</summary>
        public BiomeCatalog WorldBiomes => _worldBiomes;

        /// <summary>Streaming budget applied to the world view.</summary>
        public WorldStreamingBudget WorldBudget => _worldBudget;

        /// <summary>Distance bands of the current view.</summary>
        public WorldDistanceStrategy WorldDistance => _worldDistance;

        /// <summary>Session performance recorder (G2-I).</summary>
        public WorldPerformanceRecorder WorldMetrics => _worldMetrics;

        /// <summary>Dev overlay; visible flag is toggled with F9 or <c>-cubeDebugOverlay</c>.</summary>
        public WorldDebugOverlay DebugOverlay => _worldDebugOverlay;

        public WorldMetricsProbe MetricsProbe => _worldMetricsProbe;

        /// <summary>Entity definition seam for G3 (empty until the factory supplies definitions).</summary>
        public EntityDefinitionRegistry EntityDefinitions
        {
            get
            {
                if (_entityDefinitions == null)
                {
                    _entityDefinitions = new EntityDefinitionRegistry();
                }

                return _entityDefinitions;
            }
        }

        /// <summary>Structural problems of the foundation (computed once per session).</summary>
        public IReadOnlyList<string> WorldFoundationProblems => _foundationProblems;

        /// <summary>Current debug snapshot, or an "unavailable" snapshot outside a world.</summary>
        public WorldDebugSnapshot WorldFoundationSnapshot() => BuildWorldDebugSnapshot();

        private void SetupWorldFoundation(IWorldGenerator generator)
        {
            _worldBiomes ??= BiomeCatalog.Default();
            _worldStructureRules ??= StructureRuleSet.Default();
            _foundationProblems = WorldFoundationValidation.Check();
            if (_foundationProblems.Count > 0)
            {
                Debug.LogWarning($"[World] foundation check: {_foundationProblems.Count} problem(s): " +
                                 string.Join("; ", _foundationProblems));
            }

            _worldBudget = WorldStreamingBudget.From(new WorldBudgetRequest
            {
                RenderDistance = Math.Max(1, _options != null ? _options.RenderDistance : 50),
                ViewRadius = WorldStreamingPolicy.AlphaChunkRadius,
                KeepRadius = WorldStreamingPolicy.AlphaChunkRadius,
            });
            _worldDistance = WorldDistanceStrategy.ForRenderDistance(WorldStreamingPolicy.AlphaChunkRadius);
            _worldMetrics = new WorldPerformanceRecorder();
            _debugSampleX = int.MinValue;
            _debugSampleZ = int.MinValue;
            _debugZoneX = int.MinValue;
            _debugZoneZ = int.MinValue;

            if (generator is AlphaTerrainGenerator terrain)
            {
                _worldClimate = new AlphaWorldClimateSource(terrain);
                _worldClimateSource = _worldClimate;
                _worldStructureProbe = new WorldStructureProbe(_worldClimate,
                    (x, z) => _worldState != null ? _worldState.SurfaceAt(x, z) : int.MinValue, _worldBiomes);
            }
            else
            {
                _worldClimate = null;
                _worldClimateSource = null;
                _worldStructureProbe = null;
            }

            if (_worldView != null)
            {
                _worldView.AttachMetrics(_worldMetrics);
                _worldView.ApplyBudget(_worldBudget);
            }

            if (_worldRoot == null) return;

            var overlayObject = new GameObject("WorldDebugOverlay");
            overlayObject.transform.SetParent(_worldRoot.transform, false);
            _worldDebugOverlay = overlayObject.AddComponent<WorldDebugOverlay>();
            _worldDebugOverlay.Initialise(BuildWorldDebugSnapshot, _debugOverlayFromStart);

            var metricsObject = new GameObject("WorldMetrics");
            metricsObject.transform.SetParent(_worldRoot.transform, false);
            _worldMetricsProbe = metricsObject.AddComponent<WorldMetricsProbe>();
            _worldMetricsProbe.Initialise(_worldMetrics, MeasureWorldBudget, BuildWorldDebugSnapshot,
                _worldMetricsPath);

            Debug.Log("[World] foundation: " + WorldFoundationValidation.Describe());
            if (_session?.World != null)
            {
                Debug.Log("[World] generation identity: " +
                          WorldGenerationIdentity.Describe(_session.World.GeneratorId, _session.World.Seed.Value));
            }

            Debug.Log("[World] budget: " + _worldBudget.Describe());
            Debug.Log("[World] distance: " + _worldDistance.Describe());
        }

        private void TeardownWorldFoundation()
        {
            if (_worldMetricsProbe != null)
            {
                _worldMetricsProbe.Write();
                _worldMetricsProbe = null;
            }

            _worldDebugOverlay = null;
            _worldMetrics = null;
            _worldClimate = null;
            _worldClimateSource = null;
            _worldStructureProbe = null;
            _worldBudget = null;
            _worldDistance = null;
            _debugSampleX = int.MinValue;
            _debugSampleZ = int.MinValue;
            _debugZoneX = int.MinValue;
            _debugZoneZ = int.MinValue;
            _debugStructures = new List<StructurePlacement>();
        }

        /// <summary>Live budget measurement of the current world (G2-C/G2-I).</summary>
        public WorldBudgetMeasurement MeasureWorldBudget()
        {
            if (_worldView == null) return default;
            int npcs = (_population?.LiveCount ?? 0) + (_recoveredPopulation?.LiveCount ?? 0);
            int objects = _worldView.LoadedPlants;
            return new WorldBudgetMeasurement(
                _worldView.LoadedCount,
                _worldView.LoadedCount + _worldView.PendingCount,
                _worldView.LoadedVertices,
                _worldView.LoadedPlants,
                npcs,
                objects,
                _worldView.GeneratedZoneCount,
                _worldView.CachedZoneCount);
        }

        private WorldDebugSnapshot BuildWorldDebugSnapshot()
        {
            if (_worldView == null || _worldState == null || _session?.World == null)
            {
                return WorldDebugSnapshot.Unavailable("NO_WORLD");
            }

            Vector3 scene = _player != null ? _player.transform.position : Vector3.zero;
            _worldView.ToWorld(scene, out double worldX, out double worldY, out double worldZ);
            int voxelX = (int)Math.Floor(worldX);
            int voxelZ = (int)Math.Floor(worldZ);
            SampleDebugClimate(voxelX, voxelZ);

            int terrainHeight = _worldState.SurfaceAt(voxelX, voxelZ);
            if (terrainHeight <= int.MinValue / 4) terrainHeight = 0;

            return WorldDebugSnapshot.Build(new WorldDebugRequest
            {
                Seed = _session.World.Seed.Value,
                GeneratorId = _session.World.GeneratorId,
                GenerationFingerprint = WorldGenerationIdentity.Fingerprint(_session.World.GeneratorId,
                    _session.World.Seed.Value),
                WorldX = worldX,
                WorldY = worldY,
                WorldZ = worldZ,
                Climate = _worldClimateSource != null ? _debugAxes : (ClimateAxes?)null,
                BiomeId = _debugBiomeId,
                BiomeName = _debugBiomeName,
                TerrainHeight = terrainHeight,
                WaterLevel = _worldState.Generator.WaterLevel,
                LoadedChunks = _worldView.LoadedCount,
                PendingChunks = _worldView.PendingCount,
                GeneratedZones = _worldView.GeneratedZoneCount,
                CachedZones = _worldView.CachedZoneCount,
                Vertices = _worldView.LoadedVertices,
                Plants = _worldView.LoadedPlants,
                ActiveNpc = (_population?.LiveCount ?? 0) + (_recoveredPopulation?.LiveCount ?? 0),
                Structures = _debugStructures.Count,
                Budget = _worldBudget,
                Measurement = MeasureWorldBudget(),
                Distance = _worldDistance,
                Performance = _worldMetrics?.Snapshot(),
                StructureCandidates = _debugStructures,
                NearbySpawns = NearbySpawnLines(voxelX, voxelZ),
                Problems = _foundationProblems,
            });
        }

        private void SampleDebugClimate(int voxelX, int voxelZ)
        {
            if (_worldClimateSource == null || _worldBiomes == null) return;
            int dx = voxelX - _debugSampleX;
            int dz = voxelZ - _debugSampleZ;
            bool climateStale = _debugSampleX == int.MinValue || (long)dx * dx + (long)dz * dz > 64;
            if (climateStale)
            {
                _debugSampleX = voxelX;
                _debugSampleZ = voxelZ;
                _debugAxes = _worldClimateSource.Sample(voxelX, voxelZ);
                BiomeDefinition biome = _worldBiomes.Resolve(_debugAxes);
                _debugBiomeId = biome?.Id ?? string.Empty;
                _debugBiomeName = biome?.DisplayName ?? string.Empty;
            }

            ZoneKey zone = WorldLayout.ZoneOf(voxelX, voxelZ);
            if (_worldStructureProbe != null && (zone.X != _debugZoneX || zone.Z != _debugZoneZ))
            {
                _debugZoneX = zone.X;
                _debugZoneZ = zone.Z;
                _debugRejections.Clear();
                var problems = new List<string>();
                _debugStructures = StructurePlacementPipeline.PlanZone(_worldStructureRules, _worldBiomes,
                    _session.World.Seed.Value, zone.X, zone.Z,
                    RegionLevelAt(voxelX, voxelZ), _worldStructureProbe, _debugRejections, problems);
            }
        }

        private static int RegionLevelAt(int voxelX, int voxelZ) =>
            AlphaTerrainGenerator.Level(voxelX >> 14, voxelZ >> 14);

        private List<string> NearbySpawnLines(int voxelX, int voxelZ)
        {
            var lines = new List<string>();
            if (_population == null) return lines;
            foreach (LiveSpawn live in _population.Live)
            {
                lines.Add($"{live.Id} kind=0x{live.Definition.Kind.Value:x2} lvl={live.Definition.Level} " +
                          $"at ({live.Definition.Position.X:0}, {live.Definition.Position.Z:0})");
                if (lines.Count >= 6) break;
            }

            _ = voxelX;
            _ = voxelZ;
            return lines;
        }
    }
}
