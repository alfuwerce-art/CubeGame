// CubeGame.Gameplay.Streaming — G2-H: the world debug overlay (dev tool, not a player UI).
//
// It draws nothing but a text box: seed, coordinates (chunk/zone/region), biome and climate, terrain height, streaming
// state, budget status, distance band, performance, nearby structure candidates and spawns. The facts come from
// WorldDebugSnapshot (engine-free), so the same text can be asserted in a test or written to a Player log.
using System;
using CubeGame.Features.World.Foundation;
using UnityEngine;

namespace CubeGame.Gameplay.Streaming
{
    public sealed class WorldDebugOverlay : MonoBehaviour
    {
        /// <summary>F9 toggles the overlay; the runtime also enables it from the command line.</summary>
        public KeyCode ToggleKey = KeyCode.F9;

        private Func<WorldDebugSnapshot> _provider;
        private bool _visible;
        private string _cachedText = WorldDebugSnapshot.UnavailableLine;
        private float _nextRefresh;
        private GUIStyle _style;

        /// <summary>Overlay width/height in pixels per line — deliberately cramped and monospaced.</summary>
        public const int Width = 700;
        public const int LineHeight = 16;

        public bool Visible => _visible;

        public string CachedText => _cachedText;

        public void Initialise(Func<WorldDebugSnapshot> provider, bool visible)
        {
            _provider = provider;
            _visible = visible;
            _style = null;
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible) Refresh();
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                _visible = !_visible;
                Refresh();
            }

            if (!_visible) return;

            // 10 Hz is enough for monitoring and keeps the overlay from allocating every frame.
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                WorldDebugSnapshot snapshot = _provider?.Invoke();
                _cachedText = snapshot != null ? snapshot.Text : WorldDebugSnapshot.UnavailableLine;
            }
            catch (Exception exception)
            {
                _cachedText = "world debug failed: " + exception.Message;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;
            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = false,
                wordWrap = false,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.92f, 1f, 0.92f) },
            };

            string[] lines = _cachedText.Split('\n');
            int height = Math.Max(1, lines.Length) * LineHeight + 10;
            var rect = new Rect(8f, 8f, Width, height);
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            var textRect = new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 8f);
            GUI.Label(textRect, _cachedText, _style);
        }
    }
}
