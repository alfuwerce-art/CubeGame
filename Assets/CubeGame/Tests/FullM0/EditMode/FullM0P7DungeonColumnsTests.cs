using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CubeGame.Features.World;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>P7-B stage 4a: zone-write column loop of the Alpha dungeon generator against executed Alpha code.</summary>
    public sealed class FullM0P7DungeonColumnsTests
    {
        [Serializable]
        private sealed class Fixture
        {
            public string status;
            public int masterSeed;
            public int[] zone;
            public string zoneMaterialsSha256;
            public Case[] cases;
        }

        [Serializable]
        private sealed class Case
        {
            public int subtype;
            public int flag;
            public long crtIn;
            public int fills;
            public string fillsSha256;
            public long crtOut;
            public int @base;
            public int rotation;
            public int[] color1;
            public int[] color2;
        }

        private static Fixture Load()
        {
            string path = Path.Combine(Application.dataPath,
                "CubeGame/Tests/FullM0/EditMode/Fixtures/alpha_dungeon_columns_oracle_666666.json");
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<Fixture>(json);
        }

        [Test]
        public void TheZoneBeforeTheDungeonIsAlphasZone()
        {
            Fixture f = Load();
            AlphaZone zone = new AlphaTerrainGenerator((uint)f.masterSeed).Zone(f.zone[0], f.zone[1]);
            // Geometry and materials (bottom, count, flags byte of every voxel) equal Alpha's zone at 0x5CC510. Colours are
            // excluded: 151 columns differ by ±1 in one channel (P1 colour rounding, tracked in the parity backlog).
            Assert.AreEqual(f.zoneMaterialsSha256, ZoneSha256(zone), "terrain of the dungeon zone equals Alpha's state at 0x5CC510");
        }

        [Test]
        public void ColumnLoopMatchesTheExecutedAlphaGenerator()
        {
            Fixture f = Load();
            Assert.AreEqual("EMULATED_ALPHA_CODE", f.status);
            Assert.GreaterOrEqual(f.cases.Length, 6);
            foreach (Case c in f.cases)
            {
                string tag = $"subtype {c.subtype} flag {c.flag}";
                var terrain = new AlphaTerrainGenerator((uint)f.masterSeed);
                AlphaZone zone = terrain.Zone(f.zone[0], f.zone[1]);
                var random = new AlphaCrtRandom(unchecked((int)(uint)c.crtIn));
                AlphaDungeonRooms cells = AlphaDungeonGenerator.GenerateCells(c.subtype, c.flag != 0, random);
                AlphaDungeonBuild build = AlphaDungeonZoneWriter.WriteColumns(cells, new AlphaZoneDungeonWorld(terrain, zone),
                    f.zone[0] * 256 + 32, f.zone[1] * 256 + 32, random);

                Assert.AreEqual(c.@base, build.Base, $"{tag}: base height");
                Assert.AreEqual(c.rotation, build.Rotation, $"{tag}: rotation");
                CollectionAssert.AreEqual(c.color1, build.Color1.Select(b => (int)b).ToArray(), $"{tag}: colour 1");
                CollectionAssert.AreEqual(c.color2, build.Color2.Select(b => (int)b).ToArray(), $"{tag}: colour 2");
                Assert.AreEqual(c.fills, build.Fills.Count, $"{tag}: fill count");
                string text = string.Join(";", build.Fills.Select(x => x.ToString()));
                Assert.AreEqual(c.fillsSha256, Sha256(Encoding.ASCII.GetBytes(text)), $"{tag}: every fill (site, origin, size, type)");
                var expected = new AlphaCrtRandom(unchecked((int)(uint)c.crtOut));
                for (int i = 0; i < 4; i++)
                {
                    Assert.AreEqual(expected.Next(), random.Next(), $"{tag}: CRT state after the column loop");
                }
            }
        }

        private static string ZoneSha256(AlphaZone zone)
        {
            using (SHA256 sha = SHA256.Create())
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                for (int lz = 0; lz < AlphaZone.Size; lz++)
                {
                    for (int lx = 0; lx < AlphaZone.Size; lx++)
                    {
                        VoxelColumn col = zone.Column(zone.Zx * AlphaZone.Size + lx, zone.Zz * AlphaZone.Size + lz);
                        w.Write(col.Bottom);
                        w.Write(col.Count);
                        for (int i = 0; i < col.Count; i++) w.Write((byte)(col.Voxels[i] >> 24));
                    }
                }

                w.Flush();
                return Hex(sha.ComputeHash(stream.ToArray()));
            }
        }

        private static string Sha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(data));
        }

        private static string Hex(byte[] h) => BitConverter.ToString(h).Replace("-", string.Empty).ToLowerInvariant();
    }
}
