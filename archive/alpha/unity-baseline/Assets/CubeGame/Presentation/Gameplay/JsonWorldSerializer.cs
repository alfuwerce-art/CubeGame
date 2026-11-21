using System;
using CubeGame.Features.Persistence;
using UnityEngine;

namespace CubeGame.Gameplay
{
    /// <summary>Unity JsonUtility implementation of the world file serializer (M0-02).</summary>
    public sealed class JsonWorldSerializer : IWorldSaveSerializer
    {
        public string Serialize(WorldSaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return JsonUtility.ToJson(data, false);
        }

        public WorldSaveData Deserialize(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<WorldSaveData>(payload);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
