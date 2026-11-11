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
