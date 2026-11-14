using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using CubeGame.Features.World;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>P7-B stage 1: rooms of the Alpha dungeon generator 0x5CC510 against executed Alpha code.</summary>
    public sealed class FullM0P7DungeonLayoutTests
    {
        [Serializable]
        private sealed class Fixture
        {
            public string status;
            public Case[] cases;
        }

        [Serializable]
        private sealed class Case
        {
            public int subtype;
            public int flag;
            public long crtIn;
            public long crtOut;
            public int[] roomFlags;
            public int[] boxes;
            public int[] bossCells;
            public int volumeSize;
            public string volumeSha256;
        }

        private static Fixture Load()
        {
            string path = Path.Combine(Application.dataPath,
                "CubeGame/Tests/FullM0/EditMode/Fixtures/alpha_dungeon_layout_oracle_666666.json");
            return JsonUtility.FromJson<Fixture>(File.ReadAllText(path));
        }

        [Test]
        public void RoomsMatchTheExecutedAlphaGenerator()
        {
            Fixture fixture = Load();
            Assert.AreEqual("EMULATED_ALPHA_CODE", fixture.status);
            Assert.AreEqual(21, fixture.cases.Length);
            foreach (Case c in fixture.cases)
            {
                string tag = $"subtype {c.subtype} flag {c.flag} crt {c.crtIn}";
                var random = new AlphaCrtRandom(unchecked((int)(uint)c.crtIn));
                AlphaDungeonRooms layout = AlphaDungeonGenerator.GenerateRooms(c.subtype, c.flag != 0, random);

                for (int n = 0; n < 27; n++)
                {
                    AlphaDungeonRoom room = layout.Rooms[n];
                    int bits = (room.Present ? 1 : 0) | (room.Master ? 2 : 0) | (room.Special ? 4 : 0);
                    Assert.AreEqual(c.roomFlags[n], bits, $"{tag}: room {n} flags");
                }

                var boxes = new List<int>();
                foreach (AlphaDungeonRoom room in layout.Boxes)
                {
                    boxes.AddRange(room.Origin);
                    boxes.AddRange(room.Size);
                    boxes.Add(room.Pillars ? 1 : 0);
                }

                CollectionAssert.AreEqual(c.boxes, boxes, $"{tag}: room boxes");
                var master = new List<int>();
                foreach ((int x, int y, int z) in layout.MasterCells)
                {
                    master.Add(x);
                    master.Add(y);
                    master.Add(z);
                }

                CollectionAssert.AreEqual(c.bossCells ?? Array.Empty<int>(), master, $"{tag}: master cell");
                Assert.AreEqual(c.volumeSize, layout.Volume.SizeX);
                Assert.AreEqual(c.volumeSha256, Sha256(layout.Volume.Cells), $"{tag}: 22³ cell volume");

                var expected = new AlphaCrtRandom(unchecked((int)(uint)c.crtOut));
                for (int i = 0; i < 4; i++)
                {
                    Assert.AreEqual(expected.Next(), random.Next(), $"{tag}: CRT state after the room stage");
                }
            }
        }

        [Test]
        public void FlagOneDungeonHasNoMasterAndSubtypesShareSchemes()
        {
            var a = AlphaDungeonGenerator.GenerateRooms(3, true, new AlphaCrtRandom(7));
            Assert.IsEmpty(a.MasterCells, "zone record 5 (flag 1): six rooms, no master in this stage");
            Assert.AreEqual(6, a.Boxes.Count);

            var four = AlphaDungeonGenerator.GenerateRooms(4, false, new AlphaCrtRandom(99));
            var five = AlphaDungeonGenerator.GenerateRooms(5, false, new AlphaCrtRandom(99));
            Assert.AreEqual(Sha256(four.Volume.Cells), Sha256(five.Volume.Cells));
            Assert.AreEqual(1, four.MasterCells.Count);
            Assert.IsTrue(four.Rooms[1 * 9 + 1 * 3 + 2].Special);
        }

        private static string Sha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
