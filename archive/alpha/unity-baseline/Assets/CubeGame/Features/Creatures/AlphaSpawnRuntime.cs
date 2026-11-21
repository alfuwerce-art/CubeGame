// CubeGame.Features.Creatures — runtime of Alpha creature templates (M0-F1A).
//
// Turns recovered spawn templates into live creatures with Alpha's server-tick rules (AlphaSpawnLifecycle):
//
//   template (per loaded zone)
//     → every tick: respawn timer -= elapsed real ms                                    0x60DF9F  ALPHA_CODE_STATIC
//     → create when: time-of-day window holds, no live creature of this template,
//       timer == 0 OR day of death != current day, a player closer than radius (200)    0x60FBC1… ALPHA_CODE_STATIC
//     → creature at the TEMPLATE position (never re-rolled)                            ALPHA_DECOMPILED
//     → death: timer = 1 200 000 ms, day of death = current day                         0x596C30  ALPHA_CODE_STATIC
//     → live creature farther than 512 from every player is removed, template stays    ALPHA_DECOMPILED
//     → the template is evaluated again on later ticks (re-activation).
//
// Identity: a template is Alpha's (zone, index) pair (creature +0x1B0/+0x1B4/+0x1B8 points back to it), mapped to
// the existing StableEntityId.ForZoneSpawn(zone, channel, index). It is derived from the zone and the index the
// deterministic source gives the template — never from a runtime instance — so unload/reload, despawn/recreate
// and save/load keep it.
//
// CUBEGAME_CONTRACT (not Alpha, Alpha behaviour UNKNOWN): template state (timer, day of death) is kept per stable id
// while its zone is unloaded and restored on reload; it does not tick while unloaded (Alpha ticks only templates
// of loaded zones). Persistence across a save: SnapshotStates()/RestoreStates() give the data; the save format is
// NOT changed here (PERSISTENCE_SCHEMA_HANDOFF, see docs/reports/M0_F1A_POPULATION_RUNTIME_REPORT.md).
// Distances are 3-D in voxels (1 Alpha unit = 1 voxel); whether Alpha's activation test is 2-D or 3-D is PARTIAL.
//
// This class is engine-free: it raises events; the game creates/destroys combatants and visuals.
// It is NOT FallbackPopulationPolicy and does not use AlphaSpawnTable (vendor stock).
using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Creatures
{
    /// <summary>One Alpha template as produced by a source: definition plus the cube::Spawn runtime fields.</summary>
    public sealed class AlphaSpawnTemplateData
    {
        public AlphaSpawnTemplateData(SpawnDefinition definition, int windowStartMs, int windowEndMs,
            float activationRadius = AlphaSpawnLifecycle.DefaultActivationRadius,
            IReadOnlyList<AlphaScheduleEntry> schedule = null, IReadOnlyList<WorldPosition> walkPath = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            WindowStartMs = windowStartMs;
            WindowEndMs = windowEndMs;
            ActivationRadius = activationRadius;
            Schedule = schedule ?? Array.Empty<AlphaScheduleEntry>();
            WalkPath = walkPath ?? Array.Empty<WorldPosition>();
        }

        public SpawnDefinition Definition { get; }

        /// <summary>Template +0x40 / +0x44; equal values = no window.</summary>
        public int WindowStartMs { get; }

        public int WindowEndMs { get; }

        /// <summary>Template +0x08.</summary>
        public float ActivationRadius { get; }

        /// <summary>Template +0x10A0 (SpawnLocationBehavior list); empty for wild creatures.</summary>
        public IReadOnlyList<AlphaScheduleEntry> Schedule { get; }

        /// <summary>WalkPathBehavior points; empty when the creature has none.</summary>
        public IReadOnlyList<WorldPosition> WalkPath { get; }
    }

    /// <summary>Deterministic per-zone templates (same zone → same list, same indices).</summary>
    public interface IAlphaSpawnTemplateSource
    {
        IReadOnlyList<AlphaSpawnTemplateData> TemplatesForZone(ZoneKey zone);
    }

    /// <summary>Persisted state of one template (what Alpha keeps in +0x38 / +0x3C).</summary>
    public struct AlphaSpawnTemplateState
    {
        public StableEntityId Id;
        public int RespawnTimerMs;
        public int DayOfDeath;
    }

    /// <summary>A template of a loaded zone.</summary>
    public sealed class AlphaSpawnTemplate
    {
        internal AlphaSpawnTemplate(StableEntityId id, ZoneKey zone, int channel, AlphaSpawnTemplateData data)
        {
            Id = id;
            Zone = zone;
            Channel = channel;
            Data = data;
        }

        public StableEntityId Id { get; }

        public ZoneKey Zone { get; }

        public int Channel { get; }

        public AlphaSpawnTemplateData Data { get; }

        public SpawnDefinition Definition => Data.Definition;

        /// <summary>Template +0x38 (ms of real time).</summary>
        public int RespawnTimerMs { get; internal set; }

        /// <summary>Template +0x3C.</summary>
        public int DayOfDeath { get; internal set; }

        /// <summary>A live creature of this template exists (Alpha: lookup by template id +0x48).</summary>
        public bool Live { get; internal set; }

        /// <summary>Number of creatures created from this template so far (diagnostics only).</summary>
        public int Generation { get; internal set; }
    }

    public enum AlphaDespawnReason
    {
        /// <summary>Farther than 512 from every player (template stays, timer untouched).</summary>
        OutOfRange = 0,

        /// <summary>The zone of the template was unloaded.</summary>
        ZoneUnloaded = 1,
    }

    /// <summary>Template → creature runtime with Alpha's server-tick rules.</summary>
    public sealed class AlphaSpawnRuntime
    {
        public const string Status =
            "rules=ALPHA_CODE_STATIC (0x60FBC1-0x60FC23, 0x596C30, 0x60DF9F) + ALPHA_DECOMPILED (despawn 512); " +
            "unload/save of template state=CUBEGAME_CONTRACT (Alpha UNKNOWN)";

        /// <summary>Channel of the existing ForZoneSpawn id space used for creatures/NPCs.</summary>
        public const int CreatureChannel = 0;

        readonly List<(IAlphaSpawnTemplateSource Source, int Channel)> _sources = new List<(IAlphaSpawnTemplateSource, int)>();
        readonly Dictionary<ZoneKey, List<AlphaSpawnTemplate>> _zones = new Dictionary<ZoneKey, List<AlphaSpawnTemplate>>();
        readonly Dictionary<StableEntityId, AlphaSpawnTemplate> _templates = new Dictionary<StableEntityId, AlphaSpawnTemplate>();
        readonly Dictionary<StableEntityId, AlphaSpawnTemplateState> _parked = new Dictionary<StableEntityId, AlphaSpawnTemplateState>();
        readonly List<string> _problems = new List<string>();

        /// <summary>A creature must be created from this template now (at its template position).</summary>
        public event Action<AlphaSpawnTemplate> Created;

        /// <summary>The live creature of this template must be removed (not a death).</summary>
        public event Action<AlphaSpawnTemplate, AlphaDespawnReason> Despawned;

        public void AddSource(IAlphaSpawnTemplateSource source, int channel = CreatureChannel)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (channel < 0 || channel > 0xF) throw new ArgumentOutOfRangeException(nameof(channel));
            _sources.Add((source, channel));
        }

        public IReadOnlyCollection<ZoneKey> LoadedZones => _zones.Keys;

        public IEnumerable<AlphaSpawnTemplate> Templates => _templates.Values;

        public int TemplateCount => _templates.Count;

        public int LiveCount
        {
            get
            {
                int n = 0;
                foreach (AlphaSpawnTemplate t in _templates.Values) if (t.Live) n++;
                return n;
            }
        }

        /// <summary>Source defects found while loading (duplicate index, index out of range).</summary>
        public IReadOnlyList<string> Problems => _problems;

        public bool TryGet(StableEntityId id, out AlphaSpawnTemplate template) => _templates.TryGetValue(id, out template);

        public bool IsLoaded(ZoneKey zone) => _zones.ContainsKey(zone);

        /// <summary>Stable id of a template: Alpha's (zone, index) in the existing ForZoneSpawn space.</summary>
        public static StableEntityId IdOf(ZoneKey zone, int channel, int spawnIndex) =>
            StableEntityId.ForZoneSpawn(zone, channel, spawnIndex);

        /// <summary>Loads the templates of a zone (no creature is created until <see cref="Tick"/>).</summary>
        public void LoadZone(ZoneKey zone)
        {
            if (_zones.ContainsKey(zone)) return;
            var list = new List<AlphaSpawnTemplate>();
            _zones.Add(zone, list);
            foreach ((IAlphaSpawnTemplateSource source, int channel) in _sources)
            {
                foreach (AlphaSpawnTemplateData data in source.TemplatesForZone(zone))
                {
                    if (data == null) continue;
                    int index = data.Definition.SpawnIndex;
                    if (index < 0 || index > 0xFFF)
                    {
                        _problems.Add($"SPAWN_INDEX_OUT_OF_RANGE {zone} ch{channel} {index}");
                        continue;
                    }

                    StableEntityId id = IdOf(zone, channel, index);
                    if (_templates.ContainsKey(id))
                    {
                        _problems.Add($"SPAWN_INDEX_DUPLICATE {zone} ch{channel} {index}");
                        continue;
                    }

                    var template = new AlphaSpawnTemplate(id, zone, channel, data);
                    if (_parked.TryGetValue(id, out AlphaSpawnTemplateState state))
                    {
                        template.RespawnTimerMs = state.RespawnTimerMs;
                        template.DayOfDeath = state.DayOfDeath;
                        _parked.Remove(id);
                    }

                    _templates.Add(id, template);
                    list.Add(template);
                }
            }
        }

        /// <summary>Unloads a zone: its live creatures are removed, template state is parked by stable id.</summary>
        public void UnloadZone(ZoneKey zone)
        {
            if (!_zones.TryGetValue(zone, out List<AlphaSpawnTemplate> list)) return;
            _zones.Remove(zone);
            foreach (AlphaSpawnTemplate template in list)
            {
                if (template.Live)
                {
                    template.Live = false;
                    Despawned?.Invoke(template, AlphaDespawnReason.ZoneUnloaded);
                }

                _templates.Remove(template.Id);
                if (template.RespawnTimerMs != 0 || template.DayOfDeath != 0)
                {
                    _parked[template.Id] = new AlphaSpawnTemplateState
                    {
                        Id = template.Id, RespawnTimerMs = template.RespawnTimerMs, DayOfDeath = template.DayOfDeath,
                    };
                }
            }
        }

        /// <summary>Keeps exactly the given zones loaded (unload others first, then load new ones).</summary>
        public void SetLoadedZones(IEnumerable<ZoneKey> zones)
        {
            var wanted = new HashSet<ZoneKey>(zones ?? throw new ArgumentNullException(nameof(zones)));
            foreach (ZoneKey zone in new List<ZoneKey>(_zones.Keys))
            {
                if (!wanted.Contains(zone)) UnloadZone(zone);
            }

            foreach (ZoneKey zone in wanted) LoadZone(zone);
        }

        /// <summary>
        /// One server tick. <paramref name="elapsedRealMs"/> decrements respawn timers (real time, like Alpha);
        /// <paramref name="worldTimeMs"/> is the time of day; <paramref name="currentDay"/> the day counter (+0x800160).
        /// <paramref name="creaturePosition"/> gives the current position of a live creature (null → template position).
        /// </summary>
        public void Tick(int elapsedRealMs, int worldTimeMs, int currentDay, IReadOnlyList<WorldPosition> players,
            Func<StableEntityId, WorldPosition> creaturePosition = null)
        {
            if (elapsedRealMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedRealMs));
            if (players == null) throw new ArgumentNullException(nameof(players));
            var templates = new List<AlphaSpawnTemplate>(_templates.Values);
            foreach (AlphaSpawnTemplate t in templates)
            {
                t.RespawnTimerMs = AlphaSpawnLifecycle.TickTimer(t.RespawnTimerMs, elapsedRealMs);
            }

            foreach (AlphaSpawnTemplate t in templates)
            {
                if (t.Live)
                {
                    WorldPosition at = creaturePosition != null ? creaturePosition(t.Id) : t.Definition.Position;
                    if (AlphaSpawnLifecycle.ShouldDespawn(NearestDistance(players, at)))
                    {
                        t.Live = false;
                        Despawned?.Invoke(t, AlphaDespawnReason.OutOfRange);
                    }

                    continue;
                }

                if (AlphaSpawnLifecycle.ShouldCreate(false, t.RespawnTimerMs, t.DayOfDeath, currentDay,
                        t.Data.WindowStartMs, t.Data.WindowEndMs, worldTimeMs,
                        NearestDistance(players, t.Definition.Position), t.Data.ActivationRadius))
                {
                    t.Live = true;
                    t.Generation++;
                    Created?.Invoke(t);
                }
            }
        }

        /// <summary>The creature of a template died (0x596C30): timer 1 200 000 ms, day of death = current day.</summary>
        public bool NotifyDeath(StableEntityId id, int currentDay)
        {
            if (!_templates.TryGetValue(id, out AlphaSpawnTemplate t) || !t.Live) return false;
            t.Live = false;
            t.RespawnTimerMs = AlphaSpawnLifecycle.RespawnTimerMs;
            t.DayOfDeath = currentDay;
            return true;
        }

        /// <summary>
        /// State of every template that differs from a fresh one (loaded and parked), for the save.
        /// PERSISTENCE_SCHEMA_HANDOFF: the shared save format is not changed by this module.
        /// </summary>
        public List<AlphaSpawnTemplateState> SnapshotStates()
        {
            var list = new List<AlphaSpawnTemplateState>(_parked.Values);
            foreach (AlphaSpawnTemplate t in _templates.Values)
            {
                if (t.RespawnTimerMs != 0 || t.DayOfDeath != 0)
                {
                    list.Add(new AlphaSpawnTemplateState { Id = t.Id, RespawnTimerMs = t.RespawnTimerMs, DayOfDeath = t.DayOfDeath });
                }
            }

            list.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
            return list;
        }

        /// <summary>Restores saved template states (call before loading zones, or at any time; loaded ones update).</summary>
        public void RestoreStates(IEnumerable<AlphaSpawnTemplateState> states)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            foreach (AlphaSpawnTemplateState s in states)
            {
                if (_templates.TryGetValue(s.Id, out AlphaSpawnTemplate t))
                {
                    t.RespawnTimerMs = s.RespawnTimerMs;
                    t.DayOfDeath = s.DayOfDeath;
                }
                else
                {
                    _parked[s.Id] = s;
                }
            }
        }

        /// <summary>Removes every live creature and template (world unload); parked state is kept.</summary>
        public void Clear()
        {
            foreach (ZoneKey zone in new List<ZoneKey>(_zones.Keys)) UnloadZone(zone);
        }

        static double NearestDistance(IReadOnlyList<WorldPosition> players, WorldPosition at)
        {
            double best = double.PositiveInfinity;
            for (int i = 0; i < players.Count; i++)
            {
                double dx = players[i].X - at.X, dy = players[i].Y - at.Y, dz = players[i].Z - at.Z;
                double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d < best) best = d;
            }

            return best;
        }
    }

    /// <summary>Alpha day counter (+0x800160) for the game clock.</summary>
    public static class AlphaGameDay
    {
        public const string Status =
            "day counter +0x800160 incremented when the world time wraps (0x60C6C0); start 09:00 and rate ×10 as GameClock";

        /// <summary>Day number of a world-time total: the clock starts on day 0 at 09:00 (32 400 000 ms).</summary>
        public static int Of(long tick, int ticksPerSecond)
        {
            if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
            const long start = 32_400_000L;
            const long rate = 10L;
            long total = start + tick * 1000L / ticksPerSecond * rate;
            return (int)(total / AlphaSpawnLifecycle.DayMs);
        }

        public static int Of(CubeGame.Features.Session.GameClock clock) =>
            (clock ?? throw new ArgumentNullException(nameof(clock))).WorldDay;
    }
}
