using System;

namespace CubeGame.Features.World
{
    // Indices of GameController+0x800718. The interactive world-object and item tables are separate.
    public enum AlphaVegetationModel
    {
        Flowers2 = 0, Flowers = 1, Grass = 2, Grass2 = 3, Grass3 = 4,
        UnderwaterPlant = 5, Coral = 6, Alga = 7, LavaGrass = 9,
        ThornPlant = 10, Echinacea = 11, Leaf = 12, Reed = 0x16,
        DesertFlower1 = 0x1B, DesertFlower2 = 0x1C, WaterLily1 = 0x1F, WaterLily2 = 0x20,
    }

    public readonly struct AlphaPlantPlacement
    {
        public readonly AlphaVegetationModel Model;
        public readonly int X, Y, Z, SurfaceMaterial, AboveMaterial;
        public readonly float HeightOffset, Yaw, Scale;

        internal AlphaPlantPlacement(AlphaVegetationModel model, int x, int y, int z, int surface, int above,
            float heightOffset, float yaw, float scale)
        {
            Model = model;
            X = x;
            Y = y;
            Z = z;
            SurfaceMaterial = surface;
            AboveMaterial = above;
            HeightOffset = heightOffset;
            Yaw = yaw;
            Scale = scale;
        }
    }

    /// <summary>Surface scan of 0x5EBBA1–0x5ED105 in the Alpha zone generator.</summary>
    internal static class AlphaGroundVegetation
    {
        internal static void Populate(ZoneContentContext context)
        {
            int startX = context.Zone.Zx * AlphaZone.Size;
            int startZ = context.Zone.Zz * AlphaZone.Size;
            for (int x = startX; x < startX + AlphaZone.Size; x++)
            {
                for (int z = startZ; z < startZ + AlphaZone.Size; z++)
                {
                    VoxelColumn column = context.Zone.Column(x, z);
                    for (int y = column.Bottom; y <= column.TopStored; y++)
                    {
                        int surface = Voxel.Material(column.Read(y));
                        if (surface != 2 && surface != 3 && surface != 4 && surface != 9 &&
                            surface != 10 && surface != 11 && surface != 12)
                            continue;

                        int above = Voxel.Material(column.Read(y + 1));
                        if (above == 0 || above == 2)
                            AtSurface(context, column, x, y + 1, z, surface, above);
                    }
                }
            }
        }

