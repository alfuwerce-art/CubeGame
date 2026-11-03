using System;
using CubeGame.Core.Persistence;
using UnityEngine;

namespace CubeGame.Persistence
{
    /// <summary>
    /// Сериализация сохранения штатным средством Unity — <c>JsonUtility</c>.
    /// Собственный код здесь не нужен: модель состоит из плоских полей и списка предметов.
    /// Любая ошибка разбора превращается в <c>null</c>, а не в исключение для игрового кода.
    /// </summary>
    public sealed class JsonSaveSerializer : ISaveSerializer
    {
        private readonly bool _prettyPrint;

        public JsonSaveSerializer(bool prettyPrint = false)
        {
            _prettyPrint = prettyPrint;
        }

        public string Serialize(SaveGame data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return JsonUtility.ToJson(data, _prettyPrint);
        }

        public SaveGame Deserialize(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<SaveGame>(payload);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
