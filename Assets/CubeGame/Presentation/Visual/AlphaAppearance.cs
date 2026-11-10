// CubeGame.Presentation.Visual — appearance and item icons (D86 M0-03C/M0-04B/M0-08A).
//
// Appearance follows Alpha's proven tint law (D61): vertex colours of the model are multiplied by the
// per-draw colour constant; hair uses hair_rgb/255, every other part white. Icons are rendered from
// the same Alpha geometry the world uses, so the UI cannot drift from the 3D look.
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using CubeGame.AlphaTransfer;
using CubeGame.Features.Creatures;
using UnityEngine;

namespace CubeGame.Presentation.Visual
{
    public static class AlphaAppearance
    {
        public const string HairTintLaw = "D61: hair vertex colours x hair_rgb/255 (Alpha uniform30); other parts stay white";

        /// <summary>Set the hair colour exactly as Alpha uploads it (0..1 per channel).</summary>
        public static bool TrySetHairColour(AlphaRig rig, Color colour, out string reason)
        {
            reason = null;
            if (rig == null) { reason = "RIG_NULL"; return false; }
            int hair = rig.IndexOf("hair");
            if (hair < 0) { reason = "NO_HAIR_PART"; return false; }
            if (colour.r < 0f || colour.r > 1f || colour.g < 0f || colour.g > 1f || colour.b < 0f || colour.b > 1f)
            {
                reason = "HAIR_COLOUR_OUT_OF_RANGE";
                return false;
            }
            rig.SetTint(hair, new Color(colour.r, colour.g, colour.b, 1f));
            return true;
        }

        /// <summary>Which staged Alpha actor represents a playable race/kind (no substitution).</summary>
        public static bool TryResolveRaceActor(int kind, AlphaActorLibrary library, out string actorId, out string reason) =>
            CreatureVisualCatalog.TryResolve(kind, library, out actorId, out reason);
    }

    /// <summary>Renders an Alpha item model into a UI icon sprite (same mesher and material as the world).</summary>
    public static class AlphaItemIconRenderer
    {
        public const int DefaultSize = 64;
        public const string IconLaw = "icon = Alpha model rendered with the world material; no redrawn art";

        public static Sprite RenderFromFile(string cubdecPath, int size = DefaultSize, List<string> problems = null)
        {
            if (string.IsNullOrEmpty(cubdecPath) || !File.Exists(cubdecPath))
            {
                problems?.Add("ICON_FILE_MISSING " + cubdecPath);
                return null;
            }
            return Render(File.ReadAllBytes(cubdecPath), Path.GetFileNameWithoutExtension(cubdecPath), size, problems);
        }

        public static Sprite Render(byte[] cubdec, string name, int size = DefaultSize, List<string> problems = null)
        {
            if (size < 16 || size > 512)
            {
                problems?.Add($"ICON_SIZE {size}");
                return null;
            }
            var built = VoxelMesh.Build(cubdec, "icon_" + name, false);
            if (built.problem != null) { problems?.Add("ICON_MESH " + built.problem); return null; }
            var material = AlphaVisualResources.Base;
            if (material == null)
            {
                DestroyIconResource(built.mesh);
                problems?.Add("ICON_MATERIAL_MISSING");
                return null;
            }

            var root = new GameObject("AlphaIcon_" + name);
            RenderTexture rt = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                var meshGo = new GameObject("mesh");
                meshGo.transform.SetParent(root.transform, false);
                meshGo.AddComponent<MeshFilter>().sharedMesh = built.mesh;
                var renderer = meshGo.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var bounds = built.mesh.bounds;
                meshGo.transform.localPosition = -bounds.center;

                var camGo = new GameObject("icon_camera");
                camGo.transform.SetParent(root.transform, false);
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.15f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.transform.LookAt(Vector3.zero);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 100f;

                rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "icon_" + name };
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                cam.targetTexture = null;
                if (tex.GetPixels32().Length == 0) { problems?.Add("ICON_EMPTY_TEXTURE"); return null; }
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                DestroyIconResource(rt);
                DestroyIconResource(root);
                DestroyIconResource(built.mesh);
            }
        }

        private static void DestroyIconResource(Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) Object.Destroy(asset);
            else Object.DestroyImmediate(asset);
        }
    }
}
