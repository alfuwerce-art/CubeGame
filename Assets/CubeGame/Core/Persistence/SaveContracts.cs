using System;

namespace CubeGame.Core.Persistence
{
    /// <summary>Результат операции сохранения.</summary>
    public enum SaveStatus
    {
        Success = 0,
        ValidationFailed = 1,
        SerializationFailed = 2,
        IoError = 3,
    }

    /// <summary>Результат операции загрузки.</summary>
    public enum LoadStatus
    {
        Success = 0,
        NotFound = 1,
        Corrupted = 2,
        UnsupportedVersion = 3,
        MigrationFailed = 4,
        IoError = 5,
    }

    public readonly struct SaveResult
    {
        private SaveResult(SaveStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public SaveStatus Status { get; }

        public string Message { get; }

        public bool IsSuccess => Status == SaveStatus.Success;

        public static SaveResult Success() => new SaveResult(SaveStatus.Success, string.Empty);

        public static SaveResult Fail(SaveStatus status, string message) => new SaveResult(status, message);

        public override string ToString() => $"{Status}: {Message}";
    }

    public readonly struct LoadResult
    {
        private LoadResult(LoadStatus status, SaveGame data, string message)
        {
            Status = status;
            Data = data;
            Message = message;
        }

        public LoadStatus Status { get; }

        public SaveGame Data { get; }

        public string Message { get; }

        public bool IsSuccess => Status == LoadStatus.Success;

        public static LoadResult Success(SaveGame data) => new LoadResult(LoadStatus.Success, data, string.Empty);

        public static LoadResult Fail(LoadStatus status, string message) => new LoadResult(status, null, message);

        public override string ToString() => $"{Status}: {Message}";
    }

    /// <summary>
    /// Хранилище «ключ → строка». Реализация решает, где лежат байты (в Unity —
    /// <c>Application.persistentDataPath</c>), Core об этом не знает.
    /// </summary>
    public interface ISaveStore
    {
        bool Exists(string key);

        /// <summary>Читает содержимое или возвращает <c>null</c>, если ключа нет.</summary>
        string Read(string key);

        void Write(string key, string contents);

        void Delete(string key);
    }

    /// <summary>
    /// Преобразователь модели в текст и обратно. В Unity используется <c>JsonUtility</c>,
    /// в тестах Core — любая реализация; Core не зависит от UnityEngine.
    /// </summary>
    public interface ISaveSerializer
    {
        string Serialize(SaveGame data);

        /// <summary>Возвращает <c>null</c>, если текст не является корректным сохранением.</summary>
        SaveGame Deserialize(string payload);
    }
}
