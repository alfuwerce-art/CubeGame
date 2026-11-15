using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    public sealed class FullM0P7LandmarkPatrolSpawnsTests
    {
        [Serializable]
        private sealed class Fixture
        {
            public string status;
            public uint seed;
            public long crtIn;
            public long crtOut;
            public int[] drawSites;
            public Spawn[] spawns;
            public CompositionInput input;
            public long[] points;
        }

        [Serializable]
        private sealed class CompositionInput
        {
            public int zoneX, zoneZ, landmarkType, landmarkId, level, quality;
            public long[] centre;
            public float radius;
        }

        [Serializable]
        private sealed class Spawn
        {
            public int kind, faction, level, quality, flags, leader, classId, specialization, main, off;
            public float yaw, power;
            public long[] position;
            public long[] route;
        }

        private sealed class PatrolProbe : IZoneContentProvider
        {
            public string Key => nameof(PatrolProbe);
            public uint BeforeSpawns { get; private set; }
            public uint AfterSpawns { get; private set; }
            public List<SpawnDefinition> Spawns { get; private set; }
            public List<int> Sites { get; } = new List<int>();

            public void Populate(ZoneContentContext context)
            {
                IReadOnlyList<WorldPosition> points = AlphaLandmarkPatrolPlacement.Populate(context, out _);
                BeforeSpawns = unchecked((uint)context.Random.State);
                context.Random.AfterDraw = stream => Sites.Add(stream.SiteFrom);
                Spawns = AlphaLandmarkPatrolSpawns.Generate(context, points);
                context.Random.AfterDraw = null;
                AfterSpawns = unchecked((uint)context.Random.State);
            }
        }

        [TestCase(32795, 32804)]
        [TestCase(32812, 32812)]
        public void ReproducesAlphaLandmarkSpawnsFromTerrain(int zoneX, int zoneZ)
        {
            Fixture fixture = ReadFixture($"alpha_landmark_patrol_spawns_{zoneX}_{zoneZ}.json");
            var probe = new PatrolProbe();
            var terrain = new AlphaTerrainGenerator(fixture.seed, new[] { probe });
            // The native zone oracle only records the earlier terrain carves in these fixtures.
            AlphaZoneGenerator.Generate(terrain, zoneX, zoneZ, true, new List<CarveCall>());

            Assert.AreEqual(fixture.crtIn, probe.BeforeSpawns);
            AssertOutput(fixture, probe.Sites, probe.AfterSpawns, probe.Spawns);
        }

        [TestCase(6)]
        [TestCase(8)]
        [TestCase(10)]
        public void ReproducesAlphaCompositionOnTheNativeInputGrid(int composition)
        {
            Fixture fixture = ReadFixture($"alpha_landmark_patrol_composition_{composition}.json");
            CompositionInput input = fixture.input;
            var zone = new AlphaZone(input.zoneX, input.zoneZ);
            for (int i = 0; i < zone.Columns.Length; i++)
                zone.Columns[i] = new VoxelColumn();

            var terrain = new AlphaTerrainGenerator(fixture.seed);
            var random = new AlphaCrtRandom(unchecked((int)fixture.crtIn));
            var context = (ZoneContentContext)Activator.CreateInstance(typeof(ZoneContentContext),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { terrain, new AlphaColors(terrain), zone, random }, null);
            LandmarkColumn landmark = context.Landmark;
            void SetLandmark(string property, object value) => typeof(LandmarkColumn).GetProperty(property).SetValue(landmark, value);
            SetLandmark(nameof(LandmarkColumn.Id), input.landmarkId);
            SetLandmark(nameof(LandmarkColumn.Type), (LandmarkType)input.landmarkType);
            SetLandmark(nameof(LandmarkColumn.X), input.centre[0] / 65536.0);
            SetLandmark(nameof(LandmarkColumn.Z), input.centre[1] / 65536.0);
            SetLandmark(nameof(LandmarkColumn.Radius), input.radius);
            SetLandmark(nameof(LandmarkColumn.Level), input.level);
            SetLandmark(nameof(LandmarkColumn.LevelClass), input.quality);

            var points = new List<WorldPosition>();
            for (int i = 0; i < fixture.points.Length; i += 3)
                points.Add(Position(fixture.points, i));
            var sites = new List<int>();
            random.AfterDraw = stream => sites.Add(stream.SiteFrom);
            List<SpawnDefinition> spawns = AlphaLandmarkPatrolSpawns.Generate(context, points);
            AssertOutput(fixture, sites, unchecked((uint)random.State), spawns);
        }

        private static Fixture ReadFixture(string file)
        {
            string path = Path.Combine(Application.dataPath, "CubeGame/Tests/FullM0/EditMode/Fixtures", file);
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
            Assert.AreEqual("EMULATED_ALPHA_CODE", fixture.status);
            return fixture;
        }

        private static void AssertOutput(Fixture fixture, IReadOnlyList<int> sites, uint after, IReadOnlyList<SpawnDefinition> spawns)
        {
            Assert.AreEqual(fixture.drawSites.Length, sites.Count, "draw count");
            for (int i = 0; i < fixture.drawSites.Length; i++)
                Assert.AreEqual(fixture.drawSites[i], sites[i], $"draw {i} return address");
            Assert.AreEqual(fixture.crtOut, after);

            Assert.AreEqual(fixture.spawns.Length, spawns.Count, "spawn count");
            for (int i = 0; i < fixture.spawns.Length; i++)
                AssertSpawn(fixture.spawns[i], spawns[i], i);
        }

        private static void AssertSpawn(Spawn expected, SpawnDefinition spawn, int index)
        {
            string tag = $"spawn {index}";
            Assert.AreEqual(index, spawn.SpawnIndex, tag);
            Assert.AreEqual(expected.kind, spawn.Kind.Value, $"{tag} kind");
            Assert.AreEqual(expected.faction, spawn.Faction, $"{tag} faction");
            Assert.AreEqual(expected.level, spawn.Level, $"{tag} level");
            Assert.AreEqual(expected.quality, spawn.Quality, $"{tag} quality");
            Assert.AreEqual(expected.flags, spawn.AlphaFlags, $"{tag} flags");
            Assert.AreEqual(expected.leader, spawn.LeaderSpawnIndex, $"{tag} leader");
            Assert.AreEqual(Position(expected.position, 0), spawn.Position, $"{tag} position");
            Assert.AreEqual(expected.route.Length / 3, spawn.Route.Count, $"{tag} route count");
            for (int i = 0; i < spawn.Route.Count; i++)
                Assert.AreEqual(Position(expected.route, i * 3), spawn.Route[i], $"{tag} waypoint {i}");
            Assert.AreEqual(expected.yaw, spawn.Yaw, $"{tag} yaw");
            Assert.AreEqual(expected.classId, spawn.ClassId, $"{tag} class");
            Assert.AreEqual(expected.specialization, spawn.SpecializationId, $"{tag} specialization");
            Assert.AreEqual(expected.power, spawn.PowerScale, $"{tag} power scale");
            RawAlphaItemId? main = expected.main < 0 ? (RawAlphaItemId?)null : new RawAlphaItemId(3, expected.main);
            Assert.AreEqual(main, spawn.MainHandWeapon, $"{tag} main hand");
            Assert.AreEqual(expected.off < 0 ? (expected.main < 0 ? 0 : 1) : 2, spawn.Equipment.Count, $"{tag} hands");
            if (expected.off >= 0)
                Assert.AreEqual(new RawAlphaItemId(3, expected.off), spawn.Equipment[1], $"{tag} off hand");
        }

        private static WorldPosition Position(long[] data, int index) =>
            new WorldPosition(data[index] / 65536.0, data[index + 2] / 65536.0, data[index + 1] / 65536.0);
    }
}
