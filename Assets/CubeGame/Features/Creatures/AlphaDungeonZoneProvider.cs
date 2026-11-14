// CubeGame.Features.Creatures — the ported Alpha dungeon generator 0x5CC510 as the zone content stage (P7-B).
//
// Alpha 0x5E84A2…0x5E84F7: a zone whose 16-byte record (copied to zone+0x78) has type 3 calls
// 0x5CC510(zone, region, zoneX·256+32, zoneZ·256+32, subtype = record sub, flag = 0); type 5 calls it with subtype 3
// and flag 1. Creature level = zone+0x80 (record level), quality = zone+0x84 (record class byte), the furniture kind
// = zone+0x79 (record sub). The call runs on the zone CRT stream right after the terrain passes, before the river
// stones — so this provider must be the first content provider that draws.
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>Alpha dungeon of a type-3/5 zone record: voxels, chests, master, monster groups and guards.</summary>
    public sealed class AlphaDungeonZoneProvider : IZoneContentProvider
    {
        public const string ProviderKey = "alpha.dungeon.0x5CC510";

        public const string Provenance =
            "EMULATED_ALPHA_CODE: 0x5CC510 (subtype 1 checked draw by draw and spawn by spawn); subtypes 0/2/3 same port, " +
            "not checked by an executed trace";

        public string Key => ProviderKey;

        /// <summary>Last generated dungeon of this provider instance (diagnostics, tests).</summary>
        public List<AlphaDungeonObject> LastObjects { get; private set; }

        /// <summary>CRT state the last dungeon started from (Alpha's state at 0x5CC510).</summary>
        public int LastCrtIn { get; private set; }

        public void Populate(ZoneContentContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            AlphaZoneRecord record = context.Record;
            if (record == null || (record.Type != AlphaZoneRecord.Type3 && record.Type != AlphaZoneRecord.Type5))
            {
                return;
            }

            bool flag = record.Type == AlphaZoneRecord.Type5;
            int subtype = flag ? 3 : record.Sub;
            if (subtype == 4 || subtype == 5)
            {
                // 4a tower walls, the subtype-4 tower top and the subtype-5 mound are not ported; the region generator
                // does not write these subtypes into type-3 records, so this branch is not reached in practice.
                return;
            }

            AlphaZone zone = context.Zone;
            AlphaCrtRandom random = context.Random;
            LastCrtIn = random.State;
            int x0 = zone.Zx * AlphaZone.Size + 32;
            int y0 = zone.Zz * AlphaZone.Size + 32;
            var world = new AlphaZoneDungeonWorld(context.Terrain, zone);
            AlphaDungeonRooms cells = AlphaDungeonGenerator.GenerateCells(subtype, flag, random);
            AlphaDungeonBuild build = AlphaDungeonZoneWriter.WriteColumns(cells, world, x0, y0, random);
            List<AlphaDungeonObject> objects = AlphaDungeonContents.Write(cells, build, world, x0, y0, flag, random,
                record.Sub, record.Level, record.LevelClass & 0xFF);
            LastObjects = objects;
            context.Spawns.AddRange(Spawns(objects, new ZoneKey(zone.Zx, zone.Zz), context.Spawns.Count));
        }

        /// <summary>Creature records (chests, master, monsters) as zone spawns; Alpha (x, y, z↑) → CubeGame (x, y↑, z).</summary>
        public static List<SpawnDefinition> Spawns(IEnumerable<AlphaDungeonObject> objects, ZoneKey zone, int startIndex)
        {
            var region = new RegionKey((int)Math.Floor(zone.X / (double)AlphaWorldScale.ZonesPerRegion),
                (int)Math.Floor(zone.Z / (double)AlphaWorldScale.ZonesPerRegion));
            var result = new List<SpawnDefinition>();
            var indexOf = new Dictionary<AlphaDungeonObject, int>();
            int index = startIndex;
            foreach (AlphaDungeonObject o in objects)
            {
                if (o.List != AlphaDungeonObjectList.Creature)
                {
                    continue;
                }

                var position = new WorldPosition(o.X, o.Z, o.Y);
                if (o.Role == 6)
                {
                    result.Add(new SpawnDefinition
                    {
                        Region = region,
                        SpawnIndex = index++,
                        WorldObjectId = AlphaDungeonContentProvider.ChestObjectId,
                        Position = position,
                        Faction = (int)CreatureFaction.Passive,
                        Level = Math.Max(1, o.Level),
                        ClassId = -1,
                        BehaviourKey = AlphaDungeonContentProvider.ChestBehaviourKey,
                        Provenance = "EMULATED_ALPHA_CODE: 0x5CC510 chest 0x8F (position, level); slots drawn on opening " +
                            "(CUBEGAME_CONTRACT)",
                    });
                    continue;
                }

                bool master = (o.Flags & 0x200) != 0;
                var route = new List<WorldPosition>(o.Route.Count);
                foreach ((double X, double Y, double Z) p in o.Route) route.Add(new WorldPosition(p.X, p.Z, p.Y));
                indexOf[o] = index;
                result.Add(new SpawnDefinition
                {
                    Quality = o.Quality,
                    AlphaFlags = o.Flags,
                    PowerScale = o.Power,
                    Route = route,
                    LeaderSpawnIndex = o.Leader != null && indexOf.TryGetValue(o.Leader, out int leader) ? leader : -1,
                    Region = region,
                    SpawnIndex = index++,
                    Kind = new KindId(o.Type),
                    Position = position,
                    Faction = o.Role, // Alpha role 1 = monster, 3 with the generator flag (CreatureFaction numbering)
                    Level = Math.Max(1, o.Level),
                    ClassId = -1,
                    BehaviourKey = master ? AlphaDungeonContents.MasterBehaviourKey : AlphaDungeonContents.CreatureBehaviourKey,
                    Provenance = master
                        ? "EMULATED_ALPHA_CODE: 0x5D3ACD dungeon master (+0x10E8); equipment and potions not delivered (PARTIAL)"
                        : "EMULATED_ALPHA_CODE: 0x5D4AA9 room creature: Sequential[Combat(20), Companion(leader), WalkPath(2)]",
                });
            }

            return result;
        }
    }
}
