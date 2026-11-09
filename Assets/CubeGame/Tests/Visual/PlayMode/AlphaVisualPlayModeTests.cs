// D86 M0-03C/M0-08A — appearance tint and item icons rendered from the real Alpha geometry.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using CubeGame.Presentation.Visual;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.Visual.PlayMode
{
    public class AlphaVisualPlayModeTests
    {
        [UnityTest]
        public IEnumerator HairTintFollowsTheAlphaLaw()
        {
            var source = AlphaContentSource.Resolve();
            if (source.State != AlphaContentState.Ready) Assert.Ignore(source.Diagnosis);
            var library = new AlphaActorLibrary(source);
            if (!library.TryGet("d86_human_0x02_greatsword", out var def)) Assert.Ignore("human variant is not staged");
            var view = AlphaActorView.Spawn(def, null, new List<string>());
            try
            {
                Assert.IsTrue(view.Ready, string.Join("\n", view.Problems));
                var colour = new Color(0.25f, 0.5f, 0.75f, 1f);
                Assert.IsTrue(AlphaAppearance.TrySetHairColour(view.Rig, colour, out var reason), reason);
                int hair = view.Rig.IndexOf("hair");
                Assert.GreaterOrEqual(hair, 0);
                Assert.AreEqual(colour, view.Rig.TintOf(hair));
                var renderer = view.Rig.meshes[hair].GetComponent<Renderer>();
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.AreEqual(colour, block.GetColor(Shader.PropertyToID("_Tint")), "tint is uploaded per part");
                Assert.IsFalse(AlphaAppearance.TrySetHairColour(view.Rig, new Color(1.5f, 0f, 0f, 1f), out var bad));
                StringAssert.Contains("OUT_OF_RANGE", bad);
                yield return null;
            }
            finally
            {
                Object.Destroy(view.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ItemIconIsRenderedFromTheAlphaModel()
        {
            var source = AlphaContentSource.Resolve();
            if (source.State != AlphaContentState.Ready) Assert.Ignore(source.Diagnosis);
            var items = Path.Combine(source.Root, "items");
            var sword = Path.Combine(items, "iron-sword1.cubdec");
            if (!File.Exists(sword)) Assert.Ignore("item payloads are not staged (tools.alpha_content.extract_item_models)");
            var problems = new List<string>();
            var sprite = AlphaItemIconRenderer.RenderFromFile(sword, 64, problems);
            try
            {
                Assert.IsEmpty(problems, string.Join("\n", problems));
                Assert.IsNotNull(sprite);
                Assert.AreEqual(64, sprite.texture.width);
                Assert.AreEqual(64, sprite.texture.height);
                int opaque = 0;
                foreach (var p in sprite.texture.GetPixels32()) if (p.a > 0) opaque++;
                Assert.Greater(opaque, 40, "the icon contains the real model, not an empty frame");
                Assert.IsNull(AlphaItemIconRenderer.RenderFromFile(Path.Combine(items, "missing.cubdec"), 64, problems));
                Assert.That(problems, Has.Some.Contains("ICON_FILE_MISSING"));
                yield return null;
            }
            finally
            {
                if (sprite != null)
                {
                    Object.Destroy(sprite.texture);
                    Object.Destroy(sprite);
                }
            }
        }
        [UnityTest]
        public IEnumerator PlayerVisualBuilderAppliesHairWeaponAndOffhand()
        {
            var source = AlphaContentSource.Resolve();
            if (source.State != AlphaContentState.Ready) Assert.Ignore(source.Diagnosis);
            var items = Path.Combine(source.Root, "items");
            var sword = Path.Combine(items, "iron-sword1.cubdec");
            var shield = Path.Combine(items, "iron-shield01.cubdec");
            if (!File.Exists(sword) || !File.Exists(shield))
                Assert.Ignore("item payloads are not staged (python -m tools.alpha_content.extract_item_models)");
            var library = new AlphaActorLibrary(source);
            var request = new CharacterVisualRequest
            {
                Kind = 0x02,
                HasHairColour = true,
                HairColour = new Color(0.2f, 0.4f, 0.6f, 1f),
                WeaponSubtype = 0,
                WeaponModelPath = sword,
                OffhandSubtype = 13,
                OffhandModelPath = shield,
            };
            Assert.IsEmpty(AlphaPlayerVisualBuilder.Validate(request, library), "the descriptor matches staged content");
            var result = AlphaPlayerVisualBuilder.Build(request, null, library);
            try
            {
                Assert.IsTrue(result.Ok, result.Status);
                Assert.IsTrue(result.View.Ready);
                int hair = result.View.Rig.IndexOf("hair");
                Assert.AreEqual(request.HairColour, result.View.Rig.TintOf(hair), "hair tint is applied");
                Assert.AreEqual(0, result.View.Rig.weaponSubtype, "weapon subtype 0 was equipped");
                Assert.IsTrue(AlphaOffhandLayer.HasOffhand(result.View.Rig), "shield is attached to the left hand");
                yield return null;
            }
            finally
            {
                if (result.View != null) Object.Destroy(result.View.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator PlayerVisualBuilderReportsRefusalsInsteadOfSubstituting()
        {
            var source = AlphaContentSource.Resolve();
            if (source.State != AlphaContentState.Ready) Assert.Ignore(source.Diagnosis);
            var library = new AlphaActorLibrary(source);
            var unknown = new CharacterVisualRequest { Kind = 0xFE, WeaponSubtype = -1, OffhandSubtype = -1 };
            var result = AlphaPlayerVisualBuilder.Build(unknown, null, library);
            Assert.IsFalse(result.Ok);
            Assert.That(result.Problems, Has.Some.Contains("MISSING_VISUAL_CONTENT"));
            var badWeapon = new CharacterVisualRequest
            {
                Kind = 0x02, WeaponSubtype = 99, WeaponModelPath = "somewhere.cubdec", OffhandSubtype = -1,
            };
            var problems = AlphaPlayerVisualBuilder.Validate(badWeapon, library);
            Assert.That(problems, Has.Some.Contains("WEAPON_SUBTYPE_NOT_EQUIPPABLE 99"));
            yield return null;
        }
    }
}
