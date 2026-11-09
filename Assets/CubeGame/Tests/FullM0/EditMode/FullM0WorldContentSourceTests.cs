using System;
using CubeGame.Content;
using CubeGame.Features.World;
using CubeGame.Gameplay.Streaming;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    public sealed class FullM0WorldContentSourceTests
    {
        [Test]
        public void ContentReadsGeneratedClimateAndEditedSurface()
        {
            const uint seed = 666666u;
            var terrain = new AlphaTerrainGenerator(seed);
            var world = new WorldState(terrain, new WorldDeltaStore());
            IContentZoneSource source = WorldContentSource.Create(
                world,
                (x, z, width, depth) => x == 8 && z == 9 && width == 3 && depth == 4,
                id => id == "removed");

            WorldCellKey cell = AlphaWorldScale.CellOfVoxelPair(WorldLayout.StartVoxel, WorldLayout.StartVoxel);
            int x = AlphaWorldScale.CellOriginVoxel(cell.X) + AlphaWorldScale.CellVoxels / 2;
            int z = AlphaWorldScale.CellOriginVoxel(cell.Z) + AlphaWorldScale.CellVoxels / 2;
            ZoneClimate climate = source.ClimateOf(cell);
            VoxelColumn column = terrain.Voxels(x, z);

            Assert.That(source.Seed, Is.EqualTo(seed));
            Assert.That(climate.Humidity, Is.EqualTo((double)terrain.Humidity(x, z)));
            Assert.That(climate.Temperature, Is.EqualTo((double)terrain.Temperature(x, z)));
            Assert.That(climate.CellValue, Is.EqualTo((double)terrain.RareCellTerm(x, z)));
            Assert.That(climate.Spacing, Is.EqualTo((double)terrain.Mountain(x, z)));
            Assert.That(climate.CellType, Is.EqualTo((int)terrain.ColumnAt(x, z).Type));
            Assert.That(climate.Mode, Is.Zero);
            Assert.That(climate.Level, Is.GreaterThanOrEqualTo(column.Bottom));
            Assert.That(Voxel.IsSolid(column.Read(climate.Level)), Is.False);
            for (int y = column.Bottom; y < climate.Level; y++)
            {
                Assert.That(Voxel.IsSolid(column.Read(y)), Is.True, $"solid voxel at {y} below the scatter height");
            }

            Assert.That(source.TrySurfaceVoxel(x, z, out int beforeEdit), Is.True);
            Assert.That(beforeEdit, Is.EqualTo(column.TopSolid()));
            Assert.That(world.Deltas.SetColumnSurface(x, z, beforeEdit + 4, 50), Is.True);
            Assert.That(source.TrySurfaceVoxel(x, z, out int afterEdit), Is.True);
            Assert.That(afterEdit, Is.EqualTo(beforeEdit + 4));
            Assert.That(source.ClimateOf(cell).Level, Is.EqualTo(climate.Level));

            Assert.That(source.TrySurfaceVoxel(-1, z, out _), Is.False);
            Assert.That(source.TrySurfaceVoxel(WorldLayout.WorldVoxels, z, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => source.ClimateOf(new WorldCellKey(-1, 0)));
            Assert.That(source.IsFlatAndFree(8, 9, 3, 4), Is.True);
            Assert.That(source.IsFlatAndFree(8, 9, 4, 3), Is.False);
            Assert.That(source.IsRemoved("removed"), Is.True);
            Assert.That(source.IsRemoved("kept"), Is.False);
        }
    }
}
