using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CubeGame.AlphaModels;
using CubeGame.EditorTools;
using CubeGame.Presentation.Rendering;
using CubeGame.Presentation.Visual;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.Recovery
{
    /// <summary>
    /// M0 runtime recovery R0-R3: the local Alpha content reaches a Player build (staging + resolution order),
    /// the build gate refuses an incomplete cache, code-created materials never use a stripped shader, and the
    /// icon atlas travels with the content root.
    /// </summary>
    public sealed class ContentPackagingTests
    {
        private string _temp;

        [SetUp]
        public void SetUp()
        {
            _temp = Path.Combine(Path.GetTempPath(), "CubeGameRecoveryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, true);
        }

        private static void Touch(string path, string text = "x")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        /// <summary>A tiny cache with every runtime category and the research clutter of the real one.</summary>
        private string FakeCache(out string icons)
        {
            string root = Path.Combine(_temp, "cache");
            Touch(Path.Combine(root, "actors", "a1", "rig.json"), "{}");
            Touch(Path.Combine(root, "actors", "a1", "models", "body.cubdec"));
            Touch(Path.Combine(root, "actors", "a1", "clips", "walk.json"), "{}");
            Touch(Path.Combine(root, "items", "iron-sword1.cubdec"));
            Touch(Path.Combine(root, "appearance", "human-hair-m01.cubdec"));
            Touch(Path.Combine(root, "sounds", "hit.wav"));
            Touch(Path.Combine(root, "projects", "p1", "big.bin"));
            Touch(Path.Combine(root, "jobs", "job.json"), "{}");
            Touch(Path.Combine(root, "build_kinds.py"));
            Touch(Path.Combine(root, "actors", "a1", "notes.log"));
            icons = Path.Combine(_temp, "icons");
            Touch(Path.Combine(icons, "atlas.png"));
            Touch(Path.Combine(icons, "atlas.json"), "{}");
            return root;
        }

        [Test]
        public void StagingCopiesOnlyRuntimeCategoriesAndTheIconAtlas()
        {
            string cache = FakeCache(out string icons);
            string target = Path.Combine(_temp, "player", "CubeGame_Data", "StreamingAssets", "CubeGame", "AlphaContent");
            Touch(Path.Combine(target, "items", "stale-model.cubdec"));

            AlphaContentInventory staged = AlphaContentStaging.Stage(cache, icons, target);

            Assert.AreEqual(1, staged.actors);
            Assert.AreEqual(1, staged.actorModels);
            Assert.AreEqual(1, staged.actorClips);
            Assert.AreEqual(1, staged.itemModels);
            Assert.AreEqual(1, staged.appearanceParts);
            Assert.AreEqual(1, staged.sounds);
            Assert.AreEqual(1, staged.iconAtlas, "R3: the atlas is staged under <root>/icons");
            Assert.IsTrue(File.Exists(Path.Combine(target, "icons", "atlas.png")));
            Assert.IsFalse(Directory.Exists(Path.Combine(target, "projects")), "research projects are never shipped");
            Assert.IsFalse(Directory.Exists(Path.Combine(target, "jobs")));
            Assert.IsFalse(File.Exists(Path.Combine(target, "build_kinds.py")));
            Assert.IsFalse(File.Exists(Path.Combine(target, "actors", "a1", "notes.log")));
            Assert.IsFalse(File.Exists(Path.Combine(target, "items", "stale-model.cubdec")), "a file the cache no longer has is removed");
            Assert.IsTrue(AlphaContentInventory.TryReadManifest(target, out AlphaContentInventory manifest));
            Assert.AreEqual(staged.itemModels, manifest.itemModels);
        }

        [Test]
        public void StagedPlayerContentResolvesAsAReadyRoot()
        {
            string cache = FakeCache(out string icons);
            string target = Path.Combine(_temp, "moved-player", "AlphaContent");
            AlphaContentStaging.Stage(cache, icons, target);

            AlphaContentSource source = AlphaContentSource.Resolve(target);

            Assert.AreEqual(AlphaContentState.Ready, source.State, source.Diagnosis);
            Assert.AreEqual(AlphaContentOrigin.Explicit, source.Origin);
            StringAssert.Contains("ALPHA_CONTENT_READY", source.Diagnosis);
        }

        [Test]
        public void PackagedContentIsTheFirstStandardLocation()
        {
            string packaged = AlphaContentSource.PackagedRoot.Replace('\\', '/');
            StringAssert.EndsWith("StreamingAssets/CubeGame/AlphaContent", packaged,
                "a Player reads its own StreamingAssets, which moves with the build folder");
            StringAssert.StartsWith(Application.streamingAssetsPath.Replace('\\', '/'), packaged);
            Assert.IsFalse(AlphaContentSource.EditorCacheRoot.Replace('\\', '/').Contains("StreamingAssets"));
        }

        [Test]
        public void BuildGateRefusesACacheThatLostACategory()
        {
            string cache = FakeCache(out _);
            AlphaContentInventory inventory = AlphaContentInventory.Scan(cache);
            List<string> missing = inventory.MissingForRelease();

            Assert.IsFalse(inventory.CompleteForRelease, "one model per category is not a release catalogue");
            foreach (string category in new[] { "ACTORS", "ACTOR_MODELS", "ANIMATION_CLIPS", "ITEM_MODELS", "APPEARANCE_PARTS", "SOUNDS", "ICON_ATLAS" })
            {
                Assert.IsTrue(missing.Any(line => line.StartsWith(category + " ", StringComparison.Ordinal)),
                    category + " must be gated: " + string.Join("; ", missing));
            }

            Assert.IsEmpty(new AlphaContentInventory
            {
                actors = 156, actorModels = 747, actorClips = 927, itemModels = 1554, appearanceParts = 180, sounds = 93, iconAtlas = 1,
            }.MissingForRelease(), "the transferred catalogue passes the gate");
            Assert.IsNotEmpty(AlphaContentInventory.Scan(Path.Combine(_temp, "absent")).MissingForRelease(), "no cache = build FAIL");
        }

        [Test]
        public void LocalCacheOfThisMachineIsReleaseComplete()
        {
            if (!Directory.Exists(AlphaContentStaging.SourceRoot))
            {
                Assert.Inconclusive("no local Alpha cache on this machine: a release build here FAILS at the content gate");
            }

            AlphaContentInventory source = AlphaContentStaging.ScanSource();
            Assert.IsEmpty(source.MissingForRelease(), source.Summary());
        }

        [Test]
        public void CodeCreatedMaterialsUseTheCommittedShader()
        {
            Assert.IsNotNull(Resources.Load<Shader>(RuntimeMaterials.SolidShaderResource),
                "the fallback shader must be a Resources asset, so the Player build includes it");
            Material material = RuntimeMaterials.Solid("test", Color.red);
            try
            {
                Assert.AreEqual(RuntimeMaterials.SolidShaderName, material.shader.name);
                Assert.IsFalse(RuntimeMaterials.IsErrorMaterial(material));
                Assert.AreEqual(Color.red, material.color);
                Assert.IsTrue(RuntimeMaterials.IsErrorMaterial(null), "a missing material is what renders magenta");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RuntimeCodeNeverLooksUpBuiltInShadersByName()
        {
            // The magenta of the audit: Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") is null in
            // a Player (stripped), and new Material(null) renders magenta. Only project shaders may be looked up.
            string root = Path.Combine(Application.dataPath, "CubeGame");
            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string normalised = file.Replace('\\', '/');
                if (normalised.Contains("/Tests/") || normalised.Contains("/Editor/")) continue;
                string text = File.ReadAllText(file);
                foreach (string builtIn in new[]
                         {
                             "Shader.Find(\"Standard\")", "Shader.Find(\"Universal Render Pipeline", "Shader.Find(\"Unlit/",
                             "Shader.Find(\"Sprites/", "Shader.Find(\"Legacy Shaders/",
                         })
                {
                    if (text.Contains(builtIn)) offenders.Add(normalised.Substring(root.Length) + ": " + builtIn);
                }
            }

            Assert.IsEmpty(offenders, string.Join("\n", offenders));
        }

        [Test]
        public void IconAtlasCoversEveryStagedItemModel()
        {
            string cache = AlphaContentStaging.SourceRoot;
            string icons = AlphaContentStaging.IconSourceDirectory;
            if (!Directory.Exists(Path.Combine(cache, "items")) || !File.Exists(Path.Combine(icons, "atlas.json")))
            {
                Assert.Inconclusive("no local item cache / icon atlas on this machine");
            }

            var problems = new List<string>();
            Assert.IsTrue(AlphaIconAtlas.TryLoad(icons, out AlphaIconAtlas atlas, problems), string.Join("\n", problems));
            try
            {
                var missing = new List<string>();
                foreach (string file in Directory.GetFiles(Path.Combine(cache, "items"), "*.cubdec"))
                {
                    string entry = AlphaItemIcons.EntryOf(Path.GetFileNameWithoutExtension(file));
                    if (!atlas.TryGetRect(entry, out _)) missing.Add(entry);
                }

                Assert.IsEmpty(missing, $"{missing.Count} item models without an icon: " + string.Join(", ", missing.Take(20)));
            }
            finally
            {
                atlas.Dispose();
            }
        }
    }
}
