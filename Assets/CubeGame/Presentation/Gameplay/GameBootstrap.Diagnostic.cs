using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CubeGame.Features.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Diagnostic-only UI map for the Windows Player acceptance run (<c>-cubeUiProbe &lt;file&gt;</c>): writes the
    /// on-screen centre of every named element so an external mouse driver can click the real controls, and reads
    /// the same names back to prove which screen the Player is showing. Nothing is written unless the flag is set.
    /// </summary>
    public sealed partial class GameBootstrap
    {
        private string _uiProbePath;
        private int _uiProbeFrame = -1;
        private string _captureDir;

        /// <summary>
        /// Presentation capture for the producer review (<c>-cubeCapture &lt;dir&gt;</c>): walks the startup screens by
        /// calling the very same handlers the widgets invoke and writes one PNG per screen. This is a diagnostic for
        /// visual validation only — it changes no state machine rule and is never active without the flag.
        /// </summary>
        private System.Collections.IEnumerator CaptureStartupScreens()
        {
            if (string.IsNullOrEmpty(_captureDir)) yield break;
            System.IO.Directory.CreateDirectory(_captureDir);
            yield return new WaitForSecondsRealtime(1.5f);

            yield return CaptureShot("01_start");
            ShowStartupScreen(StartupScreen.Options);
            yield return CaptureShot("02_options");
            ShowStartupScreen(StartupScreen.Start);
            yield return CaptureShot("03_start_again");

            ShowStartupScreen(StartupScreen.Characters);
            yield return CaptureShot("04_characters");
            BeginCharacterCreation();
            yield return CaptureShot("05_editor");
            ShowStartupScreen(StartupScreen.Characters);
            yield return CaptureShot("06_characters_again");

            if (_profiles.Characters.Count > 0)
            {
                SelectCharacter(_profiles.Characters[0].Id);
                yield return CaptureShot("07_worlds");
            }

            ShowStartupScreen(StartupScreen.CreateWorld);
            yield return CaptureShot("08_create_world");

            _status = "Opening the world";
            _enteringWorld = true;
            RequestRedraw();
            yield return CaptureShot("09_wait");
            _enteringWorld = false;
            ShowStartupScreen(StartupScreen.Start);
            yield return CaptureShot("10_start_after");
            Debug.Log("[Capture] CAPTURE_DONE");
        }

        private System.Collections.IEnumerator CaptureShot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_captureDir, name + ".png");
            try
            {
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[Capture] SHOT {name} {Screen.width}x{Screen.height}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Capture] {name}: {exception.Message}");
            }

            yield return null;
            yield return new WaitForSecondsRealtime(0.6f);
        }

        private void WriteUiProbe()
        {
            if (string.IsNullOrEmpty(_uiProbePath) || _interface == null) return;
            if (_uiProbeFrame >= 0 && Time.frameCount - _uiProbeFrame < 5) return;
            _uiProbeFrame = Time.frameCount;

            var text = new StringBuilder();
            text.Append("screen ").Append(Screen.width).Append(' ').Append(Screen.height).Append('\n');
            text.Append("panel ").Append(_interface.layout.width.ToString("0.##", CultureInfo.InvariantCulture))
                .Append(' ').Append(_interface.layout.height.ToString("0.##", CultureInfo.InvariantCulture)).Append('\n');
            text.Append("state ").Append(_session.State).Append(' ')
                .Append(_startupScreen).Append(' ').Append(_overlay).Append('\n');
            if (_session.Hero != null)
            {
                WorldPosition hero = _session.Hero.Position;
                text.Append("hero ").Append(hero.X.ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(hero.Y.ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(hero.Z.ToString("0.###", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(_session.Hero.Yaw.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
            }
            List<VisualElement> elements = _interface.Query<VisualElement>().ToList();
            foreach (VisualElement element in elements)
            {
                string name = element.name;
                if (string.IsNullOrEmpty(name) || element.worldBound.width < 1f || element.worldBound.height < 1f)
                {
                    continue;
                }

                Rect bound = element.worldBound;
                text.Append(name).Append(' ')
                    .Append((bound.center.x).ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                    .Append((bound.center.y).ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(bound.width.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(bound.height.ToString("0.##", CultureInfo.InvariantCulture)).Append('\n');
            }

            try
            {
                string directory = Path.GetDirectoryName(_uiProbePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                // A single small write: readers either see the previous map or the new one. The earlier
                // delete+move pair had a window in which the file did not exist at all.
                File.WriteAllText(_uiProbePath, text.ToString());
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[UiProbe] {exception.Message}");
            }
        }
    }
}
