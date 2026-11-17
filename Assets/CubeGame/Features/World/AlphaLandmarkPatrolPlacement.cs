using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>
    /// The point selection at 0x5EAA24 and the master point selection at 0x5DD9D7.
    /// </summary>
    public static class AlphaLandmarkPatrolPlacement
    {
        private const double FixedScale = 65536.0;

        /// <summary>
        /// Continues the zone CRT from surface decoration through the patrol point list.
        /// The caller has run the terrain and early rock passes. Settlement passes are outside this route.
        /// </summary>
        public static IReadOnlyList<WorldPosition> Populate(ZoneContentContext context,
            out IReadOnlyList<AlphaSurfaceProp> surfaceProps)
        {
            List<AlphaSurfaceProp> props = AlphaZoneSurfaceDecorations.Populate(context, out WorldPosition? scatterOrigin);
            AlphaZoneTrees.PopulateBeforePatrols(context, scatterOrigin);
            AlphaZoneSurfaceDecorations.AddAfterTrees(context, props);
            surfaceProps = props;
            return SelectPoints(context.Zone, context.Landmark, context.Random);
        }

        /// <summary>Selects points from the completed decoration passes and their resulting CRT state.</summary>
        public static IReadOnlyList<WorldPosition> SelectPoints(AlphaZone zone, LandmarkColumn landmark,
            AlphaCrtRandom random)
        {
            if (landmark == null || landmark.Type == LandmarkType.None || landmark.Type == LandmarkType.Village ||
                landmark.Type == LandmarkType.Ruins || landmark.Type == LandmarkType.Unnamed10 ||
                landmark.Type == LandmarkType.Zone14)
            {
                return Array.Empty<WorldPosition>();
            }

            int startX = zone.Zx * AlphaZone.Size;
            int startZ = zone.Zz * AlphaZone.Size;
            var points = new List<WorldPosition>();
            for (int row = 0; row < 14; row++)
            {
                int x = startX + 4 + row * 18;
                // In each row Alpha tests (row + 3 * column) % 5 == 0.
                for (int column = (row * 3) % 5; column < 14; column += 5)
                {
                    int z = startZ + 4 + column * 18;
                    float chance = 0.75f * AlphaTerrainGenerator.Falloff(DistanceSquared(landmark, x, z));
                    if (random.Next() / (float)AlphaCrtRandom.RandMax > chance)
                    {
                        continue;
                    }

                    int height = FindSurface(zone.Column(x, z));
                    points.Add(new WorldPosition(x + 0.5, height, z + 0.5));
                }
            }

            return points;
        }

        internal static int FindSurface(VoxelColumn column)
        {
            int height = column.SurfaceScanStart;
            while (Voxel.IsSolid(column.Read(height)))
                height++;
            return height;
        }

        public static int SelectMasterPoint(AlphaZone zone, LandmarkColumn landmark, IReadOnlyList<WorldPosition> points)
        {
            if (landmark == null || (int)landmark.X / AlphaZone.Size != zone.Zx ||
                (int)landmark.Z / AlphaZone.Size != zone.Zz)
            {
                return -1;
            }

            int bestIndex = -1;
            float best = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                WorldPosition point = points[i];
                float weight = AlphaTerrainGenerator.Falloff(DistanceSquared(landmark, point.X, point.Z));
                if (weight > best)
                {
                    bestIndex = i;
                    best = weight;
                }
            }

            return bestIndex;
        }

        // 0x5F8E10. The noise coordinates and displacements are truncated to 16.16 before computing the distance.
        // Keep the fixed-point calculation here to preserve the established world generators' distance rules.
        internal static float DistanceSquared(LandmarkColumn landmark, double x, double z)
        {
            float radius = landmark.Radius;
            if (radius < 0.001f)
            {
                return 0f;
            }

            long dx = (long)(x * FixedScale) - (long)(landmark.X * FixedScale);
            long dz = (long)(z * FixedScale) - (long)(landmark.Z * FixedScale);
            if (landmark.Type != LandmarkType.Rock && landmark.Type != LandmarkType.Tree && landmark.Type != LandmarkType.Zone14)
            {
                double noiseX = (long)(x * FixedScale * -0.0025) / FixedScale;
                double noiseZ = (long)(z * FixedScale * -0.0025) / FixedScale;
                float amplitude = landmark.Type == LandmarkType.Peak ? 100f : 256f;
                dx += (long)(AlphaNoise.Sample(8432984.0 - noiseX, 90493.0 - noiseZ) * amplitude * (float)FixedScale);
                dz += (long)(AlphaNoise.Sample(3423.0 - noiseX, 112.0 - noiseZ) * amplitude * (float)FixedScale);
            }

            float vx = dx / (float)FixedScale;
            float vz = dz / (float)FixedScale;
            return (vx * vx + vz * vz) / (radius * radius);
        }
    }
}
