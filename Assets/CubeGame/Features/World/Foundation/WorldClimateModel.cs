// CubeGame.Features.World.Foundation — G2-B: named climate axes over the verified generation data.
//
// The ported climate grid carries two unnamed floats (Alpha Field3/Field4) plus a rare flag and a base height.
// G0 marked that layer REWORK: it is numerically correct but cannot express CubeGame concepts. This file turns
// the same numbers into *named axes* (temperature, humidity, rareness, relief, region level, surface, water) and
// freezes the meaning in one place. No number is re-derived here — the values still come from the generator.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>
    /// Climate of one column, as CubeGame understands it. All ratios are 0…1 unless stated otherwise.
    /// </summary>
    public readonly struct ClimateAxes
    {
        public ClimateAxes(float temperature, float humidity, float rareness, float relief, int regionLevel,
            int surfaceHeight, int waterLevel)
        {
            Temperature = Clamp01(temperature);
            Humidity = Clamp01(humidity);
            Rareness = Clamp01(rareness);
            Relief = Clamp01(relief);
            RegionLevel = regionLevel;
            SurfaceHeight = surfaceHeight;
            WaterLevel = waterLevel;
        }

        /// <summary>Weighted climate "field 3" (Alpha 0x5C4800) — the cold/warm axis.</summary>
        public float Temperature { get; }

        /// <summary>Weighted climate "field 4" (Alpha 0x5C4DD0) — the dry/wet axis.</summary>
        public float Humidity { get; }

        /// <summary>Share of flagged "rare" climate cells (Alpha 0x5EF040).</summary>
        public float Rareness { get; }

        /// <summary>Mountain mask (Alpha 0x5EFFA0) — 0 flat plains, 1 mountain ridge.</summary>
        public float Relief { get; }

        /// <summary>Region level (Alpha 0x5A4710): 1 at the world centre, grows outwards.</summary>
        public int RegionLevel { get; }

        /// <summary>Generated top solid voxel of the column (before player edits).</summary>
        public int SurfaceHeight { get; }

        public int WaterLevel { get; }

        public int HeightAboveWater => SurfaceHeight - WaterLevel;

        public bool IsUnderwater => SurfaceHeight < WaterLevel;

        public string Describe() =>
            $"t={Temperature.ToString("0.###", CultureInfo.InvariantCulture)} " +
            $"h={Humidity.ToString("0.###", CultureInfo.InvariantCulture)} " +
            $"rare={Rareness.ToString("0.###", CultureInfo.InvariantCulture)} " +
            $"relief={Relief.ToString("0.###", CultureInfo.InvariantCulture)} " +
            $"level={RegionLevel} surface={SurfaceHeight} water={WaterLevel}";

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            return value < 0f ? 0f : (value > 1f ? 1f : value);
        }
    }

    /// <summary>Where climate comes from. The world generator implements it; a test supplies a synthetic source.</summary>
    public interface IWorldClimateSource
    {
        ClimateAxes Sample(int x, int z);
    }

    /// <summary>
    /// Climate over the current verified generator. This is an adapter, not a reimplementation: temperature,
    /// humidity, rareness and relief are exactly the values the terrain and vegetation passes already use.
    /// </summary>
    public sealed class AlphaWorldClimateSource : IWorldClimateSource
    {
        private readonly AlphaTerrainGenerator _terrain;

        public AlphaWorldClimateSource(AlphaTerrainGenerator terrain)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        }

        public ClimateAxes Sample(int x, int z)
        {
            if (!WorldLayout.IsInsideVoxels(x, z))
            {
                return new ClimateAxes(0f, 0f, 0f, 0f, 0, 0, _terrain.WaterLevel);
            }

            int regionLevel = AlphaTerrainGenerator.Level(x >> 14, z >> 14);
            int surface = _terrain.Column(x, z).Surface;
            return new ClimateAxes(_terrain.Temperature(x, z), _terrain.Humidity(x, z),
                _terrain.RareCellTerm(x, z), _terrain.Mountain(x, z), regionLevel, surface, _terrain.WaterLevel);
        }
    }

    /// <summary>Small pure helpers for climate queries (bands, similarity) used by biomes and debug tools.</summary>
    public static class ClimateMath
    {
        public static float Band(float value, float min, float max)
        {
            if (max <= min) return value >= min ? 1f : 0f;
            float t = (value - min) / (max - min);
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        /// <summary>Normalised distance to the centre of a climate box (0 = centre, 1 = edge, &gt;1 = outside).</summary>
        public static float DistanceToEnvelope(ClimateAxes axes, float minTemperature, float maxTemperature,
            float minHumidity, float maxHumidity)
        {
            float cT = (minTemperature + maxTemperature) * 0.5f;
            float cH = (minHumidity + maxHumidity) * 0.5f;
            float rT = Math.Max(1e-4f, (maxTemperature - minTemperature) * 0.5f);
            float rH = Math.Max(1e-4f, (maxHumidity - minHumidity) * 0.5f);
            float dt = (axes.Temperature - cT) / rT;
            float dh = (axes.Humidity - cH) / rH;
            return (float)Math.Sqrt(dt * dt + dh * dh);
        }

        public static List<string> Validate()
        {
            var problems = new List<string>();
            if (Math.Abs(Band(0.5f, 0f, 1f) - 0.5f) > 1e-5f) problems.Add("CLIMATE_BAND_MID");
            if (Math.Abs(Band(-1f, 0f, 1f)) > 1e-6f) problems.Add("CLIMATE_BAND_LOW");
            if (Math.Abs(Band(2f, 0f, 1f) - 1f) > 1e-6f) problems.Add("CLIMATE_BAND_HIGH");
            var inside = new ClimateAxes(0.5f, 0.5f, 0f, 0f, 1, 10, 0);
            if (DistanceToEnvelope(inside, 0.4f, 0.6f, 0.4f, 0.6f) > 1e-5f) problems.Add("CLIMATE_ENVELOPE_CENTRE");
            var axes = new ClimateAxes(2f, -3f, float.NaN, 0f, 1, -2, 0);
            if (axes.Temperature != 1f || axes.Humidity != 0f || axes.Rareness != 0f) problems.Add("CLIMATE_AXES_CLAMP");
            if (axes.HeightAboveWater != -2 || !axes.IsUnderwater) problems.Add("CLIMATE_AXES_WATER");
            return problems;
        }
    }
}
