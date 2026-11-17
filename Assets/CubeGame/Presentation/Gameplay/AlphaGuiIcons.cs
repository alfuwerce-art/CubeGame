using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using UnityEngine;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Interface PNGs of Alpha's <c>data3.db</c> (menu buttons, hotbar actions), staged by
    /// <c>tools/alpha_content/extract_gui_icons.py</c> into the local content cache folder <c>gui</c> and travelling with
    /// the Windows build like the sounds. The images are Alpha assets and are never committed; without the cache the HUD
    /// keeps its frames and key captions and only the icon is missing (logged once as GUI_ICONS=ABSENT).
    ///
    /// ALPHA_CODE_STATIC: the action-id → file table is the one <c>GameController</c>'s constructor (0x45A…) fills
    /// with <c>GameController_saveKeyedString</c> before the hotbar is cloned (decompiled at GameController.cpp:20826+).
    /// </summary>
    internal sealed class AlphaGuiIcons : IDisposable
    {
        private readonly string _directory;
        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        public AlphaGuiIcons(string contentRoot)
        {
            string directory = string.IsNullOrEmpty(contentRoot)
                ? null
                : Path.Combine(contentRoot, AlphaContentInventory.GuiFolder);
            _directory = directory != null && Directory.Exists(directory) ? directory : null;
        }

        public bool Available => _directory != null;

        public string DirectoryPath => _directory;

        /// <summary>Point-filtered texture of a staged Alpha interface PNG, or null when it is not staged.</summary>
        public Texture2D Get(string fileName)
        {
            if (_directory == null || string.IsNullOrEmpty(fileName)) return null;
            if (_cache.TryGetValue(fileName, out Texture2D cached)) return cached;
            Texture2D texture = null;
            string path = Path.Combine(_directory, fileName);
            if (File.Exists(path))
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "alpha-gui-" + fileName,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.Destroy(texture);
                    texture = null;
                }
            }

            _cache[fileName] = texture;
            return texture;
        }

        public void Dispose()
        {
            foreach (Texture2D texture in _cache.Values)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }

            _cache.Clear();
        }

        /// <summary>Alpha action id → hotbar icon (constructor table; 0 is <c>none.png</c>).</summary>
        public static string ActionIcon(int action)
        {
            return ActionIcons.TryGetValue(action, out string name) ? name : null;
        }

        private static readonly Dictionary<int, string> ActionIcons = new Dictionary<int, string>
        {
            { 0, "none.png" }, { 57, "slam.png" }, { 58, "slam-back.png" }, { 65, "slam-left.png" },
            { 66, "slam-right.png" }, { 2, "attack-left.png" }, { 1, "attack-right.png" }, { 4, "stab-left.png" },
            { 3, "stab-right.png" }, { 67, "jab.png" }, { 59, "charge-slam.png" }, { 63, "swirl.png" },
            { 64, "charge-mutilate.png" }, { 8, "block.png" }, { 9, "shield-attack.png" }, { 10, "shield-slam.png" },
            { 18, "pierce-left.png" }, { 19, "pierce-right.png" }, { 17, "ambush.png" }, { 7, "punch-left.png" },
            { 6, "punch-right.png" }, { 20, "kick.png" }, { 21, "ranger-kick.png" }, { 13, "slash-left.png" },
            { 14, "slash-right.png" }, { 5, "perforate.png" }, { 79, "sneak.png" }, { 99, "aim.png" },
            { 100, "swiftness.png" }, { 38, "firebolt.png" }, { 39, "firebolt.png" }, { 40, "firebolt.png" },
            { 37, "fireball.png" }, { 46, "fire-salvo.png" }, { 88, "fire-explosion.png" }, { 44, "waterbolt.png" },
            { 41, "waterbolt.png" }, { 42, "waterbolt.png" }, { 43, "waterball.png" }, { 45, "water-salvo.png" },
            { 34, "healing-stream.png" }, { 30, "fire-shock.png" }, { 31, "fire-burst.png" }, { 32, "water-shock.png" },
            { 33, "water-burst.png" }, { 54, "smash.png" }, { 11, "swirl.png" }, { 86, "cyclone.png" },
            { 48, "intercept.png" }, { 49, "teleport.png" }, { 50, "retreat2.png" }, { 12, "slash.png" },
            { 16, "mutilate.png" }, { 22, "shoot.png" }, { 23, "shoot-fast.png" }, { 55, "salvo.png" },
            { 26, "boomerang.png" }, { 27, "charge-boomerang.png" }, { 24, "charge-shoot.png" }, { 25, "salvo.png" },
            { 28, "beam.png" }, { 95, "beam.png" }, { 36, "charge-bullet.png" }, { 96, "shuriken.png" },
            { 101, "bulwark.png" }, { 103, "mana-shield.png" }, { 97, "camouflage.png" }, { 102, "warfrenzy.png" },
        };
    }
}
