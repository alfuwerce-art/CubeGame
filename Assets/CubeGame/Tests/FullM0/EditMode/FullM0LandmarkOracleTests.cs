using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.Features.World;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// Region landmarks against EXECUTED Cube.exe (status EMULATED_ALPHA_CODE): landmark columns of generateRegion
    /// (0x5DA280) for 11 regions (centre 3×3, a level-16 region, an ocean region) and terrain base/mountain with the
    /// landmark columns present, sampled inside landmark disks. Fixture = numbers only.
    /// </summary>
    public sealed class FullM0LandmarkOracleTests
    {
        [Serializable]
        private sealed class Column
        {
            public int i;
            public int type;
            public int sub;
            public int id;
            public int level;
            public int cls;
            public double x;
            public double z;
            public float radius;
            public float @base;
        }

        [Serializable]
        private sealed class Region
        {
            public int rx;
            public int rz;
            public int level;
            public int variant;
            public int orientation;
            public List<Column> columns;
        }

        [Serializable]
        private sealed class Sample
        {
            public int x;
            public int z;
            public int type;
            public double @base;
            public double mountain;
        }

        [Serializable]
        private sealed class Fixture
        {
            public string status;
            public int master_seed;
            public List<Region> regions;
            public List<Sample> samples;
        }

        private static Fixture Load()
        {
            string path = Path.Combine(Application.dataPath, "CubeGame/Tests/FullM0/EditMode/Fixtures/alpha_landmarks_oracle_666666.json");
            Assert.IsTrue(File.Exists(path), path);
            Fixture fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
            Assert.AreEqual("EMULATED_ALPHA_CODE", fixture.status);
            return fixture;
        }

        [Test]
        public void LandmarkColumnsMatchExecutedGenerateRegion()
        {
            Fixture fixture = Load();
            var generator = new AlphaTerrainGenerator((uint)fixture.master_seed);
            int checkedColumns = 0;
            foreach (Region expected in fixture.regions)
            {
                AlphaRegion region = generator.RegionRecord(expected.rx, expected.rz);
                string where = $"region ({expected.rx},{expected.rz})";
                Assert.AreEqual(expected.level, region.Level, where);
                Assert.AreEqual(expected.variant, region.Variant, where);
                Assert.AreEqual(expected.orientation, region.Orientation, where);
                foreach (Column e in expected.columns)
                {
                    LandmarkColumn c = region.Columns[e.i];
                    string at = $"{where} column {e.i}";
                    Assert.AreEqual(e.type, (int)c.Type, at);
                    Assert.AreEqual(e.sub, c.Sub, at);
                    Assert.AreEqual(e.id, c.Id, at);
                    Assert.AreEqual(e.level, c.Level, at);
                    Assert.AreEqual(e.cls, c.LevelClass, at);
                    Assert.AreEqual(e.x, c.X, 0.01, at);
                    Assert.AreEqual(e.z, c.Z, 0.01, at);
                    Assert.AreEqual(e.radius, c.Radius, 0.001, at);
                    Assert.AreEqual(e.@base, c.Base, 0.01, at);
                    checkedColumns++;
                }
            }

            Assert.AreEqual(704, checkedColumns);
        }

        [Test]
        public void TerrainWithLandmarksMatchesExecutedAlphaCode()
        {
            Fixture fixture = Load();
            var generator = new AlphaTerrainGenerator((uint)fixture.master_seed);
            var worstBase = new Dictionary<int, double>();
            var worstMountain = new Dictionary<int, double>();
            foreach (Sample s in fixture.samples)
            {
                double db = Math.Abs(generator.Base(s.x, s.z) - s.@base);
                if (db > 0.02)
                {
                    LandmarkColumn col = generator.ColumnAt(s.x, s.z);
                    Debug.Log($"[FullM0] base mismatch type {s.type} at ({s.x},{s.z}): alpha {s.@base:F4} cubegame {generator.Base(s.x, s.z):F4} " +
                              $"isolated {generator.BaseIsolated(s.x, s.z):F4} column {col.Type} r={col.Radius} c=({col.X},{col.Z}) " +
                              $"sth={generator.SampleTerrainHeight(s.x, s.z):F6} rot={generator.RotateAndPlace(s.x, s.z):F6} rare={generator.RareCellTerm(s.x, s.z):F6}");
                }
                double dm = Math.Abs(generator.Mountain(s.x, s.z) - s.mountain);
                worstBase[s.type] = Math.Max(worstBase.TryGetValue(s.type, out double b) ? b : 0, db);
                worstMountain[s.type] = Math.Max(worstMountain.TryGetValue(s.type, out double m) ? m : 0, dm);
            }

            foreach (int type in worstBase.Keys)
            {
                Debug.Log($"[FullM0] landmark type {type}: worst |Δbase|={worstBase[type]:E2} |Δmountain|={worstMountain[type]:E2}");
                Assert.Less(worstBase[type], 0.02, $"base near landmark type {type}");
                Assert.Less(worstMountain[type], 1e-3, $"mountain near landmark type {type}");
            }

            Assert.Greater(fixture.samples.Count, 300);
        }
    }
}
