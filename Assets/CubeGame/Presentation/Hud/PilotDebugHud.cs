using CubeGame.Core.Generation;
using CubeGame.Presentation.Rendering;
using UnityEngine;

namespace CubeGame.Presentation.Hud
{
    /// <summary>
    /// Minimal service HUD required by S01-PILOT P1.5: seed, player block coordinates, FPS.
    /// Deliberately plain (no game art, no Alpha UI).
    /// </summary>
    public sealed class PilotDebugHud : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private PilotWorldView worldView;
        [SerializeField] private uint seed;

        private float _smoothedDeltaTime = 0.016f;
        private GUIStyle _style;

        public void Configure(uint worldSeed, Transform playerTransform, PilotWorldView view)
        {
            seed = worldSeed;
            player = playerTransform;
            worldView = view;
        }

        private void Update()
        {
            _smoothedDeltaTime = Mathf.Lerp(_smoothedDeltaTime, Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
            }

            GUI.Box(new Rect(8f, 8f, 330f, 132f), GUIContent.none);
            GUILayout.BeginArea(new Rect(16f, 14f, 320f, 124f));
            GUILayout.Label($"CubeGame pilot | Unity {Application.unityVersion}", _style);
            GUILayout.Label($"seed: {seed}", _style);

            if (player != null)
            {
                Vector3 position = player.position;
                int blockX = Mathf.FloorToInt(position.x);
                int blockY = Mathf.FloorToInt(position.y);
                int blockZ = Mathf.FloorToInt(position.z);
                int chunkX = PilotHeightField.FloorDiv(blockX, PilotWorldLayout.ChunkSize);
                int chunkZ = PilotHeightField.FloorDiv(blockZ, PilotWorldLayout.ChunkSize);
                GUILayout.Label($"block: {blockX}, {blockY}, {blockZ}", _style);
                GUILayout.Label($"chunk: {chunkX}, {chunkZ}", _style);
            }

            float fps = 1f / _smoothedDeltaTime;
            GUILayout.Label($"fps: {fps:F1} ({_smoothedDeltaTime * 1000f:F1} ms)", _style);

            if (worldView != null)
            {
                GUILayout.Label($"chunks: {worldView.ChunkCount} | gen: {worldView.LastBuildMilliseconds:F1} ms", _style);
            }

            GUILayout.Label("WASD move, Shift sprint, Space jump, RMB look", _style);
            GUILayout.EndArea();
        }
    }
}
