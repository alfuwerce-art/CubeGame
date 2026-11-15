using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    public sealed class FullM0P7LandmarkPatrolPlacementTests
    {
        [Serializable]
        private sealed class Fixture
        {
            public string status;
            public uint seed;
            public int zoneX;
            public int zoneZ;
            public int landmarkId;
            public int landmarkType;
            public long afterTerrainCrt;
            public long crtIn;
            public long crtOut;
            public int masterPointIndex;
            public long[] points;
        }

        private sealed class TerrainStreamProbe : IZoneContentProvider
        {
            public string Key => nameof(TerrainStreamProbe);
            public uint State { get; private set; }

            public void Populate(ZoneContentContext context)
            {
                State = unchecked((uint)context.Random.State);
            }
        }

        private sealed class PatrolPassProbe : IZoneContentProvider
        {
            public string Key => nameof(PatrolPassProbe);
            public uint BeforeDecorations { get; private set; }
            public uint AfterPlacement { get; private set; }
            public uint AfterLastProp { get; private set; }
            public List<int> PostTreeDrawSites { get; } = new List<int>();
            public IReadOnlyList<WorldPosition> Points { get; private set; }
            public IReadOnlyList<AlphaSurfaceProp> Props { get; private set; }

            public void Populate(ZoneContentContext context)
            {
                BeforeDecorations = unchecked((uint)context.Random.State);
                context.Random.AfterDraw = stream =>
                {
                    if (stream.SiteFrom == 0x5EA7FA || stream.SiteFrom == 0x5EA819 || stream.SiteFrom == 0x5EA994)
                    {
                        PostTreeDrawSites.Add(stream.SiteFrom);
                        AfterLastProp = unchecked((uint)stream.State);
                    }
                };
                Points = AlphaLandmarkPatrolPlacement.Populate(context, out IReadOnlyList<AlphaSurfaceProp> props);
                Props = props;
                AfterPlacement = unchecked((uint)context.Random.State);
                context.Random.AfterDraw = null;
            }
        }

        [TestCase(32795, 32804)]
        [TestCase(32812, 32812)]
        public void SelectsAlphaPointsWithTheTracedStream(int zoneX, int zoneZ)
        {
            Fixture fixture = ReadFixture(zoneX, zoneZ);
            var probe = new TerrainStreamProbe();
            var terrain = new AlphaTerrainGenerator(fixture.seed, new[] { probe });
            // The oracle records the early terrain carve calls without applying them.
            AlphaZone zone = AlphaZoneGenerator.Generate(terrain, zoneX, zoneZ, true, new List<CarveCall>());
            LandmarkColumn landmark = terrain.ColumnAt(zoneX * AlphaZone.Size, zoneZ * AlphaZone.Size);
            Assert.AreEqual(fixture.afterTerrainCrt, probe.State, "CRT after terrain (0x5E8490)");
            var random = new AlphaCrtRandom(unchecked((int)fixture.crtIn));
            IReadOnlyList<WorldPosition> points = AlphaLandmarkPatrolPlacement.SelectPoints(zone, landmark, random);
            Assert.AreEqual(fixture.crtOut, unchecked((uint)random.State), "CRT at 0x5DC6E0");
            AssertPoints(fixture, zone, landmark, points);
        }

        [TestCase(32795, 32804, 0)]
        [TestCase(32812, 32812, 1)]
        public void GetsAlphaPatrolPointsAndPropsFromTheTerrainStream(int zoneX, int zoneZ, int propCount)
        {
            Fixture fixture = ReadFixture(zoneX, zoneZ);
            var probe = new PatrolPassProbe();
            var terrain = new AlphaTerrainGenerator(fixture.seed, new[] { probe });
            AlphaZone zone = AlphaZoneGenerator.Generate(terrain, zoneX, zoneZ, true, new List<CarveCall>());
            LandmarkColumn landmark = terrain.ColumnAt(zoneX * AlphaZone.Size, zoneZ * AlphaZone.Size);

            Assert.AreEqual(fixture.afterTerrainCrt, probe.BeforeDecorations, "CRT before surface decorations");
            Assert.AreEqual(fixture.crtOut, probe.AfterPlacement, "CRT at 0x5DC6E0");
            AssertPoints(fixture, zone, landmark, probe.Points);
            Assert.AreEqual(propCount, probe.Props.Count);
            if (propCount == 1)
            {
                CollectionAssert.AreEqual(new[] { 0x5EA7FA, 0x5EA819, 0x5EA994 }, probe.PostTreeDrawSites);
                Assert.AreEqual(fixture.crtIn, probe.AfterLastProp, "CRT immediately before selecting patrol points");
                AlphaSurfaceProp prop = probe.Props[0];
                Assert.AreEqual(0, prop.ObjectId);
                Assert.AreEqual(new WorldPosition(8399947.5, 93, 8399947.5), prop.Position);
                Assert.AreEqual(3, prop.Orientation);
                Assert.AreEqual(2f, prop.Width);
                Assert.AreEqual(8f, prop.Height);
            }
            else
            {
                Assert.IsEmpty(probe.PostTreeDrawSites);
            }
        }

        private static Fixture ReadFixture(int zoneX, int zoneZ)
        {
            string path = Path.Combine(Application.dataPath, "CubeGame/Tests/FullM0/EditMode/Fixtures",
                $"alpha_landmark_patrol_points_{zoneX}_{zoneZ}.json");
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
            Assert.AreEqual("EMULATED_ALPHA_CODE", fixture.status);
            Assert.AreEqual(zoneX, fixture.zoneX);
            Assert.AreEqual(zoneZ, fixture.zoneZ);
            return fixture;
        }

        private static void AssertPoints(Fixture fixture, AlphaZone zone, LandmarkColumn landmark,
            IReadOnlyList<WorldPosition> points)
        {
            Assert.AreEqual(fixture.landmarkId, landmark.Id);
            Assert.AreEqual(fixture.landmarkType, (int)landmark.Type);
            Assert.AreEqual(fixture.points.Length / 3, points.Count, "point count");
            for (int i = 0; i < points.Count; i++)
            {
                const double scale = 65536.0;
                Assert.AreEqual(new WorldPosition(fixture.points[i * 3] / scale, fixture.points[i * 3 + 2] / scale,
                    fixture.points[i * 3 + 1] / scale), points[i], $"point {i}");
            }
            Assert.AreEqual(fixture.masterPointIndex,
                AlphaLandmarkPatrolPlacement.SelectMasterPoint(zone, landmark, points), "master point");
        }
    }
}
