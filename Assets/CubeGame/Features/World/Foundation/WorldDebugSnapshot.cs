// CubeGame.Features.World.Foundation — G2-H: what a world debug tool must be able to show.
//
// The overlay (Presentation/Gameplay/WorldDebugOverlay) is only a renderer: all facts are collected here, engine-free,
// so the same snapshot can be produced by a PlayMode test, written to a log in the Player, or diffed between runs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>Everything the debug view is built from. All fields are optional; missing data is reported as missing.</summary>
    public sealed class WorldDebugRequest
    {
        public uint Seed;
        public string GeneratorId = string.Empty;
        public string GenerationFingerprint = string.Empty;

        public double WorldX;
        public double WorldY;
        public double WorldZ;

        /// <summary>Focus chunk the distance band is measured from; null = the player's own chunk.</summary>
        public int? FocusChunkX;
        public int? FocusChunkZ;

        public ClimateAxes? Climate;
        public string BiomeId = string.Empty;
        public string BiomeName = string.Empty;
        public int TerrainHeight;
        public int WaterLevel;

        public int LoadedChunks;
        public int PendingChunks;
        public int GeneratedZones;
        public int CachedZones;
        public int Vertices;
        public int Plants;
        public int ActiveNpc;
        public int Structures;

        public WorldStreamingBudget Budget;
        public WorldBudgetMeasurement Measurement;
        public WorldDistanceStrategy Distance;
        public WorldPerformanceSnapshot Performance;

        public IReadOnlyList<StructurePlacement> StructureCandidates;
        public IReadOnlyList<string> NearbySpawns;
        public IReadOnlyList<string> Problems;
    }

    /// <summary>Text lines of the debug view plus the fields tests assert on.</summary>
    public sealed class WorldDebugSnapshot
    {
        public const string UnavailableLine = "world debug: no active world";

        public uint Seed { get; private set; }

        public string GeneratorId { get; private set; } = string.Empty;

        public string GenerationFingerprint { get; private set; } = string.Empty;

        public int ChunkX { get; private set; }
        public int ChunkZ { get; private set; }
        public int ZoneX { get; private set; }
        public int ZoneZ { get; private set; }
        public int RegionX { get; private set; }
        public int RegionZ { get; private set; }

        public string BiomeId { get; private set; } = string.Empty;
        public string BiomeName { get; private set; } = string.Empty;

        public float Temperature { get; private set; }
        public float Humidity { get; private set; }
        public float Rareness { get; private set; }
        public float Relief { get; private set; }

        public int TerrainHeight { get; private set; }
        public int WaterLevel { get; private set; }

        public int LoadedChunks { get; private set; }
        public int PendingChunks { get; private set; }
        public int GeneratedZones { get; private set; }
        public int CachedZones { get; private set; }
        public int Vertices { get; private set; }
        public int Plants { get; private set; }
        public int ActiveNpc { get; private set; }
        public int Structures { get; private set; }

        public WorldBudgetStatus BudgetStatus { get; private set; }
        public string BudgetLine { get; private set; } = string.Empty;
        public string DistanceLine { get; private set; } = string.Empty;

        public IReadOnlyList<string> Lines { get; private set; } = Array.Empty<string>();

        public string Text => string.Join("\n", Lines);

        /// <summary>False for the placeholder snapshot returned when no world is loaded.</summary>
        public bool HasWorld { get; private set; } = true;

        public string StructureBlock => string.Join("\n", _structureLines);

        public string SpawnBlock => string.Join("\n", _spawnLines);

        private readonly List<string> _structureLines = new List<string>();
        private readonly List<string> _spawnLines = new List<string>();

        public static WorldDebugSnapshot Unavailable(string reason)
        {
            var snapshot = new WorldDebugSnapshot
            {
                HasWorld = false,
                Lines = new[] { UnavailableLine, string.IsNullOrEmpty(reason) ? "reason: unknown" : "reason: " + reason },
            };
            return snapshot;
        }

        public static WorldDebugSnapshot Build(WorldDebugRequest request)
        {
            if (request == null) return Unavailable("NO_REQUEST");

            int voxelX = (int)Math.Floor(request.WorldX);
            int voxelZ = (int)Math.Floor(request.WorldZ);
            var chunk = WorldLayout.ChunkOf(voxelX, voxelZ);
            ZoneKey zone = WorldLayout.ZoneOf(voxelX, voxelZ);
            RegionKey region = WorldLayout.RegionOf(voxelX, voxelZ);

            var snapshot = new WorldDebugSnapshot
            {
                Seed = request.Seed,
                GeneratorId = request.GeneratorId ?? string.Empty,
                GenerationFingerprint = request.GenerationFingerprint ?? string.Empty,
                ChunkX = chunk.X,
                ChunkZ = chunk.Z,
                ZoneX = zone.X,
                ZoneZ = zone.Z,
                RegionX = region.X,
                RegionZ = region.Z,
                BiomeId = request.BiomeId ?? string.Empty,
                BiomeName = request.BiomeName ?? string.Empty,
                TerrainHeight = request.TerrainHeight,
                WaterLevel = request.WaterLevel,
                LoadedChunks = request.LoadedChunks,
                PendingChunks = request.PendingChunks,
                GeneratedZones = request.GeneratedZones,
                CachedZones = request.CachedZones,
                Vertices = request.Vertices,
                Plants = request.Plants,
                ActiveNpc = request.ActiveNpc,
                Structures = request.Structures,
            };

            ClimateAxes climate = request.Climate ?? default;
            snapshot.Temperature = climate.Temperature;
            snapshot.Humidity = climate.Humidity;
            snapshot.Rareness = climate.Rareness;
            snapshot.Relief = climate.Relief;

            var lines = new List<string>();
            lines.Add($"seed {snapshot.Seed}  gen {snapshot.GeneratorId}  fp {Short(snapshot.GenerationFingerprint)}");
            lines.Add($"pos ({request.WorldX:0.#}, {request.WorldY:0.#}, {request.WorldZ:0.#})  " +
                      $"chunk ({snapshot.ChunkX},{snapshot.ChunkZ})  zone ({snapshot.ZoneX},{snapshot.ZoneZ})  " +
                      $"region ({snapshot.RegionX},{snapshot.RegionZ})");
            lines.Add($"biome {snapshot.BiomeId} ({snapshot.BiomeName})  " +
                      $"t {climate.Temperature.ToString("0.##", CultureInfo.InvariantCulture)} " +
                      $"h {climate.Humidity.ToString("0.##", CultureInfo.InvariantCulture)} " +
                      $"rare {climate.Rareness.ToString("0.##", CultureInfo.InvariantCulture)} " +
                      $"relief {climate.Relief.ToString("0.##", CultureInfo.InvariantCulture)}");
            lines.Add($"surface {snapshot.TerrainHeight}  water {snapshot.WaterLevel}  " +
                      $"above {snapshot.TerrainHeight - snapshot.WaterLevel}  " +
                      $"level {climate.RegionLevel}  " +
                      $"{(snapshot.TerrainHeight < snapshot.WaterLevel ? "UNDERWATER" : "land")}");
            lines.Add($"stream chunks={snapshot.LoadedChunks} pending={snapshot.PendingChunks} " +
                      $"zones={snapshot.GeneratedZones}/{snapshot.CachedZones} vertices={snapshot.Vertices} " +
                      $"plants={snapshot.Plants} npc={snapshot.ActiveNpc} structures={snapshot.Structures}");

            if (request.Budget == null)
            {
                snapshot.BudgetLine = "budget: none";
                snapshot.BudgetStatus = WorldBudgetStatus.Unknown;
            }
            else
            {
                var measurement = request.Measurement;
                WorldBudgetReport report = WorldBudgetEvaluator.Evaluate(request.Budget, measurement);
                snapshot.BudgetStatus = report.Status;
                snapshot.BudgetLine = $"budget {report.Status}: {report.Description}";
                if (report.Overages.Count > 0)
                {
                    snapshot.BudgetLine += " | over: " + string.Join("; ", report.Overages);
                }
            }

            lines.Add(snapshot.BudgetLine);

            if (request.Distance == null)
            {
                snapshot.DistanceLine = "distance: none";
            }
            else
            {
                int focusX = request.FocusChunkX ?? snapshot.ChunkX;
                int focusZ = request.FocusChunkZ ?? snapshot.ChunkZ;
                int distance = Math.Max(Math.Abs(snapshot.ChunkX - focusX), Math.Abs(snapshot.ChunkZ - focusZ));
                DistanceBandSettings band = request.Distance.SettingsForDistance(distance);
                snapshot.DistanceLine = $"distance view={request.Distance.ViewRadius} band={band.Describe()}";
                lines.Add(snapshot.DistanceLine);
            }

            if (request.Performance != null)
            {
                lines.Add("perf " + request.Performance.Describe());
            }

            if (request.StructureCandidates != null && request.StructureCandidates.Count > 0)
            {
                lines.Add($"structures nearby: {request.StructureCandidates.Count}");
                int shown = 0;
                foreach (StructurePlacement placement in request.StructureCandidates)
                {
                    if (shown >= 6) break;
                    snapshot._structureLines.Add("  " + placement);
                    shown++;
                }

                lines.AddRange(snapshot._structureLines);
            }
            else
            {
                lines.Add("structures nearby: none");
            }

            if (request.NearbySpawns != null && request.NearbySpawns.Count > 0)
            {
                lines.Add($"spawns nearby: {request.NearbySpawns.Count}");
                int shown = 0;
                foreach (string spawn in request.NearbySpawns)
                {
                    if (shown >= 6) break;
                    snapshot._spawnLines.Add("  " + spawn);
                    shown++;
                }

                lines.AddRange(snapshot._spawnLines);
            }
            else
            {
                lines.Add("spawns nearby: none");
            }

            if (request.Problems != null && request.Problems.Count > 0)
            {
                lines.Add($"problems: {request.Problems.Count}");
                int shown = 0;
                foreach (string problem in request.Problems)
                {
                    if (shown >= 6) break;
                    lines.Add("  ! " + problem);
                    shown++;
                }
            }

            snapshot.Lines = lines;
            return snapshot;
        }

        private static string Short(string value) =>
            string.IsNullOrEmpty(value) ? "(none)" : (value.Length <= 16 ? value : value.Substring(0, 16) + "…");

        /// <summary>One-line summary for logs (Player build, save load, tests).</summary>
        public string Summary()
        {
            var sb = new StringBuilder(192);
            sb.Append("seed=").Append(Seed)
                .Append(" chunk=(").Append(ChunkX).Append(',').Append(ChunkZ).Append(')')
                .Append(" zone=(").Append(ZoneX).Append(',').Append(ZoneZ).Append(')')
                .Append(" biome=").Append(BiomeId)
                .Append(" surface=").Append(TerrainHeight)
                .Append(" chunks=").Append(LoadedChunks)
                .Append(" verts=").Append(Vertices)
                .Append(" plants=").Append(Plants)
                .Append(" npc=").Append(ActiveNpc)
                .Append(" budget=").Append(BudgetStatus);
            return sb.ToString();
        }
    }
}
