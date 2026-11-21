using System.Collections;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using CubeGame.AlphaTransfer;
using CubeGame.Core.Characters;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using CubeGame.Presentation.Visual;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.Tests.FullM0.PlayMode
{
    /// <summary>
    /// M0-F2A in the real game scene: create a character with a chosen appearance → the hero model shows that race,
    /// face, haircut and hair colour → equip / change / remove weapons and armour → drop ground loot → floating-origin
    /// shift → pickup → save → exit → load → the reloaded visual equals the saved state, without duplicates.
    /// </summary>
    public sealed class FullM0CharacterRuntimePlayModeTests
    {
        private string _saveDir;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "cubegame_f2a_" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.Destroy(_host);
            }

            foreach (string name in new[] { "Main Camera", "Sun", "WorldRoot", "Player", "TrainingTarget_PLACEHOLDER", "F2A_Races" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover != null)
                {
                    Object.Destroy(leftover);
                }
            }

            if (Directory.Exists(_saveDir))
            {
                Directory.Delete(_saveDir, true);
            }
        }

        static void RequireContent()
        {
            if (AlphaActorLibrary.Shared.State != AlphaContentState.Ready)
            {
                Assert.Ignore("Alpha content cache is not staged: " + AlphaActorLibrary.Shared.Diagnosis);
            }

            string appearance = Path.Combine(AlphaActorLibrary.Shared.Source.Root, HeroVisualComposer.AppearanceFolder);
            if (!Directory.Exists(appearance))
            {
                Assert.Ignore("head/hair payloads not staged (python -m tools.alpha_content.gen_character_models --stage): " + appearance);
            }
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null;
            }
        }

        static int CountNamed(Transform root, string name)
        {
            int n = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) n++;
            }

            return n;
        }

        static int CountGroundObjects()
        {
            int n = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name.StartsWith("GroundItem_")) n++;
            }

            return n;
        }

        static string Mesh(AlphaActorView view, string part) => AlphaPartSwap.MeshName(view.Rig, part);

        static int ActiveViews(Transform heroRoot)
        {
            int n = 0;
            foreach (AlphaActorView v in heroRoot.GetComponentsInChildren<AlphaActorView>(false))
            {
                if (v.gameObject.activeInHierarchy) n++;
            }

            return n;
        }

        [UnityTest]
        public IEnumerator CharacterVisualSurvivesEquipmentOriginShiftAndSaveLoad()
        {
            RequireContent();
            _host = new GameObject("GameBootstrap");
            GameBootstrap game = _host.AddComponent<GameBootstrap>();
            game.SaveDirectory = _saveDir;
            yield return null;

            // 1–2. Character with a non-default appearance: dwarf woman, face 3, haircut 2, blue hair; rogue kit = two daggers.
            var look = new HeroAppearance(2, 1, 2, 1, 0x20, 0x80, 0xFF);
            Assert.IsTrue(game.StartNewWorld("slot0", "F2A", 515151u, "Brena", CharacterClass.Rogue, look), game.Status);
            yield return Frames(3);
            GameVisualSink visuals = game.Visuals;
            Assert.IsTrue(visuals.TryGetView(StableEntityId.Hero, out AlphaActorView hero), string.Join("; ", visuals.Problems));
            Assert.IsTrue(hero.Ready, hero.Status());
            HeroVisualSpec spec = visuals.HeroSpec;
            Assert.AreEqual(0x0A, spec.Kind, "dwarf female");
            Assert.AreEqual(0x02, spec.RigKind, "drawn on the shared humanoid action rig");
            Assert.AreEqual("dwarf-head-f03.cub", spec.HeadModel);
            Assert.AreEqual("dwarf-hair-f02.cub", spec.HairModel);
            StringAssert.Contains("dwarf-head-f03", Mesh(hero, "head"), "the face model is the one drawn");
            StringAssert.Contains("dwarf-hair-f02", Mesh(hero, "hair"));
            Color tint = hero.Rig.TintOf(hero.Rig.IndexOf("hair"));
            Assert.AreEqual(0x20 / 255f, tint.r, 1e-4f);
            Assert.AreEqual(0x80 / 255f, tint.g, 1e-4f);
            Assert.AreEqual(1f, tint.b, 1e-4f);
            Assert.AreEqual(0.9f / 1.01f, hero.Rig.ScaleOf(hero.Rig.IndexOf("head")), 1e-5f, "species head scale");
            Assert.AreEqual("iron-dagger1.cub", spec.WeaponModel);
            Assert.AreEqual("iron-dagger1.cub", spec.OffhandModel);
            Assert.IsTrue(spec.DualWield);
            Assert.IsTrue(AlphaWeaponLayer.HasWeapon(hero.Rig), "dagger in the main hand");
            Assert.AreEqual(3, hero.Rig.weaponSubtype);
            Assert.AreEqual(1.2f / 0.95f, hero.Rig.ScaleOf(hero.Rig.IndexOf("weapon")), 1e-5f, "species weapon scale");
            Assert.AreEqual(1, CountNamed(hero.transform, AlphaOffhandLayer.MeshChildName), "one off-hand dagger");
            Transform heroRoot = hero.transform.parent;
            Assert.AreEqual(1, ActiveViews(heroRoot), "one hero model");
            int builds = visuals.HeroBuildCount;

            // 3–4. Change the weapon: a wooden greatmace from the bags replaces the dagger.
            HeroInventory inventory = game.Session.Hero.Inventory;
            var greatmace = new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Greatmace, 1, 0, 0, ItemModifier.M02);
            Assert.IsTrue(inventory.TryAdd(InventoryEntry.FromCore(greatmace)), inventory.LastRejection);
            int bag = FindBag(inventory, greatmace);
            Assert.IsTrue(inventory.TryEquip(bag, out EquipmentSlot slot), inventory.LastRejection);
            Assert.AreEqual(EquipmentSlot.MainHand, slot);
            yield return Frames(2);
            Assert.IsTrue(visuals.TryGetView(StableEntityId.Hero, out hero));
            Assert.AreEqual(builds + 1, visuals.HeroBuildCount, "one change → one rebuild");
            Assert.AreEqual("wood-greatmace02.cub", visuals.HeroSpec.WeaponModel);
            Assert.AreEqual(HeroWeaponGrip.TwoHanded, visuals.HeroSpec.WeaponGrip);
            Assert.AreEqual(17, hero.Rig.weaponSubtype);
            Assert.IsTrue(AlphaWeaponLayer.HasWeapon(hero.Rig), "greatmace mesh drawn");
            Assert.AreEqual(1, ActiveViews(heroRoot), "the old model is gone, no duplicate");
            StringAssert.Contains("dwarf-head-f03", Mesh(hero, "head"), "appearance kept across rebuilds");

            // 5. Armour on and off; off-hand off.
            var chest = new ItemInstance(ItemType.Chest, 0, 1, 0, 0, ItemModifier.M01);
            Assert.IsTrue(inventory.TryAdd(InventoryEntry.FromCore(chest)), inventory.LastRejection);
            Assert.IsTrue(inventory.TryEquip(FindBag(inventory, chest), out _), inventory.LastRejection);
            yield return Frames(2);
            Assert.IsTrue(visuals.TryGetView(StableEntityId.Hero, out hero));
            Assert.AreEqual("iron-chest1.cub", visuals.HeroSpec.ChestModel);
            Assert.IsTrue(AlphaEquipmentLayer.HasEquipment(hero.Rig, 4), "chest armour swapped onto the body");
            Assert.IsTrue(inventory.TryUnequip(EquipmentSlot.Chest), inventory.LastRejection);
            Assert.IsTrue(inventory.TryUnequip(EquipmentSlot.OffHand), inventory.LastRejection);
            yield return Frames(2);
            Assert.IsTrue(visuals.TryGetView(StableEntityId.Hero, out hero));
            Assert.IsNull(visuals.HeroSpec.ChestModel);
            Assert.IsFalse(AlphaEquipmentLayer.HasEquipment(hero.Rig, 4), "armour removed");
            Assert.AreEqual(0, CountNamed(hero.transform, AlphaOffhandLayer.MeshChildName), "off-hand removed");
            Assert.IsFalse(visuals.HeroSpec.DualWield);
            Assert.AreEqual(1, ActiveViews(heroRoot));

            // 6. Ground loot: one dropped item → one visual (owner: the sink).
            HeroState heroState = game.Session.Hero;
            var sword = new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Sword, 1, 0, 3, ItemModifier.M01);
            double dropX = heroState.Position.X + 40.0, dropZ = heroState.Position.Z;
            double dropY = game.WorldState.SurfaceAt((int)System.Math.Floor(dropX), (int)System.Math.Floor(dropZ)) + 1;
            StableEntityId dropped = game.Session.WorldItems.Drop(InventoryEntry.FromCore(sword), new WorldPosition(dropX, dropY, dropZ),
                game.Session.Clock.TimeMs);
            yield return Frames(2);
            Assert.AreEqual(1, visuals.GroundItemCount);
            Assert.AreEqual(1, CountGroundObjects(), "exactly one scene object for one item");
            Assert.IsTrue(visuals.TryGetGroundItem(dropped, out Transform ground, out string groundModel));
            Assert.AreEqual("iron-sword1-random3", groundModel, "item bytes pick the model (seed 3)");
            AssertNear(game.WorldView.ToScene(dropX, dropY, dropZ), ground.position, "ground item at its world position");

            // 7. Floating-origin shift far away and back: everything follows the world, nothing drifts.
            Vector2Int origin = game.WorldView.Origin;
            int farX = (int)heroState.Position.X + 3000, farZ = (int)heroState.Position.Z;
            game.TeleportHero(farX, farZ);
            yield return Frames(3);
            Assert.AreNotEqual(origin, game.WorldView.Origin, "the origin moved");
            AssertNear(game.WorldView.ToScene(dropX, dropY, dropZ), ground.position, "ground item re-placed after the shift");
            Assert.IsTrue(visuals.TryGetView(StableEntityId.Hero, out hero));
            Assert.AreEqual(game.PlayerTransform, hero.transform.parent.parent, "hero model stays on the player");
            AssertNear(game.PlayerTransform.position, hero.transform.parent.position, "hero visual root at the player");
            Assert.IsTrue(AlphaWeaponLayer.HasWeapon(hero.Rig), "weapon kept through the shift");

            // Pickup: the hero walks onto the item → item and its visual disappear together.
            game.TeleportHero((int)System.Math.Floor(dropX), (int)System.Math.Floor(dropZ));
            float deadline = Time.realtimeSinceStartup + 6f;
            while (visuals.GroundItemCount > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            yield return Frames(2);
            Assert.AreEqual(0, game.Session.WorldItems.Count, "picked up");
            Assert.AreEqual(0, visuals.GroundItemCount);
            Assert.AreEqual(0, CountGroundObjects(), "the visual left with the item");

            // 8–10. Save → exit → load.
            string savedSignature = visuals.HeroSpec.Signature;
            var savedEquipment = new Dictionary<VisualEquipmentSlot, HeroVisualItem>();
            foreach (var pair in visuals.HeroEquipment) savedEquipment[pair.Key] = pair.Value;
            string savedHead = Mesh(hero, "head");
            string savedWeapon = Mesh(hero, "weapon");
            Assert.IsTrue(game.SaveCheckpoint(), game.Status);
            Assert.IsTrue(game.ExitToMenu(false), game.Status);
            yield return Frames(2);
            Assert.AreEqual(0, CountGroundObjects());
            Assert.IsTrue(game.LoadWorld("slot0"), game.Status);
            yield return Frames(3);

            // 11. The reloaded visual is the saved one.
            visuals = game.Visuals;
            Assert.IsTrue(visuals.TryGetView(StableEntityId.Hero, out AlphaActorView loaded), string.Join("; ", visuals.Problems));
            HeroAppearance loadedLook = game.Session.Hero.Appearance;
            Assert.AreEqual(look.Race, loadedLook.Race);
            Assert.AreEqual(look.Gender, loadedLook.Gender);
            Assert.AreEqual(look.Face, loadedLook.Face);
            Assert.AreEqual(look.Haircut, loadedLook.Haircut);
            Assert.AreEqual(look.HairR, loadedLook.HairR);
            Assert.AreEqual(look.HairG, loadedLook.HairG);
            Assert.AreEqual(look.HairB, loadedLook.HairB);
            Assert.AreEqual(savedSignature, visuals.HeroSpec.Signature, "appearance + equipment → the same visual after load");
            CollectionAssert.AreEquivalent(savedEquipment, visuals.HeroEquipment, "weapon state restored");
            Assert.AreEqual(savedHead, Mesh(loaded, "head"));
            Assert.AreEqual(savedWeapon, Mesh(loaded, "weapon"));
            Assert.AreEqual(1, ActiveViews(loaded.transform.parent), "one hero model after load");
            Assert.AreEqual(0, visuals.GroundItemCount, "ground items are not saved (existing rule) and leave no visual");
            Debug.Log($"[F2A] {visuals.HeroSpec.Signature} builds {visuals.HeroBuildCount} problems [{string.Join("; ", visuals.Problems)}]");
        }

        [UnityTest]
        public IEnumerator EveryRaceAndSexBuildsItsOwnHeroVisual()
        {
            RequireContent();
            var host = new GameObject("F2A_Races");
            var problems = new List<string>();
            var heads = new HashSet<string>();
            for (int race = 0; race < AlphaHeroCreation.RaceCount; race++)
            {
                for (int gender = 0; gender < 2; gender++)
                {
                    var look = new HeroAppearance(race, gender, 1, 1, 0x80, 0x40, 0x20);
                    var gear = new Dictionary<VisualEquipmentSlot, HeroVisualItem>
                    {
                        { VisualEquipmentSlot.MainHand, new HeroVisualItem(3, 0, 1) },
                    };
                    HeroVisualSpec spec = HeroVisualComposer.Compose(look, gear);
                    CharacterVisualResult result = AlphaPlayerVisualBuilder.Build(spec.ToRequest(AlphaActorLibrary.Shared.Source.Root),
                        host.transform, AlphaActorLibrary.Shared, Path.Combine(AlphaActorLibrary.Shared.Source.Root, "items"));
                    string label = $"race {race} gender {gender} kind 0x{spec.Kind:x2}";
                    Assert.IsNotNull(result.View, label);
                    Assert.IsTrue(result.View.Ready, label + " " + result.Status);
                    if (spec.SharedActionRig)
                    {
                        Assert.IsEmpty(result.Problems, label + ": " + string.Join("; ", result.Problems));
                        Assert.IsTrue(AlphaWeaponLayer.HasWeapon(result.View.Rig), label + " holds the sword");
                        StringAssert.Contains(Path.GetFileNameWithoutExtension(spec.HeadModel), Mesh(result.View, "head"), label);
                        StringAssert.Contains(Path.GetFileNameWithoutExtension(spec.HairModel), Mesh(result.View, "hair"), label);
                        heads.Add(Mesh(result.View, "head"));
                    }
                    else
                    {
                        problems.Add(label + ": " + string.Join("; ", spec.Problems));
                    }

                    Object.Destroy(result.View.gameObject);
                    yield return null;
                }
            }

            Assert.AreEqual(15, heads.Count, "15 species on the shared rig, each with its own face model");
            Assert.AreEqual(1, problems.Count, "only OrcFemale keeps its own actor: " + string.Join(" | ", problems));
            StringAssert.Contains("kind 0x0c", problems[0]);
            Object.Destroy(host);
        }

        static int FindBag(HeroInventory inventory, ItemInstance item)
        {
            for (int i = 0; i < HeroInventory.Capacity; i++)
            {
                if (inventory[i] != null && ReferenceEquals(inventory[i].Core, item)) return i;
            }

            Assert.Fail("item not in the bags: " + item);
            return -1;
        }

        static void AssertNear(Vector3 expected, Vector3 actual, string message)
        {
            Assert.Less(Vector3.Distance(expected, actual), 1e-3f, $"{message}: expected {expected} got {actual}");
        }
    }
}
