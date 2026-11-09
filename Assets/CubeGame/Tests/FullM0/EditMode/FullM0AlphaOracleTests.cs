using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.Features.World;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// The C# terrain port against numbers produced by EXECUTING Cube.exe (tools/alpha_models/world/terrain_oracle.py,
    /// status EMULATED_ALPHA_CODE): climate cells 0x5D7A70, base height 0x5C5E20 and mountain mask 0x5EFFA0 at 300
    /// voxel positions around the world centre, master seed 666666, spawn = centre of zone 32800.
    /// The fixture holds only numbers computed by Alpha code, no Alpha assets.
    /// </summary>
    public sealed class FullM0AlphaOracleTests
    {
        [Serializable]
        private sealed class Cell
        {
            public int rx;
            public int rz;
            public int anchor_x;
            public int anchor_z;
            public int flag;
            public float f3;
            public float f4;
            public int id;
            public int @base;
        }

        [Serializable]
        private sealed class Sample
        {
            public int x;
            public int z;
            public double @base;
            public double mountain;
        }

        [Serializable]
        private sealed class Oracle
        {
            public string status;
            public int master_seed;
            public int world_seed;
            public int spawn_voxel;
            public List<Cell> cells;
            public List<Sample> samples;
        }

        private static Oracle Load()
        {
            string path = Path.Combine(Application.dataPath, "CubeGame/Tests/FullM0/EditMode/Fixtures/alpha_terrain_oracle_666666.json");
            Assert.IsTrue(File.Exists(path), path);
            Oracle oracle = JsonUtility.FromJson<Oracle>(File.ReadAllText(path));
            Assert.AreEqual("EMULATED_ALPHA_CODE", oracle.status);
            Assert.AreEqual(300, oracle.samples.Count);
            return oracle;
        }

        [Test]
        public void SeedTableAndSpawnMatchTheOracle()
        {
            Oracle oracle = Load();
            var seeds = new AlphaWorldSeeds((uint)oracle.master_seed);
            Assert.AreEqual(oracle.world_seed, seeds.WorldSeed);
            Assert.AreEqual(oracle.spawn_voxel, WorldLayout.StartVoxel);
        }

        [Test]
        public void ClimateCellsMatchExecutedAlphaCode()
        {
            Oracle oracle = Load();
            var grid = new AlphaClimateGrid(new AlphaWorldSeeds((uint)oracle.master_seed), WorldLayout.StartVoxel, WorldLayout.StartVoxel);
            foreach (Cell expected in oracle.cells)
            {
                AlphaClimateCell cell = grid.Get(expected.rx, expected.rz);
                string where = $"cell ({expected.rx},{expected.rz})";
                Assert.AreEqual(expected.anchor_x, cell.AnchorX, where);
                Assert.AreEqual(expected.anchor_z, cell.AnchorZ, where);
                Assert.AreEqual(expected.@base, cell.BaseHeight, where);
                Assert.AreEqual(expected.flag != 0, cell.Flag, where);
                Assert.AreEqual(expected.id, cell.Id, where);
                Assert.AreEqual(expected.f3, cell.Field3, 1e-6, where);
                Assert.AreEqual(expected.f4, cell.Field4, 1e-6, where);
            }
        }

        [Test]
        public void BaseHeightAndMountainMaskMatchExecutedAlphaCode()
        {
            Oracle oracle = Load();
            var generator = new AlphaTerrainGenerator((uint)oracle.master_seed);
            double worstBase = 0;
            double worstMountain = 0;
            foreach (Sample s in oracle.samples)
            {
                // This fixture was executed in a world without generated regions: compare the landmark-free functions.
                worstBase = Math.Max(worstBase, Math.Abs(generator.BaseIsolated(s.x, s.z) - s.@base));
                worstMountain = Math.Max(worstMountain, Math.Abs(generator.MountainIsolated(s.x, s.z) - s.mountain));
            }

            Debug.Log($"[FullM0] oracle worst |Δbase|={worstBase:E3} |Δmountain|={worstMountain:E3} over {oracle.samples.Count} samples");
            Assert.Less(worstBase, 0.01, "base height (voxels) equals Alpha's 0x5C5E20");
            Assert.Less(worstMountain, 1e-4, "mountain mask equals Alpha's 0x5EFFA0");
        }
    }
}
