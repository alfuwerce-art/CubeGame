using System;

namespace CubeGame.Features.Contracts
{
    // Shared identifiers of the M0 game API (D86 §2 "минимальный C# контракт"). Every agent uses these types;
    // no duplicates are allowed in content/visual assemblies. Raw Alpha numbers are confined to the
    // explicitly named Raw* types and to the Alpha import layer (M0-00C §0.4).

    /// <summary>World seed. Alpha uses a 32-bit seed per world.</summary>
    public readonly struct WorldSeed : IEquatable<WorldSeed>
    {
        public WorldSeed(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool Equals(WorldSeed other) => Value == other.Value;

        public override bool Equals(object obj) => obj is WorldSeed other && Equals(other);

        public override int GetHashCode() => (int)Value;

        public override string ToString() => Value.ToString();
    }

    /// <summary>Horizontal chunk coordinate (chunk size defined by the world generator in use).</summary>
    public readonly struct ChunkKey : IEquatable<ChunkKey>
    {
        public ChunkKey(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }

        public int Z { get; }

        public bool Equals(ChunkKey other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is ChunkKey other && Equals(other);

        public override int GetHashCode() => unchecked((X * 73856093) ^ (Z * 19349663));

        public override string ToString() => $"chunk({X},{Z})";
    }

    /// <summary>Horizontal region coordinate (a region groups chunks; persistence unit for deltas/entities).</summary>
    public readonly struct RegionKey : IEquatable<RegionKey>
    {
        public RegionKey(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }

        public int Z { get; }

        public bool Equals(RegionKey other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is RegionKey other && Equals(other);

        public override int GetHashCode() => unchecked((X * 83492791) ^ (Z * 1640531527));

        public override string ToString() => $"region({X},{Z})";
    }

    /// <summary>
    /// Persistent entity identity. Spawned world entities derive it from (region, spawn index, generation),
    /// so unloading and reloading a region never creates duplicates (M0-00C §8). The hero uses
    /// <see cref="Hero"/>; runtime-only entities (projectiles) use the <see cref="EntityIdKind.Runtime"/> space.
    /// </summary>
    public readonly struct StableEntityId : IEquatable<StableEntityId>
    {
        public StableEntityId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }

        public static StableEntityId None => default;

        public static StableEntityId Hero => new StableEntityId(((ulong)EntityIdKind.Hero << 60) | 1UL);

        public bool IsNone => Value == 0UL;

        public EntityIdKind Kind => (EntityIdKind)(Value >> 60);

        /// <summary>World spawn: 4-bit kind | 20-bit region x | 20-bit region z | 12-bit spawn index | 8-bit generation.</summary>
        public static StableEntityId ForSpawn(RegionKey region, int spawnIndex, int generation)
        {
            if (spawnIndex < 0 || spawnIndex > 0xFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnIndex));
            }

            if (generation < 0 || generation > 0xFF)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            ulong x = (ulong)(region.X & 0xFFFFF);
            ulong z = (ulong)(region.Z & 0xFFFFF);
            ulong value = ((ulong)EntityIdKind.WorldSpawn << 60) | (x << 40) | (z << 20) | ((ulong)spawnIndex << 8) | (ulong)generation;
            return new StableEntityId(value);
        }

        public static StableEntityId ForRuntime(ulong sequence)
        {
            return new StableEntityId(((ulong)EntityIdKind.Runtime << 60) | (sequence & 0x0FFFFFFFFFFFFFFFUL));
        }

        public bool Equals(StableEntityId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is StableEntityId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"entity:{Value:x16}";
    }

    public enum EntityIdKind
    {
        None = 0,
        Hero = 1,
        WorldSpawn = 2,
        Runtime = 3,
    }

    /// <summary>Creature/NPC/object species. For Alpha species the number equals Alpha's kind byte (+0x64).</summary>
    public readonly struct KindId : IEquatable<KindId>
    {
        public KindId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(KindId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is KindId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => $"kind:0x{Value:x2}";
    }

    /// <summary>
    /// Raw Alpha item identifiers (type byte +0x00, subtype byte +0x01). Lives next to — not instead of —
    /// Core <c>ItemType</c>; conversion is done by the item adapter (no Core enum migration, D86 §4).
    /// </summary>
    public readonly struct RawAlphaItemId : IEquatable<RawAlphaItemId>
    {
        public RawAlphaItemId(int type, int subtype)
        {
            Type = type;
            Subtype = subtype;
        }

        public int Type { get; }

        public int Subtype { get; }

        public bool Equals(RawAlphaItemId other) => Type == other.Type && Subtype == other.Subtype;

        public override bool Equals(object obj) => obj is RawAlphaItemId other && Equals(other);

        public override int GetHashCode() => (Type << 8) | (Subtype & 0xFF);

        public override string ToString() => $"alphaItem({Type},{Subtype})";
    }

    /// <summary>Action identifier. 1:1 with Alpha action ids where proven; CubeGame-only actions use ≥ 1000.</summary>
    public readonly struct ActionId : IEquatable<ActionId>
    {
        public const int FirstCubeGameOnly = 1000;

        public ActionId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool IsAlpha => Value >= 0 && Value < FirstCubeGameOnly;

        public bool Equals(ActionId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ActionId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => $"action:{Value}";
    }

    /// <summary>Game-space position (1 unit = 1 world block; see DeviationRegistry VoxelsPerBlock).</summary>
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public WorldPosition(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public bool Equals(WorldPosition other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        public override bool Equals(object obj) => obj is WorldPosition other && Equals(other);

        public override int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2);

        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:F2}, {1:F2}, {2:F2})", X, Y, Z);
    }
}
