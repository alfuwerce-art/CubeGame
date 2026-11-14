using System;
using System.IO;
using CubeGame.Features.World;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// P7-B stage 4b: dungeon contents against the executed Alpha draw log (subtype 1, zone 32812/32796). Every draw
    /// is checked by CRT state and by the Alpha call site (the state alone only proves the number of draws).
    /// </summary>
    public sealed class FullM0P7DungeonContentsTests
    {
        [Serializable]
        private sealed class Trace
        {
            public string status;
            public int masterSeed;
            public int[] zone;
            public int subtype;
            public int flag;
            public long crtIn;
            public long[] sites;
            public long[] states;
        }

        /// <summary>Code range of 0x5CC510 (its own <c>rand()</c> return sites lie inside).</summary>
        private const long AlphaGeneratorFrom = 0x5CC510;

        private const long AlphaGeneratorTo = 0x5D5FD0;

        [Test]
        public void ContentsFollowTheExecutedAlphaDrawLog()
        {
            string path = Path.Combine(Application.dataPath,
                "CubeGame/Tests/FullM0/EditMode/Fixtures/alpha_dungeon_contents_trace_666666_s1.json");
            Trace t = JsonUtility.FromJson<Trace>(File.ReadAllText(path));
            Assert.AreEqual("EMULATED_ALPHA_CODE", t.status);

            var terrain = new AlphaTerrainGenerator((uint)t.masterSeed);
            AlphaZone zone = terrain.Zone(t.zone[0], t.zone[1]);
            var world = new AlphaZoneDungeonWorld(terrain, zone);
            var random = new AlphaCrtRandom(unchecked((int)(uint)t.crtIn));
            AlphaDungeonRooms cells = AlphaDungeonGenerator.GenerateCells(t.subtype, t.flag != 0, random);
            int x0 = t.zone[0] * 256 + 32;
            int y0 = t.zone[1] * 256 + 32;
            AlphaDungeonBuild build = AlphaDungeonZoneWriter.WriteColumns(cells, world, x0, y0, random);

            int draw = 0;
            random.AfterDraw = r =>
            {
                if (draw >= t.states.Length)
                {
                    throw new AssertionException($"draw {draw}: Alpha made no more draws");
                }

                if ((uint)r.State != (uint)t.states[draw])
                {
                    throw new AssertionException($"draw {draw}: state differs; Alpha's call site 0x{t.sites[draw]:x}");
                }

                // The CRT sequence alone only proves the count of draws; the call site proves which code drew.
                long site = t.sites[draw];
                bool ok = r.SiteFrom == AlphaCrtRandom.SiteOutside
                    ? site < AlphaGeneratorFrom || site >= AlphaGeneratorTo
                    : r.SiteFrom != 0 && site >= r.SiteFrom && site <= r.SiteTo;
                if (!ok)
                {
                    throw new AssertionException(
                        $"draw {draw}: Alpha drew at 0x{site:x}, the port at 0x{r.SiteFrom:x}…0x{r.SiteTo:x}");
                }

                draw++;
            };

            try
            {
                AlphaDungeonContents.Write(cells, build, world, x0, y0, t.flag != 0, random);
            }
            catch (AlphaDungeonNotPortedException e)
            {
                if (e.Message.EndsWith(AlphaDungeonContents.PostPass, StringComparison.Ordinal))
                {
                    // the cell pass is complete: every Alpha draw before the first post-pass site is reproduced
                    int firstPost = Array.FindIndex(t.sites, s => s >= 0x5D3D60 && s < AlphaGeneratorTo);
                    Assert.AreEqual(firstPost, draw, "the cell pass makes exactly Alpha's draws");
                }

                Assert.Inconclusive($"{e.Message}; {draw} of {t.states.Length} Alpha draws matched so far");
            }

            Assert.AreEqual(t.states.Length, draw, "every Alpha draw of stage 4b is reproduced");
        }
    }
}
