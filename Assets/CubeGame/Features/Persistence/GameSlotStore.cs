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
    public enum SlotLoadStatus
    {
        Success = 0,
        NotFound = 1,
        Corrupted = 2,
        UnsupportedVersion = 3,
        GeneratorMismatch = 4,
        PairMismatch = 5,
        IoError = 6,
    }

    public sealed class SlotLoadResult
    {
        public SlotLoadStatus Status { get; set; }

        public string Message { get; set; } = string.Empty;

        public WorldInfo World { get; set; }

        public HeroState Hero { get; set; }

        public long ClockTick { get; set; }

        public long WorldTimeOffsetMs { get; set; }

        public WorldDeltaStore WorldDeltas { get; set; }

        public bool IsSuccess => Status == SlotLoadStatus.Success;

        public override string ToString() => $"{Status}: {Message}";
    }

    /// <summary>
    /// One save slot = Core character save (key, unchanged S02 format v1) + session sidecar (key + ".session").
    /// Checkpoint order: character first, session second; the session records the checksum of the exact
    /// character text it belongs to, so a checkpoint interrupted between the two writes is detected on load
    /// (<see cref="SlotLoadStatus.PairMismatch"/>) instead of silently mixing two checkpoints.
    /// Each write is atomic per file in <c>FileSaveStore</c> (temp + replace + .bak).
    /// </summary>
    public sealed class GameSlotStore
    {
        private const char Separator = '|';

        private readonly ISaveStore _store;
        private readonly ISaveSerializer _characterSerializer;
        private readonly ISessionSerializer _sessionSerializer;
        private readonly IWorldSaveSerializer _worldSerializer;

        public GameSlotStore(ISaveStore store, ISaveSerializer characterSerializer, ISessionSerializer sessionSerializer, IWorldSaveSerializer worldSerializer)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _characterSerializer = characterSerializer ?? throw new ArgumentNullException(nameof(characterSerializer));
            _sessionSerializer = sessionSerializer ?? throw new ArgumentNullException(nameof(sessionSerializer));
            _worldSerializer = worldSerializer ?? throw new ArgumentNullException(nameof(worldSerializer));
        }

        public static string SessionKey(string slotKey) => slotKey + SessionSaveFormat.KeySuffix;

        public static string WorldKey(string slotKey) => slotKey + WorldSaveFormat.KeySuffix;

        public bool Exists(string slotKey)
        {
            return !string.IsNullOrEmpty(slotKey) && _store.Exists(slotKey) && _store.Exists(SessionKey(slotKey));
        }

        /// <summary>Writes a checkpoint of the running session. Does not change the session state machine.</summary>
        public SaveResult Save(GameSession session, long nowUtcTicks)
        {
            if (session == null || !session.HasWorld || session.Hero == null || session.Clock == null)
            {
                return SaveResult.Fail(SaveStatus.ValidationFailed, "no running world to save");
            }

            string slotKey = session.SlotKey;
            var characterService = new SaveService(_store, _characterSerializer, slotKey);
            session.Hero.SyncItemsFromInventory();
            SaveGame character = CharacterSaveMapper.ToSaveGame(session.Hero.Sheet, session.Hero.Items);
            SaveResult characterResult = characterService.Save(character);
            if (!characterResult.IsSuccess)
            {
                return characterResult;
            }

            string characterText;
            try
            {
                characterText = _store.Read(slotKey);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.IoError, $"character save not readable after write: {exception.Message}");
            }

            if (string.IsNullOrEmpty(characterText))
            {
                return SaveResult.Fail(SaveStatus.IoError, "character save is empty after write");
            }

            string worldChecksum = string.Empty;
            if (session.WorldDeltas != null)
            {
                WorldSaveData world = ToWorldSave(session);
                string worldPayload;
                try
                {
                    worldPayload = _worldSerializer.Serialize(world);
                }
                catch (Exception exception)
                {
                    return SaveResult.Fail(SaveStatus.SerializationFailed, $"world serialization failed: {exception.Message}");
                }

                if (string.IsNullOrEmpty(worldPayload))
                {
                    return SaveResult.Fail(SaveStatus.SerializationFailed, "world serializer returned empty text");
                }

                string framedWorld = Frame(WorldSaveFormat.Header, WorldSaveFormat.CurrentVersion, worldPayload);
                try
                {
                    _store.Write(WorldKey(slotKey), framedWorld);
                }
                catch (Exception exception)
                {
                    return SaveResult.Fail(SaveStatus.IoError, $"world write failed: {exception.Message}");
                }

                worldChecksum = Checksum(framedWorld);
            }

            HeroState hero = session.Hero;
            var data = new SessionSaveData
            {
                formatVersion = SessionSaveFormat.CurrentVersion,
                worldName = session.World.Name,
                worldSeed = session.World.Seed.Value,
                generatorId = session.World.GeneratorId,
                createdUtcTicks = session.World.CreatedUtcTicks,
                savedUtcTicks = nowUtcTicks,
                ticksPerSecond = session.Clock.TicksPerSecond,
                clockTick = session.Clock.Tick,
                worldTimeOffsetMs = session.Clock.WorldTimeOffsetMs,
                heroName = hero.Name,
                raceId = hero.RaceId,
                heroX = hero.Position.X,
                heroY = hero.Position.Y,
                heroZ = hero.Position.Z,
                heroYaw = hero.Yaw,
                heroHealth = hero.Health.CurrentHealth,
                heroGender = hero.Appearance.Gender,
                heroFace = hero.Appearance.Face,
                heroHaircut = hero.Appearance.Haircut,
                heroHairR = hero.Appearance.HairR,
                heroHairG = hero.Appearance.HairG,
                heroHairB = hero.Appearance.HairB,
                heroSkillRanks = hero.SkillRanks.ToArray(),
                heroCopper = hero.Copper,
                heroManaCubes = hero.ManaCubes,
                huntMissions = WriteMissions(hero.Missions),
                inventoryPlacement = hero.InventoryPlacement(),
                characterChecksum = Checksum(characterText),
                worldChecksum = worldChecksum,
            };
            WriteRawItems(hero, data);

            if (!TryValidate(data, out string validationError))
            {
                return SaveResult.Fail(SaveStatus.ValidationFailed, validationError);
            }

            string payload;
            try
            {
                payload = _sessionSerializer.Serialize(data);
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
                _store.Write(SessionKey(slotKey), Frame(SessionSaveFormat.Header, SessionSaveFormat.CurrentVersion, payload));
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.IoError, $"session write failed: {exception.Message}");
            }

            return SaveResult.Success();
        }

        /// <summary>Loads a slot. <paramref name="expectedGeneratorId"/> must match the generator the game runs.</summary>
        public SlotLoadResult Load(string slotKey, string expectedGeneratorId)
        {
            if (string.IsNullOrEmpty(slotKey))
            {
                return Fail(SlotLoadStatus.NotFound, "slot key is empty");
            }

            string sessionText;
            string characterText;
            try
            {
                if (!_store.Exists(SessionKey(slotKey)) || !_store.Exists(slotKey))
                {
                    return Fail(SlotLoadStatus.NotFound, $"slot '{slotKey}' has no complete checkpoint");
                }

                sessionText = _store.Read(SessionKey(slotKey));
                characterText = _store.Read(slotKey);
            }
            catch (Exception exception)
            {
                return Fail(SlotLoadStatus.IoError, exception.Message);
            }

            if (!TryUnframe(sessionText, SessionSaveFormat.Header, out int version, out string payload, out string frameError))
            {
                return Fail(SlotLoadStatus.Corrupted, frameError);
            }

            if (version != SessionSaveFormat.CurrentVersion)
            {
                return Fail(SlotLoadStatus.UnsupportedVersion, $"session format {version}, supported {SessionSaveFormat.CurrentVersion}");
            }

            SessionSaveData data;
            try
            {
                data = _sessionSerializer.Deserialize(payload);
            }
            catch (Exception exception)
            {
                return Fail(SlotLoadStatus.Corrupted, $"session payload unreadable: {exception.Message}");
            }

            if (data == null)
            {
                return Fail(SlotLoadStatus.Corrupted, "session payload unreadable");
            }

            if (!TryValidate(data, out string validationError))
            {
                return Fail(SlotLoadStatus.Corrupted, validationError);
            }

            if (!string.Equals(data.characterChecksum, Checksum(characterText ?? string.Empty), StringComparison.Ordinal))
            {
                return Fail(SlotLoadStatus.PairMismatch, "character save and session belong to different checkpoints");
            }

            if (!string.IsNullOrEmpty(expectedGeneratorId) && !string.Equals(expectedGeneratorId, data.generatorId, StringComparison.Ordinal))
            {
                return Fail(SlotLoadStatus.GeneratorMismatch, $"world was generated by '{data.generatorId}', game runs '{expectedGeneratorId}'");
            }

            WorldDeltaStore deltas = new WorldDeltaStore();
            if (!string.IsNullOrEmpty(data.worldChecksum))
            {
                SlotLoadResult worldFailure = LoadWorld(slotKey, data, deltas);
                if (worldFailure != null)
                {
                    return worldFailure;
                }
            }

            LoadResult character = new SaveService(_store, _characterSerializer, slotKey).Load();
            if (!character.IsSuccess)
            {
                return Fail(SlotLoadStatus.Corrupted, $"character save: {character}");
            }

            CharacterSheet sheet = CharacterSaveMapper.ToCharacterSheet(character.Data);
            List<ItemInstance> items = CharacterSaveMapper.ToItems(character.Data);
            var hero = new HeroState(data.heroName, data.raceId, sheet)
            {
                Position = new WorldPosition(data.heroX, data.heroY, data.heroZ),
                Yaw = data.heroYaw,
                NeedsGroundPlacement = false,
                Copper = data.heroCopper,
                ManaCubes = data.heroManaCubes,
            };
            if (data.heroGender >= 0)
            {
                HeroAppearance d = AlphaHeroCreation.Default;
                hero.Appearance = AlphaHeroCreation.Normalize(new HeroAppearance(
                    hero.RaceId,
                    data.heroGender,
                    data.heroFace < 0 ? d.Face : data.heroFace,
                    data.heroHaircut < 0 ? d.Haircut : data.heroHaircut,
                    data.heroHairR < 0 ? d.HairR : (byte)data.heroHairR,
                    data.heroHairG < 0 ? d.HairG : (byte)data.heroHairG,
                    data.heroHairB < 0 ? d.HairB : (byte)data.heroHairB));
            }
            hero.Items.AddRange(items);
            if (data.heroSkillRanks != null && data.heroSkillRanks.Length != 0)
            {
                hero.SkillRanks.TryReplace(data.heroSkillRanks);
            }

            hero.RestoreInventory(data.inventoryPlacement);
            RestoreRawItems(hero, data);
            if (data.huntMissions != null)
            {
                foreach (HuntMissionSaveData mission in data.huntMissions)
                {
                    if (mission == null || !hero.Missions.TryRestore(new HuntMission(
                        new StableEntityId(unchecked((ulong)mission.target)), new KindId(mission.kind), mission.complete)))
                    {
                        return Fail(SlotLoadStatus.Corrupted, "invalid or duplicate hunt mission");
                    }
                }
            }
            if (data.heroHealth >= 0.0)
            {
                hero.RestoreHealth(data.heroHealth);
            }

            return new SlotLoadResult
            {
                Status = SlotLoadStatus.Success,
                World = new WorldInfo(data.worldName, new WorldSeed((uint)data.worldSeed), data.generatorId, data.createdUtcTicks),
                Hero = hero,
                ClockTick = data.clockTick,
                WorldTimeOffsetMs = data.worldTimeOffsetMs,
                WorldDeltas = deltas,
            };
        }

        public static HuntMissionSaveData[] WriteMissions(MissionJournal journal)
        {
            var result = new HuntMissionSaveData[journal.Hunts.Count];
            int index = 0;
            foreach (HuntMission mission in journal.Hunts)
            {
                result[index++] = new HuntMissionSaveData
                {
                    target = (long)mission.Target.Value,
                    kind = mission.Kind.Value,
                    complete = mission.Complete,
                };
            }

            return result;
        }

        /// <summary>Returns null on success (deltas filled), otherwise the failure result.</summary>
        private SlotLoadResult LoadWorld(string slotKey, SessionSaveData session, WorldDeltaStore deltas)
        {
            string text;
            try
            {
                text = _store.Exists(WorldKey(slotKey)) ? _store.Read(WorldKey(slotKey)) : null;
            }
            catch (Exception exception)
            {
                return Fail(SlotLoadStatus.IoError, exception.Message);
            }

            if (string.IsNullOrEmpty(text))
            {
                return Fail(SlotLoadStatus.PairMismatch, "session expects a world file that is missing");
            }

            if (!string.Equals(session.worldChecksum, Checksum(text), StringComparison.Ordinal))
            {
                return Fail(SlotLoadStatus.PairMismatch, "world file and session belong to different checkpoints");
            }

            if (!TryUnframe(text, WorldSaveFormat.Header, out int version, out string payload, out string error))
            {
                return Fail(SlotLoadStatus.Corrupted, error);
            }

            if (version != WorldSaveFormat.CurrentVersion)
            {
                return Fail(SlotLoadStatus.UnsupportedVersion, $"world format {version}, supported {WorldSaveFormat.CurrentVersion}");
            }

            WorldSaveData world;
            try
            {
                world = _worldSerializer.Deserialize(payload);
            }
            catch (Exception exception)
            {
                return Fail(SlotLoadStatus.Corrupted, $"world payload unreadable: {exception.Message}");
            }

            if (world == null || world.deltas == null || world.discovered == null)
            {
                return Fail(SlotLoadStatus.Corrupted, "world payload unreadable");
            }

            if (world.generatorId != session.generatorId || world.worldSeed != session.worldSeed)
            {
                return Fail(SlotLoadStatus.Corrupted, "world file describes another world");
            }

            if (world.deltas.Count > WorldSaveFormat.MaxDeltas || world.discovered.Count % 2 != 0)
            {
                return Fail(SlotLoadStatus.Corrupted, "world file sizes are invalid");
            }

            foreach (WorldDeltaRecord record in world.deltas)
            {
                if (record == null || !Enum.IsDefined(typeof(WorldDeltaKind), record.kind))
                {
                    return Fail(SlotLoadStatus.Corrupted, "world delta record is invalid");
                }

                var region = new RegionKey(record.regionX, record.regionZ);
                if (!WorldLayout.IsInside(region))
                {
                    return Fail(SlotLoadStatus.Corrupted, $"world delta outside the world: {region}");
                }

                deltas.Record(new WorldDelta
                {
                    Region = region,
                    Kind = (WorldDeltaKind)record.kind,
                    X = record.x,
                    Y = record.y,
                    Z = record.z,
                    Value = record.value,
                    TimeMs = record.timeMs,
                });
            }

            for (int i = 0; i < world.discovered.Count; i += 2)
            {
                deltas.MarkDiscovered(new ZoneKey(world.discovered[i], world.discovered[i + 1]));
            }

            return null;
        }

        public static WorldSaveData ToWorldSave(GameSession session)
        {
            var data = new WorldSaveData
            {
                generatorId = session.World.GeneratorId,
                worldSeed = session.World.Seed.Value,
            };

            foreach (WorldDelta delta in session.WorldDeltas.All())
            {
                data.deltas.Add(new WorldDeltaRecord
                {
                    regionX = delta.Region.X,
                    regionZ = delta.Region.Z,
                    kind = (int)delta.Kind,
                    x = delta.X,
                    y = delta.Y,
                    z = delta.Z,
                    value = delta.Value,
                    timeMs = delta.TimeMs,
                });
            }

            foreach (ZoneKey zone in session.WorldDeltas.Discovered)
            {
                data.discovered.Add(zone.X);
                data.discovered.Add(zone.Z);
            }

            return data;
        }

        public void Delete(string slotKey)
        {
            _store.Delete(SessionKey(slotKey));
            _store.Delete(WorldKey(slotKey));
            _store.Delete(slotKey);
        }

        public static bool TryValidate(SessionSaveData data, out string error)
        {
            if (data.formatVersion != SessionSaveFormat.CurrentVersion)
            {
                error = $"unexpected session format {data.formatVersion}";
                return false;
            }

            if (data.worldSeed < 0 || data.worldSeed > uint.MaxValue)
            {
                error = $"world seed out of range: {data.worldSeed}";
                return false;
            }

            if (string.IsNullOrEmpty(data.generatorId))
            {
                error = "generator id missing";
                return false;
            }

            if (data.ticksPerSecond != GameClock.DefaultTicksPerSecond)
            {
                error = $"unsupported tick rate {data.ticksPerSecond}";
                return false;
            }

            if (data.clockTick < 0)
            {
                error = $"negative clock tick {data.clockTick}";
                return false;
            }

            if (!IsFinite(data.heroX) || !IsFinite(data.heroY) || !IsFinite(data.heroZ) || !IsFinite(data.heroYaw) || !IsFinite(data.heroHealth))
            {
                error = "hero position is not finite";
                return false;
            }

            if (data.raceId < -1 || data.raceId >= AlphaHeroCreation.RaceCount || data.heroGender < -1 || data.heroGender > 1
                || data.heroHairR > 255 || data.heroHairG > 255 || data.heroHairB > 255)
            {
                error = $"hero appearance out of range: race={data.raceId} gender={data.heroGender}";
                return false;
            }

            if (string.IsNullOrEmpty(data.characterChecksum))
            {
                error = "character checksum missing";
                return false;
            }

            if (data.heroSkillRanks != null && data.heroSkillRanks.Length != 0 && !HeroSkillRanks.IsValid(data.heroSkillRanks))
            {
                error = "invalid hero skill ranks";
                return false;
            }

            if (data.heroCopper < 0 || data.heroManaCubes < 0 || data.worldTimeOffsetMs < 0
                || data.worldTimeOffsetMs > int.MaxValue * 86_400_000L
                || (data.huntMissions?.Length ?? 0) > WorldSaveFormat.MaxDeltas)
            {
                error = "invalid gameplay progress";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static string Frame(string header, int version, string payload)
        {
            int length = Encoding.UTF8.GetByteCount(payload);
            return header + Separator
                + version.ToString(CultureInfo.InvariantCulture) + Separator
                + SaveService.Fnv1a64(payload).ToString("x16", CultureInfo.InvariantCulture) + Separator
                + length.ToString(CultureInfo.InvariantCulture) + "\n" + payload;
        }

        public static bool TryUnframe(string text, string header, out int version, out string payload, out string error)
        {
            version = 0;
            payload = null;
            if (string.IsNullOrEmpty(text))
            {
                error = $"{header} file is empty";
                return false;
            }

            int newline = text.IndexOf('\n');
            if (newline < 0)
            {
                error = $"{header} header missing";
                return false;
            }

            string[] parts = text.Substring(0, newline).TrimEnd('\r').Split(Separator);
            payload = text.Substring(newline + 1);
            if (parts.Length != 4 || parts[0] != header)
            {
                error = $"{header} header not recognised";
                return false;
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out version)
                || !ulong.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong checksum)
                || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int length))
            {
                error = "session header fields not parseable";
                return false;
            }

            if (Encoding.UTF8.GetByteCount(payload) != length)
            {
                error = "session payload length mismatch (truncated file)";
                return false;
            }

            if (SaveService.Fnv1a64(payload) != checksum)
            {
                error = "session checksum mismatch (damaged file)";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string Checksum(string text)
        {
            return SaveService.Fnv1a64(text).ToString("x16", CultureInfo.InvariantCulture);
        }

        public static void WriteRawItems(HeroState hero, SessionSaveData data)
        {
            var types = new List<int>();
            var subtypes = new List<int>();
            var counts = new List<int>();
            var slots = new List<int>();
            for (int i = 0; i < HeroInventory.Capacity; i++)
            {
                Record(hero.Inventory[i], i);
            }

            foreach (ExtraEquipmentSlot slot in HeroInventory.ExtraSlots)
            {
                Record(hero.Inventory.GetExtra(slot), HeroInventory.ExtraPlacement(slot));
            }

            data.rawItemTypes = types.ToArray();
            data.rawItemSubtypes = subtypes.ToArray();
            data.rawItemCounts = counts.ToArray();
            data.rawItemSlots = slots.ToArray();

            void Record(InventoryEntry entry, int placement)
            {
                if (entry != null && !entry.IsCore)
                {
                    types.Add(entry.Raw.Type);
                    subtypes.Add(entry.Raw.Subtype);
                    counts.Add(entry.Count);
                    slots.Add(placement);
                }
            }
        }

        private static void RestoreRawItems(HeroState hero, SessionSaveData data)
        {
            int n = data.rawItemTypes?.Length ?? 0;
            if (n == 0 || data.rawItemSubtypes?.Length != n || data.rawItemCounts?.Length != n || data.rawItemSlots?.Length != n)
            {
                return;
            }

            for (int i = 0; i < n; i++)
            {
                if (data.rawItemCounts[i] <= 0 || ItemInstance.IsSupportedType(data.rawItemTypes[i]))
                {
                    continue;
                }

                InventoryEntry entry = InventoryEntry.FromRaw(new RawAlphaItemId(data.rawItemTypes[i], data.rawItemSubtypes[i]), data.rawItemCounts[i]);
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

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static SlotLoadResult Fail(SlotLoadStatus status, string message)
        {
            return new SlotLoadResult { Status = status, Message = message };
        }
    }
}
