using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Content stage of zone generation (DeepSeek #13: vegetation, structures, towns, dungeons, creatures).
    /// Alpha continues <c>WorldInfo_generateBiomeContent</c> after the terrain passes with the same zone CRT stream:
    /// first the giant tree of a Tree zone (0x5DF960), then <c>World_populateRegionDecorations</c> (0x5CC510, zone
    /// records of type 3 and 5) and the object/creature placement that follows. A provider ports one of those parts.
    ///
    /// Contract:
    /// <list type="bullet">
    /// <item>Called once per generated zone, on a worker thread, after <see cref="AlphaZoneFeatures"/>; providers run in
    /// registration order and share <see cref="ZoneContentContext.Random"/> — the Alpha zone stream exactly where the
    /// terrain passes left it (a provider that ports an Alpha function consumes rand() like that function).</item>
    /// <item>Deterministic: same seed and zone → same writes and spawns; no Unity API, no shared mutable state.</item>
    /// <item>Voxel writes go through <see cref="ZoneContentContext.Write"/> (Alpha <c>tilemap_writeGlyphColumn</c>
    /// semantics) and stay inside the zone (CubeGame rule, like the terrain features).</item>
    /// <item>Spawns are <see cref="SpawnDefinition"/> records with a stable <see cref="SpawnDefinition.SpawnIndex"/>; the
    /// gameplay side turns them into persistent entities (<see cref="StableEntityId.ForSpawn"/>).</item>
    /// </list>
    /// Changing the registered providers changes the generated world: the generator id must change with it.
    /// </summary>
    public interface IZoneContentProvider
    {
        /// <summary>Stable key recorded with the world (provenance, debugging).</summary>
        string Key { get; }

        void Populate(ZoneContentContext context);
    }

    /// <summary>Everything a content provider may read or change for one zone.</summary>
    public sealed class ZoneContentContext
    {
        private readonly AlphaColors _colors;

        internal ZoneContentContext(AlphaTerrainGenerator terrain, AlphaColors colors, AlphaZone zone, AlphaCrtRandom random)
        {
            Terrain = terrain;
            _colors = colors;
            Zone = zone;
            Random = random;
            Region = terrain.RegionRecord(zone.Zx >> 6, zone.Zz >> 6);
            Record = Region.Zone(zone.Zx & 63, zone.Zz & 63);
            Landmark = terrain.ColumnAt(zone.Zx * AlphaZone.Size, zone.Zz * AlphaZone.Size);
        }

        public AlphaTerrainGenerator Terrain { get; }

        public AlphaZone Zone { get; }

        /// <summary>Zone grid record (type 1 town / 3 settlement / 4 type-10 zone, level, class).</summary>
        public AlphaZoneRecord Record { get; }

        public AlphaRegion Region { get; }

        /// <summary>Landmark column whose 8×8-zone block holds this zone (Alpha local_1304); null outside the world.</summary>
        public LandmarkColumn Landmark { get; }

        /// <summary>Alpha zone stream <c>srand(seed + zz·0x10000 + zx)</c>, after the terrain passes.</summary>
        public AlphaCrtRandom Random { get; }

        /// <summary>Candidates recorded by the terrain passes (caves, river beds, giant tree position).</summary>
        public IReadOnlyList<ZoneSpawnCandidate> Candidates => Zone.Spawns;

        public List<SpawnDefinition> Spawns => Zone.ContentSpawns;

        public bool Contains(int x, int z)
        {
            int lx = x - Zone.Zx * AlphaZone.Size;
            int lz = z - Zone.Zz * AlphaZone.Size;
            return lx >= 0 && lz >= 0 && lx < AlphaZone.Size && lz < AlphaZone.Size;
        }

        /// <summary>Reads a voxel of this zone (Alpha read rule, <see cref="VoxelColumn.Read"/>).</summary>
        public uint Read(int x, int y, int z)
        {
            if (!Contains(x, z))
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"({x},{z}) outside zone ({Zone.Zx},{Zone.Zz})");
            }

            return Zone.Column(x, z).Read(y);
        }

        /// <summary>
        /// Writes a voxel like Alpha <c>tilemap_writeGlyphColumn</c> (air below y = 1 becomes water, gaps below the
        /// column are filled with stone, explicitly placed voxels — flag 0x80 — keep their mark). Returns false
        /// outside the zone (the write is dropped, CubeGame rule).
        /// </summary>
        public bool Write(int x, int y, int z, uint voxel)
        {
            if (!Contains(x, z))
            {
                return false;
            }

            AlphaZoneGenerator.Write(_colors, Zone.Column(x, z), x, z, y, voxel);
            return true;
        }
    }
}
