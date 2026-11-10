// CubeGame.AlphaModels — what a content root actually holds (M0 runtime recovery R0/R2).
//
// One counting rule shared by the build gate (Editor, before and after staging) and by the
// runtime gate ALPHA_CONTENT_READY (Player): a category that the game reads at runtime must be
// present, otherwise the build fails / the gate fails instead of the player getting a capsule.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CubeGame.AlphaModels
{
    /// <summary>Counts of the runtime content categories under one content root.</summary>
    [Serializable]
    public sealed class AlphaContentInventory
    {
        public const string ManifestFile = "manifest.json";
        public const string ItemsFolder = "items";
        public const string AppearanceFolder = "appearance";
        public const string SoundsFolder = "sounds";
        public const string IconsFolder = "icons";
        public const string IconAtlasImage = "atlas.png";
        public const string IconAtlasIndex = "atlas.json";

        public int actors;
        public int actorModels;
        public int actorClips;
        public int itemModels;
        public int appearanceParts;
        public int sounds;
        public int iconAtlas;
        public long bytes;
        public string sourceCommit = "";
        public string stagedUtc = "";

        /// <summary>
        /// Lower bounds of a release baseline. They are the sizes of the transferred catalogue rounded
        /// down (156 actors with 747 models and 927 clips, 1554 item models, 180 appearance parts, 93 WAV): a cache that lost a whole
        /// category or most of one is refused, while a later re-export that adds entries still passes.
        /// </summary>
        public const int MinActors = 150;
        public const int MinActorModels = 700;
        public const int MinActorClips = 900;
        public const int MinItemModels = 1500;
        public const int MinAppearanceParts = 170;
        public const int MinSounds = 90;

        public static AlphaContentInventory Scan(string root)
        {
            var inventory = new AlphaContentInventory();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return inventory;
            string actorsRoot = Path.Combine(root, AlphaContentSource.ActorsFolder);
            if (Directory.Exists(actorsRoot))
            {
                foreach (string actor in Directory.GetDirectories(actorsRoot))
                {
                    if (!File.Exists(Path.Combine(actor, "rig.json"))) continue;
                    inventory.actors++;
                    inventory.actorModels += Count(Path.Combine(actor, "models"), "*.cubdec", ref inventory.bytes);
                    inventory.actorClips += Count(Path.Combine(actor, "clips"), "*.json", ref inventory.bytes);
                }
            }

            inventory.itemModels = Count(Path.Combine(root, ItemsFolder), "*.cubdec", ref inventory.bytes);
            inventory.appearanceParts = Count(Path.Combine(root, AppearanceFolder), "*.cubdec", ref inventory.bytes);
            inventory.sounds = Count(Path.Combine(root, SoundsFolder), "*.wav", ref inventory.bytes);
            string icons = Path.Combine(root, IconsFolder);
            inventory.iconAtlas = File.Exists(Path.Combine(icons, IconAtlasImage)) && File.Exists(Path.Combine(icons, IconAtlasIndex)) ? 1 : 0;
            return inventory;
        }

        static int Count(string directory, string pattern, ref long bytes)
        {
            if (!Directory.Exists(directory)) return 0;
            string[] files = Directory.GetFiles(directory, pattern, SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++) bytes += new FileInfo(files[i]).Length;
            return files.Length;
        }

        /// <summary>Every category below its release lower bound, as "NAME have &lt; need" lines; empty = complete.</summary>
        public List<string> MissingForRelease()
        {
            var missing = new List<string>();
            Check(missing, "ACTORS", actors, MinActors);
            Check(missing, "ACTOR_MODELS", actorModels, MinActorModels);
            Check(missing, "ANIMATION_CLIPS", actorClips, MinActorClips);
            Check(missing, "ITEM_MODELS", itemModels, MinItemModels);
            Check(missing, "APPEARANCE_PARTS", appearanceParts, MinAppearanceParts);
            Check(missing, "SOUNDS", sounds, MinSounds);
            Check(missing, "ICON_ATLAS", iconAtlas, 1);
            return missing;
        }

        static void Check(List<string> missing, string name, int have, int need)
        {
            if (have < need) missing.Add($"{name} {have} < {need}");
        }

        public bool CompleteForRelease => MissingForRelease().Count == 0;

        public string Summary() =>
            $"actors={actors} actorModels={actorModels} clips={actorClips} items={itemModels} " +
            $"appearance={appearanceParts} sounds={sounds} iconAtlas={iconAtlas} bytes={bytes}";

        public string ToJson() => JsonUtility.ToJson(this, true);

        public static bool TryReadManifest(string root, out AlphaContentInventory manifest)
        {
            manifest = null;
            if (string.IsNullOrEmpty(root)) return false;
            string path = Path.Combine(root, ManifestFile);
            if (!File.Exists(path)) return false;
            try
            {
                manifest = JsonUtility.FromJson<AlphaContentInventory>(File.ReadAllText(path));
                return manifest != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
