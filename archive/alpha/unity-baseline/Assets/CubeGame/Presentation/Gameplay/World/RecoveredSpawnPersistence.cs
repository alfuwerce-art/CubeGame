using System.Linq;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.World;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>Respawn state of /6 worlds in the existing world delta envelope (CUBEGAME_CONTRACT).</summary>
    internal static class RecoveredSpawnPersistence
    {
        public static void RecordDeath(WorldDeltaStore deltas, AlphaSpawnTemplate template, long timeMs)
        {
            deltas.Record(new WorldDelta
            {
                Region = new RegionKey(template.Zone.X >> 6, template.Zone.Z >> 6),
                Kind = WorldDeltaKind.EntityStateChanged,
                X = template.Zone.X,
                Y = template.Definition.SpawnIndex,
                Z = template.Zone.Z,
                // Negative remaining milliseconds distinguish recovered deaths from legacy Value = 1.
                Value = -template.RespawnTimerMs,
                TimeMs = timeMs,
            });
        }

        public static void Restore(WorldDeltaStore deltas, AlphaSpawnRuntime runtime)
        {
            runtime.RestoreStates(deltas.All().Where(IsDeath).Select(delta => new AlphaSpawnTemplateState
            {
                Id = IdOf(delta),
                RespawnTimerMs = -delta.Value,
                DayOfDeath = AlphaGameDay.Of(delta.TimeMs, 1000),
            }));
        }

        public static void PrepareSave(WorldDeltaStore deltas, AlphaSpawnRuntime runtime, int currentDay)
        {
            var remaining = runtime.SnapshotStates().Where(s => s.RespawnTimerMs > 0 && s.DayOfDeath == currentDay)
                .ToDictionary(s => s.Id, s => s.RespawnTimerMs);
            foreach (WorldDelta delta in deltas.All().Where(IsDeath).ToArray())
            {
                if (remaining.TryGetValue(IdOf(delta), out int timer))
                {
                    delta.Value = -timer;
                }
                else
                {
                    deltas.RemoveEntityState(delta);
                }
            }
        }

        private static StableEntityId IdOf(WorldDelta delta) =>
            StableEntityId.ForZoneSpawn(new ZoneKey(delta.X, delta.Z), AlphaSpawnRuntime.CreatureChannel, delta.Y);

        private static bool IsDeath(WorldDelta delta) => delta.Kind == WorldDeltaKind.EntityStateChanged
            && delta.Y >= 0 && delta.Y <= 0xFFF && delta.Value < 0 && delta.Value >= -AlphaSpawnLifecycle.RespawnTimerMs
            && delta.TimeMs >= 0 && delta.X >= 0 && delta.Z >= 0
            && delta.X < WorldLayout.RegionGrid * WorldLayout.ZonesPerRegion
            && delta.Z < WorldLayout.RegionGrid * WorldLayout.ZonesPerRegion
            && delta.Region.Equals(new RegionKey(delta.X >> 6, delta.Z >> 6));
    }
}
