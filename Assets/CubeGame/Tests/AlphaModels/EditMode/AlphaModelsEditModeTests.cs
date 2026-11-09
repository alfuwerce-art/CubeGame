// D86 M0-01 — EditMode checks of the Alpha runtime primitives and the committed content tables.
// These tests do not need the local Alpha cache; the cache-dependent ones skip with a reason when
// the cache is absent (and run for real in the developer environment).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CubeGame.AlphaModels;
using CubeGame.AlphaTransfer;
using CubeGame.Content;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.AlphaModels.Tests.EditMode
{
    public class AlphaVoxelMeshTests
    {
        static byte[] Cubdec(int w, int d, int h, Func<int, int, int, byte[]> colour)
        {
            var bytes = new byte[12 + w * d * h * 3];
            BitConverter.GetBytes(w).CopyTo(bytes, 0);
            BitConverter.GetBytes(d).CopyTo(bytes, 4);
            BitConverter.GetBytes(h).CopyTo(bytes, 8);
            for (int z = 0; z < h; z++)
            for (int y = 0; y < d; y++)
            for (int x = 0; x < w; x++)
            {
                var c = colour(x, y, z);
                int o = 12 + ((z * d + y) * w + x) * 3;
                bytes[o] = c[0]; bytes[o + 1] = c[1]; bytes[o + 2] = c[2];
            }
            return bytes;
        }

        [Test]
        public void SingleVoxelHasSixFacesAndGridCoordinates()
        {
            var built = VoxelMesh.Build(Cubdec(1, 1, 1, (x, y, z) => new byte[] { 10, 20, 30 }), "one");
            Assert.IsNull(built.problem, built.problem);
            Assert.AreEqual(new Vector3Int(1, 1, 1), built.size);
            Assert.AreEqual(1, built.solidVoxels);
            Assert.AreEqual(6, built.faces);
            Assert.AreEqual(24, built.mesh.vertexCount);
            Assert.AreEqual(36, built.mesh.triangles.Length);
            Assert.AreEqual(new Vector3(1f, 1f, 1f), built.mesh.bounds.size);
            var colours = built.mesh.colors32;
            Assert.AreEqual(10, colours[0].r);
            Assert.AreEqual(20, colours[0].g);
            Assert.AreEqual(30, colours[0].b);
            UnityEngine.Object.DestroyImmediate(built.mesh);
        }

        [Test]
        public void MarkerVoxelIsTransparentOnlyWhenTheModelNeedsIt()
        {
            // 3x1x1: solid, red marker, solid. Marker rule D61: the marker voxel hides the two faces
            // that would touch it, and the markers themselves are not drawn.
            byte[] Blob() => Cubdec(3, 1, 1, (x, y, z) =>
                x == 1 ? new byte[] { 255, 0, 0 } : new byte[] { 40, 50, 60 });
            var transparent = VoxelMesh.Build(Blob(), "markers", true);
            Assert.IsNull(transparent.problem, transparent.problem);
            Assert.AreEqual(2, transparent.solidVoxels);
            Assert.AreEqual(1, transparent.markerVoxels);
            Assert.AreEqual(10, transparent.faces, "each solid voxel loses the face towards the marker");
            var opaque = VoxelMesh.Build(Blob(), "markers_opaque", false);
            Assert.AreEqual(3, opaque.solidVoxels);
            Assert.AreEqual(0, opaque.markerVoxels);
            Assert.AreEqual(14, opaque.faces, "an opaque model draws the marker as a normal voxel");
            UnityEngine.Object.DestroyImmediate(transparent.mesh);
            UnityEngine.Object.DestroyImmediate(opaque.mesh);
        }

        [Test]
        public void MalformedPayloadsAreRejectedNotGuessed()
        {
            var shortBlob = VoxelMesh.Build(new byte[4], "short");
            StringAssert.Contains("CUBDEC_TOO_SHORT", shortBlob.problem);
            var badSize = VoxelMesh.Build(Cubdec(1, 1, 1, (x, y, z) => new byte[] { 1, 1, 1 }).Take(12).ToArray(), "empty");
            StringAssert.Contains("CUBDEC_LENGTH", badSize.problem);
        }
    }

    public class AlphaSpaceTests
    {
        [Test]
        public void AlphaRowVectorTranslationBecomesUnitySwapYZ()
        {
            var alpha = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 3, 4, 5, 1 };
            var unity = AlphaSpace.ToUnity(alpha);
            Assert.AreEqual(new Vector3(3, 5, 4), unity.GetPosition());
            Assert.Less(AlphaSpace.MaxAbsDiff(alpha, AlphaSpace.ToAlpha(unity)), 1e-6f);
        }

        [Test]
        public void MirroredMatrixKeepsNegativeDeterminant()
        {
            var alpha = new float[] { -1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
            Assert.IsTrue(AlphaSpace.Decompose(AlphaSpace.ToUnity(alpha), out _, out _, out var scale));
            Assert.Less(scale.x, 0f);
            Assert.AreEqual(1f, scale.y, 1e-5f);
        }

        [Test]
        public void AnchorRulesMatchTheAcceptedAlphaBlocks()
        {
            var size = new Vector3Int(10, 8, 10);
            Assert.AreEqual(new Vector3(5, 4, 5), AlphaRig.Anchor("center", size, false));
            Assert.AreEqual(new Vector3(5, 8, 5), AlphaRig.Anchor("tail", size, false));
            Assert.AreEqual(new Vector3(5, 0, 5), AlphaRig.Anchor("wing", size, false));
            Assert.AreEqual(new Vector3(5, 4, 5), AlphaRig.Anchor("weapon", size, true));
            Assert.AreEqual(new Vector3(5, 4, 4), AlphaRig.Anchor("weapon", size, false),
                "weapon in the hand sits 4 voxels forward of the grip (Alpha drawModel)");
            var dragon = new Vector3Int(18, 10, 12);
            Assert.AreEqual(new Vector3(9, 2, 6), AlphaRig.Anchor("head_0x65", dragon, false));
            Assert.Throws<ArgumentException>(() => AlphaRig.Anchor("unknown_rule", size, false));
        }
    }

    public class AlphaClipParsingTests
    {
        static string F(float v) => v.ToString("0.0####", CultureInfo.InvariantCulture);

        /// <summary>16 row-major Alpha matrix numbers (row-vector convention, translation in row 3).</summary>
        static string Matrix(float x) =>
            F(1) + "," + F(0) + "," + F(0) + "," + F(0) + ", " +
            F(0) + "," + F(1) + "," + F(0) + "," + F(0) + ", " +
            F(0) + "," + F(0) + "," + F(1) + "," + F(0) + ", " + F(x) + "," + F(0) + "," + F(0) + "," + F(1);

        internal static string WalkClip(string rig, bool loop = true, float duration = 1f, float lastKey = 1f) =>
            "{\"name\":\"walk\",\"status\":\"EMULATED_ALPHA_CODE\",\"rig\":\"" + rig + "\"," +
            "\"param\":\"walk_phase\",\"loop\":" + (loop ? "true" : "false") + ",\"duration\":" + F(duration) + "," +
            "\"phaseRate\":1.0,\"weaponOnBack\":true,\"category\":\"MOTION\",\"visibilityMode\":\"STATIC_ALL\"," +
            "\"parts\":[\"body\",\"head\"],\"capturedSizes\":[1,1,1,1,1,1],\"keys\":[" +
            "{\"x\":0,\"m\":[" + Matrix(1) + "," + Matrix(3) + "]}," +
            "{\"x\":" + F(lastKey) + ",\"m\":[" + Matrix(2) + "," + Matrix(3) + "]}]}";

        [Test]
        public void ClipParsesAndSamplesBetweenKeys()
        {
            var problems = new List<string>();
            var clip = AlphaClip.Parse(WalkClip("alpha_creature/test"), "alpha_creature/test", problems);
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.IsNotNull(clip);
            var poses = new PartPose[2];
            clip.Sample(0.5f, poses);
            Assert.AreEqual(1.5f, poses[0].position.x, 1e-4f);
            Assert.AreEqual(3f, poses[1].position.x, 1e-4f);
        }

        [Test]
        public void ClipRejectsWrongRigAndMissingEndKey()
        {
            var p1 = new List<string>();
            Assert.IsNull(AlphaClip.Parse(WalkClip("alpha_creature/test"), "alpha_creature/other", p1));
            Assert.That(p1, Has.Some.Contains("CLIP_RIG_MISMATCH"));
            var p2 = new List<string>();
            Assert.IsNull(AlphaClip.Parse(WalkClip("alpha_creature/test", false, 1f, 0.5f), "alpha_creature/test", p2));
            Assert.That(p2, Has.Some.Contains("CLIP_END_KEY_MISSING"));
        }

        [Test]
        public void ClipRejectsNonIncreasingKeys()
        {
            var json = WalkClip("alpha_creature/test").Replace("{\"x\":1.0,", "{\"x\":0.0,");
            var problems = new List<string>();
            var clip = AlphaClip.Parse(json, "alpha_creature/test", problems);
            Assert.IsNull(clip);
            Assert.That(problems, Has.Some.Contains("CLIP_KEYS_NOT_INCREASING"));
        }
    }

    public class AlphaContentTablesTests
    {
        [Test]
        public void GeneratedTablesAreStructurallyValid()
        {
            var problems = AlphaContentDatabase.Validate();
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.AreEqual(24, AlphaContentTables.ItemTypes.Length);
            Assert.AreEqual(20, AlphaContentTables.WeaponSubtypes.Length);
            Assert.AreEqual(16, AlphaContentTables.Abilities.Length);
            Assert.AreEqual(156, AlphaContentTables.CreatureKinds.Length);
            Assert.AreEqual(101, AlphaContentTables.ItemActions.Length);
            Assert.AreEqual(67, AlphaContentTables.WorldObjects.Length);
            Assert.AreEqual(133, AlphaContentTables.ItemNames.Length);
            Assert.AreEqual(27, AlphaContentTables.Skills.Length);
            Assert.IsEmpty(AlphaWorldContent.Validate(), string.Join("\n", AlphaWorldContent.Validate()));
            Assert.IsEmpty(AlphaStructurePlacementPlanner.Validate(), string.Join("\n", AlphaStructurePlacementPlanner.Validate()));
            Assert.IsEmpty(AlphaDungeonRules.Validate(), string.Join("\n", AlphaDungeonRules.Validate()));
        }

        [Test]
        public void R3ItemTypeCorrectionIsPreserved()
        {
            // The accepted r3 table corrected the old Boots=5 / Gloves=6 mix-up; a regression here
            // would silently move armour models to the wrong body part.
            Assert.IsTrue(AlphaContentDatabase.TryItemType(5, out var gloves));
            Assert.AreEqual("Gloves", gloves.Name);
            Assert.IsTrue(AlphaContentDatabase.TryItemType(6, out var boots));
            Assert.AreEqual("Boots", boots.Name);
            Assert.IsTrue(AlphaContentDatabase.TryItemType(3, out var weapon));
            Assert.AreEqual("Weapon", weapon.Name);
        }

        [Test]
        public void CreatureCatalogSeparatesCombatFromPassiveObjects()
        {
            Assert.IsEmpty(AlphaCreatureCatalog.Validate(), string.Join("\n", AlphaCreatureCatalog.Validate()));
            Assert.AreEqual(26, AlphaCreatureCatalog.PassiveKinds.Count);
            Assert.AreEqual(130, AlphaCreatureCatalog.CombatKinds.Count);
            Assert.IsTrue(AlphaCreatureCatalog.IsPassiveObject(0x78), "plant object 0x78 is spawned with faction 6");
            Assert.IsFalse(AlphaCreatureCatalog.IsPassiveObject(0x02), "kind 0x02 is a creature");
            Assert.IsTrue(AlphaCreatureCatalog.TryActionClipName(0x00, 3, out var clip), "ElfMale 0x00 launches action 3");
            Assert.AreEqual("action_003", clip);
            Assert.IsFalse(AlphaCreatureCatalog.TryActionClipName(0x00, 22, out _), "action 22 was never launched for kind 0x00");
        }

        [Test]
        public void ItemRulesKeepTwoHandedAndConsumableFacts()
        {
            Assert.IsEmpty(AlphaItemRules.Validate(), string.Join("\n", AlphaItemRules.Validate()));
            CollectionAssert.AreEquivalent(new[] { 15, 16, 17 }, AlphaItemRules.TwoHandedSubtypes());
            Assert.IsTrue(AlphaItemRules.IsTwoHanded(15));
            Assert.IsFalse(AlphaItemRules.IsTwoHanded(0));
            Assert.IsTrue(AlphaItemRules.IsConsumableAction(80));
            Assert.IsTrue(AlphaItemRules.IsConsumableAction(81));
            Assert.IsFalse(AlphaItemRules.IsConsumableAction(79));
            Assert.GreaterOrEqual(AlphaItemRules.RenderedSlots().Count, 6);
            Assert.IsTrue(AlphaContentDatabase.TryItemSlot("0xaa8", out var mainHand));
            Assert.AreEqual("weapon", mainHand.Name);
            Assert.IsTrue(mainHand.Rendered);
            Assert.IsTrue(AlphaContentDatabase.TryItemSlot("0x990", out var offHand));
            Assert.AreEqual("offhand", offHand.Name);
        }

        [Test]
        public void RegionNamesComeFromTheConfirmedAlphaTable()
        {
            Assert.IsTrue(AlphaContentDatabase.TryRegionType(1, out var village));
            Assert.AreEqual("Village", village.Name);
            Assert.IsTrue(AlphaContentDatabase.TryRegionType(3, out var forest));
            Assert.AreEqual("Forest", forest.Name);
            Assert.AreEqual(65536, AlphaContentTables.WorldBlocks);
            Assert.AreEqual(1024, AlphaContentTables.WorldRegionGrid);
        }
        [Test]
        public void ScatterEvidenceIsExtractedButNotAPlacementRule()
        {
            Assert.IsEmpty(AlphaScatterEvidenceValidation.Validate(), string.Join("\n", AlphaScatterEvidenceValidation.Validate()));
            Assert.GreaterOrEqual(AlphaScatterEvidence.Groups.Length, 4);
            StringAssert.Contains("PARTIAL", AlphaScatterEvidence.Status);
            StringAssert.Contains("scatterGroups=", AlphaScatterEvidenceValidation.Summary());
            int seen = 0;
            foreach (var group in AlphaScatterEvidence.Groups) seen += group.ObjectKinds?.Length ?? 0;
            Assert.GreaterOrEqual(seen, 20, "the extracted branches carry real object ids");
        }

        [Test]
        public void ScatterSelectorUsesTheTranslatedAlphaTree()
        {
            Assert.IsEmpty(AlphaScatterSelector.Validate(), string.Join("\n", AlphaScatterSelector.Validate()));
            StringAssert.Contains("TRANSLATED_FROM_CODE", AlphaScatterTree.Status);
            StringAssert.Contains("0x005f56c0", AlphaScatterTree.Symbol);

            var underground = AlphaScatterSelector.Select(0.5, 0.5, 0.5, 0.0, -1, 0);
            Assert.AreEqual(ScatterResolution.TranslatedFromCode, underground.Resolution);
            CollectionAssert.AreEquivalent(new[] { 0x91, 0x92, 0x93, 0x96, 0x98, 0x99, 0x9b, 0x9a }, underground.ObjectKinds);

            var dry = AlphaScatterSelector.Select(0.5, 0.5, 0.0, 0.0, 6, 0);
            CollectionAssert.Contains(dry.ObjectKinds, 0x15);
            CollectionAssert.Contains(dry.ObjectKinds, 0x2e);
            var drySpaced = AlphaScatterSelector.Select(0.5, 0.5, 0.0, 0.5, 6, 0);
            CollectionAssert.Contains(drySpaced.ObjectKinds, 0x2f);

            var flagged = AlphaScatterSelector.Select(0.5, 0.5, 0.5, 0.0, 6, 1);
            Assert.IsNotEmpty(flagged.ObjectKinds, "the (char)param_4 != 0 branch returns its own objects");

            var climate = AlphaScatterSelector.Select(0.5, 0.5, 0.5, 0.0, 6, 0);
            Assert.AreEqual(ScatterResolution.TranslatedFromCode, climate.Resolution);
            Assert.IsNotNull(climate.ObjectKinds);
            StringAssert.Contains("TRANSLATED_FROM_CODE", climate.Rule);
            StringAssert.Contains("scatter dry=", AlphaScatterSelector.Summary());
        }

        [Test]
        public void VegetationPlannerPlacesOnlyStagedKinds()
        {
            Assert.IsEmpty(AlphaVegetationPlanner.Validate(), string.Join("\n", AlphaVegetationPlanner.Validate()));
            var staged = new List<int> { 0x15, 0x2e, 0x2f };
            var problems = new List<string>();
            var dry = AlphaVegetationPlanner.Plan(11u, 1, 2, 0, 0, 0.5, 0.5, 0.0, 0.5, 6, 0, staged, problems);
            Assert.IsNotEmpty(dry);
            foreach (var placement in dry)
            {
                Assert.That(staged, Has.Member(placement.Kind), "only staged Alpha objects are placed");
                StringAssert.StartsWith("scatter:", placement.StableId);
                Assert.That(placement.Source, Does.Contain("TRANSLATED_FROM_CODE"));
            }
            var again = AlphaVegetationPlanner.Plan(11u, 1, 2, 0, 0, 0.5, 0.5, 0.0, 0.5, 6, 0, staged);
            Assert.AreEqual(dry.Count, again.Count);
            for (int i = 0; i < dry.Count; i++)
            {
                Assert.AreEqual(dry[i].StableId, again[i].StableId);
                Assert.AreEqual(dry[i].X, again[i].X);
                Assert.AreEqual(dry[i].Z, again[i].Z);
            }
            var unstagedProblems = new List<string>();
            var none = AlphaVegetationPlanner.Plan(11u, 1, 2, 0, 0, 0.5, 0.5, 0.0, 0.5, 6, 0, new List<int>(), unstagedProblems);
            Assert.IsEmpty(none, "Alpha-selected but unstaged objects are not substituted");
            Assert.That(unstagedProblems, Has.Some.Contains("SCATTER_KIND_NOT_STAGED"));
        }

        [Test]
        public void ContentSummaryReportsWhatIsActuallyLoaded()
        {
            Assert.IsEmpty(AlphaContentSummary.Validate(), string.Join("\n", AlphaContentSummary.Validate()));
            var report = AlphaContentSummary.Report();
            foreach (var token in new[] { "kinds=156", "staged=156", "actions=", "clips=", "itemTypes=24",
                         "weaponAnchors=26", "worldObjects=67", "itemNames=133", "skills=27", "scatterGroups=" })
                StringAssert.Contains(token, report);
            Assert.That(report, Does.Contain("staticObjects=26"));
        }

        [Test]
        public void HeldItemModelsComeFromTheItemIconSweep()
        {
            Assert.IsEmpty(AlphaHeldItemCatalog.Validate(), string.Join("\n", AlphaHeldItemCatalog.Validate()));
            Assert.AreEqual(14, AlphaHeldItemCatalog.Items.Count);
            Assert.AreEqual("life-potion.cub", AlphaHeldItemCatalog.ModelOf(1, 1));
            Assert.AreEqual("cactus-potion.cub", AlphaHeldItemCatalog.ModelOf(1, 2));
            Assert.AreEqual("glider.cub", AlphaHeldItemCatalog.ModelOf(23, 0));
            Assert.AreEqual("boat2.cub", AlphaHeldItemCatalog.ModelOf(23, 1));
            Assert.AreEqual("lantern02.cub", AlphaHeldItemCatalog.ModelOf(24, 7), "lantern is a wildcard subtype");
            Assert.IsNull(AlphaHeldItemCatalog.ModelOf(5, 0), "gloves are worn, not held");
            StringAssert.Contains("heldItems=14", AlphaHeldItemCatalog.Summary());
        }

        [Test]
        public void EquipmentModelsFollowTheSlotAndTierLaw()
        {
            Assert.IsEmpty(AlphaEquipmentCatalog.Validate(), string.Join("\n", AlphaEquipmentCatalog.Validate()));
            Assert.AreEqual(32, AlphaEquipmentCatalog.Items.Count);
            Assert.AreEqual("iron-chest1.cub", AlphaEquipmentCatalog.Items[0].Model);
            Assert.IsTrue(AlphaEquipmentCatalog.TryModel(4, 5, out var silk));
            Assert.AreEqual("silk-chest5.cub", silk.Model);
            Assert.IsTrue(AlphaEquipmentCatalog.TryModel(7, 0, out var shoulder));
            Assert.AreEqual("iron-shoulder1.cub", shoulder.Model);
            Assert.AreEqual(EquipmentSlot.Chest, AlphaEquipmentCatalog.SlotOf(4));
            Assert.AreEqual(EquipmentSlot.Hands, AlphaEquipmentCatalog.SlotOf(5));
            Assert.AreEqual(EquipmentSlot.Feet, AlphaEquipmentCatalog.SlotOf(6));
            Assert.AreEqual(EquipmentSlot.Shoulder, AlphaEquipmentCatalog.SlotOf(7));
            Assert.AreEqual(EquipmentSlot.None, AlphaEquipmentCatalog.SlotOf(3));
            StringAssert.Contains("equipmentItems=32", AlphaEquipmentCatalog.Summary());
        }

        [Test]
        public void NpcServiceFurnitureMapsToContractKinds()
        {
            Assert.IsEmpty(AlphaNpcServices.Validate(), string.Join("\n", AlphaNpcServices.Validate()));
            Assert.IsTrue(AlphaNpcServices.TryServiceFor(21, out var market), "MarketStand1");
            Assert.AreEqual(AlphaServiceKind.Trade, market.Kind);
            Assert.IsTrue(AlphaNpcServices.TryServiceFor(72, out var anvil), "Anvil");
            Assert.AreEqual(AlphaServiceKind.Craft, anvil.Kind);
            Assert.IsTrue(AlphaNpcServices.TryServiceFor(77, out var customize), "CustomizationBench");
            Assert.AreEqual(AlphaServiceKind.Customize, customize.Kind);
            Assert.IsTrue(AlphaNpcServices.TryServiceFor(20, out var bed), "BedTable");
            Assert.AreEqual(AlphaServiceKind.Rest, bed.Kind);
            Assert.IsTrue(AlphaNpcServices.TryServiceFor(71, out var furnace), "Furnace");
            Assert.AreEqual(AlphaServiceKind.Smelt, furnace.Kind);
            Assert.IsFalse(AlphaNpcServices.TryServiceFor(1, out _), "a door is not a service");
            Assert.GreaterOrEqual(AlphaNpcServices.All().Count, 8);
            StringAssert.Contains("UNKNOWN", AlphaNpcServices.Status);
        }
    }

    public class AlphaDungeonContentTests
    {
        [Test]
        public void DungeonRulesAndLayoutAreDeterministic()
        {
            Assert.IsEmpty(AlphaDungeonRules.Validate(), string.Join("\n", AlphaDungeonRules.Validate()));
            Assert.AreEqual(10, AlphaDungeonRules.ChestItemCount, "a dungeon chest holds exactly 10 items");
            CollectionAssert.Contains(AlphaDungeonRules.EntranceNames, "underground-dungeon01");

            var problems = new List<string>();
            var a = AlphaDungeonLayoutPlanner.Plan(11u, 6, 0, 0, problems);
            var b = AlphaDungeonLayoutPlanner.Plan(11u, 6, 0, 0, new List<string>());
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.AreEqual(6, a.Rooms.Count);
            Assert.AreEqual(5, a.Corridors.Count);
            for (int i = 0; i < a.Rooms.Count; i++)
            {
                Assert.AreEqual(a.Rooms[i].X, b.Rooms[i].X);
                Assert.AreEqual(a.Rooms[i].Z, b.Rooms[i].Z);
            }
            for (int i = 1; i < a.Rooms.Count; i++)
            {
                int dx = Math.Abs(a.Rooms[i].X - a.Rooms[i - 1].X);
                int dz = Math.Abs(a.Rooms[i].Z - a.Rooms[i - 1].Z);
                Assert.AreEqual(AlphaDungeonRules.RoomStep, dx + dz, "consecutive rooms are one step apart (Q-16)");
                var corridor = a.Corridors[i - 1];
                Assert.AreEqual(a.Rooms[i - 1].Index, corridor.FromIndex);
                Assert.AreEqual(a.Rooms[i].Index, corridor.ToIndex);
                Assert.IsNotEmpty(corridor.Cells);
                var last = corridor.Cells[corridor.Cells.Count - 1];
                Assert.AreEqual(a.Rooms[i].X, last.X);
                Assert.AreEqual(a.Rooms[i].Z, last.Z);
                var first = corridor.Cells[0];
                Assert.AreEqual(1, Math.Abs(first.X - a.Rooms[i - 1].X) + Math.Abs(first.Z - a.Rooms[i - 1].Z),
                    "the corridor starts on a cell adjacent to its room");
            }

            var c = AlphaDungeonLayoutPlanner.Plan(999u, 6, 0, 0, new List<string>());
            Assert.AreNotEqual(string.Join(",", a.Rooms.ConvertAll(r => r.X + ":" + r.Z)),
                string.Join(",", c.Rooms.ConvertAll(r => r.X + ":" + r.Z)), "the seed changes the layout");

            var bad = new List<string>();
            AlphaDungeonLayoutPlanner.Plan(1u, 0, 0, 0, bad);
            Assert.That(bad, Has.Some.Contains("DUNGEON_ROOM_COUNT"));
        }
    }

    public class AlphaWorldContentTests
    {
        [Test]
        public void WorldTablesHoldAlphaNamesAndCategories()
        {
            Assert.IsEmpty(AlphaWorldContent.Validate(), string.Join("\n", AlphaWorldContent.Validate()));
            Assert.IsTrue(AlphaWorldContent.TryObject(1, out var door));
            Assert.AreEqual("Door", door.Name);
            Assert.AreEqual(AlphaWorldObjectCategory.StructurePart, AlphaWorldContent.CategoryOf(1));
            Assert.AreEqual(AlphaWorldObjectCategory.Container, AlphaWorldContent.CategoryOf(10), "Chest");
            Assert.AreEqual(AlphaWorldObjectCategory.Service, AlphaWorldContent.CategoryOf(76), "Workbench");
            Assert.AreEqual(AlphaWorldObjectCategory.Trap, AlphaWorldContent.CategoryOf(6), "FireTrap");
            Assert.IsTrue(AlphaWorldContent.TryItemName(1, 1, out var potion));
            Assert.AreEqual("LifePotion", potion.Name);
            Assert.IsTrue(AlphaWorldContent.TryItemName(0x17, 0, out var glider));
            Assert.AreEqual("HangGlider", glider.Name);
            Assert.IsTrue(AlphaWorldContent.TrySkill(2, out var climb));
            Assert.AreEqual("SkillClimbing", climb.Name);
            Assert.GreaterOrEqual(AlphaWorldContent.ServiceObjects().Count, 8);
        }

        [Test]
        public void StructureTemplatesAndPlacementAreDeterministic()
        {
            Assert.IsEmpty(AlphaStructurePlacementPlanner.Validate(), string.Join("\n", AlphaStructurePlacementPlanner.Validate()));
            Assert.IsTrue(AlphaStructureTemplates.TryGet(AlphaStructureTemplates.HouseId, out var house));
            Assert.AreEqual(3, house.Width);
            Assert.AreEqual(3, house.Depth);
            Assert.AreEqual(4, house.Height);
            Assert.AreEqual(1, house.StyleFirst);
            Assert.AreEqual(5, house.StyleLast);
            Assert.IsTrue(AlphaStructureTemplates.TryGet(AlphaStructureTemplates.DungeonRoomId, out var room));
            Assert.AreEqual(3, room.Height);

            var templates = new List<string> { AlphaStructureTemplates.HouseId, AlphaStructureTemplates.DungeonRoomId };
            var problems = new List<string>();
            var a = AlphaStructurePlacementPlanner.Plan(7u, 10, 20, 3, 4, templates, (x, z, w, d) => true, problems);
            var b = AlphaStructurePlacementPlanner.Plan(7u, 10, 20, 3, 4, templates, (x, z, w, d) => true, new List<string>());
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].StableId, b[i].StableId);
                Assert.AreEqual(a[i].X, b[i].X);
                Assert.AreEqual(a[i].Z, b[i].Z);
                Assert.AreEqual(a[i].Rotation, b[i].Rotation);
                Assert.GreaterOrEqual(a[i].Rotation, 0);
                Assert.LessOrEqual(a[i].Rotation, 3);
                Assert.Greater(a[i].Width, 0);
            }

            var blockedProblems = new List<string>();
            var blocked = AlphaStructurePlacementPlanner.Plan(7u, 10, 20, 3, 4, templates, (x, z, w, d) => false, blockedProblems);
            Assert.IsEmpty(blocked, "no building is forced onto an occupied footprint");
            Assert.That(blockedProblems, Has.Some.Contains("STRUCTURE_NO_FREE_FOOTPRINT"));

            var unknownProblems = new List<string>();
            AlphaStructurePlacementPlanner.Plan(1u, 0, 0, 0, 0, new List<string> { "castle_unknown" }, (x, z, w, d) => true, unknownProblems);
            Assert.That(unknownProblems, Has.Some.Contains("STRUCTURE_TEMPLATE_UNKNOWN"));
        }
    }

    public class AlphaItemAnchorTests
    {
        [Test]
        public void WeaponAnchorLawsCoverEverySubtype()
        {
            Assert.IsEmpty(AlphaItemRules.Validate(), string.Join("\n", AlphaItemRules.Validate()));
            Assert.AreEqual(26, AlphaItemRules.WeaponAnchorSets.Count);
            Assert.AreEqual(10, AlphaItemRules.OffhandAnchorSets.Count);

            Assert.IsTrue(AlphaItemRules.TryWeaponAnchors(0, out var sword));
            Assert.IsTrue(AlphaItemRules.IsAnchorMeasured(sword.InHand));
            sword.InHand.Anchor(10, 8, 4, out var x, out var y, out var z);
            Assert.AreEqual(5f, x, 1e-6f);
            Assert.AreEqual(4f, y, 1e-6f);
            Assert.AreEqual(4f, z, 1e-6f, "the generic hand anchor is 4 voxels forward of the grip");

            Assert.IsTrue(AlphaItemRules.TryWeaponAnchors(6, out var bow));
            bow.InHand.Anchor(10, 10, 20, out x, out y, out z);
            Assert.AreEqual(5f, x, 1e-6f);
            Assert.AreEqual(8.5f, y, 1e-6f, "bow: fVar23 = 1.5 - sy");
            Assert.AreEqual(10f, z, 1e-6f);

            Assert.IsFalse(AlphaItemRules.TryWeaponAnchors(99, out _));
        }

        [Test]
        public void OffhandLawsKeepShieldAndBraceletRules()
        {
            Assert.IsTrue(AlphaItemRules.TryOffhandAnchors(13, out var shield));
            shield.InHand.Anchor(6, 8, 10, out var x, out var y, out var z);
            Assert.AreEqual(3f, x, 1e-6f);
            Assert.AreEqual(3f, y, 1e-6f, "shield: fVar23 = 3.0");
            Assert.AreEqual(5f, z, 1e-6f);

            Assert.IsTrue(AlphaItemRules.TryOffhandAnchors(12, out var bracelet));
            Assert.AreEqual(0.6f, bracelet.InHand.Scale, 1e-6f, "bracelet is drawn at 0.6 scale");

            Assert.IsTrue(AlphaItemRules.IsAnchorMeasured(shield.InHand));
            Assert.IsFalse(AlphaItemRules.IsAnchorMeasured(default), "an empty law is not a measurement");
            Assert.GreaterOrEqual(AlphaWeaponLayer.EquippableInHand().Count, 20);
        }
    }

    public class AlphaContentCacheTests
    {
        static AlphaContentSource Source(bool requireReady = true)
        {
            var source = AlphaContentSource.Resolve();
            if (requireReady && source.State != AlphaContentState.Ready)
                Assert.Ignore(source.Diagnosis + " — the local Alpha cache is not staged here");
            return source;
        }

        [Test]
        public void CacheDiagnosticsLoadsEveryStagedActor()
        {
            var report = AlphaContentDiagnostics.Inspect();
            if (report.State == AlphaContentState.NotConfigured) Assert.Ignore(report.Diagnosis);
            Assert.IsEmpty(report.Problems, report.Detail());
            Assert.GreaterOrEqual(report.Actors.Count, 1);
        }

        [Test]
        public void HumanActorHasRigClipsContractAndActions()
        {
            var def = Source().Load("d86_human_0x02_greatsword");
            Assert.IsEmpty(def.Problems, string.Join("\n", def.Problems));
            Assert.IsTrue(def.Ready);
            Assert.AreEqual(8, def.Rig.parts.Length);
            Assert.AreEqual("ACTIONS", def.ControllerMode);
            Assert.GreaterOrEqual(def.Clips.Count, 90, "the accepted S05A-2 human library has 90+ clips");
            Assert.IsNotNull(def.Clip("walk"));
            Assert.IsNotNull(def.Clip("action_003"));
            Assert.IsTrue(def.HasContract);
            Assert.AreEqual(def.Rig.rig, def.Contract.rig);
            foreach (var part in def.Contract.parts)
                Assert.GreaterOrEqual(Array.FindIndex(def.Rig.parts, p => p.name == part.name), 0,
                    "contract part not in rig: " + part.name);
        }

        [Test]
        public void DragonUsesTheD83HeadAnchor()
        {
            var def = Source().Load("d86_dragon_0x65");
            Assert.IsEmpty(def.Problems, string.Join("\n", def.Problems));
            var head = Array.Find(def.Rig.parts, p => p.name == "head");
            Assert.IsNotNull(head);
            Assert.AreEqual("head_0x65", head.anchor);
            Assert.AreEqual("ACTIONS", def.ControllerMode);
        }

        [Test]
        public void CreatureWithoutActionProfileIsLocomotionOnly()
        {
            var def = Source().Load("whitecat_0x20_formula_vs_alpha");
            Assert.IsEmpty(def.Problems, string.Join("\n", def.Problems));
            Assert.AreEqual("LOCOMOTION_ONLY", def.ControllerMode);
            Assert.AreEqual(0, def.RequiredActions.Length);
        }

        [Test]
        public void BrokenActorIsReportedWithPreciseReasons()
        {
            var root = Path.Combine(Application.temporaryCachePath, "alpha_content_negative");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            var dir = Path.Combine(root, "actors", "broken");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "rig.json"),
                "{\"rig\":\"alpha_creature/none\",\"variant\":\"broken\",\"weaponSubtype\":-1,\"parts\":[" +
                "{\"name\":\"body\",\"model\":\"b.cub\",\"file\":\"models/body.cubdec\",\"size\":[1,1,1]," +
                "\"anchor\":\"center\",\"mirrored\":false,\"expectedVertices\":24,\"loadFlag\":1," +
                "\"markerTransparent\":false,\"markers\":[],\"tintMode\":\"NONE\",\"tint\":[]}]}");
            var source = AlphaContentSource.Resolve(root);
            Assert.AreEqual(AlphaContentState.Ready, source.State);
            var def = source.Load("broken");
            Assert.IsFalse(def.Ready);
            Assert.That(def.Problems, Has.Some.Contains("ACTOR_MODEL_MISSING body"));
            Assert.That(def.Problems, Has.Some.Contains("ACTOR_NO_CLIPS"));
            var missing = source.Load("does_not_exist");
            Assert.That(missing.Problems, Has.Some.Contains("ACTOR_DIRECTORY_MISSING"));

            File.WriteAllText(Path.Combine(dir, "rig.json"),
                "{\"rig\":\"alpha_creature/none\",\"variant\":\"broken\",\"weaponSubtype\":-1,\"parts\":[" +
                "{\"name\":\"body\",\"model\":\"b.cub\",\"file\":\"../escape.cubdec\",\"size\":[1,1,1]," +
                "\"anchor\":\"center\",\"mirrored\":false,\"expectedVertices\":24,\"loadFlag\":1," +
                "\"markerTransparent\":false,\"markers\":[],\"tintMode\":\"NONE\",\"tint\":[]}]}");
            var escape = source.Load("broken");
            Assert.That(escape.Problems, Has.Some.Contains("ACTOR_MODEL_OUTSIDE_ACTOR"));
            Directory.Delete(root, true);
        }
    }
}
