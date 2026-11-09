using System;
using System.Collections.Generic;
using System.Globalization;

namespace CubeGame.M0Pilots.World
{
    public static class WorldPilot
    {
        private const int Seed = 666666; // seed used by the existing CubeGame pilot and Alpha save sample

        public static PilotResult Run()
        {
            Check.Start("PILOT_WORLD");
            RandomGenerator();
            RegionFormula();
            RegionDeterminism();
            RegionNames();
            CoordinateMapping();
            ChunkConsistency();
            DeltaPersistence();
            return Check.Finish();
        }

        private static void RandomGenerator()
        {
            var random = new AlphaRandom(1);
            bool sequence = true;
            for (int i = 0; i < TestData.MsRandSeed1.Length; i++)
            {
                if (random.Next() != TestData.MsRandSeed1[i])
                {
                    sequence = false;
                }
            }

            Check.True("CRT rand seed=1 matches the known 41/18467/6334/26500/19169 vector", sequence);

            var first = new AlphaRandom(Seed);
            var second = new AlphaRandom(Seed);
            bool same = true;
            for (int i = 0; i < 1000; i++)
            {
                if (first.Next() != second.Next())
                {
                    same = false;
                }
            }

            Check.True("same seed produces the same 1000-call sequence", same);

            var probe = new AlphaRandom(Seed);
            int min = int.MaxValue;
            int max = int.MinValue;
            for (int i = 0; i < 10000; i++)
            {
                int value = probe.Next();
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            Check.True("rand stays inside 0..32767", min >= 0 && max <= 32767, "min=" + min + " max=" + max);
            Check.True("rand actually varies", max > min);
        }

        private static void RegionFormula()
        {
            Check.Equal("centre region (512,512) has type 1 (Village)", 1, AlphaRegion.TypeAt(512, 512));
            Check.Equal("far corner (0,0) has the documented type 545", 545, AlphaRegion.TypeAt(0, 0));
            Check.Equal("(513,512) is one block from the centre: type 2", 2, AlphaRegion.TypeAt(513, 512));
            Check.Equal("(1023,0) has type 544", 544, AlphaRegion.TypeAt(1023, 0));
            Check.Equal("(500,500) has type 14", 14, AlphaRegion.TypeAt(500, 500));

            var centre = AlphaRegion.Generate(Seed, 512, 512);
            Check.Equal("centre seed = masterSeed + z*1024 + x", unchecked(Seed + 512 * 1024 + 512), centre.Seed);
            Check.Equal("centre variant is 0 (type <= 4)", 0, centre.Variant);
            Check.True("centre orientation is in 0..3", centre.Orientation >= 0 && centre.Orientation <= 3);

            var edge = AlphaRegion.Generate(Seed, 1023, 1023);
            Check.Equal("edge seed uses z*0x400", unchecked(Seed + 1023 * 0x400 + 1023), edge.Seed);
            Check.True("edge variant is in 0..4 (type > 4)", edge.Variant >= 0 && edge.Variant <= 4);
        }

        private static void RegionDeterminism()
        {
            bool identical = true;
            for (int z = 0; z < AlphaRegion.Grid && identical; z += 37)
            {
                for (int x = 0; x < AlphaRegion.Grid && identical; x += 41)
                {
                    RegionDescriptor left = AlphaRegion.Generate(Seed, x, z);
                    RegionDescriptor right = AlphaRegion.Generate(Seed, x, z);
                    if (left.Seed != right.Seed || left.Type != right.Type || left.Variant != right.Variant
                        || left.Orientation != right.Orientation || left.NameKnown != right.NameKnown
                        || !string.Equals(left.Name, right.Name, StringComparison.Ordinal))
                    {
                        identical = false;
                    }
                }
            }

            Check.True("region generation is deterministic across the 1024x1024 grid sample", identical);

            var lowType = AlphaRegion.Generate(Seed, 513, 512);
            var expectedRng = new AlphaRandom(unchecked(Seed + 512 * 0x400 + 513));
            int expectedOrientation = expectedRng.Next() & 3;
            Check.Equal("type <= 4 consumes no variant roll before orientation", expectedOrientation, lowType.Orientation);

            var highType = AlphaRegion.Generate(Seed, 0, 0);
            var expectedHigh = new AlphaRandom(unchecked(Seed + 0 * 0x400 + 0));
            int expectedVariant = expectedHigh.Next() % 5;
            int expectedHighOrientation = expectedHigh.Next() & 3;
            Check.Equal("type > 4 consumes one variant roll before orientation", expectedVariant, highType.Variant);
            Check.Equal("orientation follows the documented call order", expectedHighOrientation, highType.Orientation);

            var seeds = new HashSet<int>();
            bool unique = true;
            for (int z = 0; z < 64 && unique; z++)
            {
                for (int x = 0; x < 64 && unique; x++)
                {
                    if (!seeds.Add(AlphaRegion.Generate(Seed, x, z).Seed))
                    {
                        unique = false;
                    }
                }
            }

            Check.True("region seeds are unique over a 64x64 region sample", unique);
        }

        private static void RegionNames()
        {
            Check.Equal("(512,512) is named Village", "Village", AlphaRegion.Generate(Seed, 512, 512).Name);
            Check.True("known type 2 maps to Mountain", AlphaRegion.TryName(2, 0, out string mountain) && mountain == "Mountain");
            Check.True("known type 11 maps to Rock", AlphaRegion.TryName(11, 0, out string rock) && rock == "Rock");
            Check.True("special (0,5) maps to Ruins", AlphaRegion.TryName(0, 5, out string ruins) && ruins == "Ruins");
            Check.True("unknown type 5 is not given an invented name", !AlphaRegion.TryName(5, 0, out _));
            Check.True("unknown type 10 is not given an invented name", !AlphaRegion.TryName(10, 0, out _));
            Check.True("unknown type 14 is not given an invented name", !AlphaRegion.TryName(14, 0, out _));

            var generated = AlphaRegion.Generate(Seed, 300, 700);
            Check.True("far generated region type has no known name (documented PARTIAL semantics)",
                generated.Type > 15 && !generated.NameKnown);
        }

        private static void CoordinateMapping()
        {
            int rx;
            int rz;
            int lx;
            int lz;

            AlphaRegion.WorldToRegion(0, 0, out rx, out rz, out lx, out lz);
            Check.True("world (0,0) -> region (0,0), local (0,0)",
                rx == 0 && rz == 0 && lx == 0 && lz == 0);

            AlphaRegion.WorldToRegion(63, 63, out rx, out rz, out lx, out lz);
            Check.True("world (63,63) stays in region 0 at local (63,63)",
                rx == 0 && rz == 0 && lx == 63 && lz == 63);

            AlphaRegion.WorldToRegion(64, 0, out rx, out rz, out lx, out lz);
            Check.True("world (64,0) starts region 1 at local (0,0)",
                rx == 1 && rz == 0 && lx == 0 && lz == 0);

            AlphaRegion.WorldToRegion(65535, 65535, out rx, out rz, out lx, out lz);
            Check.True("world (65535,65535) is region (1023,1023) local (63,63)",
                rx == 1023 && rz == 1023 && lx == 63 && lz == 63);

            AlphaRegion.WorldToRegion(512, 512, out rx, out rz, out lx, out lz);
            Check.True("world centre (512,512) is region (8,8) local (0,0)",
                rx == 8 && rz == 8 && lx == 0 && lz == 0);

            Check.Throws<ArgumentOutOfRangeException>("world coordinate 65536 is rejected",
                () => AlphaRegion.WorldToRegion(65536, 0, out _, out _, out _, out _));

            int cellX;
            int cellZ;
            int bx;
            int bz;
            AlphaRegion.LocalToCell(0, 0, out cellX, out cellZ, out bx, out bz);
            Check.True("local (0,0) -> cell (0,0) block (0,0)", cellX == 0 && cellZ == 0 && bx == 0 && bz == 0);
            AlphaRegion.LocalToCell(8, 8, out cellX, out cellZ, out bx, out bz);
            Check.True("local (8,8) -> cell (1,1) block (0,0)", cellX == 1 && cellZ == 1 && bx == 0 && bz == 0);
            AlphaRegion.LocalToCell(63, 63, out cellX, out cellZ, out bx, out bz);
            Check.True("local (63,63) -> cell (7,7) block (7,7)", cellX == 7 && cellZ == 7 && bx == 7 && bz == 7);
            Check.Throws<ArgumentOutOfRangeException>("local coordinate 64 is rejected",
                () => AlphaRegion.LocalToCell(64, 0, out _, out _, out _, out _));
        }

        private static void ChunkConsistency()
        {
            var empty = new WorldDeltaStore(Seed);
            ulong first = empty.ComputeChunkHash(8, 8, 0, 0);
            ulong second = empty.ComputeChunkHash(8, 8, 0, 0);
            Check.True("unchanged chunk regenerates to the same hash", first == second);

            var otherSeed = new WorldDeltaStore(Seed + 1);
            Check.True("different seed changes the chunk hash", first != otherSeed.ComputeChunkHash(8, 8, 0, 0));

            var edited = new WorldDeltaStore(Seed);
            edited.SetBlock(520, 40, 520, 7);
            ulong target = edited.ComputeChunkHash(8, 8, 0, 0);
            ulong neighbour = edited.ComputeChunkHash(8, 8, 1, 0);
            Check.True("an edit changes only its own chunk", target != first && neighbour == empty.ComputeChunkHash(8, 8, 1, 0));

            edited.SetBlock(527, 40, 527, 6);
            edited.SetBlock(528, 40, 520, 5);
            Check.True("block 527 stays in chunk 0", edited.TryGetBlock(527, 40, 527, out int left) && left == 6);
            Check.True("block 528 belongs to chunk 1", edited.TryGetBlock(528, 40, 520, out int right) && right == 5);
            Check.True("chunk 1 hash now differs", edited.ComputeChunkHash(8, 8, 1, 0) != neighbour);

            Check.Throws<ArgumentOutOfRangeException>("chunk coordinate outside the region is rejected",
                () => empty.ComputeChunkHash(8, 8, 4, 0));
        }

        private static void DeltaPersistence()
        {
            var original = new WorldDeltaStore(Seed);
            original.SetBlock(520, 30, 520, 3);
            original.SetBlock(521, 31, 521, 4);
            original.SetBlock(512, 20, 512, 9);

            string text = original.Serialize();
            Check.True("serialization is deterministic", text == original.Serialize());
            Check.True("serialized delta starts with the documented header", text.StartsWith("M0DELTA|1|", StringComparison.Ordinal));

            WorldDeltaStore restored;
            string error;
            Check.True("delta file parses", WorldDeltaStore.TryDeserialize(text, out restored, out error), error);
            Check.Equal("restored seed is preserved", Seed, restored.Seed);
            Check.Equal("restored edit count is preserved", original.Count, restored.Count);

            bool equal = true;
            for (int x = 515; x <= 525; x++)
            {
                for (int z = 515; z <= 525; z++)
                {
                    if (original.ResolveBlock(x, 30, z) != restored.ResolveBlock(x, 30, z))
                    {
                        equal = false;
                    }
                }
            }

            Check.True("restored delta resolves the edited area identically", equal);
            Check.True("restored chunk hash matches", original.ComputeChunkHash(8, 8, 0, 0) == restored.ComputeChunkHash(8, 8, 0, 0));
            Check.True("restored chunk 0 includes the 100/200 edit and chunk 1 the 64/64 edit",
                restored.TryGetBlock(520, 30, 520, out int a) && a == 3
                && restored.TryGetBlock(512, 20, 512, out int b) && b == 9);

            string corrupted = text.Replace("520,30,520,3", "520,30,520,4");
            WorldDeltaStore rejected;
            Check.True("corrupted payload is rejected by the checksum",
                !WorldDeltaStore.TryDeserialize(corrupted, out rejected, out error));

            string wrongVersion = text.Replace("M0DELTA|1|", "M0DELTA|2|");
            Check.True("unsupported delta version is rejected",
                !WorldDeltaStore.TryDeserialize(wrongVersion, out rejected, out error));

            string wrongCount = text.Replace("|3|", "|4|");
            Check.True("header/body edit count mismatch is rejected",
                !WorldDeltaStore.TryDeserialize(wrongCount, out rejected, out error));

            var empty = new WorldDeltaStore(Seed);
            WorldDeltaStore emptyRestored;
            Check.True("empty delta round-trips", WorldDeltaStore.TryDeserialize(empty.Serialize(), out emptyRestored, out error));
            Check.Equal("empty delta keeps zero edits", 0, emptyRestored.Count);

            Check.Throws<ArgumentOutOfRangeException>("delta block y outside 0..255 is rejected",
                () => original.SetBlock(1, 256, 1, 1));
        }
    }
}
