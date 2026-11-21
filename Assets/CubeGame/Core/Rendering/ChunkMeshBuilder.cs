using CubeGame.Core.Generation;

namespace CubeGame.Core.Rendering
{
    /// <summary>
    /// Builds the visual geometry of one chunk from the height field.
    ///
    /// Only visible faces are emitted: the top face of every column and the side faces where the
    /// neighbouring column is lower. The neighbour lookup always goes through the global height
    /// field, so adjacent chunks share identical edge heights (no cracks, TC-GEN-03).
    /// </summary>
    public static class ChunkMeshBuilder
    {
        public static MeshData Build(PilotHeightField field, int chunkX, int chunkZ)
        {
            var mesh = new MeshData();
            int size = PilotWorldLayout.ChunkSize;
            int baseX = chunkX * size;
            int baseZ = chunkZ * size;

            // Top faces.
            for (int localZ = 0; localZ < size; localZ++)
            {
                for (int localX = 0; localX < size; localX++)
                {
                    int worldX = baseX + localX;
                    int worldZ = baseZ + localZ;
                    int height = field.HeightAt(worldX, worldZ);
                    float variation = Unit(DeterministicHash.Combine(worldX, worldZ, (int)field.Seed));
                    PilotPalette.ColorFor(height, variation, out float r, out float g, out float b);

                    float y = height;
                    int start = mesh.VertexCount;
                    mesh.AddVertex(worldX, y, worldZ, 0f, 1f, 0f, r, g, b, 1f);
                    mesh.AddVertex(worldX, y, worldZ + 1, 0f, 1f, 0f, r, g, b, 1f);
                    mesh.AddVertex(worldX + 1, y, worldZ + 1, 0f, 1f, 0f, r, g, b, 1f);
                    mesh.AddVertex(worldX + 1, y, worldZ, 0f, 1f, 0f, r, g, b, 1f);
                    mesh.AddQuad(start, start + 1, start + 2, start + 3);
                }
            }

            // Side faces: -X, +X, -Z, +Z.
            for (int localZ = 0; localZ < size; localZ++)
            {
                for (int localX = 0; localX < size; localX++)
                {
                    int worldX = baseX + localX;
                    int worldZ = baseZ + localZ;
                    int height = field.HeightAt(worldX, worldZ);
                    float variation = Unit(DeterministicHash.Combine(worldX, worldZ, (int)field.Seed + 7));
                    PilotPalette.RockColor(variation, out float r, out float g, out float b);

                    EmitSide(mesh, field, worldX - 1, worldZ, height, 0f, 0f, -1f,
                        worldX, worldZ, worldX, worldZ + 1, r, g, b);
                    EmitSide(mesh, field, worldX + 1, worldZ, height, 0f, 0f, 1f,
                        worldX + 1, worldZ, worldX + 1, worldZ + 1, r, g, b);
                    EmitSide(mesh, field, worldX, worldZ - 1, height, -1f, 0f, 0f,
                        worldX, worldZ, worldX + 1, worldZ, r, g, b);
                    EmitSide(mesh, field, worldX, worldZ + 1, height, 1f, 0f, 0f,
                        worldX, worldZ + 1, worldX + 1, worldZ + 1, r, g, b);
                }
            }

            return mesh;
        }

        private static void EmitSide(
            MeshData mesh,
            PilotHeightField field,
            int neighbourX,
            int neighbourZ,
            int ownHeight,
            float normalX,
            float normalY,
            float normalZ,
            float ax,
            float az,
            float bx,
            float bz,
            float r,
            float g,
            float b)
        {
            int neighbourHeight = field.HeightAt(neighbourX, neighbourZ);
            if (neighbourHeight >= ownHeight)
            {
                return;
            }

            float bottom = neighbourHeight;
            float top = ownHeight;

            int start = mesh.VertexCount;
            if (normalZ < 0f || normalX > 0f)
            {
                mesh.AddVertex(ax, bottom, az, normalX, normalY, normalZ, r, g, b, 1f);
                mesh.AddVertex(bx, bottom, bz, normalX, normalY, normalZ, r, g, b, 1f);
                mesh.AddVertex(bx, top, bz, normalX, normalY, normalZ, r, g, b, 1f);
                mesh.AddVertex(ax, top, az, normalX, normalY, normalZ, r, g, b, 1f);
            }
            else
            {
                mesh.AddVertex(bx, bottom, bz, normalX, normalY, normalZ, r, g, b, 1f);
                mesh.AddVertex(ax, bottom, az, normalX, normalY, normalZ, r, g, b, 1f);
                mesh.AddVertex(ax, top, az, normalX, normalY, normalZ, r, g, b, 1f);
                mesh.AddVertex(bx, top, bz, normalX, normalY, normalZ, r, g, b, 1f);
            }

            mesh.AddQuad(start, start + 1, start + 2, start + 3);
        }

        private static float Unit(uint hash)
        {
            return (hash & 0xFFFFu) / 65535f;
        }
    }
}
