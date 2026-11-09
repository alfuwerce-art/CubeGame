using CubeGame.Core.Generation;
using CubeGame.Core.Rendering;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>Generated heights of one chunk plus a one-column border (thread-safe to compute: generator only).</summary>
    public sealed class ChunkHeights
    {
        public const int Size = WorldLayout.ChunkSize;
        public const int Span = Size + 2;

        public ChunkKey Key;
        public int[] Surface = new int[Span * Span];
        public bool[] Rock = new bool[Span * Span];

        public static ChunkHeights Sample(IWorldGenerator generator, ChunkKey key)
        {
            var result = new ChunkHeights { Key = key };
            int originX = key.X * Size;
            int originZ = key.Z * Size;
            for (int lz = -1; lz <= Size; lz++)
            {
                for (int lx = -1; lx <= Size; lx++)
                {
                    int i = (lz + 1) * Span + lx + 1;
                    int wx = originX + lx;
                    int wz = originZ + lz;
                    if (!WorldLayout.IsInsideVoxels(wx, wz))
                    {
                        result.Surface[i] = int.MinValue / 2;
                        continue;
                    }

                    TerrainColumn column = generator.Column(wx, wz);
                    result.Surface[i] = column.Surface;
                    result.Rock[i] = column.Rock;
                }
            }

            return result;
        }

        /// <summary>Overlays persistent column edits (main thread: the delta store is not shared with workers).</summary>
        public void ApplyDeltas(WorldDeltaStore deltas)
        {
            int originX = Key.X * Size;
            int originZ = Key.Z * Size;
            for (int lz = -1; lz <= Size; lz++)
            {
                for (int lx = -1; lx <= Size; lx++)
                {
                    if (deltas.TryGetColumnSurface(originX + lx, originZ + lz, out int edited))
                    {
                        Surface[(lz + 1) * Span + lx + 1] = edited;
                    }
                }
            }
        }

        public int At(int lx, int lz) => Surface[(lz + 1) * Span + lx + 1];

        public ChunkHeights Copy()
        {
            return new ChunkHeights { Key = Key, Surface = (int[])Surface.Clone(), Rock = (bool[])Rock.Clone() };
        }
    }

    /// <summary>
    /// Builds the render/collision mesh of one 32×32-voxel chunk in chunk-local coordinates: column tops and exposed
    /// sides, water quads below the water level. Surface colours are PLACEHOLDER_NOT_ALPHA (Alpha colour blend in
    /// 0x5E4850 not ported).
    /// </summary>
    public static class ChunkMesher
    {
        public sealed class Result
        {
            public MeshData Terrain;
            public MeshData Water;
        }

        public static Result Build(ChunkHeights heights, int waterLevel)
        {
            int size = ChunkHeights.Size;
            int originX = heights.Key.X * size;
            int originZ = heights.Key.Z * size;
            var terrain = new MeshData();
            var water = new MeshData();
            for (int lz = 0; lz < size; lz++)
            {
                for (int lx = 0; lx < size; lx++)
                {
                    int h = heights.At(lx, lz);
                    bool rock = heights.Rock[(lz + 1) * ChunkHeights.Span + lx + 1];
                    float variation = (DeterministicHash.Combine(originX + lx, originZ + lz, 17) & 0xFFFF) / 65535f;
                    TopColor(h, waterLevel, rock, variation, out float r, out float g, out float b);
                    float top = h + 1;
                    int s = terrain.VertexCount;
                    terrain.AddVertex(lx, top, lz, 0f, 1f, 0f, r, g, b, 1f);
                    terrain.AddVertex(lx, top, lz + 1, 0f, 1f, 0f, r, g, b, 1f);
                    terrain.AddVertex(lx + 1, top, lz + 1, 0f, 1f, 0f, r, g, b, 1f);
                    terrain.AddVertex(lx + 1, top, lz, 0f, 1f, 0f, r, g, b, 1f);
                    terrain.AddQuad(s, s + 1, s + 2, s + 3);

                    SideColor(variation, rock, out float sr, out float sg, out float sb);
                    Side(terrain, h, heights.At(lx - 1, lz), lx, lz, lx, lz + 1, -1f, 0f, sr, sg, sb);
                    Side(terrain, h, heights.At(lx + 1, lz), lx + 1, lz + 1, lx + 1, lz, 1f, 0f, sr, sg, sb);
                    Side(terrain, h, heights.At(lx, lz - 1), lx + 1, lz, lx, lz, 0f, -1f, sr, sg, sb);
                    Side(terrain, h, heights.At(lx, lz + 1), lx, lz + 1, lx + 1, lz + 1, 0f, 1f, sr, sg, sb);

                    if (h < waterLevel)
                    {
                        float wy = waterLevel + 0.9f;
                        int w = water.VertexCount;
                        water.AddVertex(lx, wy, lz, 0f, 1f, 0f, 0.20f, 0.42f, 0.75f, 0.7f);
                        water.AddVertex(lx, wy, lz + 1, 0f, 1f, 0f, 0.20f, 0.42f, 0.75f, 0.7f);
                        water.AddVertex(lx + 1, wy, lz + 1, 0f, 1f, 0f, 0.20f, 0.42f, 0.75f, 0.7f);
                        water.AddVertex(lx + 1, wy, lz, 0f, 1f, 0f, 0.20f, 0.42f, 0.75f, 0.7f);
                        water.AddQuad(w, w + 1, w + 2, w + 3);
                    }
                }
            }

            return new Result { Terrain = terrain, Water = water };
        }

        /// <summary>Wall from the neighbour's top up to this column's top; (ax,az) is the right-hand edge seen from outside.</summary>
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
                r = 0.80f; g = 0.74f; b = 0.52f;
            }
            else if (rock)
            {
                r = 0.52f; g = 0.50f; b = 0.47f;
            }
            else if (h > water + 160)
            {
                r = 0.93f; g = 0.95f; b = 0.97f;
            }
            else if (h > water + 90)
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

        private static void SideColor(float variation, bool rock, out float r, out float g, out float b)
        {
            float shade = 1f - variation * 0.06f;
            r = (rock ? 0.46f : 0.42f) * shade;
            g = (rock ? 0.44f : 0.36f) * shade;
            b = (rock ? 0.41f : 0.30f) * shade;
        }
    }
}
