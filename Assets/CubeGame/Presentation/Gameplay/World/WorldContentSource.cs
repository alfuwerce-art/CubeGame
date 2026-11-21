using System;
using CubeGame.Content;
using CubeGame.Features.World;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>Supplies the content planner with terrain from the active world.</summary>
    public static class WorldContentSource
    {
        public static IContentZoneSource Create(
            WorldState world,
            Func<int, int, int, int, bool> isFootprintFree,
            Func<string, bool> isRemoved)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (!(world.Generator is AlphaTerrainGenerator terrain))
            {
                throw new ArgumentException("Content placement requires an Alpha terrain generator.", nameof(world));
            }

            return new DelegateContentZoneSource
            {
                Seed = terrain.MasterSeed,
                Climate = cell => SampleClimate(terrain, cell),
                Surface = (x, z) => world.EffectiveColumn(x, z)?.TopSolid()
                    ?? DelegateContentZoneSource.UnknownSurface,
                FootprintFree = isFootprintFree ?? throw new ArgumentNullException(nameof(isFootprintFree)),
                Removed = isRemoved ?? throw new ArgumentNullException(nameof(isRemoved)),
            };
        }

        private static ZoneClimate SampleClimate(AlphaTerrainGenerator terrain, WorldCellKey cell)
        {
            if (!AlphaWorldScale.IsInsideWorld(cell))
            {
                throw new ArgumentOutOfRangeException(nameof(cell), cell, "Cell is outside the Alpha world.");
            }

            // The current planner requests one sample per cell. Alpha samples at each scatter point.
            int x = AlphaWorldScale.CellOriginVoxel(cell.X) + AlphaWorldScale.CellVoxels / 2;
            int z = AlphaWorldScale.CellOriginVoxel(cell.Z) + AlphaWorldScale.CellVoxels / 2;
            VoxelColumn column = terrain.Voxels(x, z);

            // The scatter call receives the first air/water Y encountered from the column base,
            // using generated terrain so player edits do not change the candidate selection.
            int scatterY = column.Bottom;
            while (Voxel.IsSolid(column.Read(scatterY)))
            {
                scatterY++;
            }

            return new ZoneClimate
            {
                Humidity = terrain.Humidity(x, z),
                Temperature = terrain.Temperature(x, z),
                CellValue = terrain.RareCellTerm(x, z), // 0x5EF040
                Spacing = terrain.Mountain(x, z), // 0x5EFFA0
                Level = scatterY,
                Mode = 0,
                CellType = (int)terrain.ColumnAt(x, z).Type,
            };
        }
    }
}
