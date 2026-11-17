// CubeGame.Features.World.Foundation — G2-1: the three CubeGame structure prototypes.
//
//   * settlement: reserved area + building slots + one road stub + NPC/service anchors (built from SettlementSite);
//   * dungeon:    entrance + deterministic test room, one persistent instance id (built from DungeonEntranceSite);
//   * landmark/camp/ruin/tower: generic pipeline prototypes.
//
// These are prototypes, not content: sizes are small, geometry is a platform with markers, no art is claimed. The
// value is the contract — deterministic identity, a place in the voxel world, anchors the runtime can spawn, and a
// state that survives unload/save/reload.
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Core.Generation;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>One place where a settlement building may stand. G5 fills it; G2-1 only reserves it.</summary>
    public sealed class SettlementBuildingSlot
    {
        public int Index;
        public int X;
        public int Z;
        public int SurfaceY;
        public int Width = 3;
        public int Depth = 3;
        public int Rotation;
        public string PersistentId = string.Empty;
        public string State = "reserved";

        public override string ToString() => $"slot#{Index} ({X},{SurfaceY},{Z}) {State}";
    }

    /// <summary>One road stub cell. The road pass (later stage) connects these points.</summary>
    public sealed class SettlementRoadCell
    {
        public int X;
        public int Z;
        public int SurfaceY;
    }

    /// <summary>Reserved settlement area of one zone: centre, radius, slots, one road stub, NPC/service anchors.</summary>
    public sealed class SettlementBlueprint
    {
        public string PersistentId = string.Empty;
        public int ZoneX;
        public int ZoneZ;
        public int CenterX;
        public int CenterZ;
        public int SurfaceY;
        public int RadiusVoxels = 24;
        public string BiomeId = string.Empty;
        public int Level;
        public string State = "reserved";
        public readonly List<SettlementBuildingSlot> Slots = new List<SettlementBuildingSlot>();
        public readonly List<SettlementRoadCell> RoadCells = new List<SettlementRoadCell>();
        public readonly List<NpcAnchorSpec> Anchors = new List<NpcAnchorSpec>();

        public bool Contains(int x, int z)
        {
            long dx = x - CenterX;
            long dz = z - CenterZ;
            return dx * dx + dz * dz <= (long)RadiusVoxels * RadiusVoxels;
        }

        /// <summary>
        /// Builds the prototype from the seam site. Four slots around the centre, one road stub towards −Z, anchors as
        /// the seam planned them. Surface heights come from the caller (the world), never from a constant.
        /// </summary>
        public static SettlementBlueprint FromSite(SettlementSite site, Func<int, int, int> surfaceAt)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (surfaceAt == null) throw new ArgumentNullException(nameof(surfaceAt));

            var blueprint = new SettlementBlueprint
            {
                PersistentId = site.PersistentId,
                ZoneX = site.ZoneX,
                ZoneZ = site.ZoneZ,
                CenterX = site.CenterX,
                CenterZ = site.CenterZ,
                SurfaceY = surfaceAt(site.CenterX, site.CenterZ),
                BiomeId = site.BiomeId,
                Level = site.Level,
            };

            (int dx, int dz)[] offsets = { (8, 0), (-8, 0), (0, 8), (0, -8) };
            for (int i = 0; i < offsets.Length; i++)
            {
                int x = site.CenterX + offsets[i].dx;
                int z = site.CenterZ + offsets[i].dz;
                blueprint.Slots.Add(new SettlementBuildingSlot
                {
                    Index = i,
                    X = x,
                    Z = z,
                    SurfaceY = surfaceAt(x, z),
                    Rotation = i * 90,
                    PersistentId = $"{site.PersistentId}:building:{i}",
                });
            }

            // One road connection (the seam publishes four; G2-1 builds the −Z stub that any terrain can hold).
            for (int step = 4; step <= site.RadiusVoxels; step += 2)
            {
                int x = site.CenterX;
                int z = site.CenterZ - step;
                blueprint.RoadCells.Add(new SettlementRoadCell { X = x, Z = z, SurfaceY = surfaceAt(x, z) });
            }

            foreach (NpcAnchorSpec anchor in site.Anchors)
            {
                blueprint.Anchors.Add(new NpcAnchorSpec
                {
                    Kind = anchor.Kind,
                    Index = anchor.Index,
                    X = anchor.X,
                    Z = anchor.Z,
                    SurfaceY = surfaceAt(anchor.X, anchor.Z),
                    PersistentId = anchor.PersistentId,
                });
            }

            return blueprint;
        }

        public override string ToString() =>
            $"{PersistentId} zone=({ZoneX},{ZoneZ}) centre=({CenterX},{CenterZ}) biome={BiomeId} " +
            $"slots={Slots.Count} roads={RoadCells.Count} anchors={Anchors.Count} {State}";
    }

    /// <summary>
    /// Deterministic test dungeon behind one entrance: one room, walls, floor and a return portal. The room is carved
    /// inside the entrance zone (so zone streaming owns it) and its identity is the seam's instance id.
    /// </summary>
    public sealed class DungeonPrototype
    {
        public const int RoomWidth = 11;
        public const int RoomDepth = 11;
        public const int RoomHeight = 5;

        /// <summary>East offset of the room centre from the entrance (inside the same zone).</summary>
        public const int RoomOffsetX = 20;

        public string InstanceId = string.Empty;
        public string PortalId = string.Empty;
        public int ZoneX;
        public int ZoneZ;
        public int EntranceX;
        public int EntranceY;
        public int EntranceZ;
        public int ReturnX;
        public int ReturnY;
        public int ReturnZ;
        public int FloorY;
        public int CenterX;
        public int CenterY;
        public int CenterZ;
        public int MinX;
        public int MaxX;
        public int MinZ;
        public int MaxZ;
        public int Level;
        public int Stars;
        public string State = "reserved";

        /// <summary>Problem reported by <see cref="Carve"/> (empty when the room was written).</summary>
        public string Problem = string.Empty;

        public static DungeonPrototype FromSite(DungeonEntranceSite site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            int entryX = (int)Math.Floor((double)site.X);
            int entryZ = (int)Math.Floor((double)site.Z);
            int floorY = site.Y - 8;
            int centerX = entryX + RoomOffsetX;
            int centerZ = entryZ;
            return new DungeonPrototype
            {
                InstanceId = site.DungeonInstanceId,
                PortalId = CubeGameContentIds.Portal(ParseSeedOf(site), site.ZoneX, site.ZoneZ),
                ZoneX = site.ZoneX,
                ZoneZ = site.ZoneZ,
                EntranceX = entryX,
                EntranceY = site.Y,
                EntranceZ = entryZ,
                ReturnX = entryX,
                ReturnY = site.Y + 1,
                ReturnZ = entryZ,
                FloorY = floorY,
                CenterX = centerX,
                CenterY = floorY + 1,
                CenterZ = centerZ,
                MinX = centerX - RoomWidth / 2,
                MaxX = centerX + RoomWidth / 2,
                MinZ = centerZ - RoomDepth / 2,
                MaxZ = centerZ + RoomDepth / 2,
                Level = site.Level,
                Stars = site.Stars,
            };
        }

        /// <summary>
        /// Carves the room into the zone. <paramref name="write"/> is the zone writer (voxel coordinates, packed voxel),
        /// so the plan stays engine-free and testable without a world.
        /// </summary>
        public bool Carve(Func<int, int, int, uint, bool> write, out string problem)
        {
            problem = string.Empty;
            if (write == null)
            {
                problem = "DUNGEON_NO_WRITER";
                return false;
            }

            for (int x = MinX; x <= MaxX; x++)
            {
                for (int z = MinZ; z <= MaxZ; z++)
                {
                    bool wall = x == MinX || x == MaxX || z == MinZ || z == MaxZ;
                    if (wall)
                    {
                        for (int y = FloorY + 1; y <= FloorY + RoomHeight; y++) write(x, y, z, Stone());
                        continue;
                    }

                    write(x, FloorY, z, Stone());
                    for (int y = FloorY + 1; y <= FloorY + RoomHeight; y++) write(x, y, z, Voxel.Air);
                }
            }

            // Entrance shaft from the surface down to the room floor (a 1×1 column keeps the footprint honest).
            for (int y = FloorY + 1; y < EntranceY; y++) write(EntranceX, y, EntranceZ, Voxel.Air);
            State = "generated";
            return true;
        }

        public bool Contains(double x, double y, double z) =>
            x >= MinX && x <= MaxX + 1 && z >= MinZ && z <= MaxZ + 1 && y >= FloorY && y <= FloorY + RoomHeight + 1;

        private static uint Stone() => Voxel.Pack(96, 96, 104, 6);

        private static uint ParseSeedOf(DungeonEntranceSite site)
        {
            // The seam id starts with the seed: seam.dungeon:<seed>:<zoneX>:<zoneZ>.
            string[] parts = site.PersistentId.Split(':');
            return parts.Length >= 3 && uint.TryParse(parts[2], out uint seed) ? seed : 0u;
        }

        public override string ToString() =>
            $"{InstanceId} entrance=({EntranceX},{EntranceY},{EntranceZ}) room=({MinX}…{MaxX},{FloorY},{MinZ}…{MaxZ}) " +
            $"lvl={Level} stars={Stars} {State}";
    }

    /// <summary>Deterministic placement of the three prototypes from the world seed and the zone.</summary>
    public static class CubeGameContentPlans
    {
        /// <summary>Settlement of a zone, or null when this zone does not hold the region's settlement.</summary>
        public static SettlementBlueprint SettlementForZone(uint worldSeed, int zoneX, int zoneZ,
            IWorldSeamProbe probe, StructureRuleSet rules, List<string> problems = null)
        {
            int regionX = AlphaWorldScale.RegionOfZone(zoneX);
            int regionZ = AlphaWorldScale.RegionOfZone(zoneZ);
            SettlementSite site = SettlementSeamPlanner.Plan(worldSeed, regionX, regionZ, probe, rules, problems);
            if (site == null || site.ZoneX != zoneX || site.ZoneZ != zoneZ) return null;
            return SettlementBlueprint.FromSite(site, probe.SurfaceAt);
        }

        /// <summary>Dungeon prototype of a zone, or null when this zone is not the entrance zone of its block.</summary>
        public static DungeonPrototype DungeonForZone(uint worldSeed, int zoneX, int zoneZ, IWorldSeamProbe probe,
            List<string> problems = null)
        {
            int blockX = AlphaWorldScale.RegionOfZone(zoneX) / DungeonSeamPlanner.RegionsPerBlock;
            int blockZ = AlphaWorldScale.RegionOfZone(zoneZ) / DungeonSeamPlanner.RegionsPerBlock;
            if (!DungeonSeamPlanner.IsEntranceZone(zoneX, zoneZ, unchecked((int)worldSeed), blockX, blockZ))
            {
                return null;
            }

            DungeonEntranceSite site = DungeonSeamPlanner.Plan(worldSeed, zoneX, zoneZ, probe, problems);
            return site == null ? null : DungeonPrototype.FromSite(site);
        }

        /// <summary>Stable salt for a rule id (structure candidate hash input).</summary>
        public static int RuleSalt(string ruleId)
        {
            int salt = 0x51ED;
            if (string.IsNullOrEmpty(ruleId)) return salt;
            foreach (char c in ruleId) salt = unchecked(salt * 31 + c);
            return salt;
        }

        /// <summary>Deterministic 0…1 value of a prototype choice at a zone (used by tests and the overlay).</summary>
        public static uint PrototypeHash(uint seed, int zoneX, int zoneZ, int salt) =>
            DeterministicHash.Combine(unchecked((int)seed), zoneX, zoneZ, salt);
    }
}
