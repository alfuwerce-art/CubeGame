using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Items;
using CubeGame.Features.Session;
using CubeGame.Features.World;

namespace CubeGame.Features.Persistence
{
    public enum AlphaLoadStatus
    {
        Success = 0,
        NotFound = 1,
        Corrupted = 2,
        UnsupportedVersion = 3,
        GeneratorMismatch = 4,
        IoError = 5,
    }

    /// <summary>Position of one character in one world (Alpha character blob field 18 value: pos + rotation).</summary>
    public sealed class AlphaWorldPosition
    {
        public double X;
        public double Y;
        public double Z;
        public float Yaw;
        public long SavedUtcTicks;
    }

    /// <summary>
    /// One entry of the character's per-world position map. Alpha keys that map by world seed + world name
    /// (character blob field 18/19, <c>WorldInfo_mapInsertUnique(seed, name)</c>), not by a save slot.
    /// </summary>
    public sealed class AlphaWorldVisit
    {
        public long Seed;
        public string WorldName = string.Empty;
        public AlphaWorldPosition Position = new AlphaWorldPosition();

        /// <summary>Alpha map key: requested seed and world name together.</summary>
        public static string KeyOf(long seed, string worldName) =>
            seed.ToString(CultureInfo.InvariantCulture) + "|" + (worldName ?? string.Empty);

        public string Key => KeyOf(Seed, WorldName);
    }

    /// <summary>
    /// One record of <c>worlds.db</c>: an independent world list entry (name + seed + preview).
    /// Alpha <c>WorldInfo::ctor_0</c> 0x466A70: +0x08 name, +0x20 seed, +0x24 unknown int, +0x04 preview.
    /// </summary>
    public sealed class AlphaWorldRecord
    {
        public int Id;
        public string Name = string.Empty;
        public uint Seed;
        public string GeneratorId = string.Empty;
        public long CreatedUtcTicks;
        public long SavedUtcTicks;

        /// <summary>
        /// Alpha WorldInfo +0x24. The saved Alpha world carries 1 and a newly created world carries 0
        /// (<c>loadStructureBlob</c>/<c>saveStructureBlob</c> 0x4878A0); the meaning is UNKNOWN, so it is
        /// preserved instead of being interpreted.
        /// </summary>
        public int LegacyField24;

        /// <summary>Alpha per-character map key of this world (seed + name).</summary>
        public string VisitKey => AlphaWorldVisit.KeyOf(Seed, Name);

        public WorldInfo ToWorldInfo() => new WorldInfo(Name, new WorldSeed(Seed), GeneratorId, CreatedUtcTicks);

        public override string ToString() => $"{Id}: {Name} seed={Seed}";
    }

    /// <summary>
    /// One record of <c>characters.db</c>: an independent character list entry plus the per-world position map.
    /// Alpha <c>GameController</c> reads <c>num</c> from the blob store and one Creature per index 0..num-1;
    /// the hero payload itself is the unchanged CubeGame character save (bridge format allowed by D92/P9).
    /// </summary>
    public sealed class AlphaCharacterRecord
    {
        /// <summary>Index in <c>characters.db</c>; −1 until the record has been saved.</summary>
        public int Id = -1;
        public string Name = string.Empty;
        public CharacterClass Class;
        public int Level = 1;
        public HeroAppearance Appearance = AlphaHeroCreation.Default;
        public long SavedUtcTicks;
        public long LastWorldSeed;
        public string LastWorldName = string.Empty;
        public readonly List<AlphaWorldVisit> Visits = new List<AlphaWorldVisit>();

        public AlphaWorldVisit FindVisit(AlphaWorldRecord world) =>
            world == null ? null : FindVisit(world.Seed, world.Name);

        public AlphaWorldVisit FindVisit(uint seed, string worldName)
        {
            string key = AlphaWorldVisit.KeyOf(seed, worldName);
            foreach (AlphaWorldVisit visit in Visits)
            {
                if (visit != null && visit.Key == key) return visit;
            }

            return null;
        }

        public AlphaWorldVisit Remember(AlphaWorldRecord world, WorldPosition position, float yaw, long nowUtcTicks)
        {
            if (world == null) return null;
            AlphaWorldVisit visit = FindVisit(world);
            if (visit == null)
            {
                visit = new AlphaWorldVisit { Seed = world.Seed, WorldName = world.Name };
                Visits.Add(visit);
            }

            visit.Position.X = position.X;
            visit.Position.Y = position.Y;
            visit.Position.Z = position.Z;
            visit.Position.Yaw = yaw;
            visit.Position.SavedUtcTicks = nowUtcTicks;
            LastWorldSeed = world.Seed;
            LastWorldName = world.Name;
            SavedUtcTicks = nowUtcTicks;
            return visit;
        }

        public override string ToString() => $"{Id}: {Name} ({Class}) L{Level}";
    }

