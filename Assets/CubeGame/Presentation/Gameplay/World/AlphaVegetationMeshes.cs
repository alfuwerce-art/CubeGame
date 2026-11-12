using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using CubeGame.AlphaTransfer;
using CubeGame.Features.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeGame.Gameplay.Streaming
{
    internal sealed class AlphaVegetationMeshes
    {
        private readonly Dictionary<AlphaVegetationModel, VoxelMesh.Result> _models =
            new Dictionary<AlphaVegetationModel, VoxelMesh.Result>();
        private readonly List<CombineInstance> _instances = new List<CombineInstance>();
        private readonly string _directory;

        internal AlphaVegetationMeshes()
        {
            string root = AlphaContentSource.Resolve().Root;
            if (!string.IsNullOrEmpty(root)) _directory = Path.Combine(root, AlphaContentSource.EnvironmentFolder);
        }

        internal Mesh Build(ChunkVoxels chunk, out int count)
        {
            int x0 = chunk.Key.X * ChunkVoxels.Size;
            int z0 = chunk.Key.Z * ChunkVoxels.Size;
            int vertices = 0;
            foreach (AlphaPlantPlacement plant in chunk.Plants)
            {
                int x = plant.X - x0;
                int z = plant.Z - z0;
                VoxelColumn column = chunk.At(x, z);
                if (Voxel.Material(column.Read(plant.Y - 1)) != plant.SurfaceMaterial ||
                    Voxel.Material(column.Read(plant.Y)) != plant.AboveMaterial)
                    continue;

                VoxelMesh.Result model = Load(plant.Model);
                if (model.mesh == null) continue;

                var rotation = Quaternion.Euler(0f, -plant.Yaw, 0f);
                var pivot = new Vector3(model.size.x * -0.5f, 0f, model.size.y * -0.5f);
                var position = new Vector3(x + 0.5f, plant.Y + plant.HeightOffset, z + 0.5f);
                _instances.Add(new CombineInstance
                {
                    mesh = model.mesh,
                    transform = Matrix4x4.TRS(position + rotation * (pivot * plant.Scale), rotation, Vector3.one * plant.Scale),
                });
                vertices += model.mesh.vertexCount;
            }

            count = _instances.Count;
            if (count == 0) return null;
            var mesh = new Mesh
            {
                name = $"Vegetation_{chunk.Key.X}_{chunk.Key.Z}",
                indexFormat = vertices > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.CombineMeshes(_instances.ToArray());
            mesh.UploadMeshData(true);
            _instances.Clear();
            return mesh;
        }

        private VoxelMesh.Result Load(AlphaVegetationModel id)
        {
            if (_models.TryGetValue(id, out var model)) return model;
            string name = id switch
            {
                AlphaVegetationModel.Flowers2 => "flowers2",
                AlphaVegetationModel.Flowers => "flowers",
                AlphaVegetationModel.Grass => "grass",
                AlphaVegetationModel.Grass2 => "grass2",
                AlphaVegetationModel.Grass3 => "grass3",
                AlphaVegetationModel.UnderwaterPlant => "underwater-plant",
                AlphaVegetationModel.Coral => "coral",
                AlphaVegetationModel.Alga => "alga",
                AlphaVegetationModel.LavaGrass => "lava-grass",
                AlphaVegetationModel.ThornPlant => "thorn-plant",
                AlphaVegetationModel.Echinacea => "echinacea2",
                AlphaVegetationModel.Leaf => "leaf",
                AlphaVegetationModel.Reed => "reed",
                AlphaVegetationModel.DesertFlower1 => "desert-flower01",
                AlphaVegetationModel.DesertFlower2 => "desert-flower02",
                AlphaVegetationModel.WaterLily1 => "water-lily01",
                AlphaVegetationModel.WaterLily2 => "water-lily02",
                _ => null,
            };
            string path = name != null && _directory != null ? Path.Combine(_directory, name + ".cubdec") : null;
            try
            {
                if (path != null && File.Exists(path)) model = VoxelMesh.Build(File.ReadAllBytes(path), name);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"[AlphaVegetation] {name}: {exception.Message}");
            }
            if (model.mesh == null || model.problem != null)
            {
                Debug.LogWarning($"[AlphaVegetation] missing or invalid model {id} ({name}): {model.problem}");
                if (model.mesh != null) Object.Destroy(model.mesh);
                model = default;
            }
            _models.Add(id, model);
            return model;
        }

        internal void Clear()
        {
            foreach (var model in _models.Values)
                if (model.mesh != null) Object.Destroy(model.mesh);
            _models.Clear();
        }
    }
}
