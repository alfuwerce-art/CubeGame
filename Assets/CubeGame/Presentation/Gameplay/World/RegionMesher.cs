using CubeGame.Core.Generation;
using CubeGame.Core.Rendering;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>
    /// Builds the render/collision mesh of one 64×64 region from <see cref="WorldState"/> (generator + deltas),
    /// in region-local coordinates (the region GameObject sits at its block origin). Column tops and exposed sides,
    /// like the S01 chunk mesher; surface colours are PLACEHOLDER_NOT_ALPHA (Alpha colour blend 0x5E4850 not ported).
    /// </summary>
    public static class RegionMesher
    {
        public const int Size = WorldLayout.RegionSize;

        public sealed class Result
        {
            public MeshData Terrain;
            public MeshData Water;
            public int MinSurface;
            public int MaxSurface;
        }

        public static Result Build(WorldState world, RegionKey region)
        {
            int originX = region.X * Size;
            int originZ = region.Z * Size;
            int water = world.Generator.WaterLevel;

            // Heights with a one-column border for the side faces.
            var heights = new int[Size + 2, Size + 2];
            var rock = new bool[Size, Size];
            int min = int.MaxValue;
            int max = int.MinValue;
            for (int lz = -1; lz <= Size; lz++)
            {
                for (int lx = -1; lx <= Size; lx++)
                {
                    int wx = originX + lx;
                    int wz = originZ + lz;
                    if (!WorldLayout.IsInsideBlocks(wx, wz))
                    {
                        heights[lx + 1, lz + 1] = int.MinValue / 2;
                        continue;
                    }

                    TerrainColumn column = world.ColumnAt(wx, wz);
                    heights[lx + 1, lz + 1] = column.Surface;
                    if (lx >= 0 && lz >= 0 && lx < Size && lz < Size)
                    {
                        rock[lx, lz] = column.Rock;
                        if (column.Surface < min)
                        {
                            min = column.Surface;
                        }

                        if (column.Surface > max)
                        {
                            max = column.Surface;
                        }
                    }
                }
            }

            var terrain = new MeshData();
            var waterMesh = new MeshData();
            for (int lz = 0; lz < Size; lz++)
            {
                for (int lx = 0; lx < Size; lx++)
                {
                    int h = heights[lx + 1, lz + 1];
                    float variation = (DeterministicHash.Combine(originX + lx, originZ + lz, 17) & 0xFFFF) / 65535f;
                    TopColor(h, water, rock[lx, lz], variation, out float r, out float g, out float b);
                    float top = h + 1;
                    int s = terrain.VertexCount;
                    terrain.AddVertex(lx, top, lz, 0f, 1f, 0f, r, g, b, 1f);
                    terrain.AddVertex(lx, top, lz + 1, 0f, 1f, 0f, r, g, b, 1f);
                    terrain.AddVertex(lx + 1, top, lz + 1, 0f, 1f, 0f, r, g, b, 1f);
                    terrain.AddVertex(lx + 1, top, lz, 0f, 1f, 0f, r, g, b, 1f);
                    terrain.AddQuad(s, s + 1, s + 2, s + 3);

                    SideColor(variation, out float sr, out float sg, out float sb);
                    Side(terrain, h, heights[lx, lz + 1], lx, lz, lx, lz + 1, -1f, 0f, sr, sg, sb);
                    Side(terrain, h, heights[lx + 2, lz + 1], lx + 1, lz + 1, lx + 1, lz, 1f, 0f, sr, sg, sb);
                    Side(terrain, h, heights[lx + 1, lz], lx + 1, lz, lx, lz, 0f, -1f, sr, sg, sb);
                    Side(terrain, h, heights[lx + 1, lz + 2], lx, lz + 1, lx + 1, lz + 1, 0f, 1f, sr, sg, sb);

                    if (h < water)
                    {
                        float wy = water + 0.9f;
                        int w = waterMesh.VertexCount;
                        waterMesh.AddVertex(lx, wy, lz, 0f, 1f, 0f, 0.20f, 0.42f, 0.75f, 0.7f);
                        waterMesh.AddVertex(lx, wy, lz + 1, 0f, 1f, 0f, 0.20f, 0.42f, 0.75f, 0.7f);
                        waterMesh.AddVertex(lx + 1, wy, lz + 1, 0f, 1f, 0f, 0.20f, 0.42f, 0.75f, 0.7f);
                        waterMesh.AddVertex(lx + 1, wy, lz, 0f, 1f, 0f, 0.20f, 0.42f, 0.75f, 0.7f);
                        waterMesh.AddQuad(w, w + 1, w + 2, w + 3);
                    }
                }
            }

            return new Result { Terrain = terrain, Water = waterMesh, MinSurface = min, MaxSurface = max };
        }

        /// <summary>Wall from the neighbour's top up to this column's top; (ax,az) is the right-hand edge seen from outside (clockwise winding).</summary>
        private static void Side(MeshData mesh, int own, int neighbour, float ax, float az, float bx, float bz, float nx, float nz, float r, float g, float b)
        {
            if (neighbour >= own)
            {
                return;
            }

            float top = own + 1;
            float bottom = neighbour + 1;
            int s = mesh.VertexCount;
            mesh.AddVertex(ax, bottom, az, nx, 0f, nz, r, g, b, 1f);
            mesh.AddVertex(bx, bottom, bz, nx, 0f, nz, r, g, b, 1f);
            mesh.AddVertex(bx, top, bz, nx, 0f, nz, r, g, b, 1f);
            mesh.AddVertex(ax, top, az, nx, 0f, nz, r, g, b, 1f);
            mesh.AddQuad(s, s + 1, s + 2, s + 3);
        }

        private static void TopColor(int h, int water, bool rock, float variation, out float r, out float g, out float b)
        {
            float shade = 1f - variation * 0.08f;
            if (h < water + 1)
            {
                r = 0.80f; g = 0.74f; b = 0.52f; // seabed / shore sand
            }
            else if (rock)
            {
                r = 0.50f; g = 0.48f; b = 0.46f; // Alpha material 6 (stone)
            }
            else if (h > water + 140)
            {
                r = 0.93f; g = 0.95f; b = 0.97f;
            }
            else if (h > water + 70)
            {
                r = 0.34f; g = 0.47f; b = 0.29f;
            }
            else
            {
                r = 0.38f; g = 0.64f; b = 0.31f;
            }

            r *= shade;
            g *= shade;
            b *= shade;
        }

        private static void SideColor(float variation, out float r, out float g, out float b)
        {
            float shade = 1f - variation * 0.06f;
            r = 0.42f * shade;
            g = 0.36f * shade;
            b = 0.30f * shade;
        }
    }
}
