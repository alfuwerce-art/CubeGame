// CubeGame.Features.Creatures — D3: dungeon rooms and chests of a dungeon zone.
//
// Alpha evidence (Q-16/Q-17, see AlphaDungeons/AlphaDungeonChests): rooms are 3×3×3 with step 7, corridors
// are L-shaped, a dungeon chest holds exactly 10 items, entrance names are overground-/underground-dungeon01.
// The layout itself is a deterministic CubeGame contract (PARTIAL) — it produces a connected skeleton with
// the proven spacing; carving order, room contents, lighting and encounter tables stay UNKNOWN.
//
// Which world zones are dungeons is NOT assumed here: the caller passes the predicate (Claude's C5 decides
// the world rule), so no invented "every 4th zone is a dungeon" mapping appears in the content layer.
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>Content stage for one dungeon zone: rooms and chests as spawn records.</summary>
    public sealed class AlphaDungeonContentProvider : IZoneContentProvider
    {
        public const string ProviderKey = "deepseek.dungeon-content.v1";

        /// <summary>Room records describe the static geometry carved by this provider.</summary>
        public const string RoomBehaviourKey = AlphaZoneContentProvider.StructureBehaviourKey;

        /// <summary>Chest entity; opening/loot is gameplay (Claude, C5), this provider only places it.</summary>
        public const string ChestBehaviourKey = "chest_static";
        public const int ChestObjectId = 10;

        public const string RoomProvenance =
            "CUBEGAME_CONTRACT: reconstructed layout/carving using Q-16 room spacing and L-corridors";

        public const string ChestProvenance =
            "CUBEGAME_CONTRACT: room-centre anchor; Alpha kind 0x8F and 10 items (Q-17); loot table UNKNOWN";

        readonly Func<ZoneContentContext, bool> _isDungeonZone;
        readonly int _rooms;
        readonly int _chests;

        /// <param name="isDungeonZone">World rule that decides which zones are dungeons (null → never runs).</param>
        /// <param name="rooms">Maximum rooms requested from the 5×5 lattice walk.</param>
        /// <param name="chests">Chests in the dungeon.</param>
        public AlphaDungeonContentProvider(Func<ZoneContentContext, bool> isDungeonZone,
            int rooms = AlphaDungeonLayoutPlanner.MaxRooms, int chests = 3)
        {
            if (rooms < 1 || rooms > AlphaDungeonLayoutPlanner.MaxRooms)
                throw new ArgumentOutOfRangeException(nameof(rooms));
            if (chests < 0) throw new ArgumentOutOfRangeException(nameof(chests));
            _isDungeonZone = isDungeonZone;
            _rooms = rooms;
            _chests = chests;
        }

        public string Key => ProviderKey;

        public void Populate(ZoneContentContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_isDungeonZone == null || !_isDungeonZone(context)) return;
            var zone = new WorldZoneKey(context.Zone.Zx, context.Zone.Zz);
            uint seed = context.Terrain.MasterSeed;
            int extent = (AlphaDungeonLayoutPlanner.LatticeSize - 1) * AlphaDungeonRules.RoomStep
                + AlphaDungeonRules.RoomSize;
            int offset = (AlphaZone.Size - extent) / 2;
            var layout = AlphaDungeonLayoutPlanner.Plan(seed, _rooms,
                zone.X * AlphaZone.Size + offset, zone.Z * AlphaZone.Size + offset);

            // CubeGame placement rule: keep a roof block below the lowest surface along the layout.
            int lowestSurface = int.MaxValue;
            foreach (var (x, _, z) in AlphaDungeonCarver.Cells(layout, 0))
                lowestSurface = Math.Min(lowestSurface, context.Zone.Column(x, z).TopSolid());
            int floorY = lowestSurface - AlphaDungeonRules.RoomSize - 1;
            if (floorY < 1) return;

            var spawns = BuildSpawns(seed, zone, layout, _chests, context.Spawns.Count, floorY + 1,
                out var chestPlacements);
            CarvedVoxels = AlphaDungeonCarver.Carve(layout, floorY, (x, y, z, voxel) => context.Write(x, y, z, voxel));
            LastLayout = layout;
            LastChests = chestPlacements;
            context.Spawns.AddRange(spawns);
        }

        /// <summary>Voxels the last populated dungeon zone cleared (diagnostics).</summary>
        public int CarvedVoxels { get; private set; }

        public AlphaDungeonLayout LastLayout { get; private set; }

        public IReadOnlyList<AlphaDungeonChestPlacement> LastChests { get; private set; }

        /// <summary>
        /// Deterministic local plan for previews: room spawns then chest spawns, indices starting at
        /// <paramref name="startIndex"/>. The terrain provider positions the plan in world coordinates.
        /// </summary>
        public static List<SpawnDefinition> BuildPlan(uint seed, WorldZoneKey zone, int rooms, int chests,
            int startIndex, out AlphaDungeonLayout layout, out IReadOnlyList<AlphaDungeonChestPlacement> chestPlacements)
        {
            if (rooms < 1) throw new ArgumentOutOfRangeException(nameof(rooms));
            if (chests < 0) throw new ArgumentOutOfRangeException(nameof(chests));
            layout = AlphaDungeonLayoutPlanner.Plan(seed, rooms);
            return BuildSpawns(seed, zone, layout, chests, startIndex, 0, out chestPlacements);
        }

        static List<SpawnDefinition> BuildSpawns(uint seed, WorldZoneKey zone, AlphaDungeonLayout layout,
            int chests, int startIndex, int walkY, out IReadOnlyList<AlphaDungeonChestPlacement> chestPlacements)
        {
            var region = new RegionKey((int)Math.Floor(zone.X / (double)AlphaWorldScale.ZonesPerRegion),
                (int)Math.Floor(zone.Z / (double)AlphaWorldScale.ZonesPerRegion));
            var anchors = AlphaDungeonChests.Plan(layout, chests, seed, region.X, region.Z);
            var result = new List<SpawnDefinition>(layout.Rooms.Count + anchors.Count);
            int index = startIndex;
            foreach (var room in layout.Rooms)
            {
                result.Add(new SpawnDefinition
                {
                    Region = region,
                    SpawnIndex = index++,
                    Kind = new KindId(0),
                    Position = new WorldPosition(room.X, room.Y + walkY, room.Z),
                    Faction = (int)CreatureFaction.Passive,
                    Level = 1,
                    ClassId = -1,
                    BehaviourKey = RoomBehaviourKey,
                    Provenance = RoomProvenance,
                });
            }

            for (int i = 0; i < anchors.Count; i++)
            {
                var chest = anchors[i];
                chest.Y += walkY;
                anchors[i] = chest;
                result.Add(new SpawnDefinition
                {
                    Region = region,
                    SpawnIndex = index++,
                    WorldObjectId = ChestObjectId,
                    Position = new WorldPosition(chest.X, chest.Y, chest.Z),
                    Faction = (int)CreatureFaction.Passive,
                    Level = 1,
                    ClassId = -1,
                    BehaviourKey = ChestBehaviourKey,
                    Provenance = ChestProvenance,
                });
            }

            chestPlacements = anchors;
            return result;
        }
    }
}
