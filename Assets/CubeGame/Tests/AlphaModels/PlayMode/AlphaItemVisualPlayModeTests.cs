// D86 M0-04B — PlayMode checks: the weapon model of a real rig is swapped for Alpha item payloads and
// placed with the measured anchor laws (generic hand, bow, back-centre); unknown subtypes are refused.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using CubeGame.Content;
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
        [UnityTest]
        public IEnumerator HeldItemsAttachToTheRightHand()
        {
            var source = AlphaContentSource.Resolve();
            if (source.State != AlphaContentState.Ready) Assert.Ignore(source.Diagnosis);
            var items = Path.Combine(source.Root, "items");
            foreach (var name in new[] { "life-potion.cubdec", "lantern02.cubdec", "glider.cubdec" })
                if (!File.Exists(Path.Combine(items, name))) Assert.Ignore("held item payload is not staged: " + name);
            var library = new AlphaActorLibrary(source);
            if (!library.TryGet("d86_human_0x02_greatsword", out var def)) Assert.Ignore("human variant is not staged");
            var view = AlphaActorView.Spawn(def, null, new List<string>());
            try
            {
                Assert.IsTrue(view.Ready, string.Join("\n", view.Problems));
                Assert.IsFalse(AlphaHeldItemLayer.HasHeldItem(view.Rig));
                Assert.IsTrue(AlphaHeldItemLayer.TryEquipFromItem(view.Rig, items, 1, 1, out var reason), reason);
                Assert.IsTrue(AlphaHeldItemLayer.HasHeldItem(view.Rig));
                int hand = view.Rig.IndexOf("hand_r");
                var child = view.Rig.parts[hand].Find(AlphaHeldItemLayer.MeshChildName);
                Assert.IsNotNull(child, "the held model is a child of the posed right hand");
                var mesh = child.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(AlphaHeldItemLayer.CenterOffset(AlphaHeldItemLayer.LastEquippedSize), child.localPosition,
                    "held block uses the center anchor of the model grid");
                Assert.Greater(mesh.vertexCount, 0);

                Assert.IsTrue(AlphaHeldItemLayer.TryEquipFromItem(view.Rig, items, 24, 7, out var lampReason), lampReason);
                Assert.IsTrue(AlphaHeldItemLayer.TryEquipFromItem(view.Rig, items, 23, 0, out var gliderReason), gliderReason);
                Assert.IsFalse(AlphaHeldItemLayer.TryEquipFromItem(view.Rig, items, 5, 0, out var bad));
                StringAssert.Contains("HELD_ITEM_NO_MODEL", bad);
                AlphaHeldItemLayer.Clear(view.Rig);
                Assert.IsFalse(AlphaHeldItemLayer.HasHeldItem(view.Rig));
                yield return null;
            }
            finally
            {
                Object.Destroy(view.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator OffhandModelFollowsTheLeftHandWithMeasuredAnchor()
        {
            var source = AlphaContentSource.Resolve();
            if (source.State != AlphaContentState.Ready) Assert.Ignore(source.Diagnosis);
            var items = Path.Combine(source.Root, "items");
            var shield = Path.Combine(items, "iron-shield01.cubdec");
            var dagger = Path.Combine(items, "iron-dagger1.cubdec");
            if (!File.Exists(shield) || !File.Exists(dagger))
                Assert.Ignore("item payloads are not staged (python -m tools.alpha_content.extract_item_models)");
            var library = new AlphaActorLibrary(source);
            if (!library.TryGet("d86_human_0x02_greatsword", out var def)) Assert.Ignore("human variant is not staged");
            var view = AlphaActorView.Spawn(def, null, new List<string>());
            try
            {
                Assert.IsTrue(view.Ready, string.Join("\n", view.Problems));
                int hand = view.Rig.IndexOf("hand_l");
                Assert.GreaterOrEqual(hand, 0);
                Assert.IsFalse(AlphaOffhandLayer.HasOffhand(view.Rig));

                Assert.IsTrue(AlphaOffhandLayer.TryEquipFromFile(view.Rig, shield, 13, out var reason), reason);
                Assert.IsTrue(AlphaOffhandLayer.HasOffhand(view.Rig));
                var child = view.Rig.parts[hand].Find(AlphaOffhandLayer.MeshChildName);
                Assert.IsNotNull(child, "the off-hand model is a child of the posed left hand");
                Assert.IsTrue(AlphaItemRules.TryOffhandAnchors(13, out var set));
                var mesh = child.GetComponent<MeshFilter>().sharedMesh;
                var bounds = mesh.bounds.size;
                var grid = new Vector3Int(Mathf.RoundToInt(bounds.x), Mathf.RoundToInt(bounds.z), Mathf.RoundToInt(bounds.y));
                Assert.AreEqual(AlphaOffhandLayer.Offset(set.InHand, grid), child.localPosition,
                    "shield: anchor = (sx/2, 3, sz/2)");
                Assert.Greater(mesh.vertexCount, 0);

                Assert.IsTrue(AlphaOffhandLayer.TryEquipFromFile(view.Rig, dagger, 3, out var daggerReason), daggerReason);
                Assert.AreEqual(1, CountChildren(view.Rig.parts[hand]), "equipping again replaces the model, no duplicates");
                Assert.IsFalse(AlphaOffhandLayer.TryEquipFromFile(view.Rig, shield, 99, out var bad));
                StringAssert.Contains("OFFHAND_SUBTYPE_UNKNOWN", bad);
                AlphaOffhandLayer.Clear(view.Rig);
                Assert.IsFalse(AlphaOffhandLayer.HasOffhand(view.Rig));
                yield return null;
            }
            finally
            {
                Object.Destroy(view.gameObject);
            }
        }

        static int CountChildren(Transform parent)
        {
            int count = 0;
            foreach (Transform child in parent) if (child.name == AlphaOffhandLayer.MeshChildName) count++;
            return count;
        }
    }
}
