using System;
using System.Globalization;
using System.Text;

namespace CubeGame.Features.Session
{
    /// <summary>
    /// Alpha <c>options.cfg</c> (12 integer settings, one "key value" line each, in this order). Defaults come from
    /// <c>OptionsWidget_initDefaults</c> 0x4CE660 and the installed Alpha file uses the same keys:
    /// fullscreen, resolutionX, resolutionY, antiAliasing, renderDistance, soundVolume, musicVolume, cameraSpeed,
    /// cameraSmoothness, language, invertY, minTimeStep.
    /// </summary>
    public sealed class AlphaOptions
    {
        public int Fullscreen;
        public int ResolutionX = 1920;
        public int ResolutionY = 1080;
        public int AntiAliasing;
        public int RenderDistance = 50;
        public int SoundVolume = 100;
        public int MusicVolume = 100;
        public int CameraSpeed = 50;
        public int CameraSmoothness = 80;
        public int Language;
        public int InvertY;
        public int MinTimeStep = 9;

        public static AlphaOptions Default => new AlphaOptions();

        public string Serialize()
        {
            var text = new StringBuilder();
            Line("fullscreen", Fullscreen);
            Line("resolutionX", ResolutionX);
            Line("resolutionY", ResolutionY);
            Line("antiAliasing", AntiAliasing);
            Line("renderDistance", RenderDistance);
            Line("soundVolume", SoundVolume);
            Line("musicVolume", MusicVolume);
            Line("cameraSpeed", CameraSpeed);
            Line("cameraSmoothness", CameraSmoothness);
            Line("language", Language);
            Line("invertY", InvertY);
            Line("minTimeStep", MinTimeStep);
            return text.ToString();

            void Line(string key, int value) =>
                text.Append(key).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        /// <summary>Unknown keys are ignored and missing keys keep their Alpha default, like the original parser.</summary>
        public static AlphaOptions Parse(string text)
        {
            var options = Default;
            if (string.IsNullOrEmpty(text)) return options;
            foreach (string raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                int split = line.IndexOf(' ');
                string key = split < 0 ? line : line.Substring(0, split);
                string value = split < 0 ? string.Empty : line.Substring(split + 1).Trim();
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;
                switch (key)
                {
                    case "fullscreen": options.Fullscreen = number; break;
                    case "resolutionX": options.ResolutionX = number; break;
                    case "resolutionY": options.ResolutionY = number; break;
                    case "antiAliasing": options.AntiAliasing = number; break;
                    case "renderDistance": options.RenderDistance = number; break;
                    case "soundVolume": options.SoundVolume = number; break;
                    case "musicVolume": options.MusicVolume = number; break;
                    case "cameraSpeed": options.CameraSpeed = number; break;
                    case "cameraSmoothness": options.CameraSmoothness = number; break;
                    case "language": options.Language = number; break;
                    case "invertY": options.InvertY = number; break;
                    case "minTimeStep": options.MinTimeStep = number; break;
                }
            }

            return options;
        }

        public AlphaOptions Clone() => new AlphaOptions
        {
            Fullscreen = Fullscreen,
            ResolutionX = ResolutionX,
            ResolutionY = ResolutionY,
            AntiAliasing = AntiAliasing,
            RenderDistance = RenderDistance,
            SoundVolume = SoundVolume,
            MusicVolume = MusicVolume,
            CameraSpeed = CameraSpeed,
            CameraSmoothness = CameraSmoothness,
            Language = Language,
            InvertY = InvertY,
            MinTimeStep = MinTimeStep,
        };
    }
}