    public sealed class AlphaCharacterLoad
    {
        public AlphaLoadStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public AlphaCharacterRecord Record { get; set; }
        public HeroState Hero { get; set; }
        public SessionSaveData Session { get; set; }
        public bool IsSuccess => Status == AlphaLoadStatus.Success;
        public override string ToString() => $"{Status}: {Message}";
    }

    public sealed class AlphaWorldLoad
    {
        public AlphaLoadStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public AlphaWorldRecord Record { get; set; }
        public WorldDeltaStore Deltas { get; set; }
        public long ClockTick { get; set; }
        public long WorldTimeOffsetMs { get; set; }
        public int TicksPerSecond { get; set; }
        public bool IsSuccess => Status == AlphaLoadStatus.Success;
        public override string ToString() => $"{Status}: {Message}";
    }

    /// <summary>
    /// Alpha-shaped save selection store: characters and worlds are two independent lists
    /// (<c>Save/characters.db</c> and <c>Save/worlds.db</c>), and a character keeps its own position per world
    /// key (seed + name). Nothing here pairs a character with a world slot: the world list is shared by every
    /// character, exactly like the Alpha select screens 0x483E70 / 0x484170.
    ///
    /// File layout (CubeGame bridge format, P9 allows it as long as the user-visible semantics match Alpha):
    /// <c>characters.index</c> / <c>worlds.index</c> — the lists (<c>num</c> + one line per record, Alpha keys
    /// <c>num</c> and decimal index); <c>character_&lt;id&gt;</c> + <c>character_&lt;id&gt;.session</c> — the hero
    /// payload and its per-world map; <c>world_&lt;id&gt;</c> — the world state (deltas, explored zones, clock).
    /// </summary>
    public sealed class AlphaProfileStore
    {
        public const string CharactersIndexKey = "characters.index";
        public const string WorldsIndexKey = "worlds.index";

        private const string CharactersHeader = "CUBEGAME_CHARACTERS";
        private const string WorldsHeader = "CUBEGAME_WORLDS";
        private const int IndexVersion = 1;

        private static readonly string[] LegacySlotKeys = { "slot0", "slot1", "slot2" };

        private readonly ISaveStore _store;
        private readonly ISaveSerializer _characterSerializer;
        private readonly ISessionSerializer _sessionSerializer;
        private readonly IWorldSaveSerializer _worldSerializer;
        private readonly List<AlphaCharacterRecord> _characters = new List<AlphaCharacterRecord>();
        private readonly List<AlphaWorldRecord> _worlds = new List<AlphaWorldRecord>();

        public AlphaProfileStore(ISaveStore store, ISaveSerializer characterSerializer, ISessionSerializer sessionSerializer,
            IWorldSaveSerializer worldSerializer)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _characterSerializer = characterSerializer ?? throw new ArgumentNullException(nameof(characterSerializer));
            _sessionSerializer = sessionSerializer ?? throw new ArgumentNullException(nameof(sessionSerializer));
            _worldSerializer = worldSerializer ?? throw new ArgumentNullException(nameof(worldSerializer));
        }

        public IReadOnlyList<AlphaCharacterRecord> Characters => _characters;

        public IReadOnlyList<AlphaWorldRecord> Worlds => _worlds;

        /// <summary>Characters first, worlds second — the two Alpha blob stores are loaded independently.</summary>
        public string LastReport { get; private set; } = string.Empty;

        public static string CharacterKey(int id) => "character_" + id.ToString(CultureInfo.InvariantCulture);

        public static string CharacterSessionKey(int id) => CharacterKey(id) + SessionSaveFormat.KeySuffix;

        public static string WorldKey(int id) => "world_" + id.ToString(CultureInfo.InvariantCulture);

        public bool Reload()
        {
            _characters.Clear();
            _worlds.Clear();
            var report = new StringBuilder();

            bool migrated = false;
            if (!_store.Exists(CharactersIndexKey) || !_store.Exists(WorldsIndexKey))
            {
                migrated = MigrateLegacySlots(report);
                // The migration writes the indexes; the lists are rebuilt below from what is now stored.
                _characters.Clear();
                _worlds.Clear();
            }

            if (!LoadCharacters(report) || !LoadWorlds(report))
            {
                LastReport = report.ToString();
                return false;
            }

            if (!migrated && _characters.Count == 0 && _worlds.Count == 0)
            {
                report.Append("empty");
            }

            LastReport = report.ToString();
            return true;
        }

        public AlphaWorldRecord FindWorld(uint seed, string name)
        {
            foreach (AlphaWorldRecord world in _worlds)
            {
                if (world.Seed == seed && string.Equals(world.Name, name, StringComparison.Ordinal)) return world;
            }

            return null;
        }

        public AlphaWorldRecord WorldById(int id)
        {
            foreach (AlphaWorldRecord world in _worlds)
            {
                if (world.Id == id) return world;
            }

            return null;
        }

        public AlphaCharacterRecord CharacterById(int id)
        {
            foreach (AlphaCharacterRecord character in _characters)
            {
                if (character.Id == id) return character;
            }

            return null;
        }

