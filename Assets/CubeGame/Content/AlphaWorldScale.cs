// CubeGame.Content — Alpha world scale and coordinate keys (D86 M0-02/M0-06/M0-07).
//
// Where the numbers come from (evidence, not invention): M0-02 packet II-b/c executed Cube.exe.
// `WorldInfo_generateBiomeContent(x, z)` is called by the frame updater (0x46ACD1) with zone
// coordinates 0..0xFFFF and the zone grid index `x >> 6 < 0x400`; inside it builds a 256×256 voxel
// field, and the terrain functions take voxel coordinates (`voxel >> 14` = region). Hence:
//
//   zone   = 256 × 256 voxels
//   region = 64 zones = 16 384 voxels
//   world  = 1024 × 1024 regions = 65 536 zones per axis = 2^24 = 16 777 216 voxels
//
// The landmark "cell" of a region (`World_getRegionCellPtr`, `AlphaContentTables.RegionCells = 8`)
// is 8×8 zones = 2 048 voxels; 0x5EFFA0 / 0x5E4850 address it as `voxel >> 11`.
//
// READ THIS BEFORE USING THE GENERATED TABLE: `AlphaContentTables.RegionBlocks = 64` counts ZONES
// PER REGION, not voxels. Reading it as a voxel size (region = 64 voxels, cell = 8 voxels) shrinks
// every placed object by a factor of 256 and puts it in the wrong zone — that was the defect fixed
// by D86 content placement. `WorldBlocks = 65 536` likewise counts zones per world axis.
//
// Coordinates are kept away from Unity: absolute positions are `double` voxels, and the conversion
// to a Unity `Vector3` happens only relative to a moving origin (floating origin), so an 8.4 M
// voxel world position never has to be stored in a 32-bit float.
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    /// <summary>Region key of the Alpha grid (1024×1024 regions per world).</summary>
    public readonly struct WorldRegionKey : IEquatable<WorldRegionKey>
    {
        public readonly int X;
        public readonly int Z;

        public WorldRegionKey(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(WorldRegionKey other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldRegionKey other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Z);
        public override string ToString() => $"region({X},{Z})";
        public static bool operator ==(WorldRegionKey a, WorldRegionKey b) => a.Equals(b);
        public static bool operator !=(WorldRegionKey a, WorldRegionKey b) => !a.Equals(b);
    }

    /// <summary>Zone key of the Alpha grid (256×256 voxels per zone, 65 536 zones per world axis).</summary>
    public readonly struct WorldZoneKey : IEquatable<WorldZoneKey>
    {
        public readonly int X;
        public readonly int Z;

        public WorldZoneKey(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(WorldZoneKey other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldZoneKey other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Z);
        public override string ToString() => $"zone({X},{Z})";
        public static bool operator ==(WorldZoneKey a, WorldZoneKey b) => a.Equals(b);
        public static bool operator !=(WorldZoneKey a, WorldZoneKey b) => !a.Equals(b);
    }

    /// <summary>Landmark cell key (8×8 zones = 2 048 voxels, the unit of the region's zone record grid).</summary>
    public readonly struct WorldCellKey : IEquatable<WorldCellKey>
    {
        public readonly int X;
        public readonly int Z;

        public WorldCellKey(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(WorldCellKey other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldCellKey other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Z);
        public override string ToString() => $"cell({X},{Z})";
        public static bool operator ==(WorldCellKey a, WorldCellKey b) => a.Equals(b);
        public static bool operator !=(WorldCellKey a, WorldCellKey b) => !a.Equals(b);
    }

    /// <summary>
    /// Absolute position in Alpha terrain voxels (1 voxel = 1 CubeGame block = 1 Unity unit).
    /// `double` on purpose: the world is 2^24 voxels wide, a float would snap to ~0.5 voxel.
    /// </summary>
    public readonly struct WorldVoxelPoint : IEquatable<WorldVoxelPoint>
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public WorldVoxelPoint(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>Unity-local x for a floating origin at <paramref name="originX"/>.</summary>
        public float LocalX(double originX) => (float)(X - originX);

        /// <summary>Unity-local y (the vertical axis is never shifted: terrain stays inside a few hundred voxels).</summary>
        public float LocalY => (float)Y;

        /// <summary>Unity-local z for a floating origin at <paramref name="originZ"/>.</summary>
        public float LocalZ(double originZ) => (float)(Z - originZ);

        public bool Equals(WorldVoxelPoint other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is WorldVoxelPoint other && Equals(other);
        public override int GetHashCode() => unchecked(((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode());
        public override string ToString() => $"voxel({X},{Y},{Z})";
    }

    /// <summary>Single truth about the Alpha world scale and the conversions between its grids.</summary>
    public static class AlphaWorldScale
    {
        /// <summary>Zone edge in voxels (Alpha 0x100).</summary>
        public const int ZoneVoxels = 256;

        /// <summary>Zones per region edge (Alpha 0x4000 / 0x100; also `AlphaContentTables.RegionBlocks`).</summary>
        public const int ZonesPerRegion = 64;

        /// <summary>Zones per landmark cell edge (`AlphaContentTables.RegionCells`).</summary>
        public const int ZonesPerCell = 8;

        /// <summary>Region edge in voxels (Alpha 0x4000 = 16 384).</summary>
        public const int RegionVoxels = ZoneVoxels * ZonesPerRegion;

        /// <summary>Landmark cell edge in voxels (8 zones = 2 048; Alpha `voxel >> 11`).</summary>
        public const int CellVoxels = ZoneVoxels * ZonesPerCell;

        /// <summary>Regions per world axis.</summary>
        public const int RegionsPerAxis = 1024;

        /// <summary>Landmark cells per region axis.</summary>
        public const int CellsPerRegion = ZonesPerRegion / ZonesPerCell;

        /// <summary>Landmark cells per world axis.</summary>
        public const int CellsPerAxis = RegionsPerAxis * CellsPerRegion;

        /// <summary>Zones per world axis (65 536; also `AlphaContentTables.WorldBlocks`).</summary>
        public const int ZonesPerAxis = RegionsPerAxis * ZonesPerRegion;

        /// <summary>World edge in voxels (2^24 = 16 777 216).</summary>
        public const int WorldVoxels = ZonesPerAxis * ZoneVoxels;

        /// <summary>Alpha saved hero position 32 800.5 zones (OQ-22 PARTIAL) — the start zone on both axes.</summary>
        public const int StartZoneX = 32800;
        public const int StartZoneZ = 32800;

        /// <summary>Start region of the Alpha hero (512, 512) — centre region of the world.</summary>
        public const int StartRegionX = StartZoneX / ZonesPerRegion;
        public const int StartRegionZ = StartZoneZ / ZonesPerRegion;

        /// <summary>Start voxel = centre of the start zone on both axes.</summary>
        public const int StartVoxelX = StartZoneX * ZoneVoxels + ZoneVoxels / 2;
        public const int StartVoxelZ = StartZoneZ * ZoneVoxels + ZoneVoxels / 2;

        public const string Status =
            "VERIFIED_LAYOUT: zone 256 voxels, 64 zones/region = 16384 voxels, 1024x1024 regions, world 2^24 voxels, " +
            "landmark cell 8 zones = 2048 voxels (executed Alpha generateBiomeContent, M0-02b/c)";

        public static int ZoneOriginVoxel(int zone) => zone * ZoneVoxels;

        public static int CellOriginVoxel(int cell) => cell * CellVoxels;

        public static int RegionOriginVoxel(int region) => region * RegionVoxels;

        /// <summary>Voxel → zone index (floor division, so negative input cannot silently round up).</summary>
        public static int ZoneOfVoxel(int voxel) => FloorDiv(voxel, ZoneVoxels);

        public static int CellOfVoxel(int voxel) => FloorDiv(voxel, CellVoxels);

        public static int RegionOfVoxel(int voxel) => FloorDiv(voxel, RegionVoxels);

        /// <summary>Zone index → landmark cell index.</summary>
        public static int CellOfZone(int zone) => FloorDiv(zone, ZonesPerCell);

        /// <summary>Zone index → region index.</summary>
        public static int RegionOfZone(int zone) => FloorDiv(zone, ZonesPerRegion);

        public static WorldZoneKey ZoneOf(double voxelX, double voxelZ) =>
            new WorldZoneKey(ZoneOfVoxel((int)Math.Floor(voxelX)), ZoneOfVoxel((int)Math.Floor(voxelZ)));

        public static WorldCellKey CellOf(WorldZoneKey zone) => new WorldCellKey(CellOfZone(zone.X), CellOfZone(zone.Z));

        public static WorldRegionKey RegionOf(WorldZoneKey zone) =>
            new WorldRegionKey(RegionOfZone(zone.X), RegionOfZone(zone.Z));

        public static WorldCellKey CellOfVoxelPair(int voxelX, int voxelZ) =>
            new WorldCellKey(CellOfVoxel(voxelX), CellOfVoxel(voxelZ));

        public static WorldRegionKey RegionOfVoxelPair(int voxelX, int voxelZ) =>
            new WorldRegionKey(RegionOfVoxel(voxelX), RegionOfVoxel(voxelZ));

        public static bool IsInsideWorld(WorldZoneKey zone) =>
            zone.X >= 0 && zone.Z >= 0 && zone.X < ZonesPerAxis && zone.Z < ZonesPerAxis;

        public static bool IsInsideWorld(WorldCellKey cell) =>
            cell.X >= 0 && cell.Z >= 0 && cell.X < CellsPerAxis && cell.Z < CellsPerAxis;

        public static bool IsInsideWorld(WorldRegionKey region) =>
            region.X >= 0 && region.Z >= 0 && region.X < RegionsPerAxis && region.Z < RegionsPerAxis;

        public static bool IsInsideWorldVoxel(int voxelX, int voxelZ) =>
            voxelX >= 0 && voxelZ >= 0 && voxelX < WorldVoxels && voxelZ < WorldVoxels;

        /// <summary>Centre of a zone voxel cell (x + 0.5) in absolute voxels.</summary>
        public static double VoxelCentre(int originVoxel, int local) => originVoxel + local + 0.5;

        public static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            if (value % divisor != 0 && (value < 0) != (divisor < 0)) q--;
            return q;
        }

        /// <summary>Problems with the constants themselves; a drift here invalidates every placed object.</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            if (ZoneVoxels != 256) problems.Add($"SCALE_ZONE {ZoneVoxels}");
            if (RegionVoxels != 16384) problems.Add($"SCALE_REGION {RegionVoxels}");
            if (CellVoxels != 2048) problems.Add($"SCALE_CELL {CellVoxels}");
            if (WorldVoxels != 16777216) problems.Add($"SCALE_WORLD {WorldVoxels}");
            if (ZonesPerAxis != 65536) problems.Add($"SCALE_ZONES {ZonesPerAxis}");
            if (RegionsPerAxis != 1024) problems.Add($"SCALE_REGIONS {RegionsPerAxis}");
            if (StartRegionX != 512 || StartRegionZ != 512) problems.Add($"SCALE_START_REGION {StartRegionX},{StartRegionZ}");
            if (StartVoxelX != 8396928) problems.Add($"SCALE_START_VOXEL {StartVoxelX}");
            // The generated table names zones per region; keep the two in sync instead of re-reading it as voxels.
            if (AlphaContentTables.RegionBlocks != ZonesPerRegion) problems.Add("SCALE_TABLE_ZONES_PER_REGION");
            if (AlphaContentTables.RegionCells != ZonesPerCell) problems.Add("SCALE_TABLE_ZONES_PER_CELL");
            if (AlphaContentTables.WorldBlocks != ZonesPerAxis) problems.Add("SCALE_TABLE_ZONES_PER_AXIS");
            // Round trips of the logical grids.
            var zone = new WorldZoneKey(StartZoneX, StartZoneZ);
            if (!IsInsideWorld(zone)) problems.Add("SCALE_START_ZONE_OUTSIDE");
            if (ZoneOfVoxel(ZoneOriginVoxel(zone.X)) != zone.X) problems.Add("SCALE_ZONE_ROUNDTRIP");
            if (RegionOfVoxel(ZoneOriginVoxel(zone.X)) != zone.X / ZonesPerRegion) problems.Add("SCALE_REGION_ROUNDTRIP");
            if (CellOfZone(zone.X) != zone.X / ZonesPerCell) problems.Add("SCALE_CELL_ROUNDTRIP");
            return problems;
        }

        public static string Describe() =>
            $"zone={ZoneVoxels} zonesPerRegion={ZonesPerRegion} region={RegionVoxels} cell={CellVoxels} " +
            $"world={WorldVoxels} start=({StartZoneX},{StartZoneZ})->voxel({StartVoxelX},{StartVoxelZ})";
    }
}
