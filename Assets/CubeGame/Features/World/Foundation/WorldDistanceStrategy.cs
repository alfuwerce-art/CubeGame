// CubeGame.Features.World.Foundation — G2-D: distance strategy for the production world.
//
// Voxel terrain does not get classic mesh LOD for free: chunks are already flat 32×32 quad sets and the streamer
// drops the ones the vertex budget cannot hold. What the world does need is a *decided* behaviour per distance band:
// what is drawn, which plants still exist, whether creatures simulate, and where fog starts masking the end of the
// world. This file freezes those decisions and exposes them to the streamer/vegetation/spawn layers.
//
// The bands are a policy, not geometry: G9 can swap the numbers (presets) without touching the streaming code.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CubeGame.Features.World.Foundation
{
    public enum WorldDistanceBand
    {
        Near = 0,
        Mid = 1,
        Far = 2,
        Offscreen = 3,
    }

    /// <summary>Everything the world decides per band.</summary>
    public readonly struct DistanceBandSettings
    {
        public DistanceBandSettings(WorldDistanceBand band, int innerChunks, int outerChunks, bool vegetation,
            bool structures, bool creatureSimulation, int maxPlantsPerChunk, float fogStartFraction,
            float fogEndFraction)
        {
            Band = band;
            InnerChunks = innerChunks;
            OuterChunks = outerChunks;
            Vegetation = vegetation;
            Structures = structures;
            CreatureSimulation = creatureSimulation;
            MaxPlantsPerChunk = maxPlantsPerChunk;
            FogStartFraction = fogStartFraction;
            FogEndFraction = fogEndFraction;
        }

        public WorldDistanceBand Band { get; }

        /// <summary>Inclusive chunk-distance range of the band (relative to the focus chunk).</summary>
        public int InnerChunks { get; }

        public int OuterChunks { get; }

        public bool Vegetation { get; }

        public bool Structures { get; }

        public bool CreatureSimulation { get; }

        /// <summary>
        /// Cap of plant instances in one chunk of this band. 0 = no per-band cap (the vegetation flag still decides
        /// whether the band has plants at all).
        /// </summary>
        public int MaxPlantsPerChunk { get; }

        /// <summary>Fog start as a fraction of the visible radius (0…1).</summary>
        public float FogStartFraction { get; }

        public float FogEndFraction { get; }

        public bool Contains(int distanceChunks) => distanceChunks >= InnerChunks && distanceChunks <= OuterChunks;

        public string Describe() =>
            $"{Band} [{InnerChunks}…{OuterChunks}] veg={Vegetation}({MaxPlantsPerChunk}) struct={Structures} " +
            $"sim={CreatureSimulation} fog=[{FogStartFraction.ToString("0.##", CultureInfo.InvariantCulture)}…" +
            $"{FogEndFraction.ToString("0.##", CultureInfo.InvariantCulture)}]";
    }

    /// <summary>Distance policy of the current world: near/mid/far bands derived from the render distance.</summary>
    public sealed class WorldDistanceStrategy
    {
        public const string Provenance =
            "CUBEGAME_NATIVE: distance bands over the streamed voxel world (G2-D); classic mesh LOD is not used";

        private readonly DistanceBandSettings[] _bands;

        private WorldDistanceStrategy(int viewRadius, DistanceBandSettings[] bands)
        {
            ViewRadius = viewRadius;
            _bands = bands;
        }

        /// <summary>Chunk radius the world is willing to draw (the streaming window).</summary>
        public int ViewRadius { get; }

        public IReadOnlyList<DistanceBandSettings> Bands => _bands;

        public static WorldDistanceStrategy ForRenderDistance(int viewRadius)
        {
            int radius = Math.Max(1, viewRadius);
            int nearOuter = Math.Max(1, radius / 4);
            int midOuter = Math.Max(nearOuter + 1, radius / 2);
            var bands = new[]
            {
                new DistanceBandSettings(WorldDistanceBand.Near, 0, nearOuter, true, true, true, 0, 0.70f, 0.95f),
                new DistanceBandSettings(WorldDistanceBand.Mid, nearOuter + 1, midOuter, true, true, false, 64,
                    0.75f, 0.97f),
                new DistanceBandSettings(WorldDistanceBand.Far, midOuter + 1, radius, false, true, false, 0,
                    0.55f, 0.92f),
                new DistanceBandSettings(WorldDistanceBand.Offscreen, radius + 1, int.MaxValue, false, false, false,
                    0, 1f, 1f),
            };
            return new WorldDistanceStrategy(radius, bands);
        }

        public static WorldDistanceStrategy Default() => ForRenderDistance(WorldStreamingPolicy.AlphaChunkRadius);

        public WorldDistanceBand Classify(int distanceChunks)
        {
            if (distanceChunks <= 0) return WorldDistanceBand.Near;
            foreach (DistanceBandSettings band in _bands)
            {
                if (band.Contains(distanceChunks)) return band.Band;
            }

            return WorldDistanceBand.Offscreen;
        }

        public DistanceBandSettings SettingsOf(WorldDistanceBand band)
        {
            foreach (DistanceBandSettings settings in _bands)
            {
                if (settings.Band == band) return settings;
            }

            return _bands[_bands.Length - 1];
        }

        public DistanceBandSettings SettingsForDistance(int distanceChunks) => SettingsOf(Classify(distanceChunks));

        public bool AllowsVegetation(int distanceChunks) => SettingsForDistance(distanceChunks).Vegetation;

        public bool AllowsCreatureSimulation(int distanceChunks) =>
            SettingsForDistance(distanceChunks).CreatureSimulation;

        public int MaxPlantsPerChunk(int distanceChunks) => SettingsForDistance(distanceChunks).MaxPlantsPerChunk;

        /// <summary>
        /// The streamer already fades the horizon with a shader term based on the nearest missing chunk. The strategy
        /// states where fog must start so a new band cannot appear before the world stops.
        /// </summary>
        public float FogStartVoxels(int chunkSize) =>
            SettingsOf(WorldDistanceBand.Far).FogStartFraction * ViewRadius * Math.Max(1, chunkSize);

        public float FogEndVoxels(int chunkSize) =>
            SettingsOf(WorldDistanceBand.Far).FogEndFraction * ViewRadius * Math.Max(1, chunkSize);

        public string Describe() =>
            $"view={ViewRadius} " + string.Join(" | ", DescribeBands());

        public IEnumerable<string> DescribeBands()
        {
            foreach (DistanceBandSettings band in _bands) yield return band.Describe();
        }

        public static List<string> Validate()
        {
            var problems = new List<string>();
            var strategy = Default();
            if (strategy.Bands.Count != 4) problems.Add("DISTANCE_BANDS_COUNT");
            if (strategy.Classify(0) != WorldDistanceBand.Near) problems.Add("DISTANCE_CLASSIFY_ZERO");
            if (strategy.Classify(-3) != WorldDistanceBand.Near) problems.Add("DISTANCE_CLASSIFY_NEGATIVE");
            if (strategy.Classify(strategy.ViewRadius) != WorldDistanceBand.Far)
            {
                problems.Add("DISTANCE_CLASSIFY_EDGE");
            }

            if (strategy.Classify(strategy.ViewRadius + 1) != WorldDistanceBand.Offscreen)
            {
                problems.Add("DISTANCE_CLASSIFY_BEYOND");
            }

            if (!strategy.AllowsVegetation(0)) problems.Add("DISTANCE_NEAR_VEGETATION");
            if (strategy.AllowsVegetation(strategy.ViewRadius)) problems.Add("DISTANCE_FAR_VEGETATION");
            if (!strategy.AllowsCreatureSimulation(0)) problems.Add("DISTANCE_NEAR_SIMULATION");
            if (strategy.AllowsCreatureSimulation(strategy.ViewRadius)) problems.Add("DISTANCE_FAR_SIMULATION");
            if (strategy.FogStartVoxels(32) >= strategy.FogEndVoxels(32)) problems.Add("DISTANCE_FOG_ORDER");
            if (strategy.FogEndVoxels(32) > strategy.ViewRadius * 32) problems.Add("DISTANCE_FOG_BEYOND_WORLD");

            // Bands must tile 0…radius without gaps or overlaps.
            int expected = 0;
            foreach (DistanceBandSettings band in strategy.Bands)
            {
                if (band.InnerChunks != expected && band.Band != WorldDistanceBand.Offscreen)
                {
                    problems.Add($"DISTANCE_BAND_GAP {band.Band} inner={band.InnerChunks} expected={expected}");
                }

                if (band.OuterChunks < band.InnerChunks) problems.Add($"DISTANCE_BAND_ORDER {band.Band}");
                expected = band.OuterChunks == int.MaxValue ? expected : band.OuterChunks + 1;
            }

            var tiny = ForRenderDistance(1);
            if (tiny.Classify(1) == WorldDistanceBand.Offscreen) problems.Add("DISTANCE_TINY_VIEW");
            return problems;
        }
    }
}