        /// <summary>
        /// The world the request resolves to: an existing record with the same seed+name, otherwise a new record
        /// (Alpha <c>WorldInfo::ctor_0</c> 0x46A8A0 creates the missing <c>worlds.db</c> entry on load).
        /// </summary>
        public AlphaWorldRecord ResolveWorld(string name, uint seed, string generatorId, long nowUtcTicks, out bool created)
        {
            created = false;
            AlphaWorldRecord world = FindWorld(seed, name);
            if (world != null) return world;

            world = new AlphaWorldRecord
            {
                Id = NextWorldId(),
                Name = name ?? string.Empty,
                Seed = seed,
                GeneratorId = generatorId ?? string.Empty,
                CreatedUtcTicks = nowUtcTicks,
                SavedUtcTicks = nowUtcTicks,
            };
            _worlds.Add(world);
            created = true;
            return world;
        }

        public SaveResult AddWorld(AlphaWorldRecord world, long nowUtcTicks)
        {
            if (world == null) return SaveResult.Fail(SaveStatus.ValidationFailed, "world is null");
            if (FindWorld(world.Seed, world.Name) != null)
            {
                return SaveResult.Fail(SaveStatus.ValidationFailed, $"world '{world.Name}' with this seed already exists");
            }

            world.Id = NextWorldId();
            if (world.CreatedUtcTicks == 0) world.CreatedUtcTicks = nowUtcTicks;
            world.SavedUtcTicks = nowUtcTicks;
            _worlds.Add(world);
            return WriteWorldsIndex();
        }

        public SaveResult SaveCharacter(AlphaCharacterRecord record, HeroState hero, SessionSaveData session, long nowUtcTicks)
        {
            if (record == null || hero == null || session == null)
            {
                return SaveResult.Fail(SaveStatus.ValidationFailed, "character save arguments are incomplete");
            }

            if (record.Id < 0)
            {
                record.Id = NextCharacterId();
            }

            if (!_characters.Contains(record)) _characters.Add(record);

            record.Name = hero.Name;
            record.Class = hero.Sheet.Class;
            record.Level = hero.Sheet.Progression.Level;
            record.Appearance = hero.Appearance;
            record.SavedUtcTicks = nowUtcTicks;

            string key = CharacterKey(record.Id);
            var service = new SaveService(_store, _characterSerializer, key);
            hero.SyncItemsFromInventory();
            SaveGame character = CharacterSaveMapper.ToSaveGame(hero.Sheet, hero.Items);
            SaveResult characterResult = service.Save(character);
            if (!characterResult.IsSuccess) return characterResult;

            string characterText;
            try
            {
                characterText = _store.Read(key);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.IoError, $"character save not readable after write: {exception.Message}");
            }

