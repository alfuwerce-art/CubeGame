// CubeGame.Gameplay — G2 smoke automation: leave the game deterministically after N seconds in the world.
//
// Until now a batchmode Player run had to be killed or needed a screenshot capture to exit, which made "did the
// world save?" unverifiable (a kill skips the checkpoint). `-cubeSmokeSeconds <n>` runs the real game for n seconds,
// logs the world foundation summary, optionally saves the checkpoint (`-cubeSaveCheckpoint`) and quits through the
// normal quit path. G2-I metrics are written by the metrics probe on destroy.
using CubeGame.Features.Session;
using CubeGame.Features.World.Foundation;
using UnityEngine;

namespace CubeGame.Gameplay
{
    public sealed partial class GameBootstrap
    {
        private float _smokeSeconds;
        private float _smokeElapsed;
        private bool _smokeDone;

        /// <summary>Seconds to play before saving and quitting; 0 = disabled (normal interactive run).</summary>
        public float SmokeSeconds => _smokeSeconds;

        private void UpdateSmoke(float deltaSeconds)
        {
            if (_smokeSeconds <= 0f || _smokeDone) return;
            if (_session == null || _session.State != SessionState.InGame || _worldView == null) return;

            _smokeElapsed += Mathf.Max(0f, deltaSeconds);
            if (_smokeElapsed < _smokeSeconds) return;

            _smokeDone = true;
            WorldDebugSnapshot snapshot = WorldFoundationSnapshot();
            WorldPerformanceSnapshot performance = _worldMetrics?.Snapshot();
            Debug.Log($"[Smoke] SMOKE_SECONDS={_smokeSeconds:0.#} elapsed={_smokeElapsed:0.#} state={_session.State} " +
                      $"world={snapshot.Summary()}");
            if (performance != null) Debug.Log($"[Smoke] metrics {performance.Describe()}");
            if (_worldMetricsProbe != null)
            {
                _worldMetricsProbe.Write();
                Debug.Log($"[Smoke] metrics file {_worldMetricsProbe.OutputPath}");
            }

            _saveCheckpoint = true;   // the checkpoint is written by QuitGame/Quit
            Debug.Log("[Smoke] quitting");
            QuitGame();
        }
    }
}
