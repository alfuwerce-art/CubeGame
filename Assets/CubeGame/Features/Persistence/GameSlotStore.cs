using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CubeGame.Core.Characters;
using CubeGame.Core.Items;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;

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

        public GameSlotStore(ISaveStore store, ISaveSerializer characterSerializer, ISessionSerializer sessionSerializer)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _characterSerializer = characterSerializer ?? throw new ArgumentNullException(nameof(characterSerializer));
            _sessionSerializer = sessionSerializer ?? throw new ArgumentNullException(nameof(sessionSerializer));
        }

        public static string SessionKey(string slotKey) => slotKey + SessionSaveFormat.KeySuffix;

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
                heroName = hero.Name,
                raceId = hero.RaceId,
                heroX = hero.Position.X,
                heroY = hero.Position.Y,
                heroZ = hero.Position.Z,
                heroYaw = hero.Yaw,
                characterChecksum = Checksum(characterText),
            };

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
                _store.Write(SessionKey(slotKey), Frame(payload));
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

            if (!TryUnframe(sessionText, out int version, out string payload, out string frameError))
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
            };
            hero.Items.AddRange(items);

            return new SlotLoadResult
            {
                Status = SlotLoadStatus.Success,
                World = new WorldInfo(data.worldName, new WorldSeed((uint)data.worldSeed), data.generatorId, data.createdUtcTicks),
                Hero = hero,
                ClockTick = data.clockTick,
            };
        }

        public void Delete(string slotKey)
        {
            _store.Delete(SessionKey(slotKey));
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

            if (!IsFinite(data.heroX) || !IsFinite(data.heroY) || !IsFinite(data.heroZ) || !IsFinite(data.heroYaw))
            {
                error = "hero position is not finite";
                return false;
            }

            if (string.IsNullOrEmpty(data.characterChecksum))
            {
                error = "character checksum missing";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static string Frame(string payload)
        {
            int length = Encoding.UTF8.GetByteCount(payload);
            return SessionSaveFormat.Header + Separator
                + SessionSaveFormat.CurrentVersion.ToString(CultureInfo.InvariantCulture) + Separator
                + SaveService.Fnv1a64(payload).ToString("x16", CultureInfo.InvariantCulture) + Separator
                + length.ToString(CultureInfo.InvariantCulture) + "\n" + payload;
        }

        public static bool TryUnframe(string text, out int version, out string payload, out string error)
        {
            version = 0;
            payload = null;
            if (string.IsNullOrEmpty(text))
            {
                error = "session file is empty";
                return false;
            }

            int newline = text.IndexOf('\n');
            if (newline < 0)
            {
                error = "session header missing";
                return false;
            }

            string[] parts = text.Substring(0, newline).TrimEnd('\r').Split(Separator);
            payload = text.Substring(newline + 1);
            if (parts.Length != 4 || parts[0] != SessionSaveFormat.Header)
            {
                error = "session header not recognised";
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
