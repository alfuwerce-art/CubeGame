using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            string json = File.ReadAllText(path);
            Trace t = JsonUtility.FromJson<Trace>(json);
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

            List<AlphaDungeonObject> objects;
            try
            {
                objects = AlphaDungeonContents.Write(cells, build, world, x0, y0, t.flag != 0, random);
            }
            catch (AlphaDungeonNotPortedException e)
            {
                Assert.Inconclusive($"{e.Message}; {draw} of {t.states.Length} Alpha draws matched so far");
                return;
            }

            Assert.AreEqual(t.states.Length, draw, "every Alpha draw of stage 4b is reproduced");

            // every creature Alpha spawned (chests, master, groups, followers, guards) in order: position, role, kind,
            // level, quality, flags
            List<long[]> spawns = Spawns(json);
            List<AlphaDungeonObject> creatures = objects.Where(o => o.List == AlphaDungeonObjectList.Creature).ToList();
            Assert.AreEqual(spawns.Count, creatures.Count, "creature count");
            for (int i = 0; i < spawns.Count; i++)
            {
                long[] a = spawns[i];
                AlphaDungeonObject o = creatures[i];
                string tag = $"creature {i} ({o})";
                Assert.AreEqual(a[0], o.X, 1e-3, tag + " x");
                Assert.AreEqual(a[1], o.Y, 1e-3, tag + " y");
                Assert.AreEqual(a[2], o.Z, 1e-3, tag + " z");
                Assert.AreEqual(a[3], o.Role, tag + " role");
                Assert.AreEqual(a[4], o.Type, tag + " kind");
                Assert.AreEqual(a[5], o.Level, tag + " level");
                Assert.AreEqual(a[6], o.Quality, tag + " quality");
                Assert.AreEqual(a[7], o.Flags, tag + " flags");
            }
        }

        /// <summary>The fixture's "spawns" rows [x, y, z, role, kind, level, quality, flags, master] (JsonUtility
        /// cannot read nested arrays).</summary>
        private static List<long[]> Spawns(string json)
        {
            int start = json.IndexOf("\"spawns\"", StringComparison.Ordinal);
            int open = json.IndexOf('[', start);
            int end = json.IndexOf("]]", open, StringComparison.Ordinal);
            return Regex.Matches(json.Substring(open + 1, end - open), @"\[([^\[\]]*)\]")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Split(',').Select(x => (long)double.Parse(x, CultureInfo.InvariantCulture)).ToArray())
                .ToList();
        }
    }
}
