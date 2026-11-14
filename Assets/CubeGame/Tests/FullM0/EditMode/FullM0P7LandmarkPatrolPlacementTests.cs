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

        [TestCase(32795, 32804)]
        [TestCase(32812, 32812)]
        public void SelectsAlphaPointsWithTheTracedStream(int zoneX, int zoneZ)
        {
            string path = Path.Combine(Application.dataPath, "CubeGame/Tests/FullM0/EditMode/Fixtures",
                $"alpha_landmark_patrol_points_{zoneX}_{zoneZ}.json");
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
            Assert.AreEqual("EMULATED_ALPHA_CODE", fixture.status);
            Assert.AreEqual(zoneX, fixture.zoneX);
            Assert.AreEqual(zoneZ, fixture.zoneZ);

            var probe = new TerrainStreamProbe();
            var terrain = new AlphaTerrainGenerator(fixture.seed, new[] { probe });
            // The oracle records the early terrain carve calls without applying them.
            AlphaZone zone = AlphaZoneGenerator.Generate(terrain, zoneX, zoneZ, true, new List<CarveCall>());
            LandmarkColumn landmark = terrain.ColumnAt(zoneX * AlphaZone.Size, zoneZ * AlphaZone.Size);
            Assert.AreEqual(fixture.afterTerrainCrt, probe.State, "CRT after terrain (0x5E8490)");
            Assert.AreEqual(fixture.landmarkId, landmark.Id);
            Assert.AreEqual(fixture.landmarkType, (int)landmark.Type);
            var random = new AlphaCrtRandom(unchecked((int)fixture.crtIn));
            IReadOnlyList<WorldPosition> points = AlphaLandmarkPatrolPlacement.SelectPoints(zone, landmark, random);
            Assert.AreEqual(fixture.crtOut, unchecked((uint)random.State), "CRT at 0x5DC6E0");
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
