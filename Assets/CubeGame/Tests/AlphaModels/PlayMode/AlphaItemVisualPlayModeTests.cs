// D86 M0-04B — PlayMode checks: the weapon model of a real rig is swapped for Alpha item payloads and
// placed with the measured anchor laws (generic hand, bow, back-centre); unknown subtypes are refused.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.AlphaModels.Tests.PlayMode
{
    public class AlphaItemVisualPlayModeTests
    {
        [UnityTest]
        public IEnumerator WeaponSwapUsesMeasuredAlphaAnchors()
        {
            var source = AlphaContentSource.Resolve();
            if (source.State != AlphaContentState.Ready) Assert.Ignore(source.Diagnosis);
            var items = Path.Combine(source.Root, "items");
            var sword = Path.Combine(items, "iron-sword1.cubdec");
            var bow = Path.Combine(items, "bow.cubdec");
            if (!File.Exists(sword) || !File.Exists(bow))
                Assert.Ignore("item payloads are not staged (python -m tools.alpha_content.extract_item_models)");
            var library = new AlphaActorLibrary(source);
            if (!library.TryGet("d86_human_0x02_greatsword", out var def)) Assert.Ignore("human variant is not staged");
            var view = AlphaActorView.Spawn(def, null, new List<string>());
            try
            {
                Assert.IsTrue(view.Ready, string.Join("\n", view.Problems));
                int weapon = view.Rig.IndexOf("weapon");
                Assert.GreaterOrEqual(weapon, 0);

                Assert.IsTrue(AlphaWeaponLayer.TryEquipFromFile(view.Rig, sword, 0, out var reason), reason);
                var size = view.Rig.sizes[weapon];
                Assert.Greater(size.x, 0);
                Assert.Greater(view.Rig.meshes[weapon].GetComponent<MeshFilter>().sharedMesh.vertexCount, 0);
                view.Rig.SetWeaponOnBack(false);
                Assert.AreEqual(new Vector3(-size.x * 0.5f, -4f, -size.y * 0.5f),
                    view.Rig.meshes[weapon].localPosition, "sword in hand: anchor = (sx/2, sy/2, 4)");
                view.Rig.SetWeaponOnBack(true);
                Assert.AreEqual(new Vector3(-size.x * 0.5f, -size.z * 0.5f, -size.y * 0.5f),
                    view.Rig.meshes[weapon].localPosition, "on the back the weapon is centred");

                Assert.IsTrue(AlphaWeaponLayer.TryEquipFromFile(view.Rig, bow, 6, out var bowReason), bowReason);
                size = view.Rig.sizes[weapon];
                view.Rig.SetWeaponOnBack(false);
                Assert.AreEqual(new Vector3(-size.x * 0.5f, -size.z * 0.5f, -(size.y - 1.5f)),
                    view.Rig.meshes[weapon].localPosition, "bow: anchor = (sx/2, sy-1.5, sz/2)");

                Assert.IsFalse(AlphaWeaponLayer.TryEquipFromFile(view.Rig, sword, 99, out var badSubtype));
                StringAssert.Contains("WEAPON_SUBTYPE_UNKNOWN", badSubtype);
                Assert.IsFalse(AlphaWeaponLayer.TryEquipFromFile(view.Rig, Path.Combine(items, "missing.cubdec"), 0, out var missing));
                StringAssert.Contains("WEAPON_FILE_MISSING", missing);
                yield return null;
            }
            finally
            {
                Object.Destroy(view.gameObject);
            }
        }
    }
}
