using System.Collections.Generic;
using System.IO;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using CubeGame.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    public sealed class FullM0PersistenceTests
    {
        private const string Generator = "test.gen/1";

        private sealed class MemoryStore : ISaveStore
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();

            public bool Exists(string key) => Files.ContainsKey(key);

            public string Read(string key) => Files.TryGetValue(key, out string value) ? value : null;

            public void Write(string key, string contents) => Files[key] = contents;

            public void Delete(string key) => Files.Remove(key);
        }

        private static GameSlotStore Slots(ISaveStore store) =>
            new GameSlotStore(store, new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());

        private static GameSession RunningSession(uint seed, CharacterClass heroClass, string slot = "slot0")
        {
            var session = new GameSession();
            session.EnterMainMenu();
            Assert.IsTrue(session.BeginNewWorld(slot, new NewWorldRequest
            {
                WorldName = "Persist",
                Seed = seed,
                GeneratorId = Generator,
                HeroName = "Bo",
                Class = heroClass,
                Spawn = new WorldPosition(10.5, 40.25, -3.75),
            }, 123L));
            Assert.IsTrue(session.CompleteLoading());
            return session;
        }

        [Test]
        public void CheckpointRoundTripRestoresWorldHeroClockAndItems()
        {
            var store = new MemoryStore();
            GameSlotStore slots = Slots(store);
            GameSession session = RunningSession(4000000000u, CharacterClass.Rogue);
            session.Clock.StepTicks(451);
            session.Hero.Sheet.AddExperience(5);
            session.Hero.Yaw = 33.5f;
            var ranks = new[] { 2, 0, 7, 6, 3, 0, 1, 9, 0, 0, 4 };
            Assert.IsTrue(session.Hero.SkillRanks.TryReplace(ranks));
            Assert.IsTrue(ItemInstance.TryCreateFromIdentifiers((int)ItemType.Weapon, 3, 4, 2, 99, 0, 0, 1, out ItemInstance sword, out string error), error);
            Assert.IsTrue(session.Hero.Inventory.TryAdd(Features.Items.InventoryEntry.FromCore(sword)), session.Hero.Inventory.LastRejection);
            session.Hero.Health.ApplyDamage(17.25);
            double heroHp = session.Hero.Health.CurrentHealth;

            SaveResult saved = slots.Save(session, 999L);
            Assert.IsTrue(saved.IsSuccess, saved.ToString());
            Assert.IsTrue(slots.Exists("slot0"));

            SlotLoadResult loaded = slots.Load("slot0", Generator);
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            Assert.AreEqual(4000000000u, loaded.World.Seed.Value);
            Assert.AreEqual("Persist", loaded.World.Name);
            Assert.AreEqual(451L, loaded.ClockTick);
            Assert.AreEqual(CharacterClass.Rogue, loaded.Hero.Sheet.Class);
            Assert.AreEqual(session.Hero.Sheet.Progression.Experience, loaded.Hero.Sheet.Progression.Experience);
            Assert.AreEqual(new WorldPosition(10.5, 40.25, -3.75), loaded.Hero.Position);
            Assert.AreEqual(33.5f, loaded.Hero.Yaw);
            CollectionAssert.AreEqual(ranks, loaded.Hero.SkillRanks.ToArray());
            // C1: a new hero carries the Alpha start kit, so the added sword is the last Core item.
            Assert.AreEqual(session.Hero.Items.Count, loaded.Hero.Items.Count);
            Assert.AreEqual(sword, loaded.Hero.Items[loaded.Hero.Items.Count - 1]);
            Assert.AreEqual(heroHp, loaded.Hero.Health.CurrentHealth, 1e-9, "current HP survives the checkpoint");
            Assert.IsNotNull(loaded.Hero.Loadout.GetEquipped(Core.Equipment.EquipmentSlot.MainHand), "first weapon re-equipped");
            Assert.IsFalse(loaded.Hero.NeedsGroundPlacement);

            var restored = new GameSession();
            restored.EnterMainMenu();
            Assert.IsTrue(restored.BeginLoadedWorld("slot0", loaded.World, loaded.Hero, loaded.ClockTick));
            Assert.IsTrue(restored.CompleteLoading());
            Assert.AreEqual(451L, restored.Clock.Tick);
        }

        [Test]
        public void SessionWrittenBeforeHeroHealthFieldMeansFullHealth()
        {
            // A session payload of format v1 written before M0-05A has no heroHealth field.
            var legacy = new SessionSaveData { heroName = "Old", worldName = "W", generatorId = Generator };
            string json = new JsonSessionSerializer().Serialize(legacy).Replace(",\"heroHealth\":-1.0", string.Empty);
            StringAssert.DoesNotContain("heroHealth", json);
            SessionSaveData read = new JsonSessionSerializer().Deserialize(json);
            Assert.IsNotNull(read);
            Assert.AreEqual(-1.0, read.heroHealth, "missing field keeps the 'full HP' sentinel");
            Assert.AreEqual(SessionSaveFormat.CurrentVersion, read.formatVersion);
        }

        [Test]
        public void LegacySessionWithoutProgressionFieldsLoadsWithDefaults()
        {
            var store = new MemoryStore();
            GameSlotStore slots = Slots(store);
            Assert.IsTrue(slots.Save(RunningSession(5u, CharacterClass.Warrior), 1L).IsSuccess);
            const string key = "slot0.session";
            Assert.IsTrue(GameSlotStore.TryUnframe(store.Files[key], SessionSaveFormat.Header, out int version, out string payload, out string error), error);
            string legacy = payload.Replace(",\"heroSkillRanks\":[0,0,0,0,0,0,0,0,0,0,0]", string.Empty)
                .Replace(",\"heroCopper\":0", string.Empty).Replace(",\"heroManaCubes\":0", string.Empty)
                .Replace(",\"huntMissions\":[]", string.Empty).Replace(",\"worldTimeOffsetMs\":0", string.Empty);
            foreach (string field in new[] { "heroSkillRanks", "heroCopper", "heroManaCubes", "huntMissions", "worldTimeOffsetMs" })
            {
                StringAssert.DoesNotContain(field, legacy);
            }
            store.Files[key] = GameSlotStore.Frame(SessionSaveFormat.Header, version, legacy);

            SlotLoadResult loaded = slots.Load("slot0", Generator);
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            CollectionAssert.AreEqual(new int[11], loaded.Hero.SkillRanks.ToArray());
            Assert.AreEqual(0, loaded.Hero.Copper);
            Assert.AreEqual(0, loaded.Hero.ManaCubes);
            Assert.IsEmpty(loaded.Hero.Missions.Hunts);
            Assert.AreEqual(0L, loaded.WorldTimeOffsetMs);
        }

        [TestCase("[1]")]
        [TestCase("[0,0,0,0,-1,0,0,0,0,0,0]")]
        public void InvalidSavedRanksAreRejected(string invalidRanks)
        {
            var store = new MemoryStore();
            GameSlotStore slots = Slots(store);
            Assert.IsTrue(slots.Save(RunningSession(5u, CharacterClass.Warrior), 1L).IsSuccess);
            const string key = "slot0.session";
            Assert.IsTrue(GameSlotStore.TryUnframe(store.Files[key], SessionSaveFormat.Header, out int version, out string payload, out string error), error);
            string damaged = payload.Replace("\"heroSkillRanks\":[0,0,0,0,0,0,0,0,0,0,0]", "\"heroSkillRanks\":" + invalidRanks);
            Assert.AreNotEqual(payload, damaged);
            store.Files[key] = GameSlotStore.Frame(SessionSaveFormat.Header, version, damaged);

            SlotLoadResult loaded = slots.Load("slot0", Generator);
            Assert.AreEqual(SlotLoadStatus.Corrupted, loaded.Status, loaded.ToString());
            StringAssert.Contains("skill ranks", loaded.Message);
        }

        [Test]
        public void CharacterFileStaysCoreV1AndLoadsWithUnchangedSaveService()
        {
            var store = new MemoryStore();
            Slots(store).Save(RunningSession(5u, CharacterClass.Warrior), 1L);

            StringAssert.StartsWith("CUBEGAME_SAVE|1|", store.Files["slot0"]);
            StringAssert.StartsWith("CUBEGAME_SESSION|1|", store.Files["slot0.session"]);
            LoadResult v1 = new SaveService(store, new JsonSaveSerializer(), "slot0").Load();
            Assert.IsTrue(v1.IsSuccess, v1.ToString());
        }

        [Test]
        public void LegacyV1SaveWithoutSessionIsNotASlotButIsPreserved()
        {
            var store = new MemoryStore();
            var legacy = new SaveService(store, new JsonSaveSerializer(), "slot1");
            Assert.IsTrue(legacy.Save(CharacterSaveMapper.ToSaveGame(new CharacterSheet())).IsSuccess);

            GameSlotStore slots = Slots(store);
            Assert.IsFalse(slots.Exists("slot1"));
            Assert.AreEqual(SlotLoadStatus.NotFound, slots.Load("slot1", Generator).Status);
            Assert.IsTrue(store.Exists("slot1"), "loading must not delete the legacy file");
        }

        [Test]
        public void InterruptedCheckpointIsDetectedAsPairMismatch()
        {
            var store = new MemoryStore();
            GameSlotStore slots = Slots(store);
            GameSession session = RunningSession(9u, CharacterClass.Ranger);
            Assert.IsTrue(slots.Save(session, 1L).IsSuccess);
            string firstSession = store.Files["slot0.session"];

            session.Hero.Sheet.AddExperience(7);
            Assert.IsTrue(slots.Save(session, 2L).IsSuccess);
            store.Files["slot0.session"] = firstSession; // crash after the character write of checkpoint 2

            Assert.AreEqual(SlotLoadStatus.PairMismatch, slots.Load("slot0", Generator).Status);
        }

        [Test]
        public void DamagedTruncatedForeignAndMismatchedFilesAreRefused()
        {
            var store = new MemoryStore();
            GameSlotStore slots = Slots(store);
            Assert.IsTrue(slots.Save(RunningSession(9u, CharacterClass.Mage), 1L).IsSuccess);
            string good = store.Files["slot0.session"];

            store.Files["slot0.session"] = good.Replace("\"heroName\":\"Bo\"", "\"heroName\":\"Bx\"");
            Assert.AreEqual(SlotLoadStatus.Corrupted, slots.Load("slot0", Generator).Status, "checksum");

            store.Files["slot0.session"] = good.Substring(0, good.Length - 5);
            Assert.AreEqual(SlotLoadStatus.Corrupted, slots.Load("slot0", Generator).Status, "truncated");

            store.Files["slot0.session"] = good.Replace("CUBEGAME_SESSION|1|", "CUBEGAME_SESSION|2|");
            Assert.AreEqual(SlotLoadStatus.UnsupportedVersion, slots.Load("slot0", Generator).Status);

            store.Files["slot0.session"] = good;
            Assert.AreEqual(SlotLoadStatus.GeneratorMismatch, slots.Load("slot0", "other.gen/2").Status);
            Assert.IsTrue(slots.Load("slot0", Generator).IsSuccess);
        }

        [Test]
        public void FileStoreCheckpointSurvivesRealDisk()
        {
            string dir = Path.Combine(Path.GetTempPath(), "cubegame_fullm0_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var slots = new GameSlotStore(new FileSaveStore(dir), new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());
                GameSession session = RunningSession(77u, CharacterClass.Warrior, "slot2");
                session.Clock.StepTicks(30);
                Assert.IsTrue(slots.Save(session, 5L).IsSuccess);
                Assert.IsTrue(slots.Save(session, 6L).IsSuccess, "overwrite through temp+replace");
                SlotLoadResult loaded = slots.Load("slot2", Generator);
                Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
                Assert.AreEqual(30L, loaded.ClockTick);
                Assert.IsTrue(File.Exists(Path.Combine(dir, "slot2.session.sav")));
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        [Test]
        public void SaveWithoutRunningWorldIsRefused()
        {
            var session = new GameSession();
            session.EnterMainMenu();
            Assert.AreEqual(SaveStatus.ValidationFailed, Slots(new MemoryStore()).Save(session, 0L).Status);
        }
    }

    public sealed class FullM0AssemblyGraphTests
    {
        [System.Serializable]
        private sealed class AsmDef
        {
            public string name;
            public string[] references;
            public bool noEngineReferences;
        }

        private static AsmDef Read(string relative)
        {
            string path = Path.Combine(Application.dataPath, relative);
            Assert.IsTrue(File.Exists(path), path);
            return JsonUtility.FromJson<AsmDef>(File.ReadAllText(path));
        }

        [Test]
        public void FeaturesIsEngineFreeAndDependsOnlyOnCore()
        {
            AsmDef features = Read("CubeGame/Features/CubeGame.Features.asmdef");
            Assert.IsTrue(features.noEngineReferences);
            CollectionAssert.AreEqual(new[] { "CubeGame.Core" }, features.references);

            AsmDef core = Read("CubeGame/Core/CubeGame.Core.asmdef");
            Assert.IsTrue(core.noEngineReferences);
            Assert.AreEqual(0, core.references.Length, "Core must not reference anything (ADR-0001)");
        }

        [Test]
        public void GameplayIsTheOnlyLayerJoiningFeaturesAndPresentation()
        {
            AsmDef presentation = Read("CubeGame/Presentation/CubeGame.Presentation.asmdef");
            CollectionAssert.DoesNotContain(presentation.references, "CubeGame.Features");
            CollectionAssert.DoesNotContain(presentation.references, "CubeGame.Gameplay");

            AsmDef gameplay = Read("CubeGame/Presentation/Gameplay/CubeGame.Gameplay.asmdef");
            CollectionAssert.IsSubsetOf(new[] { "CubeGame.Features", "CubeGame.Presentation" }, gameplay.references);
        }
    }
}
