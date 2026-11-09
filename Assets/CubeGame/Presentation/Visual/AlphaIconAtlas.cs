// CubeGame.Presentation.Visual — runtime loader for the item icon atlas (D86 M0-08A).
//
// The editor writes AlphaOutput/icons/atlas.png + atlas.json (origin bottom-left, 8 columns, 64px cells).
// Claude's UI can consume that atlas through this loader instead of re-implementing the layout. The atlas
// is a local artifact (not in Git); when it is absent TryLoad returns false with a reason.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CubeGame.Presentation.Visual
{
    public sealed class AlphaIconAtlas
    {
        public Texture2D Texture { get; private set; }
        public IReadOnlyDictionary<string, Rect> Rects => _rects;
        public string SourceDirectory { get; private set; }

        readonly Dictionary<string, Rect> _rects = new Dictionary<string, Rect>();
        readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        static readonly Regex RectPattern = new Regex(
            "\"([^\"]+)\"\\s*:\\s*\\{\\s*\"x\"\\s*:\\s*(-?\\d+)\\s*,\\s*\"y\"\\s*:\\s*(-?\\d+)\\s*,\\s*\"w\"\\s*:\\s*(\\d+)\\s*,\\s*\"h\"\\s*:\\s*(\\d+)\\s*\\}",
            RegexOptions.Compiled);

        AlphaIconAtlas(Texture2D texture, string directory) { Texture = texture; SourceDirectory = directory; }

        public bool TryGetRect(string itemName, out Rect rect) => _rects.TryGetValue(itemName, out rect);

        public Sprite TryGetSprite(string itemName)
        {
            if (_sprites.TryGetValue(itemName, out var cached)) return cached;
            if (!_rects.TryGetValue(itemName, out var rect)) return null;
            var sprite = Sprite.Create(Texture, rect, new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "alpha_icon_" + itemName;
            _sprites[itemName] = sprite;
            return sprite;
        }

        public static bool TryLoad(string atlasDirectory, out AlphaIconAtlas atlas, List<string> problems)
        {
            atlas = null;
            var png = Path.Combine(atlasDirectory, "atlas.png");
            var json = Path.Combine(atlasDirectory, "atlas.json");
            if (!File.Exists(png) || !File.Exists(json))
            {
                problems?.Add("ICON_ATLAS_MISSING " + atlasDirectory);
                return false;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(png)))
            {
                problems?.Add("ICON_ATLAS_PNG_INVALID");
                UnityEngine.Object.DestroyImmediate(texture);
                return false;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.name = "alpha_item_icon_atlas";
            var result = new AlphaIconAtlas(texture, atlasDirectory);
            foreach (Match match in RectPattern.Matches(File.ReadAllText(json)))
            {
                var name = match.Groups[1].Value;
                float x = int.Parse(match.Groups[2].Value);
                float y = int.Parse(match.Groups[3].Value);
                float w = int.Parse(match.Groups[4].Value);
                float h = int.Parse(match.Groups[5].Value);
                if (x < 0 || y < 0 || x + w > texture.width || y + h > texture.height)
                {
                    problems?.Add($"ICON_ATLAS_RECT_OUTSIDE {name}");
                    continue;
                }
                result._rects[name] = new Rect(x, y, w, h);
            }
            if (result._rects.Count == 0)
            {
                problems?.Add("ICON_ATLAS_EMPTY");
                UnityEngine.Object.DestroyImmediate(texture);
                return false;
            }
            atlas = result;
            return true;
        }

        public static string DefaultDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "AlphaOutput", "icons"));

        public static List<string> Validate(AlphaIconAtlas atlas)
        {
            var p = new List<string>();
            if (atlas == null || atlas.Texture == null) { p.Add("ATLAS_NULL"); return p; }
            if (atlas.Rects.Count < 40) p.Add($"ATLAS_RECTS {atlas.Rects.Count} < 40");
            foreach (var pair in atlas.Rects)
                if (pair.Value.width <= 0 || pair.Value.height <= 0) p.Add("ATLAS_RECT " + pair.Key);
            return p;
        }
    }
}
