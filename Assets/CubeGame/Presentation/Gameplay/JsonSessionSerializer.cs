using System;
using CubeGame.Features.Persistence;
using UnityEngine;

namespace CubeGame.Gameplay
{
    /// <summary>Unity JsonUtility implementation of the session sidecar serializer.</summary>
    public sealed class JsonSessionSerializer : ISessionSerializer
    {
        public string Serialize(SessionSaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return JsonUtility.ToJson(data, false);
        }

        public SessionSaveData Deserialize(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<SessionSaveData>(payload);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
