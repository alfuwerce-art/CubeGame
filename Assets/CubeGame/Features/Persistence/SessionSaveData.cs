using System;
using System.Collections.Generic;

namespace CubeGame.Features.Persistence
{
    /// <summary>
    /// Session sidecar of a save slot (M0-01, <c>CUBEGAME_SESSION</c> v1). Stored next to the unchanged Core
    /// character save (<c>CUBEGAME_SAVE</c> v1); it is NOT the save v2 migration (D86 §4 — separate decision).
    /// Flat public fields so that any serializer (Unity JsonUtility in the game) can handle it.
    /// </summary>
    [Serializable]
    public sealed class SessionSaveData
    {
        public int formatVersion = SessionSaveFormat.CurrentVersion;

        public string worldName = string.Empty;

        /// <summary>World seed (uint stored as long for serializer portability).</summary>
        public long worldSeed;

        public string generatorId = string.Empty;

        public long createdUtcTicks;

        public long savedUtcTicks;

        public int ticksPerSecond;

        public long clockTick;

        public string heroName = string.Empty;

        public int raceId = -1;

        public double heroX;

        public double heroY;

        public double heroZ;

        public float heroYaw;

        /// <summary>
        /// Current hero HP (M0-05A). Optional addition to format v1: -1 (and slots written before this field) = full HP.
        /// </summary>
        public double heroHealth = -1.0;

        /// <summary>
        /// C1, optional: hero editor choice (gender 0/1, face, haircut, hair colour bytes). −1 (and slots written before
        /// C1) = Alpha defaults (male, face 0, haircut 0, hair FF C8 32); race is <see cref="raceId"/> (−1 → Human).
        /// </summary>
        public int heroGender = -1;

        public int heroFace = -1;

        public int heroHaircut = -1;

        public int heroHairR = -1;

        public int heroHairG = -1;

        public int heroHairB = -1;

        /// <summary>C3: eleven ranks in Alpha order. Slots without this field start with zero ranks.</summary>
        public int[] heroSkillRanks;

        /// <summary>Optional M0-F3 fields. Pre-F3 slots start with zero copper and mana cubes and an empty journal.</summary>
        public int heroCopper;

        public int heroManaCubes;

        /// <summary>
        /// Optional P5 field: platinum coins (Alpha type 0x0D counter, 0x46EBE0). Older slots start with zero.
        /// Copper keeps the whole Alpha money field (copper ×1, silver ×100, gold ×10000).
        /// </summary>
        public int heroPlatinum;

        public HuntMissionSaveData[] huntMissions;

        /// <summary>Calendar advances from inn visits, separate from the combat and movement tick.</summary>
        public long worldTimeOffsetMs;

        /// <summary>
        /// M0-04B, optional: placement of the Core items of the character save (same order): bag slot ≥ 0 or
        /// −(1+equipment slot). C5 extra slots use the Alpha creature offset instead of a Core slot index.
        /// Missing (older slots) → items go to the bags, first weapon to the main hand.
        /// </summary>
        public int[] inventoryPlacement;

        /// <summary>M0-04B, optional: Alpha items Core does not model (consumables, materials…) — parallel arrays.</summary>
        public int[] rawItemTypes;

        public int[] rawItemSubtypes;

        public int[] rawItemCounts;

        /// <summary>Bag slot ≥ 0 or C5 extra equipment −(1+Alpha creature offset).</summary>
        public int[] rawItemSlots;

        /// <summary>
        /// Optional P5 fields, parallel to <see cref="rawItemTypes"/>: the Alpha record of raw items (seed +0x04,
        /// variant +0x08, rarity +0x0C, modifier +0x0D, level +0x10). Missing arrays restore the Alpha defaults
        /// (level 1, other fields 0), which is what pre-P5 raw items carried.
        /// </summary>
        public int[] rawItemSeeds;

        public int[] rawItemVariants;

        public int[] rawItemRarities;

        public int[] rawItemModifiers;

        public int[] rawItemLevels;

        /// <summary>FNV-1a 64 (hex) of the framed character save text written in the same checkpoint.</summary>
        public string characterChecksum = string.Empty;

        /// <summary>FNV-1a 64 (hex) of the framed world file of the same checkpoint; empty = slot written before M0-02 (no world file).</summary>
        public string worldChecksum = string.Empty;
    }

    [Serializable]
    public sealed class HuntMissionSaveData
    {
        public long target;
        public int kind;
        public bool complete;
    }

    public static class SessionSaveFormat
    {
        public const int CurrentVersion = 1;

        public const string Header = "CUBEGAME_SESSION";

        public const string KeySuffix = ".session";
    }

    /// <summary>World file of a save slot (M0-02): <c>CUBEGAME_WORLD</c> v1 — deltas and explored regions.</summary>
    public static class WorldSaveFormat
    {
        public const int CurrentVersion = 1;

        public const string Header = "CUBEGAME_WORLD";

        public const string KeySuffix = ".world";

        public const int MaxDeltas = 1000000;
    }

    [Serializable]
    public sealed class WorldDeltaRecord
    {
        public int regionX;
        public int regionZ;
        public int kind;
        public int x;
        public int y;
        public int z;
        public int value;
        public long timeMs;
    }

    [Serializable]
    public sealed class WorldSaveData
    {
        public int formatVersion = WorldSaveFormat.CurrentVersion;

        public string generatorId = string.Empty;

        public long worldSeed;

        public List<WorldDeltaRecord> deltas = new List<WorldDeltaRecord>();

        /// <summary>Explored zones (256×256 voxels) as flat pairs (x0, z0, x1, z1, …) — Alpha "discovered" blob, own format.</summary>
        public List<int> discovered = new List<int>();

        /// <summary>World identity of this file (Alpha <c>worlds.db</c> record: name + seed; the state file is <c>world_&lt;name&gt;.db</c>).</summary>
        public string worldName = string.Empty;

        public long createdUtcTicks;

        public long savedUtcTicks;

        /// <summary>
        /// World time. Alpha stores it inside the world blob (<c>world_&lt;name&gt;.db</c> key <c>time</c>),
        /// so the clock belongs to the world, not to a character/world pair.
        /// </summary>
        public long clockTick;

        public int ticksPerSecond;

        public long worldTimeOffsetMs;
    }

    public interface IWorldSaveSerializer
    {
        string Serialize(WorldSaveData data);

        WorldSaveData Deserialize(string payload);
    }

    public interface ISessionSerializer
    {
        string Serialize(SessionSaveData data);

        /// <summary>Returns null when the payload is not a session save.</summary>
        SessionSaveData Deserialize(string payload);
    }
}
