using System.Collections.Generic;
using CubeGame.Core.Characters;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using CubeGame.Persistence;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// M0 P0 save-selection semantics recovered from Cube.exe: <c>characters.db</c> and <c>worlds.db</c> are two
    /// independent lists (GameController ctor reads <c>num</c> of each store), and a character keeps its own position
    /// per world, keyed by seed + world name (character blob fields 18/19, <c>WorldInfo_mapInsertUnique</c> 0x44B460).
    /// </summary>
    public sealed class AlphaProfileStoreTests
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

        private static AlphaProfileStore Profiles(ISaveStore store) =>
            new AlphaProfileStore(store, new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());

        private static HeroState Hero(string name, CharacterClass heroClass)
        {
            var sheet = new CharacterSheet(heroClass, CharacterSpecialization.Primary);
            var hero = new HeroState(name, 0, sheet) { Appearance = AlphaHeroCreation.Default };
            hero.GiveStarterKit();
            return hero;
        }

        private static AlphaWorldRecord World(AlphaProfileStore profiles, string name, uint seed, long now)
        {
            var world = new AlphaWorldRecord { Name = name, Seed = seed, GeneratorId = Generator, CreatedUtcTicks = now };
            Assert.IsTrue(profiles.AddWorld(world, now).IsSuccess);
            return world;
        }

        [Test]
        public void CharactersAndWorldsAreTwoIndependentLists()
        {
            var store = new MemoryStore();
            AlphaProfileStore profiles = Profiles(store);
            Assert.IsTrue(profiles.Reload());

            AlphaWorldRecord first = World(profiles, "Alpha", 1001, 1);
            AlphaWorldRecord second = World(profiles, "Beta", 1002, 2);
            AlphaCharacterRecord warrior = new AlphaCharacterRecord { Name = "Bo" };
            AlphaCharacterRecord ranger = new AlphaCharacterRecord { Name = "Ayla" };
            Assert.IsTrue(profiles.SaveCharacter(warrior, Hero("Bo", CharacterClass.Warrior), new SessionSaveData(), 3).IsSuccess);
            Assert.IsTrue(profiles.SaveCharacter(ranger, Hero("Ayla", CharacterClass.Ranger), new SessionSaveData(), 4).IsSuccess);

            AlphaProfileStore reloaded = Profiles(store);
            Assert.IsTrue(reloaded.Reload());
            Assert.AreEqual(2, reloaded.Characters.Count, "characters.db num");
            Assert.AreEqual(2, reloaded.Worlds.Count, "worlds.db num");

            // The world list is shared: both characters see both worlds, no character/world pairing.
            Assert.AreEqual("Alpha", reloaded.WorldById(first.Id).Name);
            Assert.AreEqual("Beta", reloaded.WorldById(second.Id).Name);
            Assert.AreEqual(CharacterClass.Warrior, reloaded.CharacterById(warrior.Id).Class);
            Assert.AreEqual(CharacterClass.Ranger, reloaded.CharacterById(ranger.Id).Class);

            string index = store.Read(AlphaProfileStore.CharactersIndexKey);
            StringAssert.Contains("num=2", index);
            StringAssert.StartsWith("CUBEGAME_CHARACTERS 1", index);
            StringAssert.Contains("num=2", store.Read(AlphaProfileStore.WorldsIndexKey));
            Assert.IsTrue(store.Exists(AlphaProfileStore.CharacterKey(warrior.Id)));
            Assert.IsTrue(store.Exists(AlphaProfileStore.CharacterSessionKey(warrior.Id)));
        }

        [Test]
        public void CharacterKeepsOnePositionPerWorldKey()
        {
            var store = new MemoryStore();
            AlphaProfileStore profiles = Profiles(store);
            profiles.Reload();
            AlphaWorldRecord alpha = World(profiles, "Alpha", 1001, 1);
            AlphaWorldRecord beta = World(profiles, "Beta", 1002, 2);
            AlphaCharacterRecord hero = new AlphaCharacterRecord { Name = "Bo" };
            HeroState state = Hero("Bo", CharacterClass.Warrior);
            Assert.IsTrue(profiles.SaveCharacter(hero, state, new SessionSaveData(), 3).IsSuccess);

            hero.Remember(alpha, new WorldPosition(10.5, 40.25, -3.75), 12f, 10);
            Assert.IsTrue(profiles.SaveCharacter(hero, state, new SessionSaveData(), 10).IsSuccess);
            hero.Remember(beta, new WorldPosition(-500.5, 70.5, 900.25), 200f, 20);
            Assert.IsTrue(profiles.SaveCharacter(hero, state, new SessionSaveData(), 20).IsSuccess);

            AlphaProfileStore reloaded = Profiles(store);
            reloaded.Reload();
            AlphaCharacterLoad reloadedCharacter = reloaded.LoadCharacter(hero.Id);
            Assert.IsTrue(reloadedCharacter.IsSuccess, reloadedCharacter.Message);
            AlphaCharacterRecord loaded = reloadedCharacter.Record;
            Assert.AreEqual(2, loaded.Visits.Count, "per-world position map");
            AlphaWorldVisit inAlpha = loaded.FindVisit(alpha);
            Assert.IsNotNull(inAlpha);
            Assert.AreEqual(10.5, inAlpha.Position.X, 1e-9);
            Assert.AreEqual(40.25, inAlpha.Position.Y, 1e-9);
            Assert.AreEqual(-3.75, inAlpha.Position.Z, 1e-9);
            Assert.AreEqual(12f, inAlpha.Position.Yaw);
            AlphaWorldVisit inBeta = loaded.FindVisit(beta);
            Assert.IsNotNull(inBeta);
            Assert.AreEqual(-500.5, inBeta.Position.X, 1e-9);
            Assert.AreEqual(200f, inBeta.Position.Yaw);
            Assert.AreEqual("Beta", loaded.LastWorldName);
            Assert.AreEqual(1002L, loaded.LastWorldSeed);
        }

        [Test]
        public void PositionMapKeyIsSeedAndWorldName()
        {
            var store = new MemoryStore();
            AlphaProfileStore profiles = Profiles(store);
            profiles.Reload();
            AlphaWorldRecord sameSeedOtherName = World(profiles, "Alpha", 777, 1);
            AlphaWorldRecord otherSeed = World(profiles, "Beta", 778, 2);
            AlphaCharacterRecord hero = new AlphaCharacterRecord { Name = "Bo" };
            HeroState state = Hero("Bo", CharacterClass.Mage);
            profiles.SaveCharacter(hero, state, new SessionSaveData(), 3);

            hero.Remember(sameSeedOtherName, new WorldPosition(1, 2, 3), 0f, 4);
            profiles.SaveCharacter(hero, state, new SessionSaveData(), 4);

            Assert.IsNotNull(hero.FindVisit(777, "Alpha"));
            Assert.IsNull(hero.FindVisit(777, "Beta"), "the name is part of the Alpha key");
            Assert.IsNull(hero.FindVisit(otherSeed), "a character that never entered a world has no position there");
        }

        [Test]
        public void WorldClockAndDeltasBelongToTheWorld()
        {
            var store = new MemoryStore();
            AlphaProfileStore profiles = Profiles(store);
            profiles.Reload();
            AlphaWorldRecord world = World(profiles, "Clock", 4242, 1);
            AlphaWorldRecord untouched = World(profiles, "Fresh", 4243, 2);

            var data = new WorldSaveData
            {
                clockTick = 987654,
                ticksPerSecond = 20,
                worldTimeOffsetMs = 4321,
            };
            Assert.IsTrue(profiles.SaveWorld(world, data, 9).IsSuccess);

            AlphaWorldLoad loaded = profiles.LoadWorld(world.Id, Generator);
            Assert.IsTrue(loaded.IsSuccess, loaded.Message);
            Assert.AreEqual(987654L, loaded.ClockTick);
            Assert.AreEqual(4321L, loaded.WorldTimeOffsetMs);
            Assert.AreEqual(20, loaded.TicksPerSecond);

            AlphaWorldLoad fresh = profiles.LoadWorld(untouched.Id, Generator);
            Assert.IsTrue(fresh.IsSuccess, fresh.Message);
            Assert.AreEqual(0L, fresh.ClockTick, "a world that was never entered starts at tick 0");

            AlphaWorldLoad wrongGenerator = profiles.LoadWorld(world.Id, "other.gen/9");
            Assert.AreEqual(AlphaLoadStatus.GeneratorMismatch, wrongGenerator.Status);
        }

        [Test]
        public void SeedFieldFollowsTheAlphaIntegerParse()
        {
            // 0x482530 parses the seed edit with wistream >> int, without range validation.
            Assert.AreEqual(0u, GameBootstrap.ParseAlphaSeed(string.Empty));
            Assert.AreEqual(0u, GameBootstrap.ParseAlphaSeed("   "));
            Assert.AreEqual(0u, GameBootstrap.ParseAlphaSeed("not a number"));
            Assert.AreEqual(666666u, GameBootstrap.ParseAlphaSeed("666666"));
            Assert.AreEqual(4294967295u, GameBootstrap.ParseAlphaSeed("-1"));
            Assert.AreEqual(0u, GameBootstrap.ParseAlphaSeed("99999999999"));
        }

        [Test]
        public void LegacyPairedSlotIsMigratedIntoCharacterAndWorldLists()
        {
            var store = new MemoryStore();
            var session = new GameSession();
            Assert.IsTrue(session.EnterMainMenu());
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest
            {
                WorldName = "Old World",
                Seed = 666666,
                GeneratorId = Generator,
                HeroName = "S00G0",
                Class = CharacterClass.Warrior,
                Spawn = new WorldPosition(100.5, 60.25, 200.75),
            }, 1234L));
            Assert.IsTrue(session.CompleteLoading());
            session.Clock.StepTicks(55);
            var slots = new GameSlotStore(store, new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());
            Assert.IsTrue(slots.Save(session, 5678L).IsSuccess);

            AlphaProfileStore profiles = Profiles(store);
            Assert.IsTrue(profiles.Reload());
            StringAssert.Contains("migrated slot0", profiles.LastReport);
            Assert.AreEqual(1, profiles.Characters.Count);
            Assert.AreEqual(1, profiles.Worlds.Count);
            Assert.AreEqual("S00G0", profiles.Characters[0].Name);
            Assert.AreEqual("Old World", profiles.Worlds[0].Name);
            Assert.AreEqual(666666u, profiles.Worlds[0].Seed);

            AlphaCharacterLoad hero = profiles.LoadCharacter(profiles.Characters[0].Id);
            Assert.IsTrue(hero.IsSuccess, hero.Message);
            AlphaWorldVisit visit = hero.Record.FindVisit(profiles.Worlds[0]);
            Assert.IsNotNull(visit, "the paired slot position becomes the per-world map entry");
            Assert.AreEqual(100.5, visit.Position.X, 1e-6);
            Assert.AreEqual(60.25, visit.Position.Y, 1e-6);

            AlphaWorldLoad world = profiles.LoadWorld(profiles.Worlds[0].Id, Generator);
            Assert.IsTrue(world.IsSuccess, world.Message);
            Assert.AreEqual(session.Clock.Tick, world.ClockTick, "the world clock moved to the world record");

            // Legacy files stay untouched for the previous live version.
            Assert.IsTrue(store.Exists("slot0"));
            Assert.IsTrue(store.Exists("slot0.session"));
        }
    }
}
