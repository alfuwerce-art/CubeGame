using System;
using System.Collections.Generic;

namespace CubeGame.Features.Contracts
{
    /// <summary>
    /// Content-provided description of one spawn in a region (DeepSeek: spawn tables / structures, issue #13).
    /// The gameplay side turns it into a persistent entity with <see cref="StableEntityId.ForSpawn"/>.
    /// Class/specialisation/weapon of an NPC come from the spawn record, not from the species (B P1-6).
    /// </summary>
    public sealed class SpawnDefinition
    {
        public RegionKey Region { get; set; }

        /// <summary>Stable index of this spawn inside its region (0..4095), deterministic from the region seed.</summary>
        public int SpawnIndex { get; set; }

        public KindId Kind { get; set; }

        public int? WorldObjectId { get; set; }

        public WorldPosition Position { get; set; }

        public float Yaw { get; set; }

        /// <summary>Faction id (Alpha: 6 = passive object/plant, no walk/action timer — D69).</summary>
        public int Faction { get; set; }

        public int Level { get; set; } = 1;

        /// <summary>Core CharacterClass id for humanoid NPCs; -1 when the species has no class.</summary>
        public int ClassId { get; set; } = -1;

        public int SpecializationId { get; set; }

        /// <summary>Equipment as raw Alpha ids (converted by the item adapter on spawn).</summary>
        public IReadOnlyList<RawAlphaItemId> Equipment { get; set; } = Array.Empty<RawAlphaItemId>();

        /// <summary>Raw Alpha weapon (type 3) proved to be in the main hand; null if the hand is unknown or empty.</summary>
        public RawAlphaItemId? MainHandWeapon { get; set; }

        /// <summary>Alpha spawn quality byte (+0x58, becomes the creature's stars +0x1A8); 0 when unknown.</summary>
        public int Quality { get; set; }

        /// <summary>Alpha spawn flags word (+0x7A): 0x1000 dungeon/hostile group creature, 0x200 elite/master.</summary>
        public int AlphaFlags { get; set; }

        /// <summary>Alpha power factor scale of the spawn (+0xF60 multiplier, 1 = the kind's own value).</summary>
        public float PowerScale { get; set; } = 1f;

        /// <summary>Waypoints of the spawn's WalkPathBehavior (0x58CFA0), world voxels; empty = none.</summary>
        public IReadOnlyList<WorldPosition> Route { get; set; } = Array.Empty<WorldPosition>();

        /// <summary>Spawn index (same zone and channel) of the leader a CompanionBehavior follows; −1 = none.</summary>
        public int LeaderSpawnIndex { get; set; } = -1;

        /// <summary>Behaviour profile key owned by Features/AI (DeepSeek); empty = static object.</summary>
        public string BehaviourKey { get; set; } = string.Empty;

        /// <summary>Evidence status of this record, e.g. EMULATED_ALPHA_CODE, PLACEHOLDER_NOT_ALPHA.</summary>
        public string Provenance { get; set; } = string.Empty;
    }

    /// <summary>
    /// Population of the world (D88: implemented by DeepSeek in Features/Creatures or Content, consumed by gameplay).
    /// Deterministic: the same seed and zone always give the same list, so unloading and reloading a zone never
    /// creates new entities. Gameplay turns each record into a persistent entity (StableEntityId.ForSpawn),
    /// a combatant, AI and a visual, and applies saved deltas (killed/looted) on top.
    /// </summary>
    public interface IWorldPopulationSource
    {
        /// <summary>Spawns (creatures, NPCs, passive objects) of one zone; empty list for an empty zone.</summary>
        IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone);
    }

    /// <summary>One persistent change of a generated world region relative to the generator output.</summary>
    public sealed class WorldDelta
    {
        public RegionKey Region { get; set; }

        public WorldDeltaKind Kind { get; set; }

        /// <summary>Block coordinates for block edits; spawn index for entity/container changes.</summary>
        public int X { get; set; }

        public int Y { get; set; }

        public int Z { get; set; }

        /// <summary>New block/state value; meaning depends on <see cref="Kind"/>.</summary>
        public int Value { get; set; }

        /// <summary>Game time (ms) when the change happened; later deltas on the same key win.</summary>
        public long TimeMs { get; set; }
    }

    public enum WorldDeltaKind
    {
        BlockSet = 0,
        ObjectRemoved = 1,
        ContainerOpened = 2,
        EntityStateChanged = 3,

        /// <summary>Height-field edit: X/Z column, Value = new top solid block Y (M0-02).</summary>
        ColumnSurfaceSet = 4,
    }

    /// <summary>
    /// Compact persisted state of a world entity while its region is unloaded (M0-00C §8, offscreen policy
    /// FROZEN until B P1-5 research: no offscreen AI ticks).
    /// </summary>
    public sealed class NpcStateSnapshot
    {
        public StableEntityId Id { get; set; }

        public KindId Kind { get; set; }

        public bool Alive { get; set; } = true;

        public double Health { get; set; }

        public WorldPosition Position { get; set; }

        public float Yaw { get; set; }

        /// <summary>AI state key owned by Features/AI; opaque for the gameplay core.</summary>
        public string AiState { get; set; } = string.Empty;

        public long LastUpdateMs { get; set; }
    }

    /// <summary>Version tags written into every CubeGame save section.</summary>
    public static class SaveVersion
    {
        /// <summary>Core character save (S02 SaveGame). Unchanged by M0-01; v2 needs a separate producer decision.</summary>
        public const int CharacterSave = 1;

        /// <summary>Session sidecar (world, clock, hero placement) introduced by M0-01.</summary>
        public const int SessionSave = 1;
    }
}
