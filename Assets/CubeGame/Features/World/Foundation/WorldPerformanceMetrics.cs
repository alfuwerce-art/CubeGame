// CubeGame.Features.World.Foundation — G2-I: reproducible performance measurement of the world.
//
// The G1-00 baseline gave single numbers (start-up 67–357 ms, 25 chunks, 12 582 912 vertices, 2.4 GB peak). That is
// not enough to plan G2-D/G9: we need per-session, per-chunk measurements that can be repeated on the same build and
// compared between builds. This recorder collects them without Unity APIs (so EditMode tests can feed it directly)
// and serialises a fixed, sorted JSON so two runs can be diffed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>Immutable measurement result of one session (or one test scenario).</summary>
    public sealed class WorldPerformanceSnapshot
    {
        public double SampleSeconds { get; internal set; }
        public int FrameSamples { get; internal set; }
        public double MeanFrameMs { get; internal set; }
        public double P95FrameMs { get; internal set; }
        public double MaxFrameMs { get; internal set; }
        public int FrameSpikeCount { get; internal set; }

        public int ChunksGenerated { get; internal set; }
        public double ChunksPerSecond { get; internal set; }
        public double ZoneGenerationMsTotal { get; internal set; }
        public double MeanZoneGenerationMs { get; internal set; }
        public double MeanChunkBuildMs { get; internal set; }
        public double MaxChunkBuildMs { get; internal set; }

        public int Vertices { get; internal set; }
        public int ActiveChunks { get; internal set; }
        public int CachedChunks { get; internal set; }
        public int GeneratedZones { get; internal set; }
        public int VegetationCount { get; internal set; }
        public int ActiveNpc { get; internal set; }
        public int WorldObjects { get; internal set; }
        public int Structures { get; internal set; }

        public long GcAllocatedBytes { get; internal set; }
        public long WorkingSetBytes { get; internal set; }

        public string Describe() =>
            $"frames={FrameSamples} mean={MeanFrameMs.ToString("0.###", CultureInfo.InvariantCulture)}ms " +
            $"p95={P95FrameMs.ToString("0.###", CultureInfo.InvariantCulture)}ms " +
            $"max={MaxFrameMs.ToString("0.###", CultureInfo.InvariantCulture)}ms spikes={FrameSpikeCount} | " +
            $"chunks={ChunksGenerated} rate={ChunksPerSecond.ToString("0.##", CultureInfo.InvariantCulture)}/s " +
            $"zoneMean={MeanZoneGenerationMs.ToString("0.#", CultureInfo.InvariantCulture)}ms " +
            $"chunkMean={MeanChunkBuildMs.ToString("0.###", CultureInfo.InvariantCulture)}ms " +
            $"chunkMax={MaxChunkBuildMs.ToString("0.###", CultureInfo.InvariantCulture)}ms | " +
            $"verts={Vertices} active={ActiveChunks} cached={CachedChunks} zones={GeneratedZones} " +
            $"plants={VegetationCount} npc={ActiveNpc} objects={WorldObjects} structures={Structures} | " +
            $"gc={GcAllocatedBytes}B ws={WorkingSetBytes}B";

        /// <summary>Stable JSON (fixed key order, invariant numbers) so two runs can be compared textually.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder(768);
            sb.Append('{');
            Append(sb, "sampleSeconds", SampleSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            Append(sb, "frameSamples", FrameSamples.ToString(CultureInfo.InvariantCulture));
            Append(sb, "meanFrameMs", MeanFrameMs.ToString("0.###", CultureInfo.InvariantCulture));
            Append(sb, "p95FrameMs", P95FrameMs.ToString("0.###", CultureInfo.InvariantCulture));
            Append(sb, "maxFrameMs", MaxFrameMs.ToString("0.###", CultureInfo.InvariantCulture));
            Append(sb, "frameSpikes", FrameSpikeCount.ToString(CultureInfo.InvariantCulture));
            Append(sb, "chunksGenerated", ChunksGenerated.ToString(CultureInfo.InvariantCulture));
            Append(sb, "chunksPerSecond", ChunksPerSecond.ToString("0.###", CultureInfo.InvariantCulture));
            Append(sb, "zoneGenerationMsTotal", ZoneGenerationMsTotal.ToString("0.###", CultureInfo.InvariantCulture));
            Append(sb, "meanZoneGenerationMs", MeanZoneGenerationMs.ToString("0.###", CultureInfo.InvariantCulture));
            Append(sb, "meanChunkBuildMs", MeanChunkBuildMs.ToString("0.###", CultureInfo.InvariantCulture));
            Append(sb, "maxChunkBuildMs", MaxChunkBuildMs.ToString("0.###", CultureInfo.InvariantCulture));
            Append(sb, "vertices", Vertices.ToString(CultureInfo.InvariantCulture));
            Append(sb, "activeChunks", ActiveChunks.ToString(CultureInfo.InvariantCulture));
            Append(sb, "cachedChunks", CachedChunks.ToString(CultureInfo.InvariantCulture));
            Append(sb, "generatedZones", GeneratedZones.ToString(CultureInfo.InvariantCulture));
            Append(sb, "vegetation", VegetationCount.ToString(CultureInfo.InvariantCulture));
            Append(sb, "activeNpc", ActiveNpc.ToString(CultureInfo.InvariantCulture));
            Append(sb, "worldObjects", WorldObjects.ToString(CultureInfo.InvariantCulture));
            Append(sb, "structures", Structures.ToString(CultureInfo.InvariantCulture));
            Append(sb, "gcAllocatedBytes", GcAllocatedBytes.ToString(CultureInfo.InvariantCulture));
            Append(sb, "workingSetBytes", WorkingSetBytes.ToString(CultureInfo.InvariantCulture));
            // Remove the trailing comma of the last Append call.
            if (sb[sb.Length - 1] == ',') sb.Length--;
            sb.Append('}');
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":").Append(value).Append(',');
        }
    }

    /// <summary>
    /// Collects world performance data. All entry points take the caller's clock, so the same code runs in a test,
    /// in the editor and in the Player, and the numbers are always wall-clock seconds of that caller.
    /// </summary>
    public sealed class WorldPerformanceRecorder
    {
        /// <summary>A frame longer than this multiple of the mean is counted as a spike.</summary>
        public const double SpikeFactor = 3.0;

        private const int FrameRingSize = 1024;

        private readonly double[] _frameMs = new double[FrameRingSize];
        private readonly double[] _sorted = new double[FrameRingSize];
        private int _frameCount;
        private int _frameCursor;

        private double _firstNow = double.NaN;
        private double _lastNow = double.NaN;

        private int _chunksGenerated;
        private double _chunkBuildMsTotal;
        private double _chunkBuildMsMax;

        private int _zonesGenerated;
        private double _zoneMsTotal;

        private int _vertices;
        private int _activeChunks;
        private int _cachedChunks;
        private int _generatedZones;
        private int _vegetation;
        private int _activeNpc;
        private int _worldObjects;
        private int _structures;
        private long _gcAllocated;
        private long _workingSet;

        public void Reset()
        {
            _frameCount = 0;
            _frameCursor = 0;
            _firstNow = double.NaN;
            _lastNow = double.NaN;
            _chunksGenerated = 0;
            _chunkBuildMsTotal = 0;
            _chunkBuildMsMax = 0;
            _zonesGenerated = 0;
            _zoneMsTotal = 0;
            _vertices = 0;
            _activeChunks = 0;
            _cachedChunks = 0;
            _generatedZones = 0;
            _vegetation = 0;
            _activeNpc = 0;
            _worldObjects = 0;
            _structures = 0;
            _gcAllocated = 0;
            _workingSet = 0;
        }

        private void Touch(double nowSeconds)
        {
            if (double.IsNaN(_firstNow) || nowSeconds < _firstNow) _firstNow = nowSeconds;
            if (double.IsNaN(_lastNow) || nowSeconds > _lastNow) _lastNow = nowSeconds;
        }

        public void RecordZoneGenerated(double nowSeconds, double milliseconds)
        {
            Touch(nowSeconds);
            _zonesGenerated++;
            _zoneMsTotal += Math.Max(0.0, milliseconds);
        }

        /// <summary>
        /// One finished chunk mesh. Vertices and plants are accepted for symmetry with the streamer's call site, but
        /// only absolute counters (<see cref="RecordCounts"/>) feed the snapshot: summing per-chunk numbers would
        /// double count chunks that were released again.
        /// </summary>
        public void RecordChunkBuilt(double nowSeconds, double milliseconds, int vertices, int plants)
        {
            Touch(nowSeconds);
            _chunksGenerated++;
            double ms = Math.Max(0.0, milliseconds);
            _chunkBuildMsTotal += ms;
            if (ms > _chunkBuildMsMax) _chunkBuildMsMax = ms;
            _ = vertices;
            _ = plants;
        }

        public void RecordFrame(double nowSeconds, double deltaSeconds, long gcAllocatedBytes)
        {
            Touch(nowSeconds);
            double ms = Math.Max(0.0, deltaSeconds) * 1000.0;
            _frameMs[_frameCursor] = ms;
            _frameCursor = (_frameCursor + 1) % FrameRingSize;
            if (_frameCount < FrameRingSize) _frameCount++;
            if (gcAllocatedBytes > 0) _gcAllocated = gcAllocatedBytes;
        }

        /// <summary>Absolute counters sampled from the runtime (vertices/chunks/plants/npc/…).</summary>
        public void RecordCounts(int vertices, int activeChunks, int cachedChunks, int generatedZones, int vegetation,
            int activeNpc, int worldObjects, int structures)
        {
            _vertices = Math.Max(0, vertices);
            _activeChunks = Math.Max(0, activeChunks);
            _cachedChunks = Math.Max(0, cachedChunks);
            _generatedZones = Math.Max(0, generatedZones);
            _vegetation = Math.Max(0, vegetation);
            _activeNpc = Math.Max(0, activeNpc);
            _worldObjects = Math.Max(0, worldObjects);
            _structures = Math.Max(0, structures);
        }

        public void RecordMemory(long workingSetBytes)
        {
            if (workingSetBytes > 0) _workingSet = workingSetBytes;
        }

        public WorldPerformanceSnapshot Snapshot()
        {
            var snapshot = new WorldPerformanceSnapshot
            {
                ChunksGenerated = _chunksGenerated,
                ZoneGenerationMsTotal = _zoneMsTotal,
                MeanZoneGenerationMs = _zonesGenerated == 0 ? 0.0 : _zoneMsTotal / _zonesGenerated,
                MeanChunkBuildMs = _chunksGenerated == 0 ? 0.0 : _chunkBuildMsTotal / _chunksGenerated,
                MaxChunkBuildMs = _chunkBuildMsMax,
                Vertices = _vertices,
                ActiveChunks = _activeChunks,
                CachedChunks = _cachedChunks,
                GeneratedZones = _generatedZones,
                VegetationCount = _vegetation,
                ActiveNpc = _activeNpc,
                WorldObjects = _worldObjects,
                Structures = _structures,
                GcAllocatedBytes = _gcAllocated,
                WorkingSetBytes = _workingSet,
                FrameSamples = _frameCount,
            };
            if (!double.IsNaN(_firstNow) && !double.IsNaN(_lastNow) && _lastNow > _firstNow)
            {
                snapshot.SampleSeconds = _lastNow - _firstNow;
                snapshot.ChunksPerSecond = _chunksGenerated / snapshot.SampleSeconds;
            }

            double mean = 0.0;
            if (_frameCount > 0)
            {
                Array.Copy(_frameMs, _sorted, _frameCount);
                double sum = 0.0;
                for (int i = 0; i < _frameCount; i++)
                {
                    sum += _sorted[i];
                    if (_sorted[i] > snapshot.MaxFrameMs) snapshot.MaxFrameMs = _sorted[i];
                }

                mean = sum / _frameCount;
                Array.Sort(_sorted, 0, _frameCount);
                int p95 = (int)Math.Ceiling(_frameCount * 0.95) - 1;
                snapshot.P95FrameMs = _sorted[Math.Max(0, Math.Min(_frameCount - 1, p95))];
                double threshold = Math.Max(mean * SpikeFactor, mean + 1.0);
                int spikes = 0;
                for (int i = 0; i < _frameCount; i++)
                {
                    if (_frameMs[i] > threshold) spikes++;
                }

                snapshot.FrameSpikeCount = spikes;
            }

            snapshot.MeanFrameMs = mean;
            return snapshot;
        }
    }
}
