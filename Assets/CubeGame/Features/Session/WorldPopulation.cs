using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Features.Session
{
    /// <summary>A world spawn that is currently alive in the game (its zone is active).</summary>
    public sealed class LiveSpawn
    {
        public StableEntityId Id { get; set; }

        public ZoneKey Zone { get; set; }

        public int Channel { get; set; }

        public SpawnDefinition Definition { get; set; }
    }

    /// <summary>
    /// World population in the running game (C6): turns a deterministic <see cref="IWorldPopulationSource"/> into
    /// live entities around the hero and keeps the killed ones dead.
    ///
    /// Activation (CUBEGAME_CONTRACT — Alpha's live limit N-20 and off-screen rule N-21 are not recovered):
    /// the hero's zone and its neighbours within <see cref="ActiveRadius"/> zones are populated; a zone farther than
    /// <see cref="KeepRadius"/> zones is released and its entities despawn. Re-entering a zone rebuilds the same
    /// entities from the same source (same ids), so nothing is duplicated.
    /// Alpha's server (0x5322D0, decomp line ≈2160) despawns a non-player creature when no player is within
    /// <c>creature+0x1D2C + 20</c> blocks; the meaning of +0x1D2C is not established — documented, not ported.
    ///
    /// Killed spawns are stored as <see cref="WorldDeltaKind.EntityStateChanged"/> deltas (X = zone x, Z = zone z,
    /// Y = channel·4096 + index, Value = 1) in the world deltas that the save already writes; they never come back.
    /// Respawn timing of Alpha is not recovered.
    /// </summary>
    public sealed class WorldPopulation
    {
        public const int Killed = 1;

        private readonly List<(IWorldPopulationSource Source, int Channel)> _sources = new List<(IWorldPopulationSource, int)>();
        private readonly WorldDeltaStore _deltas;
        private readonly Dictionary<ZoneKey, List<StableEntityId>> _zones = new Dictionary<ZoneKey, List<StableEntityId>>();
        private readonly Dictionary<StableEntityId, LiveSpawn> _live = new Dictionary<StableEntityId, LiveSpawn>();

        public WorldPopulation(WorldDeltaStore deltas, int activeRadius = 1, int keepRadius = 2)
        {
            _deltas = deltas ?? throw new ArgumentNullException(nameof(deltas));
            if (activeRadius < 0 || keepRadius < activeRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(keepRadius), "need 0 ≤ active ≤ keep");
            }

            ActiveRadius = activeRadius;
            KeepRadius = keepRadius;
        }

        public int ActiveRadius { get; }

        public int KeepRadius { get; }

        public event Action<LiveSpawn> Spawned;

        public event Action<LiveSpawn> Despawned;

        public int LiveCount => _live.Count;

        public IEnumerable<LiveSpawn> Live => _live.Values;

        public IReadOnlyCollection<ZoneKey> ActiveZones => _zones.Keys;

        public void AddSource(IWorldPopulationSource source, int channel)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _sources.Add((source, channel));
        }

        public bool TryGet(StableEntityId id, out LiveSpawn spawn) => _live.TryGetValue(id, out spawn);

        /// <summary>Activates zones around the hero and releases far zones. Call when the hero moves.</summary>
        public void Update(WorldPosition hero)
        {
            ZoneKey center = WorldLayout.ZoneOf((int)Math.Floor(hero.X), (int)Math.Floor(hero.Z));
            var release = new List<ZoneKey>();
            foreach (ZoneKey zone in _zones.Keys)
            {
                if (Math.Abs(zone.X - center.X) > KeepRadius || Math.Abs(zone.Z - center.Z) > KeepRadius)
                {
                    release.Add(zone);
                }
            }

            foreach (ZoneKey zone in release)
            {
                Release(zone);
            }

            for (int dz = -ActiveRadius; dz <= ActiveRadius; dz++)
            {
                for (int dx = -ActiveRadius; dx <= ActiveRadius; dx++)
                {
                    var zone = new ZoneKey(center.X + dx, center.Z + dz);
                    if (zone.X >= 0 && zone.Z >= 0 && zone.X <= 0xFFFF && zone.Z <= 0xFFFF && !_zones.ContainsKey(zone))
                    {
                        Activate(zone);
                    }
                }
            }
        }

        /// <summary>Records a killed world spawn (persistent). Returns false for ids that are not live world spawns.</summary>
        public bool MarkKilled(StableEntityId id, long timeMs)
        {
            if (!_live.TryGetValue(id, out LiveSpawn spawn))
            {
                return false;
            }

            _deltas.Record(new WorldDelta
            {
                Region = RegionOf(spawn.Zone),
                Kind = WorldDeltaKind.EntityStateChanged,
                X = spawn.Zone.X,
                Y = spawn.Channel * 4096 + spawn.Definition.SpawnIndex,
                Z = spawn.Zone.Z,
                Value = Killed,
                TimeMs = timeMs,
            });
            return true;
        }

        public bool IsKilled(ZoneKey zone, int channel, int spawnIndex)
        {
            int y = channel * 4096 + spawnIndex;
            foreach (WorldDelta delta in _deltas.DeltasOf(RegionOf(zone)))
            {
                if (delta.Kind == WorldDeltaKind.EntityStateChanged && delta.X == zone.X && delta.Z == zone.Z && delta.Y == y)
                {
                    return delta.Value == Killed;
                }
            }

            return false;
        }

        /// <summary>Despawns everything (world unload).</summary>
        public void Clear()
        {
            foreach (ZoneKey zone in new List<ZoneKey>(_zones.Keys))
            {
                Release(zone);
            }
        }

        private void Activate(ZoneKey zone)
        {
            var ids = new List<StableEntityId>();
            _zones.Add(zone, ids);
            foreach ((IWorldPopulationSource source, int channel) in _sources)
            {
                foreach (SpawnDefinition definition in source.SpawnsForZone(zone))
                {
                    if (definition == null || IsKilled(zone, channel, definition.SpawnIndex))
                    {
                        continue;
                    }

                    StableEntityId id = StableEntityId.ForZoneSpawn(zone, channel, definition.SpawnIndex);
                    if (_live.ContainsKey(id))
                    {
                        continue;   // a source must not repeat an index; never create a duplicate entity
                    }

                    var live = new LiveSpawn { Id = id, Zone = zone, Channel = channel, Definition = definition };
                    _live.Add(id, live);
                    ids.Add(id);
                    Spawned?.Invoke(live);
                }
            }
        }

        private void Release(ZoneKey zone)
        {
            if (!_zones.TryGetValue(zone, out List<StableEntityId> ids))
            {
                return;
            }

            _zones.Remove(zone);
            foreach (StableEntityId id in ids)
            {
                if (_live.TryGetValue(id, out LiveSpawn live))
                {
                    _live.Remove(id);
                    Despawned?.Invoke(live);
                }
            }
        }

        private static RegionKey RegionOf(ZoneKey zone) => new RegionKey(zone.X >> 6, zone.Z >> 6);
    }
}
