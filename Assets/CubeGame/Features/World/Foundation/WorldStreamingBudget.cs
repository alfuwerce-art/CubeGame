// CubeGame.Features.World.Foundation — G2-C: an explicit world streaming budget instead of magic numbers.
//
// The current runtime grows until memory stops it. The numbers that describe the resulting behaviour were scattered:
// radius 20 chunks (Alpha window), vertex limit from the render-distance option (0x469396), zone cache 24, and a
// measured ~8 998 vertices per 32×32 chunk. G2 puts them into one model with names, an estimate and a status:
//
//   visible chunks   = (2·radius + 1)² of the 41×41 Alpha window
//   vertex budget    = renderDistance · 0.01 · 0x1800000 (the existing, Alpha-derived limit, kept)
//   affordable chunks= vertex budget / measured vertices per chunk
//
// With render distance 50 that is 1 681 requested / 12 582 912 vertices / ≈1 398 chunk meshes — which is exactly
// where the observed "soft overflow" came from. Nothing is optimised here: the model only makes the trade-off
// explicit and measurable, so G2-D and G9 can choose presets deliberately.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>What the game asks the world for. Everything is a decision, not a constant buried in a builder.</summary>
    public sealed class WorldBudgetRequest
    {
        /// <summary>Player option 10…100 (Alpha render distance, drives the vertex limit).</summary>
        public int RenderDistance = 50;

        /// <summary>Chunk radius of the requested window (Alpha streams a 41×41 window).</summary>
        public int ViewRadius = WorldStreamingPolicy.AlphaChunkRadius;

        /// <summary>Chunk radius kept before release.</summary>
        public int KeepRadius = WorldStreamingPolicy.AlphaChunkRadius;

        public int ChunkSize = WorldLayout.ChunkSize;

        /// <summary>Upper bound of plants per chunk mesh (0 = unlimited).</summary>
        public int VegetationPerChunk = 512;

        /// <summary>Upper bound of simultaneously simulated creatures/NPCs.</summary>
        public int NpcBudget = 128;

        /// <summary>Upper bound of active world objects (props, structures, containers).</summary>
        public int WorldObjectBudget = 4096;

        /// <summary>Generated zones kept in memory by the terrain generator.</summary>
        public int ZoneCacheBudget = AlphaTerrainGenerator.ZoneCacheSize;
    }

    /// <summary>Frozen, named budget for one session. Pure data; the runtime reads it, nothing mutates it.</summary>
    public sealed class WorldStreamingBudget
    {
        /// <summary>Baseline measurement of the current mesher (G1-00: 1 398 chunks / 12 582 912 vertices).</summary>
        public const int MeasuredVerticesPerChunk = 8998;

        public const string Provenance =
            "CUBEGAME_NATIVE: budget model over the verified Alpha-derived window and vertex limit (G2-C)";

        private WorldStreamingBudget(WorldBudgetRequest request, int visibleChunks, int retainedChunks,
            long vertexBudget, long vegetationBudget)
        {
            Request = request;
            VisibleChunks = visibleChunks;
            RetainedChunks = retainedChunks;
            VertexBudget = vertexBudget;
            VegetationBudget = vegetationBudget;
        }

        public WorldBudgetRequest Request { get; }

        public int ChunkSize => Request.ChunkSize;

        public int RenderDistance => Request.RenderDistance;

        /// <summary>Chunks the window asks for: (2·ViewRadius + 1)².</summary>
        public int VisibleChunks { get; }

        /// <summary>Chunks the policy may keep before release: (2·KeepRadius + 1)².</summary>
        public int RetainedChunks { get; }

        public long VertexBudget { get; }

        /// <summary>Total plants allowed across loaded chunks (0 = unlimited).</summary>
        public long VegetationBudget { get; }

        public int NpcBudget => Request.NpcBudget;

        public int WorldObjectBudget => Request.WorldObjectBudget;

        public int ZoneCacheBudget => Request.ZoneCacheBudget;

        /// <summary>How many chunk meshes the vertex budget can actually hold at the measured density.</summary>
        public int AffordableChunks =>
            MeasuredVerticesPerChunk <= 0 ? 0 : (int)Math.Min(int.MaxValue, VertexBudget / MeasuredVerticesPerChunk);

        /// <summary>True when the requested window cannot be held by the vertex budget (the observed soft overflow).</summary>
        public bool WindowExceedsVertexBudget => AffordableChunks < VisibleChunks;

        public static WorldStreamingBudget From(WorldBudgetRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            int view = Math.Max(0, request.ViewRadius);
            int keep = Math.Max(view, request.KeepRadius);
            int visible = (2 * view + 1) * (2 * view + 1);
            int retained = (2 * keep + 1) * (2 * keep + 1);
            long vertexBudget = WorldStreamingPolicy.AlphaVertexBudget(request.RenderDistance);
            long vegetation = request.VegetationPerChunk <= 0 ? 0L : (long)request.VegetationPerChunk * visible;
            return new WorldStreamingBudget(request, visible, retained, vertexBudget, vegetation);
        }

        public static WorldStreamingBudget Default() => From(new WorldBudgetRequest());

        public string Describe() =>
            $"render={RenderDistance} chunk={ChunkSize} visible={VisibleChunks} retained={RetainedChunks} " +
            $"vertices={VertexBudget} affordable={AffordableChunks} overflow={WindowExceedsVertexBudget} " +
            $"vegetation={VegetationBudget} npc={NpcBudget} objects={WorldObjectBudget} zones={ZoneCacheBudget}";

        public static List<string> Validate()
        {
            var problems = new List<string>();
            var budget = Default();
            if (budget.RenderDistance != 50) problems.Add("BUDGET_DEFAULT_RENDER_DISTANCE");
            if (budget.VisibleChunks != 1681) problems.Add($"BUDGET_DEFAULT_VISIBLE {budget.VisibleChunks}");
            if (budget.VertexBudget != 12582912L) problems.Add($"BUDGET_DEFAULT_VERTICES {budget.VertexBudget}");
            if (budget.AffordableChunks < 1300 || budget.AffordableChunks > 1450)
            {
                problems.Add($"BUDGET_AFFORDABLE_DRIFT {budget.AffordableChunks}");
            }

            if (!budget.WindowExceedsVertexBudget) problems.Add("BUDGET_OVERFLOW_NOT_VISIBLE");
            if (WorldStreamingPolicy.AlphaVertexBudget(0) != 0) problems.Add("BUDGET_ZERO_RENDER_DISTANCE");
            if (WorldStreamingPolicy.AlphaVertexBudget(100) <= WorldStreamingPolicy.AlphaVertexBudget(50))
            {
                problems.Add("BUDGET_RENDER_DISTANCE_MONOTONIC");
            }

            var tiny = From(new WorldBudgetRequest { RenderDistance = 10, ViewRadius = 2, KeepRadius = 3 });
            if (tiny.VisibleChunks != 25) problems.Add($"BUDGET_TINY_VISIBLE {tiny.VisibleChunks}");
            if (tiny.RetainedChunks != 49) problems.Add($"BUDGET_TINY_RETAINED {tiny.RetainedChunks}");

            var negative = From(new WorldBudgetRequest { RenderDistance = -5, ViewRadius = -1, KeepRadius = -4 });
            if (negative.VertexBudget != 0 || negative.VisibleChunks != 1) problems.Add("BUDGET_NEGATIVE_INPUT");
            if (From(new WorldBudgetRequest { VegetationPerChunk = 0 }).VegetationBudget != 0)
            {
                problems.Add("BUDGET_VEGETATION_UNLIMITED");
            }

            return problems;
        }
    }

    /// <summary>What the runtime actually holds right now.</summary>
    public readonly struct WorldBudgetMeasurement
    {
        public WorldBudgetMeasurement(int loadedChunks, int retainedChunks, int vertices, int plants, int npcs,
            int worldObjects, int generatedZones, int cachedZones)
        {
            LoadedChunks = loadedChunks;
            RetainedChunks = retainedChunks;
            Vertices = vertices;
            Plants = plants;
            Npcs = npcs;
            WorldObjects = worldObjects;
            GeneratedZones = generatedZones;
            CachedZones = cachedZones;
        }

        public int LoadedChunks { get; }

        public int RetainedChunks { get; }

        public int Vertices { get; }

        public int Plants { get; }

        public int Npcs { get; }

        public int WorldObjects { get; }

        public int GeneratedZones { get; }

        public int CachedZones { get; }

        public string Describe() =>
            $"chunks={LoadedChunks}/{RetainedChunks} vertices={Vertices} plants={Plants} npc={Npcs} " +
            $"objects={WorldObjects} zones={GeneratedZones}/{CachedZones}";
    }

    public enum WorldBudgetStatus
    {
        Unknown = 0,
        WithinBudget = 1,
        SoftOver = 2,
        Over = 3,
    }

    public readonly struct WorldBudgetReport
    {
        public WorldBudgetReport(WorldBudgetStatus status, IReadOnlyList<string> overages, string description)
        {
            Status = status;
            Overages = overages ?? Array.Empty<string>();
            Description = description ?? string.Empty;
        }

        public WorldBudgetStatus Status { get; }

        public IReadOnlyList<string> Overages { get; }

        public string Description { get; }

        public bool IsFailure => Status == WorldBudgetStatus.Over;

        public override string ToString() =>
            Overages.Count == 0 ? Status.ToString() : $"{Status} [{string.Join("; ", Overages)}]";
    }

    /// <summary>
    /// Compares a measurement against the budget. "SoftOver" is the honest state of the current runtime: the window
    /// asks for more chunk meshes than the vertex limit can hold, and the streamer releases the surplus. A transient
    /// vertex overshoot inside <see cref="VertexTolerance"/> is the same soft limit being enforced one frame later;
    /// beyond it the enforcement is broken, not the design.
    /// </summary>
    public static class WorldBudgetEvaluator
    {
        /// <summary>Vertex overshoot accepted as "the streamer is still releasing" (5 %).</summary>
        public const double VertexTolerance = 1.05;

        public static WorldBudgetReport Evaluate(WorldStreamingBudget budget, in WorldBudgetMeasurement measurement)
        {
            if (budget == null) return new WorldBudgetReport(WorldBudgetStatus.Unknown, null, "no budget");
            var overages = new List<string>();
            bool hard = false;

            if (measurement.Vertices > budget.VertexBudget * VertexTolerance)
            {
                overages.Add($"vertices {measurement.Vertices} > {budget.VertexBudget} (+5 %)");
                hard = true;
            }
            else if (measurement.Vertices > budget.VertexBudget)
            {
                overages.Add($"vertices {measurement.Vertices} > {budget.VertexBudget} (releasing)");
            }

            if (budget.VegetationBudget > 0 && measurement.Plants > budget.VegetationBudget)
            {
                overages.Add($"plants {measurement.Plants} > {budget.VegetationBudget}");
            }

            if (measurement.Npcs > budget.NpcBudget)
            {
                overages.Add($"npc {measurement.Npcs} > {budget.NpcBudget}");
            }

            if (measurement.WorldObjects > budget.WorldObjectBudget)
            {
                overages.Add($"objects {measurement.WorldObjects} > {budget.WorldObjectBudget}");
            }

            if (measurement.CachedZones > budget.ZoneCacheBudget)
            {
                overages.Add($"zones {measurement.CachedZones} > {budget.ZoneCacheBudget}");
            }

            if (measurement.LoadedChunks > budget.VisibleChunks)
            {
                overages.Add($"chunks {measurement.LoadedChunks} > {budget.VisibleChunks}");
            }

            WorldBudgetStatus status;
            if (hard) status = WorldBudgetStatus.Over;
            else if (overages.Count > 0) status = WorldBudgetStatus.SoftOver;
            else status = WorldBudgetStatus.WithinBudget;
            return new WorldBudgetReport(status, overages,
                $"{measurement.Describe()} | {budget.Describe()}");
        }

        /// <summary>
        /// Integrity check used by tests: the runtime may be over a soft limit, but a hard overage means a bug in
        /// enforcement, not a design trade-off.
        /// </summary>
        public static bool IsAcceptable(in WorldBudgetReport report) => !report.IsFailure;

        public static string Format(in WorldBudgetMeasurement measurement) =>
            string.Format(CultureInfo.InvariantCulture, "chunks={0} vertices={1} plants={2} npc={3}",
                measurement.LoadedChunks, measurement.Vertices, measurement.Plants, measurement.Npcs);
    }
}
