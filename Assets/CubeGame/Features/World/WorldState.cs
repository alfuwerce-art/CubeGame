using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>Generator output with persistent deltas applied: the one place every system asks "what is the ground here".</summary>
    public sealed class WorldState
    {
        public WorldState(IWorldGenerator generator, WorldDeltaStore deltas)
        {
            Generator = generator ?? throw new ArgumentNullException(nameof(generator));
            Deltas = deltas ?? throw new ArgumentNullException(nameof(deltas));
        }

        public IWorldGenerator Generator { get; }

        public WorldDeltaStore Deltas { get; }

        /// <summary>Voxel column with every persistent edit applied (the generated column itself when unedited).</summary>
        public VoxelColumn EffectiveColumn(int x, int z)
        {
            return Apply(Generator.Voxels(x, z), Deltas, x, z);
        }

        /// <summary>Applies column/block edits of (x, z) to a copy of <paramref name="generated"/>; returns it unchanged when unedited.</summary>
        public static VoxelColumn Apply(VoxelColumn generated, WorldDeltaStore deltas, int x, int z)
        {
            if (generated == null || deltas == null || !deltas.HasColumnEdits(x, z))
            {
                return generated;
            }

            VoxelColumn column = generated.Clone();
            if (deltas.TryGetColumnSurface(x, z, out int surface))
            {
                column.SetSurface(surface);
            }

            foreach (System.Collections.Generic.KeyValuePair<int, uint> block in deltas.BlocksOf(x, z))
            {
                column.Overwrite(block.Key, block.Value);
            }

            return column;
        }

        public uint VoxelAt(int x, int y, int z)
        {
            if (Deltas.TryGetVoxel(x, y, z, out uint edited))
            {
                return edited;
            }

            VoxelColumn column = EffectiveColumn(x, z);
            return column?.Read(y) ?? Voxel.Air;
        }

        /// <summary>Top solid voxel Y of column (x, z) — where things stand.</summary>
        public int SurfaceAt(int x, int z)
        {
            VoxelColumn column = EffectiveColumn(x, z);
            return column?.TopSolid() ?? int.MinValue / 2;
        }

        public TerrainColumn ColumnAt(int x, int z)
        {
            TerrainColumn generated = Generator.Column(x, z);
            return new TerrainColumn(SurfaceAt(x, z), generated.Rock, generated.Mountain, generated.BaseHeight);
        }

        /// <summary>Creates the generator recorded in a world's id. Unknown ids are refused, never substituted.</summary>
        public static bool TryCreateGenerator(string generatorId, uint seed, out IWorldGenerator generator,
            IReadOnlyList<IZoneContentProvider> contentProviders = null)
        {
            if (generatorId == AlphaTerrainGenerator.Id)
            {
                generator = new AlphaTerrainGenerator(seed);
                return true;
            }

            if ((generatorId == AlphaTerrainGenerator.ContentId || generatorId == AlphaTerrainGenerator.PreviousWorldRuntimeId ||
                 AlphaTerrainGenerator.UsesP2Environment(generatorId))
                && contentProviders != null && contentProviders.Count > 0)
            {
                generator = new AlphaTerrainGenerator(seed, contentProviders, generatorId);
                return true;
            }

            generator = null;
            return false;
        }

        public static WorldPosition StartPosition(IWorldGenerator generator, WorldDeltaStore deltas)
        {
            int x = WorldLayout.StartVoxel;
            int z = WorldLayout.StartVoxel;
            // Spawn in the start village's Town zone (nearest generated town to the climate anchor), so the
            // hero sees NPCs and houses immediately instead of four zones away (OQ-22 note).
            if (generator is AlphaTerrainGenerator terrain)
            {
                AlphaRegion region = terrain.RegionRecord(WorldLayout.CenterRegion, WorldLayout.CenterRegion);
                int slot = region != null ? Array.FindIndex(region.Zones, r => r.Type == AlphaZoneRecord.Town) : -1;
                if (slot >= 0)
                {
                    int lx = slot / WorldLayout.ZonesPerRegion;
                    int lz = slot % WorldLayout.ZonesPerRegion;
                    x = (WorldLayout.CenterRegion * WorldLayout.ZonesPerRegion + lx) * WorldLayout.ZoneSize + WorldLayout.ZoneSize / 2;
                    z = (WorldLayout.CenterRegion * WorldLayout.ZonesPerRegion + lz) * WorldLayout.ZoneSize + WorldLayout.ZoneSize / 2;
                }
            }

            int surface = Apply(generator.Voxels(x, z), deltas, x, z).TopSolid();
            int top = Math.Max(surface, generator.WaterLevel);
            return new WorldPosition(x + 0.5, top + 1.2, z + 0.5);
        }
    }

    /// <summary>
    /// Registry for explicitly assembling content providers. A versioned generator uses the provider set
    /// assigned to its id so registrations cannot change an existing world.
    /// </summary>
    public static class ZoneContentRegistry
    {
        private static readonly List<IZoneContentProvider> _providers = new List<IZoneContentProvider>();

        public static IReadOnlyList<IZoneContentProvider> Providers
        {
            get
            {
                lock (_providers)
                {
                    return _providers.ToArray();
                }
            }
        }

        public static void Register(IZoneContentProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            lock (_providers)
            {
                if (!_providers.Contains(provider))
                {
                    _providers.Add(provider);
                }
            }
        }

        /// <summary>Tests only.</summary>
        public static void Clear()
        {
            lock (_providers)
            {
                _providers.Clear();
            }
        }
    }
}
