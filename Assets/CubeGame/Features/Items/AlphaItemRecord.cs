using System;

namespace CubeGame.Features.Items
{
    /// <summary>
    /// Alpha item record (0x118 bytes in Cube.exe; the inventory entry adds a count in front, stride 0x11C).
    /// Only the fields that the item generators, <c>Item_equals</c> 0x42F4A0 and the wallet read are kept:
    /// <c>+0x00</c> type, <c>+0x01</c> subtype, <c>+0x04</c> seed, <c>+0x08</c> variant (formula target type),
    /// <c>+0x0C</c> rarity, <c>+0x0D</c> modifier/material, <c>+0x10</c> level (i16; for coins the coin count).
    /// The colour payload <c>+0x14…+0x113</c> and its element count <c>+0x114</c> are not produced by the
    /// generators restored in P5 (they always clear it), so they are not represented.
    /// Provenance: ALPHA_CODE_STATIC (field writes of 0x5F51E0, 0x5F8AD0, 0x5F8750, 0x5F6D50, 0x59B180).
    /// </summary>
    public readonly struct AlphaItemRecord : IEquatable<AlphaItemRecord>
    {
        public AlphaItemRecord(int type, int subtype, int seed = 0, int variant = 0, int rarity = 0, int modifier = 0, int level = 1)
        {
            Type = type;
            Subtype = subtype;
            Seed = seed;
            Variant = variant;
            Rarity = rarity;
            Modifier = modifier;
            Level = level;
        }

        public int Type { get; }
        public int Subtype { get; }
        public int Seed { get; }
        public int Variant { get; }
        public int Rarity { get; }
        public int Modifier { get; }
        public int Level { get; }

        /// <summary>Type 0 is an empty record (Alpha keeps such entries in containers; nothing is delivered).</summary>
        public bool IsEmpty => Type == 0;

        public AlphaItemRecord WithLevel(int level) => new AlphaItemRecord(Type, Subtype, Seed, Variant, Rarity, Modifier, level);

        public bool Equals(AlphaItemRecord o) =>
            Type == o.Type && Subtype == o.Subtype && Seed == o.Seed && Variant == o.Variant
            && Rarity == o.Rarity && Modifier == o.Modifier && Level == o.Level;

        public override bool Equals(object obj) => obj is AlphaItemRecord o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Type;
                h = h * 31 + Subtype;
                h = h * 31 + Seed;
                h = h * 31 + Variant;
                h = h * 31 + Rarity;
                h = h * 31 + Modifier;
                return h * 31 + Level;
            }
        }

        public override string ToString() =>
            $"alpha({Type:x2}/{Subtype:x2} L{Level} r{Rarity} mod={Modifier:x2} seed={Seed} var={Variant})";
    }
}
