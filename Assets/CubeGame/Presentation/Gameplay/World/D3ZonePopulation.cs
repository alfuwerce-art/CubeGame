using System;
using System.Collections.Generic;
using System.Linq;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.World;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>Versioned zone generation and its W-18 objects and creatures on population channel 1.</summary>
    public sealed class D3ZonePopulation : IWorldPopulationSource
    {
        private readonly AlphaTerrainGenerator _terrain;

        public D3ZonePopulation(AlphaTerrainGenerator terrain)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        }

        public static bool TryCreateGenerator(string id, uint seed, out IWorldGenerator generator)
        {
            if (id != AlphaTerrainGenerator.ContentId)
            {
                return WorldState.TryCreateGenerator(id, seed, out generator);
            }

            var settings = new ContentPlanSettings
            {
                // Use the committed catalog so installing or removing visual assets cannot change a saved world.
                StagedObjectKinds = AlphaCreatureCatalog.All.Select(kind => kind.Kind).ToArray(),
                CreatureSlots = 0,
                DecorSlots = 0,
            };
            return WorldState.TryCreateGenerator(id, seed, out generator,
                new IZoneContentProvider[] { new AlphaZoneContentProvider(settings) });
        }

        public IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone)
        {
            var spawns = new List<SpawnDefinition>();
            foreach (SpawnDefinition definition in _terrain.Zone(zone.X, zone.Z).ContentSpawns)
            {
                if (definition.BehaviourKey == AlphaZoneContentProvider.PassiveBehaviourKey
                    || definition.BehaviourKey == AlphaZoneContentProvider.CreatureBehaviourKey)
                {
                    spawns.Add(definition);
                }
            }

            return spawns;
        }
    }
}
