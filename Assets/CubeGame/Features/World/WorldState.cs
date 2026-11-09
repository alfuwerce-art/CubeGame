using System;
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

        public int SurfaceAt(int x, int z)
        {
            return Deltas.TryGetColumnSurface(x, z, out int edited)
                ? edited
                : Generator.Column(x, z).Surface;
        }

        public TerrainColumn ColumnAt(int x, int z)
        {
            TerrainColumn generated = Generator.Column(x, z);
            return Deltas.TryGetColumnSurface(x, z, out int edited)
                ? new TerrainColumn(edited, generated.Rock, generated.Mountain, generated.BaseHeight)
                : generated;
        }

        /// <summary>Creates the generator recorded in a world's id. Unknown ids are refused, never substituted.</summary>
        public static bool TryCreateGenerator(string generatorId, uint seed, out IWorldGenerator generator)
        {
            if (generatorId == AlphaTerrainGenerator.Id)
            {
                generator = new AlphaTerrainGenerator(seed);
                return true;
            }

            generator = null;
            return false;
        }

        public static WorldPosition StartPosition(IWorldGenerator generator, WorldDeltaStore deltas)
        {
            int x = WorldLayout.StartVoxel;
            int z = WorldLayout.StartVoxel;
            int surface = deltas != null && deltas.TryGetColumnSurface(x, z, out int edited) ? edited : generator.Column(x, z).Surface;
            int top = Math.Max(surface, generator.WaterLevel);
            return new WorldPosition(x + 0.5, top + 1.2, z + 0.5);
        }
    }
}
