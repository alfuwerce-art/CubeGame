// CubeGame.Gameplay.Streaming — G2-I: samples the real runtime into the world performance recorder.
//
// The recorder itself is engine-free (Features/World/Foundation); this probe is the only place that reads Unity time,
// GC and memory. It logs a fixed summary every few seconds and writes one JSON file per session so two runs of the
// same build can be compared without a profiler:
//
//     CubeGame.exe -cubeWorldMetrics D:\...\metrics.json
//
// Metrics are diagnostics, not gameplay: a failure to write the file is logged once and never breaks the game.
using System;
using System.Globalization;
using System.IO;
using CubeGame.Features.World.Foundation;
using UnityEngine;

namespace CubeGame.Gameplay.Streaming
{
    public sealed class WorldMetricsProbe : MonoBehaviour
    {
        /// <summary>Seconds between log lines and count samples.</summary>
        public float SampleInterval = 5f;

        private WorldPerformanceRecorder _recorder;
        private Func<WorldBudgetMeasurement> _measurement;
        private Func<WorldDebugSnapshot> _snapshot;
        private string _outputPath;
        private float _nextSample;
        private bool _writeFailed;
        private int _lastBudgetLogSecond = -1;
        private int _frames;
        private long _allocatedBytes;
        private string _lastWorldSummary = string.Empty;

        public WorldPerformanceSnapshot LastSnapshot { get; private set; }

        public string OutputPath => _outputPath ?? string.Empty;

        public void Initialise(WorldPerformanceRecorder recorder, Func<WorldBudgetMeasurement> measurement,
            Func<WorldDebugSnapshot> snapshot, string outputPath)
        {
            _recorder = recorder;
            _measurement = measurement;
            _snapshot = snapshot;
            _outputPath = outputPath ?? string.Empty;
            _nextSample = 0f;
            _writeFailed = false;
        }

        private void Update()
        {
            if (_recorder == null) return;

            double now = Time.realtimeSinceStartupAsDouble;
            // GC.GetTotalMemory is a runtime call: sample it every 30 frames, not on every frame.
            if (++_frames % 30 == 0) _allocatedBytes = GC.GetTotalMemory(false);
            _recorder.RecordFrame(now, Time.unscaledDeltaTime, _allocatedBytes);
            if (Time.unscaledTime < _nextSample) return;
            _nextSample = Time.unscaledTime + Math.Max(0.5f, SampleInterval);

            WorldBudgetMeasurement measurement = default;
            if (_measurement != null)
            {
                try
                {
                    measurement = _measurement();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[WorldMetrics] measurement failed: {exception.Message}");
                }
            }

            _recorder.RecordCounts(measurement.Vertices, measurement.LoadedChunks, measurement.CachedZones,
                measurement.GeneratedZones, measurement.Plants, measurement.Npcs, measurement.WorldObjects, 0);
            _recorder.RecordMemory(GC.GetTotalMemory(false));

            LastSnapshot = _recorder.Snapshot();
            int second = (int)Time.unscaledTime;
            if (second / Math.Max(1, (int)SampleInterval) == _lastBudgetLogSecond) return;
            _lastBudgetLogSecond = second / Math.Max(1, (int)SampleInterval);
            Debug.Log($"[WorldMetrics] {LastSnapshot.Describe()}");
            string summary = _snapshot?.Invoke()?.Summary();
            if (!string.IsNullOrEmpty(summary)) Debug.Log($"[WorldMetrics] world {summary}");
        }

        private void OnDestroy() => Write();

        private void OnApplicationQuit() => Write();

        /// <summary>Writes the session measurement (and the last debug summary) next to the player's logs.</summary>
        public void Write()
        {
            if (_recorder == null || string.IsNullOrEmpty(_outputPath) || _writeFailed) return;
            try
            {
                WorldPerformanceSnapshot snapshot = _recorder.Snapshot();
                LastSnapshot = snapshot;
                string directory = Path.GetDirectoryName(_outputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string text = snapshot.ToJson();
                // The probe is destroyed with the world root: keep the last summary of a real world, so the write from
                // OnDestroy does not replace it with the "no world" placeholder.
                WorldDebugSnapshot debug = _snapshot?.Invoke();
                if (debug != null && debug.HasWorld) _lastWorldSummary = debug.Summary();
                if (!string.IsNullOrEmpty(_lastWorldSummary))
                {
                    text = text.Substring(0, text.Length - 1) + ",\"world\":\"" +
                           _lastWorldSummary.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
                }

                File.WriteAllText(_outputPath, text);
                Debug.Log($"[WorldMetrics] wrote {_outputPath} ({snapshot.Describe()})");
            }
            catch (Exception exception)
            {
                _writeFailed = true;
                Debug.LogWarning($"[WorldMetrics] cannot write '{_outputPath}': {exception.Message}");
            }
        }

        public string Describe()
        {
            WorldPerformanceSnapshot snapshot = LastSnapshot ?? _recorder?.Snapshot();
            return snapshot == null
                ? "metrics: idle"
                : string.Format(CultureInfo.InvariantCulture, "metrics {0}", snapshot.Describe());
        }
    }
}
