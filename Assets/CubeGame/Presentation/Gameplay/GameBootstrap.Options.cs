using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CubeGame.Features.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Options screen of Alpha (<c>options.cfg</c>, OptionsWidget 0x4CF3C0; labels taken from the UTF-16 string
    /// table of Cube.exe: Mode / Resolution / Anti-aliasing / Render Distance / Sound FX Volume / Music Volume /
    /// Camera Speed / Camera Smoothness / Invert Y Axis / FPS Limit / Language). Eleven rows, each a left/right
    /// carousel, plus Apply / OK / Cancel (the three connection targets of the widget constructor).
    ///
    /// Music and language still require their runtime consumers.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private const string OptionsFileName = "options.cfg";

        private AlphaOptions _options = AlphaOptions.Default;
        private AlphaOptions _optionsDraft;
        private bool _optionsLoaded;

        public AlphaOptions Options => _options;

        private string OptionsPath =>
            Path.Combine(string.IsNullOrEmpty(_saveDirectory) ? SaveRootDirectory() : _saveDirectory, OptionsFileName);

        private static string SaveRootDirectory() =>
            Path.Combine(Application.persistentDataPath, "CubeGame", "saves");

        private void LoadOptions()
        {
            if (_optionsLoaded) return;
            _optionsLoaded = true;
            try
            {
                string path = OptionsPath;
                if (File.Exists(path))
                {
                    _options = AlphaOptions.Parse(File.ReadAllText(path));
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Options] {exception.Message}");
            }

            _optionsDraft = _options.Clone();
            ApplyOptions(_options);
        }

        private void SaveOptions()
        {
            try
            {
                string path = OptionsPath;
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, _options.Serialize());
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Options] write failed: {exception.Message}");
            }
        }

        public void ApplyOptionDraft()
        {
            if (_optionsDraft == null) return;
            _options = _optionsDraft.Clone();
            ApplyOptions(_options);
            SaveOptions();
        }

        private void CancelOptions()
        {
            _optionsDraft = _options.Clone();
            CloseOptions();
        }

        private void CloseOptions()
        {
            if (_session.State == SessionState.InGame)
            {
                _overlay = GameOverlay.None;
            }
            else
            {
                _startupScreen = StartupScreen.Start;
            }

            RequestRedraw();
            SetGameplayInput(true);
        }

        private void ApplyOptions(AlphaOptions options)
        {
            // Mode + Resolution: Alpha stores the pair in options.cfg; Unity applies them directly.
            if (options.ResolutionX > 0 && options.ResolutionY > 0)
            {
                if (Screen.width != options.ResolutionX || Screen.height != options.ResolutionY ||
                    Screen.fullScreen != (options.Fullscreen != 0))
                {
                    Screen.SetResolution(options.ResolutionX, options.ResolutionY, options.Fullscreen != 0);
                }
            }
            else
            {
                Screen.fullScreen = options.Fullscreen != 0;
            }

            QualitySettings.antiAliasing = NearestAntiAliasing(options.AntiAliasing);
            Application.targetFrameRate = options.MinTimeStep > 0
                ? Mathf.Clamp(1000 / options.MinTimeStep, 1, 1000)
                : -1;

            if (_audio != null) _audio.Volume = Mathf.Clamp01(options.SoundVolume / 100f);
            _worldView?.SetRenderDistance(options.RenderDistance);
            if (_orbitCamera != null)
            {
                _orbitCamera.SpeedScale = options.CameraSpeed <= 0 ? 0f : options.CameraSpeed / 50f;
                _orbitCamera.Smoothness = Mathf.Clamp01(options.CameraSmoothness / 100f);
                _orbitCamera.InvertY = options.InvertY != 0;
            }

            Debug.Log($"[Options] applied mode={(options.Fullscreen != 0 ? "Fullscreen" : "Windowed")} " +
                      $"{options.ResolutionX}x{options.ResolutionY} aa={QualitySettings.antiAliasing} fps={Application.targetFrameRate} " +
                      $"sound={options.SoundVolume} music={options.MusicVolume} camera={options.CameraSpeed}/{options.CameraSmoothness} " +
                      $"invertY={options.InvertY} renderDistance={options.RenderDistance} language={options.Language} " +
                      $"minTimeStep={options.MinTimeStep}");
        }

        private static int NearestAntiAliasing(int requested)
        {
            int[] levels = { 0, 2, 4, 8 };
            int best = 0;
            int distance = int.MaxValue;
            foreach (int level in levels)
            {
                int delta = Math.Abs(level - requested);
                if (delta < distance)
                {
                    distance = delta;
                    best = level;
                }
            }

            return best;
        }

        // ----- screen -----

        private void BuildOptionsScreen(VisualElement panel, VisualElement footer, bool inGame)
        {
            LoadOptions();
            if (_optionsDraft == null) _optionsDraft = _options.Clone();
            Text(panel, "Options", "screen-title", "heading");
            _statusLabel = Text(panel, "", "options-status", "muted");

            AddOptionCarousel(panel, "Mode", "option-mode",
                new List<string> { "Windowed", "Fullscreen" }, _optionsDraft.Fullscreen == 0 ? 0 : 1,
                index => _optionsDraft.Fullscreen = index);

            List<(int Width, int Height)> modes = ScreenModes();
            int mode = modes.FindIndex(m => m.Width == _optionsDraft.ResolutionX && m.Height == _optionsDraft.ResolutionY);
            if (mode < 0) mode = 0;
            AddOptionCarousel(panel, "Resolution", "option-resolution", ModeNames(modes), mode, index =>
            {
                _optionsDraft.ResolutionX = modes[index].Width;
                _optionsDraft.ResolutionY = modes[index].Height;
            });

            AddOptionCarousel(panel, "Anti-aliasing", "option-antialiasing",
                new List<string> { "Disabled", "2x", "4x", "8x" }, AntiAliasingIndex(_optionsDraft.AntiAliasing),
                index => _optionsDraft.AntiAliasing = index == 0 ? 0 : 1 << (index + 1));

            AddOptionCarousel(panel, "Render Distance", "option-render-distance", Numbers(10, 200, 10),
                IndexOf(_optionsDraft.RenderDistance, 10, 10, 19), index => _optionsDraft.RenderDistance = 10 + index * 10);

            AddOptionCarousel(panel, "Sound FX Volume", "option-sound", Numbers(0, 100, 5),
                IndexOf(_optionsDraft.SoundVolume, 0, 5, 20), index => _optionsDraft.SoundVolume = index * 5);

            AddOptionCarousel(panel, "Music Volume", "option-music", Numbers(0, 100, 5),
                IndexOf(_optionsDraft.MusicVolume, 0, 5, 20), index => _optionsDraft.MusicVolume = index * 5);

            AddOptionCarousel(panel, "Camera Speed", "option-camera-speed", Numbers(0, 100, 5),
                IndexOf(_optionsDraft.CameraSpeed, 0, 5, 20), index => _optionsDraft.CameraSpeed = index * 5);

            AddOptionCarousel(panel, "Camera Smoothness", "option-camera-smoothness", Numbers(0, 100, 5),
                IndexOf(_optionsDraft.CameraSmoothness, 0, 5, 20), index => _optionsDraft.CameraSmoothness = index * 5);

            AddOptionCarousel(panel, "Invert Y Axis", "option-invert-y", new List<string> { "Off", "On" },
                _optionsDraft.InvertY == 0 ? 0 : 1, index => _optionsDraft.InvertY = index,
                new List<string> { "Off", "On" });

            AddOptionCarousel(panel, "FPS Limit", "option-fps", Numbers(1, 24),
                Mathf.Clamp(_optionsDraft.MinTimeStep, 1, 24) - 1, index => _optionsDraft.MinTimeStep = index + 1);

            AddOptionCarousel(panel, "Language", "option-language", Numbers(0, 15),
                Mathf.Clamp(_optionsDraft.Language, 0, 15), index => _optionsDraft.Language = index);

            var buttons = footer != null ? Row(footer, "options-buttons") : Row(panel, "options-buttons");
            ButtonOf(buttons, "Apply", "options-apply", () =>
            {
                ApplyOptionDraft();
                Report(true, "options applied");
            });
            ButtonOf(buttons, "OK", "options-ok", () =>
            {
                ApplyOptionDraft();
                CloseOptions();
            });
            ButtonOf(buttons, "Cancel", "options-cancel", CancelOptions);
        }

        private static List<(int Width, int Height)> ScreenModes()
        {
            var modes = new List<(int, int)>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                if (resolution.width <= 0 || resolution.height <= 0) continue;
                var mode = (resolution.width, resolution.height);
                if (!modes.Contains(mode)) modes.Add(mode);
            }

            if (modes.Count == 0) modes.Add((Screen.width, Screen.height));
            return modes;
        }

        private static List<string> ModeNames(List<(int Width, int Height)> modes)
        {
            var names = new List<string>(modes.Count);
            foreach ((int width, int height) in modes) names.Add($"{width} x {height}");
            return names;
        }

        private static int AntiAliasingIndex(int value)
        {
            int[] levels = { 0, 2, 4, 8 };
            int best = 0;
            int distance = int.MaxValue;
            for (int i = 0; i < levels.Length; i++)
            {
                int delta = Math.Abs(levels[i] - value);
                if (delta < distance)
                {
                    distance = delta;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Values first..last with the given step, as editor labels.</summary>
        private static List<string> Numbers(int first, int last, int step = 1)
        {
            var values = new List<string>();
            for (int value = first; value <= last; value += step)
            {
                values.Add(value.ToString(CultureInfo.InvariantCulture));
            }

            return values;
        }

        private static int IndexOf(int value, int first, int step, int count)
        {
            int index = step == 0 ? 0 : (value - first) / step;
            return Mathf.Clamp(index, 0, count);
        }

        private void AddOptionCarousel(VisualElement panel, string label, string name, List<string> values, int index,
            Action<int> changed, List<string> labels = null)
        {
            index = Mathf.Clamp(index, 0, Math.Max(0, values.Count - 1));
            labels ??= values;
            var row = Row(panel, "option-row");
            Text(row, label, name + "-label", "option-label");
            ButtonOf(row, "<", name + "-prev", () =>
            {
                changed((index - 1 + values.Count) % values.Count);
                RequestRedraw();
                _audio.Play("menu-select.wav");
            });
            Text(row, labels[Mathf.Clamp(index, 0, labels.Count - 1)], name, "option-value");
            ButtonOf(row, ">", name + "-next", () =>
            {
                changed((index + 1) % values.Count);
                RequestRedraw();
                _audio.Play("menu-select.wav");
            });
        }
    }
}
