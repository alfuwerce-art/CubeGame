using System.Collections.Generic;
using CubeGame.Core.Generation;
using CubeGame.Core.Rendering;
using UnityEngine;

namespace CubeGame.Presentation.Rendering
{
    /// <summary>
    /// Presentation of the pilot world: one GameObject per chunk with a merged mesh and a collider.
    /// Geometry comes from the engine-independent Core builder (ChunkMeshBuilder), so the same
    /// seed always produces the same mesh (S01-PILOT P2 determinism cases).
    /// </summary>
    public sealed class PilotWorldView : MonoBehaviour
    {
        private readonly List<GameObject> _chunks = new List<GameObject>();
        private readonly List<Mesh> _meshes = new List<Mesh>();

        public int ChunkCount => _chunks.Count;
        public int ChunkSize => PilotWorldLayout.ChunkSize;
        public double LastBuildMilliseconds { get; private set; }
        public int LastVertexCount { get; private set; }
        public Material TerrainMaterial { get; set; }

        public void Build(uint seed, int chunksPerAxis, int originChunkX, int originChunkZ)
        {
            Clear();
            var field = new PilotHeightField(seed);
            Material material = PilotMaterials.Terrain(TerrainMaterial);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int chunkZ = 0; chunkZ < chunksPerAxis; chunkZ++)
            {
                for (int chunkX = 0; chunkX < chunksPerAxis; chunkX++)
                {
                    int cx = originChunkX + chunkX;
                    int cz = originChunkZ + chunkZ;
                    MeshData data = ChunkMeshBuilder.Build(field, cx, cz);
                    LastVertexCount += data.VertexCount;

                    var chunkObject = new GameObject($"Chunk_{cx}_{cz}");
                    chunkObject.transform.SetParent(transform, false);

                    Mesh mesh = UnityMeshFactory.Create(data, $"ChunkMesh_{cx}_{cz}");
                    _meshes.Add(mesh);

                    var filter = chunkObject.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh;

                    var renderer = chunkObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;

                    var collider = chunkObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;

                    _chunks.Add(chunkObject);
                }
            }

            stopwatch.Stop();
            LastBuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        }

        public void Clear()
        {
            foreach (GameObject chunk in _chunks)
            {
                if (chunk != null)
                {
                    DestroyTracked(chunk);
                }
            }

            foreach (Mesh mesh in _meshes)
            {
                if (mesh != null)
                {
                    DestroyTracked(mesh);
                }
            }

            _chunks.Clear();
            _meshes.Clear();
            LastVertexCount = 0;
        }

        private static void DestroyTracked(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
