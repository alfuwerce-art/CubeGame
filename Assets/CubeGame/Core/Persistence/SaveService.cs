using System;
using System.Globalization;
using System.Text;

namespace CubeGame.Core.Persistence
{
    /// <summary>
    /// Сервис сохранения/загрузки базового состояния CubeGame.
    ///
    /// Формат файла (собственный, не формат Alpha v11):
    /// <code>
    /// CUBEGAME_SAVE|&lt;version&gt;|&lt;fnv1a64 hex&gt;|&lt;payload length&gt;
    /// &lt;payload&gt;
    /// </code>
    /// Контрольная сумма и длина позволяют отличить повреждённый/обрезанный файл от валидного
    /// и не поднимать исключение в игровой код: вызывающий получает <see cref="LoadStatus"/>.
    ///
    /// Слой не зависит от Unity: файловый ввод-вывод приходит через <see cref="ISaveStore"/>,
    /// сериализация — через <see cref="ISaveSerializer"/>.
    /// </summary>
    public sealed class SaveService
    {
        /// <summary>Ключ сохранения по умолчанию (один слот первой партии).</summary>
        public const string DefaultKey = "slot0";

        private const char Separator = '|';

        private readonly ISaveStore _store;
        private readonly ISaveSerializer _serializer;
        private readonly string _key;

        public SaveService(ISaveStore store, ISaveSerializer serializer, string key = DefaultKey)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _key = string.IsNullOrEmpty(key) ? DefaultKey : key;
        }

        public string Key => _key;

        public bool Exists()
        {
            return _store.Exists(_key);
        }

        public SaveResult Save(SaveGame data)
        {
            if (!SaveValidator.TryValidate(data, out string validationError))
            {
                return SaveResult.Fail(SaveStatus.ValidationFailed, validationError);
            }

            string payload;
            try
            {
                payload = _serializer.Serialize(data);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.SerializationFailed, $"Сериализация не удалась: {exception.Message}");
            }

            if (string.IsNullOrEmpty(payload))
            {
                return SaveResult.Fail(SaveStatus.SerializationFailed, "Сериализатор вернул пустой результат.");
            }

            string framed = Frame(SaveFormat.CurrentVersion, payload);

            try
            {
                _store.Write(_key, framed);
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.IoError, $"Запись сохранения не удалась: {exception.Message}");
            }

            return SaveResult.Success();
        }

        public LoadResult Load()
        {
            string text;
            try
            {
                if (!_store.Exists(_key))
                {
                    return LoadResult.Fail(LoadStatus.NotFound, $"Сохранение '{_key}' не найдено.");
                }

                text = _store.Read(_key);
            }
            catch (Exception exception)
            {
                return LoadResult.Fail(LoadStatus.IoError, $"Чтение сохранения не удалось: {exception.Message}");
            }

            if (string.IsNullOrEmpty(text))
            {
                return LoadResult.Fail(LoadStatus.Corrupted, "Файл сохранения пуст.");
            }

            if (!TryUnframe(text, out int version, out string payload, out string frameError))
            {
                return LoadResult.Fail(LoadStatus.Corrupted, frameError);
            }

            if (version > SaveFormat.CurrentVersion)
            {
                return LoadResult.Fail(
                    LoadStatus.UnsupportedVersion,
                    $"Версия сохранения {version} новее поддерживаемой {SaveFormat.CurrentVersion}.");
            }

            if (version < SaveFormat.CurrentVersion)
            {
                // Миграций пока нет: предыдущая версия не выпускалась. Ветка сохранена как точка расширения.
                return LoadResult.Fail(
                    LoadStatus.MigrationFailed,
                    $"Миграция сохранения с версии {version} на {SaveFormat.CurrentVersion} не реализована.");
            }

            SaveGame data;
            try
            {
                data = _serializer.Deserialize(payload);
            }
            catch (Exception exception)
            {
                return LoadResult.Fail(LoadStatus.Corrupted, $"Разбор сохранения не удался: {exception.Message}");
            }

            if (data == null)
            {
                return LoadResult.Fail(LoadStatus.Corrupted, "Сохранение не разобрано: некорректный формат данных.");
            }

            if (!SaveValidator.TryValidate(data, out string validationError))
            {
                return LoadResult.Fail(LoadStatus.Corrupted, $"Данные сохранения не прошли проверку: {validationError}");
            }

            return LoadResult.Success(data);
        }

        public SaveResult Delete()
        {
            try
            {
                _store.Delete(_key);
                return SaveResult.Success();
            }
            catch (Exception exception)
            {
                return SaveResult.Fail(SaveStatus.IoError, $"Удаление сохранения не удалось: {exception.Message}");
            }
        }

        /// <summary>Собирает текст файла с заголовком, контрольной суммой и длиной полезной нагрузки.</summary>
        public static string Frame(int version, string payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            int payloadLength = Encoding.UTF8.GetByteCount(payload);
            ulong checksum = Fnv1a64(payload);

            var builder = new StringBuilder(payload.Length + 64);
            builder.Append(SaveFormat.Header).Append(Separator)
                .Append(version.ToString(CultureInfo.InvariantCulture)).Append(Separator)
                .Append(checksum.ToString("x16", CultureInfo.InvariantCulture)).Append(Separator)
                .Append(payloadLength.ToString(CultureInfo.InvariantCulture)).Append('\n')
                .Append(payload);
            return builder.ToString();
        }

        /// <summary>Разбирает текст файла: заголовок, версия, контрольная сумма, длина.</summary>
        public static bool TryUnframe(string text, out int version, out string payload, out string error)
        {
            version = 0;
            payload = null;

            if (string.IsNullOrEmpty(text))
            {
                error = "Файл сохранения пуст.";
                return false;
            }

            int newline = text.IndexOf('\n');
            if (newline < 0)
            {
                error = "В файле сохранения нет заголовка (нет перевода строки).";
                return false;
            }

            string header = text.Substring(0, newline).TrimEnd('\r');
            payload = text.Substring(newline + 1);

            string[] parts = header.Split(Separator);
            if (parts.Length != 4 || parts[0] != SaveFormat.Header)
            {
                error = "Заголовок файла сохранения не распознан.";
                return false;
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
            {
                error = "Версия формата в заголовке не является числом.";
                return false;
            }

            if (!ulong.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong expectedChecksum))
            {
                error = "Контрольная сумма в заголовке не распознана.";
                return false;
            }

            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int expectedLength) || expectedLength < 0)
            {
                error = "Длина полезной нагрузки в заголовке не распознана.";
                return false;
            }

            int actualLength = Encoding.UTF8.GetByteCount(payload);
            if (actualLength != expectedLength)
            {
                error = $"Длина полезной нагрузки не совпадает: в заголовке {expectedLength}, фактически {actualLength}.";
                return false;
            }

            ulong actualChecksum = Fnv1a64(payload);
            if (actualChecksum != expectedChecksum)
            {
                error = "Контрольная сумма сохранения не совпадает — файл повреждён.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>FNV-1a 64 бита по UTF-8 представлению строки (детектор повреждений, не защита).</summary>
        public static ulong Fnv1a64(string value)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offsetBasis;
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }

            return hash;
        }
    }
}
