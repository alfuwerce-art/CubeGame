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

        /// <summary>
        /// M0-04B, optional: placement of the Core items of the character save (same order): bag slot ≥ 0 or
        /// −(1+equipment slot). Missing (older slots) → items go to the bags, first weapon to the main hand.
        /// </summary>
        public int[] inventoryPlacement;

        /// <summary>M0-04B, optional: Alpha items Core does not model (consumables, materials…) — parallel arrays.</summary>
        public int[] rawItemTypes;

        public int[] rawItemSubtypes;

        public int[] rawItemCounts;

        public int[] rawItemSlots;

        /// <summary>FNV-1a 64 (hex) of the framed character save text written in the same checkpoint.</summary>
        public string characterChecksum = string.Empty;

        /// <summary>FNV-1a 64 (hex) of the framed world file of the same checkpoint; empty = slot written before M0-02 (no world file).</summary>
        public string worldChecksum = string.Empty;
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
