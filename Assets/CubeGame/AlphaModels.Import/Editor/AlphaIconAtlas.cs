using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CubeGame.AlphaModels;
using CubeGame.AlphaTransfer;
using CubeGame.Content;
using CubeGame.Features.AI;
using CubeGame.Features.Creatures;
using CubeGame.Presentation.Visual;
using UnityEditor;
using UnityEngine;

namespace CubeGame.AlphaModels.Import
{
    public static partial class AlphaModelsImport
    {
        // ---------------------------------------------------------------- icon atlas
        [MenuItem("CubeGame/Alpha Models/Build Item Icon Atlas")]
        public static void BuildIconAtlas()
        {
            var problems = new List<string>();
            var source = AlphaContentSource.Resolve();
            var items = source.Root != null ? Path.Combine(source.Root, "items") : null;
            if (items == null || !Directory.Exists(items))
            {
                Write("icons_atlas.txt", "ICON_ATLAS_RESULT=NOT_CONFIGURED item cache missing");
                return;
            }
            var files = new List<string>(Directory.GetFiles(items, "*.cubdec"));
            files.Sort(StringComparer.OrdinalIgnoreCase);
            const int cell = 64, cols = 8;
            int rows = Mathf.CeilToInt(files.Count / (float)cols);
            var atlas = new Texture2D(cols * cell, Mathf.Max(1, rows) * cell, TextureFormat.RGBA32, false);
            var clear = new Color32[atlas.width * atlas.height];
            atlas.SetPixels32(clear);
            var rects = new StringBuilder();
            int placed = 0;
            for (int i = 0; i < files.Count; i++)
            {
                var name = Path.GetFileNameWithoutExtension(files[i]);
                var sprite = AlphaItemIconRenderer.RenderFromFile(files[i], cell, problems);
                if (sprite == null) continue;
                var pixels = sprite.texture.GetPixels32();
                int x = (i % cols) * cell;
                int y = atlas.height - ((i / cols) + 1) * cell;   // Unity origin is bottom-left
                atlas.SetPixels32(x, y, cell, cell, pixels);
                if (placed > 0) rects.Append(',');
                rects.Append('"').Append(name).Append("\":{\"x\":").Append(x).Append(",\"y\":").Append(y)
                     .Append(",\"w\":").Append(cell).Append(",\"h\":").Append(cell).Append('}');
                UnityEngine.Object.DestroyImmediate(sprite.texture);
                UnityEngine.Object.DestroyImmediate(sprite);
                placed++;
            }
            atlas.Apply(false, false);
            var outDir = Path.Combine(OutDir, "icons");
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, "atlas.png"), atlas.EncodeToPNG());
            string json = "{\"format\":\"cubegame.item_icon_atlas/1\",\"origin\":\"bottom-left\",\"cell\":" + cell +
                          ",\"columns\":" + cols + ",\"size\":[" + atlas.width + "," + atlas.height + "],\"icons\":{" +
                          rects + "}}";
            File.WriteAllText(Path.Combine(outDir, "atlas.json"), json);
            UnityEngine.Object.DestroyImmediate(atlas);
            var summary = new StringBuilder();
            summary.Append($"ICON_ATLAS_RESULT={(problems.Count == 0 && placed > 0 ? "PASS" : "FAIL")} icons={placed} " +
                           $"columns={cols} size={cols * cell}x{Mathf.Max(1, rows) * cell} problems={problems.Count}");
            foreach (var p in problems) summary.Append('\n').Append(p);
            Write("icons_atlas.txt", summary.ToString());
        }

        public static void RunBuildIconAtlas()
        {
            int exit = 1;
            try { BuildIconAtlas(); exit = 0; }
            catch (Exception e) { Write("icons_atlas.txt", "ICON_ATLAS_RESULT=FAIL exception=" + e); }
            EditorApplication.Exit(exit);
        }
    }
}
