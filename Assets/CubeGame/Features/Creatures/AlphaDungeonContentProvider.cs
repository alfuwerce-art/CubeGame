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

        /// <summary>Rooms are static geometry; their carving is a separate world step (PENDING).</summary>
        public const string RoomBehaviourKey = AlphaZoneContentProvider.StructureBehaviourKey;

        /// <summary>Chest entity; opening/loot is gameplay (Claude, C5), this provider only places it.</summary>
        public const string ChestBehaviourKey = "chest_static";

        public const string RoomProvenance =
            "PARTIAL: Q-16 room size 3, step 7, L-corridors; carving order/contents UNKNOWN";

        public const string ChestProvenance =
            "Q-17: dungeon chest holds 10 items; loot table OQ-11 (contents come from gameplay)";

        readonly Func<ZoneContentContext, bool> _isDungeonZone;
        readonly int _rooms;
        readonly int _chests;

        /// <param name="isDungeonZone">World rule that decides which zones are dungeons (null → never runs).</param>
        /// <param name="rooms">Rooms in the layout (5×5 lattice walk; default 27 as in the pilot).</param>
        /// <param name="chests">Chests in the dungeon.</param>
        public AlphaDungeonContentProvider(Func<ZoneContentContext, bool> isDungeonZone, int rooms = 27, int chests = 3)
        {
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
            var spawns = BuildPlan(context.Terrain.MasterSeed, zone, _rooms, _chests, context.Spawns.Count,
                out var layout, out var chestPlacements);
            LastLayout = layout;
            LastChests = chestPlacements;
            for (int i = 0; i < spawns.Count; i++) context.Spawns.Add(spawns[i]);
        }

        public AlphaDungeonLayout LastLayout { get; private set; }

        public IReadOnlyList<AlphaDungeonChestPlacement> LastChests { get; private set; }

        /// <summary>
        /// Deterministic dungeon plan of one zone: room spawns then chest spawns, indices starting at
        /// <paramref name="startIndex"/>. Pure: same seed and zone → same list.
        /// </summary>
        public static List<SpawnDefinition> BuildPlan(uint seed, WorldZoneKey zone, int rooms, int chests,
            int startIndex, out AlphaDungeonLayout layout, out IReadOnlyList<AlphaDungeonChestPlacement> chestPlacements)
        {
            if (rooms < 1) throw new ArgumentOutOfRangeException(nameof(rooms));
            if (chests < 0) throw new ArgumentOutOfRangeException(nameof(chests));
            layout = AlphaDungeonLayoutPlanner.Plan(seed, rooms);
            var region = new RegionKey((int)Math.Floor(zone.X / (double)AlphaWorldScale.ZonesPerRegion),
                (int)Math.Floor(zone.Z / (double)AlphaWorldScale.ZonesPerRegion));
            chestPlacements = chests > 0
                ? AlphaDungeonChests.Plan(layout, chests, seed, region.X, region.Z)
                : (IReadOnlyList<AlphaDungeonChestPlacement>)Array.Empty<AlphaDungeonChestPlacement>();
            var result = new List<SpawnDefinition>(layout.Rooms.Count + chestPlacements.Count);
            int index = startIndex;
            foreach (var room in layout.Rooms)
            {
                result.Add(new SpawnDefinition
                {
                    Region = region,
                    SpawnIndex = index++,
                    Kind = new KindId(0),
                    Position = new WorldPosition(room.X, room.Y, room.Z),
                    Faction = (int)CreatureFaction.Passive,
                    Level = 1,
                    ClassId = -1,
                    BehaviourKey = RoomBehaviourKey,
                    Provenance = RoomProvenance,
                });
            }

            for (int i = 0; i < chestPlacements.Count; i++)
            {
                var chest = chestPlacements[i];
                result.Add(new SpawnDefinition
                {
                    Region = region,
                    SpawnIndex = index++,
                    Kind = new KindId(0),
                    Position = new WorldPosition(chest.X, chest.Y, chest.Z),
                    Faction = (int)CreatureFaction.Passive,
                    Level = 1,
                    ClassId = -1,
                    BehaviourKey = ChestBehaviourKey,
                    Provenance = ChestProvenance,
                });
            }

            return result;
        }
    }
}