            session.formatVersion = SessionSaveFormat.CurrentVersion;
            session.savedUtcTicks = nowUtcTicks;
            session.heroName = hero.Name;
            session.raceId = hero.RaceId;
            session.heroHealth = hero.Health.CurrentHealth;
            session.heroGender = hero.Appearance.Gender;
            session.heroFace = hero.Appearance.Face;
            session.heroHaircut = hero.Appearance.Haircut;
            session.heroHairR = hero.Appearance.HairR;
            session.heroHairG = hero.Appearance.HairG;
            session.heroHairB = hero.Appearance.HairB;
            session.heroSkillRanks = hero.SkillRanks.ToArray();
            session.heroCopper = hero.Copper;
            session.heroManaCubes = hero.ManaCubes;
            session.heroPlatinum = hero.Platinum;
            session.huntMissions = GameSlotStore.WriteMissions(hero.Missions);
            session.inventoryPlacement = hero.InventoryPlacement();
            GameSlotStore.WriteRawItems(hero, session);
            GameSlotStore.WriteRecipes(hero, session);
            session.characterChecksum = Checksum(characterText ?? string.Empty);
            session.worldName = record.LastWorldName;
            session.worldSeed = record.LastWorldSeed;
            try
            {
                WriteVisits(record);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.IoError, $"character world map write failed: {exception.Message}");
            }

            string payload;
            try
            {
                payload = _sessionSerializer.Serialize(session);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.SerializationFailed, exception.Message);
            }

            if (string.IsNullOrEmpty(payload))
            {
                return SaveResult.Fail(SaveStatus.SerializationFailed, "session serializer returned empty text");
            }

            try
            {
                _store.Write(CharacterSessionKey(record.Id),
                    GameSlotStore.Frame(SessionSaveFormat.Header, SessionSaveFormat.CurrentVersion, payload));
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.IoError, $"character session write failed: {exception.Message}");
            }

            return WriteCharactersIndex();
        }

        public SaveResult SaveWorld(AlphaWorldRecord record, WorldSaveData data, long nowUtcTicks)
        {
            if (record == null || data == null)
            {
                return SaveResult.Fail(SaveStatus.ValidationFailed, "world save arguments are incomplete");
            }

            record.SavedUtcTicks = nowUtcTicks;
            data.worldName = record.Name;
            data.worldSeed = record.Seed;
            data.generatorId = record.GeneratorId;
            data.createdUtcTicks = record.CreatedUtcTicks;
            data.savedUtcTicks = nowUtcTicks;

            string payload;
            try
            {
                payload = _worldSerializer.Serialize(data);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.SerializationFailed, $"world serialization failed: {exception.Message}");
            }

            if (string.IsNullOrEmpty(payload))
            {
                return SaveResult.Fail(SaveStatus.SerializationFailed, "world serializer returned empty text");
            }

            try
            {
                _store.Write(WorldKey(record.Id),
                    GameSlotStore.Frame(WorldSaveFormat.Header, WorldSaveFormat.CurrentVersion, payload));
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.IoError, $"world write failed: {exception.Message}");
            }

            return WriteWorldsIndex();
        }

        public AlphaCharacterLoad LoadCharacter(int id)
        {
            AlphaCharacterRecord record = CharacterById(id);
            if (record == null)
            {
                return FailCharacter(AlphaLoadStatus.NotFound, $"character {id} is not in {CharactersIndexKey}");
            }

            string key = CharacterKey(id);
            string sessionKey = CharacterSessionKey(id);
            if (!_store.Exists(key) || !_store.Exists(sessionKey))
            {
                return FailCharacter(AlphaLoadStatus.NotFound, $"character {id} has no complete checkpoint");
            }

            string characterText;
            string sessionText;
            try
            {
                characterText = _store.Read(key);
                sessionText = _store.Read(sessionKey);
            }
            catch (Exception exception)
            {
                return FailCharacter(AlphaLoadStatus.IoError, exception.Message);
            }

            if (!GameSlotStore.TryUnframe(sessionText, SessionSaveFormat.Header, out int version, out string payload, out string error))
            {
                return FailCharacter(AlphaLoadStatus.Corrupted, error);
            }

            if (version != SessionSaveFormat.CurrentVersion)
            {
                return FailCharacter(AlphaLoadStatus.UnsupportedVersion,
                    $"session format {version}, supported {SessionSaveFormat.CurrentVersion}");
            }

            SessionSaveData session;
            try
            {
                session = _sessionSerializer.Deserialize(payload);
            }
            catch (Exception exception)
            {
                return FailCharacter(AlphaLoadStatus.Corrupted, $"session payload unreadable: {exception.Message}");
            }

            if (session == null) return FailCharacter(AlphaLoadStatus.Corrupted, "session payload unreadable");

            if (!string.IsNullOrEmpty(session.characterChecksum) &&
                !string.Equals(session.characterChecksum, Checksum(characterText ?? string.Empty), StringComparison.Ordinal))
            {
                return FailCharacter(AlphaLoadStatus.Corrupted, "character save and session belong to different checkpoints");
            }

            LoadResult character = new SaveService(_store, _characterSerializer, key).Load();
            if (!character.IsSuccess)
            {
                return FailCharacter(AlphaLoadStatus.Corrupted, $"character save: {character}");
            }

            CharacterSheet sheet = CharacterSaveMapper.ToCharacterSheet(character.Data);
            List<ItemInstance> items = CharacterSaveMapper.ToItems(character.Data);
            var hero = new HeroState(session.heroName, session.raceId, sheet)
            {
                NeedsGroundPlacement = false,
                Copper = session.heroCopper,
                ManaCubes = session.heroManaCubes,
                Platinum = session.heroPlatinum,
            };
            if (session.heroGender >= 0)
            {
                HeroAppearance d = AlphaHeroCreation.Default;
                hero.Appearance = AlphaHeroCreation.Normalize(new HeroAppearance(
                    hero.RaceId,
                    session.heroGender,
                    session.heroFace < 0 ? d.Face : session.heroFace,
                    session.heroHaircut < 0 ? d.Haircut : session.heroHaircut,
                    session.heroHairR < 0 ? d.HairR : (byte)session.heroHairR,
                    session.heroHairG < 0 ? d.HairG : (byte)session.heroHairG,
                    session.heroHairB < 0 ? d.HairB : (byte)session.heroHairB));
            }

            hero.Items.AddRange(items);
            if (session.heroSkillRanks != null && session.heroSkillRanks.Length != 0)
            {
                hero.SkillRanks.TryReplace(session.heroSkillRanks);
            }

            hero.RestoreInventory(session.inventoryPlacement);
            RestoreRawItems(hero, session);
            GameSlotStore.RestoreRecipes(hero, session);
            if (session.huntMissions != null)
            {
                foreach (HuntMissionSaveData mission in session.huntMissions)
                {
                    if (mission == null) continue;
                    hero.Missions.TryRestore(new HuntMission(new StableEntityId(unchecked((ulong)mission.target)),
                        new KindId(mission.kind), mission.complete));
                }
            }

            if (session.heroHealth >= 0.0) hero.RestoreHealth(session.heroHealth);

            record.Name = hero.Name;
            record.Class = sheet.Class;
            record.Level = sheet.Progression.Level;
            record.Appearance = hero.Appearance;
            record.LastWorldSeed = session.worldSeed;
            record.LastWorldName = session.worldName ?? string.Empty;
            ReadVisits(record);

            return new AlphaCharacterLoad
            {
                Status = AlphaLoadStatus.Success,
                Record = record,
                Hero = hero,
                Session = session,
            };
        }

        public AlphaWorldLoad LoadWorld(int id, string expectedGeneratorId)
        {
            AlphaWorldRecord record = WorldById(id);
            if (record == null)
            {
                return FailWorld(AlphaLoadStatus.NotFound, $"world {id} is not in {WorldsIndexKey}");
            }

            if (!string.IsNullOrEmpty(expectedGeneratorId) &&
                !string.Equals(expectedGeneratorId, record.GeneratorId, StringComparison.Ordinal))
            {
                return FailWorld(AlphaLoadStatus.GeneratorMismatch,
                    $"world was generated by '{record.GeneratorId}', game runs '{expectedGeneratorId}'");
            }

            var deltas = new WorldDeltaStore();
            var result = new AlphaWorldLoad { Status = AlphaLoadStatus.Success, Record = record, Deltas = deltas };

            if (!_store.Exists(WorldKey(id)))
            {
                // A world record without a state file is a world that has never been entered: Alpha
                // regenerates it from the seed and keeps no deltas (World_open(seed, name) 0x46A8A0).
                result.TicksPerSecond = 0;
                return result;
            }

            string text;
            try
            {
                text = _store.Read(WorldKey(id));
            }
            catch (Exception exception)
            {
                return FailWorld(AlphaLoadStatus.IoError, exception.Message);
            }

            if (!GameSlotStore.TryUnframe(text, WorldSaveFormat.Header, out int version, out string payload, out string error))
            {
                return FailWorld(AlphaLoadStatus.Corrupted, error);
            }

            if (version != WorldSaveFormat.CurrentVersion)
            {
                return FailWorld(AlphaLoadStatus.UnsupportedVersion,
                    $"world format {version}, supported {WorldSaveFormat.CurrentVersion}");
            }

            WorldSaveData data;
            try
            {
                data = _worldSerializer.Deserialize(payload);
            }
            catch (Exception exception)
            {
                return FailWorld(AlphaLoadStatus.Corrupted, $"world payload unreadable: {exception.Message}");
            }

            if (data == null || data.deltas == null || data.discovered == null)
            {
                return FailWorld(AlphaLoadStatus.Corrupted, "world payload unreadable");
            }

            if (data.worldSeed != record.Seed || (!string.IsNullOrEmpty(data.worldName) &&
                !string.Equals(data.worldName, record.Name, StringComparison.Ordinal)))
            {
                return FailWorld(AlphaLoadStatus.Corrupted, "world file describes another world");
            }

            if (data.deltas.Count > WorldSaveFormat.MaxDeltas || data.discovered.Count % 2 != 0)
            {
                return FailWorld(AlphaLoadStatus.Corrupted, "world file sizes are invalid");
            }

            foreach (WorldDeltaRecord delta in data.deltas)
            {
                if (delta == null || !Enum.IsDefined(typeof(WorldDeltaKind), delta.kind))
                {
                    return FailWorld(AlphaLoadStatus.Corrupted, "world delta record is invalid");
                }

                var region = new RegionKey(delta.regionX, delta.regionZ);
                if (!WorldLayout.IsInside(region))
                {
                    return FailWorld(AlphaLoadStatus.Corrupted, $"world delta outside the world: {region}");
                }

                deltas.Record(new WorldDelta
                {
                    Region = region,
                    Kind = (WorldDeltaKind)delta.kind,
                    X = delta.x,
                    Y = delta.y,
                    Z = delta.z,
                    Value = delta.value,
                    TimeMs = delta.timeMs,
                });
            }

            for (int i = 0; i < data.discovered.Count; i += 2)
            {
                deltas.MarkDiscovered(new ZoneKey(data.discovered[i], data.discovered[i + 1]));
            }

            result.ClockTick = data.clockTick;
            result.WorldTimeOffsetMs = data.worldTimeOffsetMs;
            result.TicksPerSecond = data.ticksPerSecond;
            return result;
        }

        public void DeleteWorld(int id)
        {
            _worlds.RemoveAll(world => world.Id == id);
            try
            {
                if (_store.Exists(WorldKey(id))) _store.Delete(WorldKey(id));
            }
            catch (Exception)
            {
                // The list entry is already gone; a stale state file is harmless (it is not indexed).
            }

            WriteWorldsIndex();
        }

        // ----- index files -----

        private bool LoadCharacters(StringBuilder report)
        {
            if (!_store.Exists(CharactersIndexKey))
            {
                report.Append("no ").Append(CharactersIndexKey).Append("; ");
                return true;
            }

            string[] lines;
            try
            {
                lines = ReadLines(CharactersIndexKey);
            }
            catch (Exception exception)
            {
                report.Append("characters.index unreadable: ").Append(exception.Message).Append("; ");
                return false;
            }

            if (lines.Length < 2 || !lines[0].StartsWith(CharactersHeader + " ", StringComparison.Ordinal))
            {
                report.Append("characters.index has no header; ");
                return false;
            }

            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split(' ');
                if (parts.Length < 7 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    report.Append("bad character line ").Append(i + 1).Append("; ");
                    continue;
                }

                var record = new AlphaCharacterRecord
                {
                    Id = id,
                    Name = Unb64(parts[1]),
                    Class = Enum.IsDefined(typeof(CharacterClass), int.Parse(parts[2], CultureInfo.InvariantCulture))
                        ? (CharacterClass)int.Parse(parts[2], CultureInfo.InvariantCulture)
                        : CharacterClass.Warrior,
                    Level = int.Parse(parts[3], CultureInfo.InvariantCulture),
                    SavedUtcTicks = long.Parse(parts[4], CultureInfo.InvariantCulture),
                    LastWorldSeed = long.Parse(parts[5], CultureInfo.InvariantCulture),
                    LastWorldName = Unb64(parts[6]),
                };
                try
                {
                    // Alpha deserializes every character blob at boot, including its per-world position map.
                    ReadVisits(record);
                }
                catch (Exception exception)
                {
                    report.Append("world map of character ").Append(id).Append(" unreadable: ")
                        .Append(exception.Message).Append("; ");
                }

                _characters.Add(record);
            }

            _characters.Sort((a, b) => a.Id.CompareTo(b.Id));
            report.Append("characters=").Append(_characters.Count).Append("; ");
            return true;
        }

        private bool LoadWorlds(StringBuilder report)
        {
            if (!_store.Exists(WorldsIndexKey))
            {
                report.Append("no ").Append(WorldsIndexKey).Append("; ");
                return true;
            }

            string[] lines;
            try
            {
                lines = ReadLines(WorldsIndexKey);
            }
            catch (Exception exception)
            {
                report.Append("worlds.index unreadable: ").Append(exception.Message).Append("; ");
                return false;
            }

            if (lines.Length < 2 || !lines[0].StartsWith(WorldsHeader + " ", StringComparison.Ordinal))
            {
                report.Append("worlds.index has no header; ");
                return false;
            }

            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split(' ');
                if (parts.Length < 7 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    report.Append("bad world line ").Append(i + 1).Append("; ");
                    continue;
                }

                _worlds.Add(new AlphaWorldRecord
                {
                    Id = id,
                    Name = Unb64(parts[1]),
                    Seed = uint.Parse(parts[2], CultureInfo.InvariantCulture),
                    CreatedUtcTicks = long.Parse(parts[3], CultureInfo.InvariantCulture),
                    SavedUtcTicks = long.Parse(parts[4], CultureInfo.InvariantCulture),
                    GeneratorId = Unb64(parts[5]),
                    LegacyField24 = int.Parse(parts[6], CultureInfo.InvariantCulture),
                });
            }

            _worlds.Sort((a, b) => a.Id.CompareTo(b.Id));
            report.Append("worlds=").Append(_worlds.Count).Append("; ");
            return true;
        }

        private SaveResult WriteCharactersIndex()
        {
            var text = new StringBuilder();
            text.Append(CharactersHeader).Append(' ').Append(IndexVersion).Append('\n');
            text.Append("num=").Append(_characters.Count).Append('\n');
            foreach (AlphaCharacterRecord record in _characters)
            {
                text.Append(record.Id.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(B64(record.Name)).Append(' ')
                    .Append(((int)record.Class).ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(record.Level.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(record.SavedUtcTicks.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(record.LastWorldSeed.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(B64(record.LastWorldName)).Append('\n');
            }

            try
            {
                _store.Write(CharactersIndexKey, text.ToString());
                return SaveResult.Success();
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.IoError, $"characters.index write failed: {exception.Message}");
            }
        }

        private SaveResult WriteWorldsIndex()
        {
            var text = new StringBuilder();
            text.Append(WorldsHeader).Append(' ').Append(IndexVersion).Append('\n');
            text.Append("num=").Append(_worlds.Count).Append('\n');
            foreach (AlphaWorldRecord record in _worlds)
            {
                text.Append(record.Id.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(B64(record.Name)).Append(' ')
                    .Append(record.Seed.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(record.CreatedUtcTicks.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(record.SavedUtcTicks.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(B64(record.GeneratorId)).Append(' ')
                    .Append(record.LegacyField24.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            try
            {
                _store.Write(WorldsIndexKey, text.ToString());
                return SaveResult.Success();
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.IoError, $"worlds.index write failed: {exception.Message}");
            }
        }

        private string[] ReadLines(string key)
        {
            string text = _store.Read(key) ?? string.Empty;
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private int NextCharacterId()
        {
            int id = 0;
            foreach (AlphaCharacterRecord record in _characters) id = Math.Max(id, record.Id + 1);
            return id;
        }

        private int NextWorldId()
        {
            int id = 0;
            foreach (AlphaWorldRecord record in _worlds) id = Math.Max(id, record.Id + 1);
            return id;
        }

        // ----- per-world position map (Alpha character blob fields 18/19) -----
        //
        // Alpha keeps this map inside the character blob as sorted (seed, world name) → 6 dwords of position and
        // rotation. The port keeps the same key and value in a separate small file next to the character save, so
        // the Unity character payload stays the unchanged Core format.

        public static string CharacterWorldMapKey(int id) => CharacterKey(id) + ".worldmap";

        private void WriteVisits(AlphaCharacterRecord record)
        {
            var text = new StringBuilder();
            text.Append("CUBEGAME_WORLDMAP 1\n");
            text.Append("num=").Append(record.Visits.Count).Append('\n');
            foreach (AlphaWorldVisit visit in record.Visits)
            {
                text.Append(visit.Seed.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(B64(visit.WorldName)).Append(' ')
                    .Append(visit.Position.X.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(visit.Position.Y.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(visit.Position.Z.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(visit.Position.Yaw.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
                    .Append(visit.Position.SavedUtcTicks.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            _store.Write(CharacterWorldMapKey(record.Id), text.ToString());
        }

        private void ReadVisits(AlphaCharacterRecord record)
        {
            record.Visits.Clear();
            string key = CharacterWorldMapKey(record.Id);
            if (!_store.Exists(key)) return;
            string[] lines = ReadLines(key);
            for (int i = 2; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split(' ');
                if (parts.Length < 7 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out long seed) ||
                    !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
                    !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) ||
                    !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double z) ||
                    !float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float yaw))
                {
                    continue;
                }

                var visit = new AlphaWorldVisit { Seed = seed, WorldName = Unb64(parts[1]) };
                visit.Position.X = x;
                visit.Position.Y = y;
                visit.Position.Z = z;
                visit.Position.Yaw = yaw;
                long.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out visit.Position.SavedUtcTicks);
                record.Visits.Add(visit);
            }
        }

        // ----- legacy migration -----

        /// <summary>
        /// Imports the old CubeGame paired slots (<c>slot0..2</c>) once, so the installed user keeps the hero and
        /// the world that were saved by the previous live version. Legacy files stay untouched.
        /// </summary>
        private bool MigrateLegacySlots(StringBuilder report)
        {
            bool any = false;
            foreach (string slot in LegacySlotKeys)
            {
                if (!_store.Exists(slot) || !_store.Exists(slot + SessionSaveFormat.KeySuffix)) continue;
                any = true;
                try
                {
                    string characterText = _store.Read(slot);
                    string sessionText = _store.Read(slot + SessionSaveFormat.KeySuffix);
                    if (!GameSlotStore.TryUnframe(sessionText, SessionSaveFormat.Header, out int version,
                            out string payload, out _) || version != SessionSaveFormat.CurrentVersion)
                    {
                        report.Append("migrate ").Append(slot).Append(": unreadable session; ");
                        continue;
                    }

                    SessionSaveData session = _sessionSerializer.Deserialize(payload);
                    if (session == null)
                    {
                        report.Append("migrate ").Append(slot).Append(": empty session; ");
                        continue;
                    }

                    LoadResult loaded = new SaveService(_store, _characterSerializer, slot).Load();
                    if (!loaded.IsSuccess)
                    {
                        report.Append("migrate ").Append(slot).Append(": character ").Append(loaded).Append("; ");
                        continue;
                    }

                    AlphaWorldRecord world = FindWorld((uint)session.worldSeed, session.worldName);
                    if (world == null)
                    {
                        world = new AlphaWorldRecord
                        {
                            Id = NextWorldId(),
                            Name = session.worldName ?? "World",
                            Seed = (uint)session.worldSeed,
                            GeneratorId = session.generatorId ?? string.Empty,
                            CreatedUtcTicks = session.createdUtcTicks,
                            SavedUtcTicks = session.savedUtcTicks,
                        };
                        _worlds.Add(world);
                    }

                    var record = new AlphaCharacterRecord
                    {
                        Id = NextCharacterId(),
                        Name = session.heroName ?? "Hero",
                        Class = Enum.IsDefined(typeof(CharacterClass), loaded.Data.character.classId)
                            ? (CharacterClass)loaded.Data.character.classId
                            : CharacterClass.Warrior,
                        Level = Math.Max(1, loaded.Data.character.level),
                        SavedUtcTicks = session.savedUtcTicks,
                        LastWorldSeed = world.Seed,
                        LastWorldName = world.Name,
                    };
                    record.Remember(world, new WorldPosition(session.heroX, session.heroY, session.heroZ),
                        session.heroYaw, session.savedUtcTicks);
                    _characters.Add(record);

                    // The hero payload and its per-world map are rewritten in the new location; the world state
                    // (deltas + explored zones) is moved to the world file of the catalog.
                    var hero = new HeroState(session.heroName, session.raceId,
                        CharacterSaveMapper.ToCharacterSheet(loaded.Data));
                    hero.Items.AddRange(CharacterSaveMapper.ToItems(loaded.Data));
                    if (session.heroGender >= 0)
                    {
                        HeroAppearance d = AlphaHeroCreation.Default;
                        hero.Appearance = AlphaHeroCreation.Normalize(new HeroAppearance(
                            hero.RaceId, session.heroGender,
                            session.heroFace < 0 ? d.Face : session.heroFace,
                            session.heroHaircut < 0 ? d.Haircut : session.heroHaircut,
                            session.heroHairR < 0 ? d.HairR : (byte)session.heroHairR,
                            session.heroHairG < 0 ? d.HairG : (byte)session.heroHairG,
                            session.heroHairB < 0 ? d.HairB : (byte)session.heroHairB));
                    }

            if (session.heroSkillRanks != null && session.heroSkillRanks.Length != 0)
                    {
                        hero.SkillRanks.TryReplace(session.heroSkillRanks);
                    }

                    hero.RestoreInventory(session.inventoryPlacement);
                    RestoreRawItems(hero, session);
                    GameSlotStore.RestoreRecipes(hero, session);
                    if (session.huntMissions != null)
                    {
                        foreach (HuntMissionSaveData mission in session.huntMissions)
                        {
                            if (mission == null) continue;
                            hero.Missions.TryRestore(new HuntMission(new StableEntityId(unchecked((ulong)mission.target)),
                                new KindId(mission.kind), mission.complete));
                        }
                    }

                    if (session.heroHealth >= 0.0) hero.RestoreHealth(session.heroHealth);
                    SaveResult characterResult = SaveCharacter(record, hero, session, session.savedUtcTicks);
                    if (!characterResult.IsSuccess)
                    {
                        report.Append("migrate ").Append(slot).Append(": ").Append(characterResult).Append("; ");
                    }

                    if (_store.Exists(slot + WorldSaveFormat.KeySuffix))
                    {
                        if (GameSlotStore.TryUnframe(_store.Read(slot + WorldSaveFormat.KeySuffix),
                                WorldSaveFormat.Header, out _, out string worldPayload, out _))
                        {
                            WorldSaveData worldData = _worldSerializer.Deserialize(worldPayload);
                            if (worldData != null)
                            {
                                worldData.clockTick = session.clockTick;
                                worldData.worldTimeOffsetMs = session.worldTimeOffsetMs;
                                worldData.ticksPerSecond = session.ticksPerSecond;
                                SaveWorld(world, worldData, session.savedUtcTicks);
                            }
                        }
                    }

                    report.Append("migrated ").Append(slot).Append(" -> character ").Append(record.Id)
                        .Append(", world ").Append(world.Id).Append("; ");
                }
                catch (Exception exception)
                {
                    report.Append("migrate ").Append(slot).Append(": ").Append(exception.Message).Append("; ");
                }
            }

            if (any)
            {
                WriteCharactersIndex();
                WriteWorldsIndex();
            }

            return any;
        }

        // ----- helpers -----

        private static AlphaCharacterLoad FailCharacter(AlphaLoadStatus status, string message) =>
            new AlphaCharacterLoad { Status = status, Message = message };

        private static AlphaWorldLoad FailWorld(AlphaLoadStatus status, string message) =>
            new AlphaWorldLoad { Status = status, Message = message };

        private static string B64(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

        private static string Unb64(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }

        private static string Checksum(string value) => SaveService.Fnv1a64(value ?? string.Empty).ToString("x16", CultureInfo.InvariantCulture);

        private static void RestoreRawItems(HeroState hero, SessionSaveData data)
        {
            // Same rule as GameSlotStore: raw Alpha items are restored into the recorded bag/equipment slot.
            int n = data.rawItemTypes?.Length ?? 0;
            if (n == 0 || data.rawItemSubtypes?.Length != n || data.rawItemCounts?.Length != n ||
                data.rawItemSlots?.Length != n)
            {
                return;
            }

            for (int i = 0; i < n; i++)
            {
                InventoryEntry entry = GameSlotStore.RawEntryFromSave(data, i);
                if (entry == null)
                {
                    continue;
                }

                int placement = data.rawItemSlots[i];
                bool placed = placement >= 0
                    ? hero.Inventory.TryPlace(placement, entry)
                    : hero.Inventory.TryRestoreExtra(placement, entry);
                if (!placed)
                {
                    hero.Inventory.TryAdd(entry);
                }
            }
        }
    }
}
