using CubeGame.Core.Rendering;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>
    /// Voxel columns of one 32×32 chunk plus a one-column border (the border decides which side faces are exposed).
    /// <see cref="Sample"/> touches the generator only and is safe on worker threads; <see cref="ApplyDeltas"/> runs on
    /// the main thread (the delta store is not shared with workers). Generated columns are shared and never mutated:
    /// edited columns are replaced by edited copies.
    /// </summary>
    public sealed class ChunkVoxels
    {
        public const int Size = WorldLayout.ChunkSize;
        public const int Span = Size + 2;

        public ChunkKey Key;
        public VoxelColumn[] Columns = new VoxelColumn[Span * Span];

        public static ChunkVoxels Sample(IWorldGenerator generator, ChunkKey key)
        {
            var result = new ChunkVoxels { Key = key };
            int originX = key.X * Size;
            int originZ = key.Z * Size;
            for (int lz = -1; lz <= Size; lz++)
            {
                for (int lx = -1; lx <= Size; lx++)
                {
                    result.Columns[(lz + 1) * Span + lx + 1] = generator.Voxels(originX + lx, originZ + lz);
                }
            }

            return result;
        }

        public void ApplyDeltas(WorldDeltaStore deltas)
        {
            int originX = Key.X * Size;
            int originZ = Key.Z * Size;
            for (int lz = -1; lz <= Size; lz++)
            {
                for (int lx = -1; lx <= Size; lx++)
                {
                    int i = (lz + 1) * Span + lx + 1;
                    Columns[i] = WorldState.Apply(Columns[i], deltas, originX + lx, originZ + lz);
                }
            }
        }

        /// <summary>Column at chunk-local (lx, lz), −1…32; null outside the world.</summary>
        public VoxelColumn At(int lx, int lz) => Columns[(lz + 1) * Span + lx + 1];

        public ChunkVoxels Copy()
        {
            return new ChunkVoxels { Key = Key, Columns = (VoxelColumn[])Columns.Clone() };
        }
    }

    /// <summary>
    /// Builds the render/collision mesh of one chunk from Alpha voxels (chunk-local coordinates): one quad per exposed
    /// face of every solid voxel (tops, sides, cave ceilings), coloured with the voxel's own RGB (Alpha colour functions,
    /// <see cref="AlphaColors"/>); a separate water mesh for water voxels facing air. Faces are not merged.
    /// Water colour/transparency is PLACEHOLDER_NOT_ALPHA (Alpha draws water voxels with its own water shader).
    /// </summary>
    public static class ChunkMesher
    {
        private const float WaterR = 0.20f;
        private const float WaterG = 0.42f;
        private const float WaterB = 0.75f;
        private const float WaterA = 0.7f;

        /// <summary>Water surface sits slightly below the voxel top so shores stay visible.</summary>
        public const float WaterSurfaceDrop = 0.1f;

        public sealed class Result
        {
            public MeshData Terrain;
            public MeshData Water;
        }

        public static Result Build(ChunkVoxels chunk)
        {
            int size = ChunkVoxels.Size;
            var terrain = new MeshData();
            var water = new MeshData();
            var neighbours = new VoxelColumn[4];
            for (int lz = 0; lz < size; lz++)
            {
                for (int lx = 0; lx < size; lx++)
                {
                    VoxelColumn c = chunk.At(lx, lz);
                    if (c == null)
                    {
                        continue;
                    }

                    neighbours[0] = chunk.At(lx - 1, lz);
                    neighbours[1] = chunk.At(lx + 1, lz);
                    neighbours[2] = chunk.At(lx, lz - 1);
                    neighbours[3] = chunk.At(lx, lz + 1);
                    // Start one below the lowest stored voxel: the implicit solid just below Bottom can have an exposed top.
                    int yMin = c.Bottom - 1;
                    foreach (VoxelColumn n in neighbours)
                    {
                        if (n != null && n.Bottom - 1 < yMin)
                        {
                            yMin = n.Bottom - 1;
                        }
                    }

                    int top = c.TopSolid();
                    int yMax = c.TopStored > 0 ? c.TopStored : 0;
                    for (int y = yMin; y <= yMax; y++)
                    {
                        uint v = c.Read(y);
                        if (Voxel.IsSolid(v))
                        {
                            float r = Voxel.R(v) / 255f;
                            float g = Voxel.G(v) / 255f;
                            float b = Voxel.B(v) / 255f;
                            // Alpha sunlight fill (ChunkBuffer): sky-exposed voxels are full-bright, voxels under
                            // an overhang / inside a cave fade with depth instead of looking flat.
                            float sun = y >= top ? 1f : System.MathF.Max(0.35f, 1f - (top - y) * 0.07f);
                            r *= sun;
                            g *= sun;
                            b *= sun;
                            if (!Voxel.IsSolid(c.Read(y + 1)))
                            {
                                // Standard voxel AO on the top face vertices: each vertex sees its two edge
                                // neighbours and the corner at the face level.
                                float a0 = VertexAo(chunk, lx, lz, y + 1, -1, -1);
                                float a1 = VertexAo(chunk, lx, lz, y + 1, -1, 1);
                                float a2 = VertexAo(chunk, lx, lz, y + 1, 1, 1);
                                float a3 = VertexAo(chunk, lx, lz, y + 1, 1, -1);
                                TopWithAo(terrain, lx, y + 1, lz, r, g, b, a0, a1, a2, a3);
                            }

                            if (y >= c.Bottom && !Voxel.IsSolid(c.Read(y - 1)))
                            {
                                Bottom(terrain, lx, y, lz, r, g, b);
                            }

                            for (int side = 0; side < 4; side++)
                            {
                                VoxelColumn n = neighbours[side];
                                if (n != null && !Voxel.IsSolid(n.Read(y)))
                                {
                                    Side(terrain, side, lx, lz, y, y + 1, r, g, b, 1f);
                                }
                            }
                        }
                        else if (Voxel.IsWater(v))
                        {
                            uint above = c.Read(y + 1);
                            bool surface = !Voxel.IsSolid(above) && !Voxel.IsWater(above);
                            float topY = surface ? y + 1 - WaterSurfaceDrop : y + 1;
                            if (surface)
                            {
                                Top(water, lx, topY, lz, WaterR, WaterG, WaterB, WaterA);
                            }

                            for (int side = 0; side < 4; side++)
                            {
                                VoxelColumn n = neighbours[side];
                                if (n == null)
                                {
                                    continue;
                                }

                                uint nv = n.Read(y);
                                if (!Voxel.IsSolid(nv) && !Voxel.IsWater(nv))
                                {
                                    Side(water, side, lx, lz, y, topY, WaterR, WaterG, WaterB, WaterA);
                                }
                            }
                        }
                    }
                }
            }

            return new Result { Terrain = terrain, Water = water };
        }

        /// <summary>Standard voxel AO for one top-face vertex (dx, dz are the corner direction, ±1).</summary>
        private static float VertexAo(ChunkVoxels chunk, int x, int z, int y, int dx, int dz)
        {
            bool sideX = SolidAt(chunk, x + dx, z, y);
            bool sideZ = SolidAt(chunk, x, z + dz, y);
            bool corner = SolidAt(chunk, x + dx, z + dz, y);
            if (sideX && sideZ) return 0.55f;
            int count = (sideX ? 1 : 0) + (sideZ ? 1 : 0) + (corner ? 1 : 0);
            return 1f - count * 0.15f;
        }

        private static bool SolidAt(ChunkVoxels chunk, int x, int z, int y)
        {
            VoxelColumn c = chunk.At(x, z);
            return c != null && Voxel.IsSolid(c.Read(y));
        }

        private static void TopWithAo(MeshData mesh, float x, float y, float z, float r, float g, float b,
            float a0, float a1, float a2, float a3)
        {
            int s = mesh.VertexCount;
            mesh.AddVertex(x, y, z, 0f, 1f, 0f, r * a0, g * a0, b * a0, 1f);
            mesh.AddVertex(x, y, z + 1, 0f, 1f, 0f, r * a1, g * a1, b * a1, 1f);
            mesh.AddVertex(x + 1, y, z + 1, 0f, 1f, 0f, r * a2, g * a2, b * a2, 1f);
            mesh.AddVertex(x + 1, y, z, 0f, 1f, 0f, r * a3, g * a3, b * a3, 1f);
            mesh.AddQuad(s, s + 1, s + 2, s + 3);
        }

        private static void Top(MeshData mesh, float x, float y, float z, float r, float g, float b, float a)
        {
            int s = mesh.VertexCount;
            mesh.AddVertex(x, y, z, 0f, 1f, 0f, r, g, b, a);
            mesh.AddVertex(x, y, z + 1, 0f, 1f, 0f, r, g, b, a);
            mesh.AddVertex(x + 1, y, z + 1, 0f, 1f, 0f, r, g, b, a);
            mesh.AddVertex(x + 1, y, z, 0f, 1f, 0f, r, g, b, a);
            mesh.AddQuad(s, s + 1, s + 2, s + 3);
        }

        private static void Bottom(MeshData mesh, float x, float y, float z, float r, float g, float b)
        {
            int s = mesh.VertexCount;
            mesh.AddVertex(x, y, z, 0f, -1f, 0f, r, g, b, 1f);
            mesh.AddVertex(x + 1, y, z, 0f, -1f, 0f, r, g, b, 1f);
            mesh.AddVertex(x + 1, y, z + 1, 0f, -1f, 0f, r, g, b, 1f);
            mesh.AddVertex(x, y, z + 1, 0f, -1f, 0f, r, g, b, 1f);
            mesh.AddQuad(s, s + 1, s + 2, s + 3);
        }

        /// <summary>Side 0: −X, 1: +X, 2: −Z, 3: +Z; (a, b) is the bottom edge seen from outside, left to right.</summary>
        private static void Side(MeshData mesh, int side, int x, int z, float y0, float y1, float r, float g, float b, float a)
        {
            float ax, az, bx, bz, nx = 0f, nz = 0f;
            switch (side)
            {
                case 0: ax = x; az = z; bx = x; bz = z + 1; nx = -1f; break;
                case 1: ax = x + 1; az = z + 1; bx = x + 1; bz = z; nx = 1f; break;
                case 2: ax = x + 1; az = z; bx = x; bz = z; nz = -1f; break;
                default: ax = x; az = z + 1; bx = x + 1; bz = z + 1; nz = 1f; break;
            }

            int s = mesh.VertexCount;
            mesh.AddVertex(ax, y0, az, nx, 0f, nz, r, g, b, a);
            mesh.AddVertex(bx, y0, bz, nx, 0f, nz, r, g, b, a);
            mesh.AddVertex(bx, y1, bz, nx, 0f, nz, r, g, b, a);
            mesh.AddVertex(ax, y1, az, nx, 0f, nz, r, g, b, a);
            mesh.AddQuad(s, s + 1, s + 2, s + 3);
        }
    }
}
