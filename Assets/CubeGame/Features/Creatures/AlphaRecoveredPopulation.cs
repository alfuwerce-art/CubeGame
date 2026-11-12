// CubeGame.Features.Creatures — wild population built with the RECOVERED Alpha rules (D1 REVIEW-FIX).
//
// Alpha path: AlphaWildSpawnRules (3×3 grid, skip, jitter, level range, pack, companion kind) — see that file
// for addresses. What is NOT Alpha here and is labelled so in every definition:
//   * the random numbers: Alpha draws CRT rand() in the order of the whole zone generator, which is not
//     reproduced; this source draws from its own deterministic stream (CUBEGAME_CONTRACT);
//   * the kind: either a caller function (old constructor), or — M0-F1A — the candidate list of the recovered
//     selector AlphaCreatureKindSelector (0x5F56C0, EMULATED_ALPHA_CODE) picked with list[rand() % n] from this
//     source's own stream (the pick rule is Alpha's, the rand() value is not);
//   * the generator conditions listed as NOT MODELLED in AlphaWildSpawnRules.
// So the STRUCTURE of the population follows Alpha; exact Alpha coordinates are not claimed.
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>Zone → wild creature templates by Alpha's grid rules.</summary>
    public sealed class AlphaRecoveredPopulation : IWorldPopulationSource, IAlphaSpawnTemplateSource
    {
        public const string Provenance =
            "grid/level/pack=ALPHA_CODE_STATIC (Cube.exe 0x5EAE6A-0x5EBBB1); random stream/kind source=CUBEGAME_CONTRACT";

        /// <summary>Provenance when the kind comes from the recovered selector 0x5F56C0.</summary>
        public const string RecoveredKindProvenance =
            "grid/level/pack=ALPHA_CODE_STATIC (Cube.exe 0x5EAE6A-0x5EBBB1); kind=EMULATED_ALPHA_CODE 0x5F56C0; random stream=CUBEGAME_CONTRACT";

        public const string ZoneStreamProvenance =
            "ALPHA_CODE_STATIC / ALPHA_DECOMPILED 0x5EAE6A-0x5EBBB1; kind=EMULATED_ALPHA_CODE 0x5F56C0; zone RNG entry=PARTIAL";

        public const string BehaviourKey = "creature_ai";

        /// <summary>Spawn indices of one cell: leader + up to four members (pack range max 5).</summary>
        public const int IndicesPerCell = 8;

        readonly WorldSeed _seed;
        readonly Func<ZoneKey, int, int, int> _kindAt;
        readonly Func<ZoneKey, int, int, double> _groundHeight;
        readonly Func<int, int, int[]> _candidatesAt;
        readonly AlphaTerrainGenerator _terrain;

        public AlphaRecoveredPopulation(AlphaTerrainGenerator terrain) =>
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));

        /// <param name="candidatesAt">absolute voxel x/z → candidate kinds of 0x5F56C0
        /// (<see cref="AlphaCreatureKindSelector.FromClimate"/>); the kind is list[rand() % n], 0x3C when empty.</param>
        /// <param name="groundHeight">zone + local x/z → world Y of the surface; null → 0.</param>
        public static AlphaRecoveredPopulation WithKindSelector(WorldSeed seed, Func<int, int, int[]> candidatesAt,
            Func<ZoneKey, int, int, double> groundHeight = null) =>
            new AlphaRecoveredPopulation(seed, candidatesAt ?? throw new ArgumentNullException(nameof(candidatesAt)),
                groundHeight, true);

        AlphaRecoveredPopulation(WorldSeed seed, Func<int, int, int[]> candidatesAt,
            Func<ZoneKey, int, int, double> groundHeight, bool recoveredKind)
        {
            _seed = seed;
            _candidatesAt = candidatesAt;
            _groundHeight = groundHeight;
        }

        /// <summary>True when kinds come from the recovered selector 0x5F56C0.</summary>
        public bool UsesRecoveredKindSelector => _candidatesAt != null || _terrain != null;

        /// <param name="kindAt">zone + local x/z → Alpha kind (the 0x5F56C0 choice), or −1 for "nothing here".</param>
        /// <param name="groundHeight">zone + local x/z → world Y of the surface; null → 0.</param>
        public AlphaRecoveredPopulation(WorldSeed seed, Func<ZoneKey, int, int, int> kindAt,
            Func<ZoneKey, int, int, double> groundHeight = null)
        {
            _seed = seed;
            _kindAt = kindAt ?? throw new ArgumentNullException(nameof(kindAt));
            _groundHeight = groundHeight;
        }

        public IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone)
        {
            var list = new List<SpawnDefinition>();
            if (_terrain != null)
            {
                foreach (AlphaSpawnTemplateData template in ZoneStreamTemplates(zone)) list.Add(template.Definition);
                return list;
            }

            var stream = new Stream(_seed.Value, zone);
            var region = new RegionKey((int)Math.Floor(zone.X / (double)AlphaWorldScale.ZonesPerRegion),
                (int)Math.Floor(zone.Z / (double)AlphaWorldScale.ZonesPerRegion));
            for (int cx = 0; cx < AlphaWildSpawnRules.CellsPerAxis; cx++)
            {
                for (int cz = 0; cz < AlphaWildSpawnRules.CellsPerAxis; cz++)
                {
                    int cell = cx * AlphaWildSpawnRules.CellsPerAxis + cz;
                    if (AlphaWildSpawnRules.CellSkipped(stream.Next())) continue;
                    int localX = AlphaWildSpawnRules.CellCoordinate(cx, stream.Next());
                    int localZ = AlphaWildSpawnRules.CellCoordinate(cz, stream.Next());
                    int kind;
                    if (_candidatesAt != null)
                    {
                        int[] candidates = _candidatesAt(zone.X * AlphaWorldScale.ZoneVoxels + localX,
                            zone.Z * AlphaWorldScale.ZoneVoxels + localZ);
                        kind = AlphaCreatureKindSelector.Pick(candidates, stream.Next());
                    }
                    else
                    {
                        kind = _kindAt(zone, localX, localZ);
                        if (kind < 0) continue;
                    }

                    double leaderX = zone.X * (double)AlphaWorldScale.ZoneVoxels + localX;
                    double leaderZ = zone.Z * (double)AlphaWorldScale.ZoneVoxels + localZ;
                    bool passive = CreatureKinds.IsPassiveObject(kind);
                    list.Add(Definition(zone, region, cell * IndicesPerCell, kind, passive, leaderX, leaderZ, stream));
                    if (passive) continue;
                    int extra = AlphaWildSpawnRules.ExtraPackMembers(kind, stream.Next());
                    for (int i = 0; i < extra && i < IndicesPerCell - 1; i++)
                    {
                        AlphaWildSpawnRules.PackOffset(i, extra, out double dx, out double dz);
                        int memberKind = AlphaWildSpawnRules.CompanionKind(kind, stream.Next());
                        list.Add(Definition(zone, region, cell * IndicesPerCell + 1 + i, memberKind, false,
                            leaderX + dx, leaderZ + dz, stream));
                    }
                }
            }

            return list;
        }

        SpawnDefinition Definition(ZoneKey zone, RegionKey region, int index, int kind, bool passive, double x, double z,
            Stream stream)
        {
            int level = passive ? 1 : AlphaWildSpawnRules.Level(kind, stream.Next());
            float yaw = stream.Next() * 360f / 32767f;   // Alpha: rand()·360/32767 (template +0x54)
            double y = 0.0;
            if (_groundHeight != null)
            {
                y = _groundHeight(zone, (int)Math.Floor(x - zone.X * (double)AlphaWorldScale.ZoneVoxels),
                    (int)Math.Floor(z - zone.Z * (double)AlphaWorldScale.ZoneVoxels));
            }

            return new SpawnDefinition
            {
                Region = region,
                SpawnIndex = index,
                Kind = new KindId(kind),
                Position = new WorldPosition(x, y, z),
                Yaw = yaw,
                Faction = passive ? AlphaWildSpawnRules.FactionPassive : AlphaWildSpawnRules.FactionHostile,
                Level = level,
                ClassId = -1,
                SpecializationId = 0,
                Equipment = Array.Empty<RawAlphaItemId>(),
                BehaviourKey = passive ? "static_object" : BehaviourKey,
                Provenance = _candidatesAt != null ? RecoveredKindProvenance : Provenance,
            };
        }

        /// <summary>
        /// Runtime templates of a zone (M0-F1A): the definitions plus Alpha's per-template fields. A hostile template
        /// of the grid gets the window 06:00…24:00 (0x5EB838…0x5EB845); every template keeps the constructor's
        /// activation radius 200 (0x5ADB29) — the wild grid does not overwrite it.
        /// </summary>
        public IReadOnlyList<AlphaSpawnTemplateData> TemplatesForZone(ZoneKey zone)
        {
            if (_terrain != null) return ZoneStreamTemplates(zone);
            IReadOnlyList<SpawnDefinition> definitions = SpawnsForZone(zone);
            var list = new List<AlphaSpawnTemplateData>(definitions.Count);
            foreach (SpawnDefinition definition in definitions)
            {
                bool hostile = definition.Faction == AlphaWildSpawnRules.FactionHostile;
                list.Add(new AlphaSpawnTemplateData(definition,
                    hostile ? AlphaWildSpawnRules.HostileWindowStartMs : 0,
                    hostile ? AlphaWildSpawnRules.HostileWindowEndMs : 0,
                    AlphaSpawnLifecycle.DefaultActivationRadius));
            }

            return list;
        }

        private IReadOnlyList<AlphaSpawnTemplateData> ZoneStreamTemplates(ZoneKey key)
        {
            var result = new List<AlphaSpawnTemplateData>();
            AlphaZone zone = _terrain.Zone(key.X, key.Z);
            if (zone == null) return result;

            var random = new AlphaCrtRandom(zone.PopulationRandomState);
            int originX = key.X * AlphaZone.Size;
            int originZ = key.Z * AlphaZone.Size;
            LandmarkColumn landmark = _terrain.ColumnAt(originX, originZ);
            var region = new RegionKey(key.X >> 6, key.Z >> 6);
            const int cellsPerAxis = AlphaWildSpawnRules.CellsPerAxis;
            for (int cell = 0; cell < cellsPerAxis * cellsPerAxis; cell++)
            {
                if (AlphaWildSpawnRules.CellSkipped(random.Next())) continue;
                int x = originX + AlphaWildSpawnRules.CellCoordinate(cell / cellsPerAxis, random.Next());
                int z = originZ + AlphaWildSpawnRules.CellCoordinate(cell % cellsPerAxis, random.Next());
                if (landmark != null && landmark.Type != LandmarkType.None && landmark.Type != LandmarkType.Village &&
                    AlphaTerrainGenerator.Falloff(_terrain.LandmarkDistance2(landmark, x, z)) > 0.3f)
                    continue;

                VoxelColumn column = zone.Column(x, z);
                if (AlphaWildSpawnRules.ClimateSkipped(column.Humidity, column.Temperature, random.Next) ||
                    _terrain.HeightAtXY(x, z) > 0f || _terrain.RotateAndPlace(x, z) < 1f)
                    continue;

                int y = column.Bottom;
                while (Voxel.IsSolid(column.Read(y))) y++;

                // The leader draws orientation before its kind. The ground override branches then
                // consume their own rolls and send faction-6 placements down a separate content path.
                float yaw = random.Next() * 360f / AlphaCrtRandom.RandMax;
                var climate = new AlphaClimateSample(column.Humidity, column.Temperature, column.Rare, _terrain.Mountain(x, z));
                int[] candidates = AlphaCreatureKindSelector.Candidates(climate, y);
                int kind = AlphaCreatureKindSelector.Pick(candidates, candidates.Length == 0 ? 0 : random.Next());
                if (GroundOverridesCreature(Voxel.Material(column.Read(y - 1)), y, climate, random)) continue;

                random.Next(); // rand()%100 flag 0x200, whose downstream behaviour is still open.
                int level = AlphaWildSpawnRules.Level(kind, random.Next());
                int index = cell * IndicesPerCell;
                var home = new WorldPosition(x + 0.5, y, z + 0.5);
                AddActor(result, region, index, kind, level, yaw, home, true);

                int members = AlphaWildSpawnRules.ExtraPackMembers(kind, random.Next());
                for (int i = 0; i < members; i++)
                {
                    AlphaWildSpawnRules.PackOffset(i, members, out double dx, out double dz);
                    int member = AlphaWildSpawnRules.CompanionKind(kind, random.Next);
                    int memberLevel = AlphaWildSpawnRules.Level(member, random.Next());
                    float memberYaw = random.Next() * 360f / AlphaCrtRandom.RandMax;
                    var at = new WorldPosition(home.X + (int)(dx * 65536) / 65536.0, home.Y,
                        home.Z + (int)(dz * 65536) / 65536.0);
                    AddActor(result, region, index + 1 + i, member, memberLevel, memberYaw, at, false);
                }
            }
            return result;
        }

        private static bool GroundOverridesCreature(int material, int height, AlphaClimateSample climate, AlphaCrtRandom random)
        {
            if (material == 12)
            {
                random.Next();
                return true;
            }
            if (height < 0) return false;
            if (material == 4 || material == 5 || material == 9)
            {
                if (random.Next() % 3 != 0 && climate.Humidity > 0.8f && climate.Temperature < 0.1f)
                {
                    random.Next();
                    return true;
                }
                if (material == 4 && random.Next() % 3 != 0 && climate.Temperature > 0.1f)
                {
                    random.Next();
                    return true;
                }
            }
            else if (material == 10 && AlphaWildSpawnRules.CellSkipped(random.Next()))
            {
                random.Next();
                return true;
            }
            return false;
        }

        private static void AddActor(List<AlphaSpawnTemplateData> result, RegionKey region, int index, int kind, int level,
            float yaw, WorldPosition position, bool leader)
        {
            // Keep catalogued passive objects out of the combat/actor channel.
            if (CreatureKinds.IsPassiveObject(kind)) return;
            var actor = new SpawnDefinition
            {
                Region = region,
                SpawnIndex = index,
                Kind = new KindId(kind),
                Position = position,
                Yaw = yaw,
                Faction = AlphaWildSpawnRules.FactionHostile,
                Level = level,
                ClassId = 0,
                BehaviourKey = BehaviourKey,
                Provenance = ZoneStreamProvenance,
            };
            result.Add(new AlphaSpawnTemplateData(actor, leader ? AlphaWildSpawnRules.HostileWindowStartMs : 0,
                leader ? AlphaWildSpawnRules.HostileWindowEndMs : 0));
        }

        /// <summary>CUBEGAME_CONTRACT: deterministic stand-in for CRT rand() (15-bit values, like MSVC).</summary>
        sealed class Stream
        {
            ulong _state;

            public Stream(uint seed, ZoneKey zone)
            {
                _state = seed * 0x9E3779B97F4A7C15UL ^ ((ulong)(uint)zone.X << 32 | (uint)zone.Z) * 0xC2B2AE3D27D4EB4FUL;
                if (_state == 0) _state = 1;
            }

            public int Next()
            {
                _state ^= _state >> 33;
                _state *= 0xFF51AFD7ED558CCDUL;
                _state ^= _state >> 29;
                _state += 0x9E3779B97F4A7C15UL;
                return (int)((_state >> 20) & 0x7FFF);
            }
        }
    }
}
