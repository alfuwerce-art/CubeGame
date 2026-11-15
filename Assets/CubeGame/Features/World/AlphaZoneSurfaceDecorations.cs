using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>A static object record written to the Alpha zone's +0x0C list.</summary>
    public readonly struct AlphaSurfaceProp
    {
        internal AlphaSurfaceProp(int objectId, WorldPosition position, int orientation, float width, float height)
        {
            ObjectId = objectId;
            Position = position;
            Orientation = orientation;
            Width = width;
            Height = height;
        }

        public int ObjectId { get; }
        public WorldPosition Position { get; }
        public int Orientation { get; }
        public float Width { get; }
        public float Height { get; }
    }

    /// <summary>
    /// Surface props (0x5E8D86 / 0x5AD320) and boulders (0x5E8F8D…0x5E9655).
    /// Takes the zone after the river and cave rock passes and before settlement and tree generation.
    /// </summary>
    public static class AlphaZoneSurfaceDecorations
    {
        public static IReadOnlyList<AlphaSurfaceProp> Populate(ZoneContentContext context) => Populate(context, out _);

        internal static List<AlphaSurfaceProp> Populate(ZoneContentContext context, out WorldPosition? scatterOrigin)
        {
            var props = new List<AlphaSurfaceProp>();
            AlphaCrtRandom random = context.Random;
            int previousSiteFrom = random.SiteFrom;
            int previousSiteTo = random.SiteTo;
            int startX = context.Zone.Zx * AlphaZone.Size;
            int startZ = context.Zone.Zz * AlphaZone.Size;
            try
            {
                scatterOrigin = ScatterProps(context, startX, startZ, props);
                PlaceBoulders(context, startX, startZ, scatterOrigin);
                return props;
            }
            finally
            {
                random.At(previousSiteFrom, previousSiteTo);
            }
        }

        // 0x5EA7E7…0x5EAA24: the even-zone object after trees and before patrol placement.
        internal static void AddAfterTrees(ZoneContentContext context, List<AlphaSurfaceProp> props)
        {
            AlphaZone zone = context.Zone;
            if (((zone.Zx + zone.Zz) & 1) != 0)
                return;

            AlphaCrtRandom random = context.Random;
            int from = random.SiteFrom, to = random.SiteTo;
            try
            {
                int x = zone.Zx * AlphaZone.Size + 16 + Draw(random, 0x5EA7FA) % 224;
                int z = zone.Zz * AlphaZone.Size + 16 + Draw(random, 0x5EA819) % 224;
                VoxelColumn column = zone.Column(x, z);
                if (column == null)
                    return;

                int y = column.TopSolid() + 1;
                int yaw = Draw(random, 0x5EA994) & 3;
                props.Add(new AlphaSurfaceProp(0, new WorldPosition(x + 0.5, y, z + 0.5), yaw, 2f, 8f));
            }
            finally
            {
                random.At(from, to);
            }
        }

        private static WorldPosition? ScatterProps(ZoneContentContext context, int startX, int startZ,
            List<AlphaSurfaceProp> props)
        {
            if (((context.Zone.Zx + context.Zone.Zz) & 1) == 0)
            {
                return null;
            }

            AlphaCrtRandom random = context.Random;
            LandmarkColumn landmark = context.Landmark;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                // Alpha draws Z before X here.
                double z = startZ + 48.5 + Draw(random, 0x5E8DE1) % 160;
                double x = startX + 48.5 + Draw(random, 0x5E8E1C) % 160;
                if (landmark != null && (landmark.Type == LandmarkType.Village || landmark.Type == LandmarkType.Ruins)
                    && AlphaLandmarkPatrolPlacement.DistanceSquared(landmark, x, z) < 1f)
                {
                    continue;
                }

                VoxelColumn column = context.Zone.Column((int)x, (int)z);
                int y = column.TopStored + 1;
                if (TryScatterAt(context, x, y, z, props))
                {
                    return new WorldPosition(x, y, z);
                }
            }

            return null;
        }

        private static bool TryScatterAt(ZoneContentContext context, double x, int y, double z, List<AlphaSurfaceProp> props)
        {
            if (context.Terrain.SampleTerrainHeight((int)x, (int)z) * 50f <= 1f)
            {
                return false;
            }

            AlphaCrtRandom random = context.Random;
            int orientation = Draw(random, 0x5AD40B) & 3;
            for (int dx = 0; dx < 3; dx++)
            {
                for (int dz = 0; dz < 3; dz++)
                {
                    if (!TryFitProp(context, x + dx, y, z + dz, 2.4f, out int ground))
                    {
                        continue;
                    }

                    props.Add(new AlphaSurfaceProp(0x41, new WorldPosition(x + dx, ground, z + dz), orientation, 2.4f, 0.5f));
                    for (int offsetX = 0; offsetX <= 7; offsetX += 7)
                    {
                        for (int offsetZ = 0; offsetZ <= 7; offsetZ += 7)
                        {
                            int variant = Draw(random, 0x5AD635) & 3;
                            (int id, float width, float height) = variant switch
                            {
                                1 => (0x10, 1f, 0.5f),
                                2 => (0x0C, 3f, 1f),
                                3 => (0x45, 2f, 0.1f),
                                _ => (0x42, 4f, 3f),
                            };
                            int yaw = Draw(random, 0x5AD700) & 3;
                            double px = x + offsetX - 3.5;
                            double pz = z + offsetZ - 3.5;
                            if (TryFitProp(context, px, y, pz, width, out int py))
                            {
                                props.Add(new AlphaSurfaceProp(id, new WorldPosition(px, py, pz), yaw, width, height));
                            }
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        // 0x5F4DA0, with a square footprint and the caller's require-support flag.
        private static bool TryFitProp(ZoneContentContext context, double x, int y, double z, float width, out int height)
        {
            // Alpha truncates the half-width on the 16.16 grid before converting the box to integer voxels.
            double half = (long)(width * 0.5f * 65536f) / 65536.0;
            int minX = (int)(x - half);
            int maxX = (int)(x + half);
            int minZ = (int)(z - half);
            int maxZ = (int)(z + half);
            bool FootprintContains(int at, bool solid)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    for (int pz = minZ; pz <= maxZ; pz++)
                    {
                        if (Voxel.IsSolid(context.Read(px, at, pz)) == solid)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            height = y;
            for (int i = 0; i < 50 && !FootprintContains(height, true); i++)
            {
                height--;
            }
            for (int i = 0; i < 50 && FootprintContains(height, true); i++)
            {
                height++;
            }
            return height > 0 && !FootprintContains(height - 1, false) &&
                !Voxel.IsWater(context.Read((int)x, height, (int)z));
        }

        private static void PlaceBoulders(ZoneContentContext context, int startX, int startZ, WorldPosition? scatterOrigin)
        {
            int type = context.Record.Type;
            if (type == AlphaZoneRecord.Special10 || type == AlphaZoneRecord.Town || type == AlphaZoneRecord.Type3)
            {
                return;
            }

            AlphaCrtRandom random = context.Random;
            int count = Draw(random, 0x5E8FBB) % 10;
            VoxelColumn centre = context.Zone.Column(startX + 128, startZ + 128);
            float humidity = centre.Humidity, temperature = centre.Temperature;
            if ((humidity > 0.6f || humidity < 0.3f) &&
                (temperature > 0.7f || (temperature < 0.4f && (temperature > 0.2f || humidity < 0.8f))))
            {
                count += 10;
            }

            var colors = new AlphaColors(context.Terrain);
            for (int i = 0; i < count; i++)
            {
                int radiusX = (Draw(random, 0x5E9058) & 7) + 3;
                int radiusZ = (Draw(random, 0x5E9072) & 7) + 3;
                int radiusY = (Draw(random, 0x5E9089) & 7) + 3;
                int x = startX + 2 * radiusX + Draw(random, 0x5E90BB) % ((64 - radiusX) * 4);
                int z = startZ + 2 * radiusZ + Draw(random, 0x5E90D9) % ((64 - radiusZ) * 4);
                float dx = x - WorldLayout.StartVoxel, dz = z - WorldLayout.StartVoxel;
                if (dx * dx + dz * dz < 400f ||
                    (scatterOrigin.HasValue && DistanceSquared(scatterOrigin.Value, x, z) < 1600f) ||
                    context.Terrain.HeightAtXY(x, z) > 0.25f)
                {
                    continue;
                }

                VoxelColumn column = context.Zone.Column(x, z);
                int y = column.TopSolid() + 1;
                int flags = Voxel.Flags(column.Read(y - 1));
                int material = flags & 0x1F;
                if ((flags & 0x40) != 0 || material == 7 || material == 8 || material == 11)
                {
                    continue;
                }
                WriteBoulder(context, colors, x, y, z, radiusX, radiusY, radiusZ);
            }
        }

        private static void WriteBoulder(ZoneContentContext context, AlphaColors colors,
            int x, int y, int z, int radiusX, int radiusY, int radiusZ)
        {
            for (int px = x - radiusX; px <= x + radiusX; px++)
            {
                for (int pz = z - radiusZ; pz <= z + radiusZ; pz++)
                {
                    float dx = (px - x) / (float)radiusX;
                    float dz = (pz - z) / (float)radiusZ;
                    float horizontal = dx * dx + dz * dz;
                    float noise = AlphaNoise.Sample(px * 0.05, pz * 0.05) * 0.8f;
                    VoxelColumn column = context.Zone.Column(px, pz);
                    for (int py = y + 2 * radiusY; py >= y - 2 * radiusY; py--)
                    {
                        float dy = (py - y) / (float)radiusY + noise;
                        if (horizontal + dy * dy > 1f || (Voxel.Flags(column.Read(py)) & 0x40) != 0)
                        {
                            continue;
                        }
                        colors.SoilColor(px, pz, py, column.Humidity, column.Rare, out float r, out float g, out float b);
                        context.Write(px, py, pz, Voxel.Pack((int)r, (int)g, (int)b, 0x26));
                    }
                }
            }
        }

        internal static double DistanceSquared(WorldPosition point, int x, int z)
        {
            double dx = x - point.X, dz = z - point.Z;
            return dx * dx + dz * dz;
        }

        private static int Draw(AlphaCrtRandom random, int returnAddress)
        {
            random.At(returnAddress, returnAddress);
            return random.Next();
        }
    }
}