        private static void AtSurface(ZoneContentContext context, VoxelColumn column, int x, int y, int z,
            int surface, int above)
        {
            AlphaCrtRandom random = context.Random;
            void Place(AlphaVegetationModel model, float yaw, float scale, float heightOffset = 0f)
            {
                context.Zone.AddPlant(new AlphaPlantPlacement(model, x, y, z, surface, above, heightOffset, yaw, scale));
            }

            if (above == 0 && surface == 11)
            {
                // Original street-light checks consume the placement and orientation draws.
                if ((x + z * 90) % 470 == 0 && context.Terrain.HeightAtXY(x, z) > 0.75f &&
                    (random.Next() & 15) == 0)
                {
                    for (int height = y; height < y + 7; height++)
                        if (Voxel.IsSolid(column.Read(height))) return;
                    random.Next();
                }
                return;
            }

            if (above == 0 && surface == 2)
            {
                if (y > 0 && column.Humidity > 0.2f &&
                    Math.Abs(AlphaNoise.Sample(x * 0.05f + 24234f, z * 0.05f + 53565f)) > 0.7f &&
                    random.Next() % 10 == 0)
                {
                    float yaw = Angle(random);
                    Place((AlphaVegetationModel)(0x1F + (random.Next() & 1)), yaw, 0.09f, -0.1f);
                }
                return;
            }

            if (surface != 3 && surface != 4 && surface != 9 && surface != 10 && surface != 12) return;
            float patch = Math.Abs(AlphaNoise.Sample(x * 0.05f + 9843f, z * 0.05f + 8437f));
            if (above == 0 && surface == 3)
            {
                if (column.Humidity > 0.2f && patch > 0.5f && (random.Next() & 7) == 0)
                    Place(AlphaVegetationModel.Reed, Angle(random), 0.09f);
                return;
            }

            if (surface == 3 || patch <= 0.6f || (random.Next() & 7) != 0) return;
            if (y <= 0)
            {
                if (y > -5) return;
                float yaw = QuarterTurn(random);
                if (AlphaNoise.Sample(x * 0.01 + 9843.0, z * 0.01 + 8437.0) <= 0f)
                {
                    Place(AlphaVegetationModel.Alga, yaw, 0.1f);
                }
                else if ((random.Next() & 1) == 0)
                {
                    Place(AlphaVegetationModel.UnderwaterPlant, yaw, 0.075f);
                }
                else
                {
                    Place(AlphaVegetationModel.Coral, yaw, 0.1f * (Unit(random) + 1f));
                }
                return;
            }

            if (surface == 12)
            {
                float yaw = QuarterTurn(random);
                if (AlphaNoise.Sample(x * 0.01 + 9843.0, z * 0.01 + 8437.0) > 0f)
                    Place(AlphaVegetationModel.LavaGrass, yaw, 0.075f);
                else
                    Place(AlphaVegetationModel.ThornPlant, yaw, 0.03f + Unit(random) * 0.02f);
                return;
            }

            if (surface != 10)
            {
                double noiseX = x * 0.01;
                double noiseZ = z * 0.01;
                bool primary = AlphaNoise.Sample(noiseX + 9843.0, noiseZ + 8437.0) > 0f;
                float secondary = primary
                    ? AlphaNoise.Sample(x * 0.01f + 34234f, z * 0.01f + 234234f)
                    : AlphaNoise.Sample(noiseX + 34234.0, noiseZ + 234234.0);
                if (column.Humidity <= 0.75f)
                {
                    float yaw = QuarterTurn(random);
                    int model;
                    if (!primary) model = secondary <= 0f ? 1 : 0;
                    else if (column.Temperature <= 0.5f) model = secondary <= 0f ? 3 : 2;
                    else model = secondary > 0f ? 12 : 4;

                    float scale = model == 12 ? 0.1f + Unit(random) * 0.02f : 0.075f;
                    if (surface == 4 || model == 2 || model == 3 || model == 4)
                        Place((AlphaVegetationModel)model, yaw, scale);
                }
                else if (column.Temperature <= 0.25f)
                {
                    if (random.Next() % 100 == 0)
                    {
                        float yaw = QuarterTurn(random);
                        Place((AlphaVegetationModel)(0x1C - (random.Next() & 1)), yaw, 0.075f);
                    }
                }
                else
                {
                    float yaw = Angle(random);
                    int model = primary ? (secondary > 0f ? 4 : 3) : (secondary <= 0.5f ? 12 : 11);
                    float scale = model == 11 ? 0.05f + Unit(random) * 0.05f :
                        model == 12 ? 0.1f + Unit(random) * 0.02f : 0.075f;
                    if (surface == 4 || model == 2 || model == 3)
                    {
                        Place((AlphaVegetationModel)model, yaw, scale);
                    }
                }
            }

            // Collectible plants go through the Alpha item vector (0x148), not the decorative list.
            if ((surface == 4 || surface == 10) && random.Next() % (surface == 4 ? 150 : 300) == 0)
            {
                random.Next();
                random.Next();
            }
        }

        private static float Unit(AlphaCrtRandom random) => random.Next() / (float)AlphaCrtRandom.RandMax;
        private static float Angle(AlphaCrtRandom random) => random.Next() * 360f / AlphaCrtRandom.RandMax;
        private static float QuarterTurn(AlphaCrtRandom random) => (random.Next() & 3) * 90f;
    }
}
