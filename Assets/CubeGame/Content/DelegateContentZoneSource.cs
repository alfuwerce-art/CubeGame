// CubeGame.Content — delegate-based world source (D86 M0-02/M0-06/M0-07 integration seam).
//
// The world module (Claude's Features/World) does not share types with this module, so the game wires
// its facts in through delegates at composition time:
//
//   var source = new DelegateContentZoneSource {
//       Seed        = session.Seed,
//       Climate     = cell => climateFor(cell),
//       Surface     = (x, z) => worldState.SurfaceAt(x, z),          // top solid voxel
//       FootprintFree = (x, z, w, d) => worldState.IsFlatAndFree(x, z, w, d),
//       Removed     = id => deltaStore.IsRemoved(id),
//   };
//
// Missing pieces fail loudly instead of silently inventing world data: no climate → exception, no
// surface → the placement is reported and skipped by the planner.
using System;

namespace CubeGame.Content
{
    /// <summary>Adapter that lets the world layer supply the content layer's inputs as delegates.</summary>
    public sealed class DelegateContentZoneSource : IContentZoneSource
    {
        public uint Seed { get; set; }

        /// <summary>Climate of a landmark cell; required.</summary>
        public Func<WorldCellKey, ZoneClimate> Climate { get; set; }

        /// <summary>Top solid voxel of a column, or <see cref="UnknownSurface"/> when the world has no data.</summary>
        public Func<int, int, int> Surface { get; set; }

        /// <summary>Footprint predicate for structures (flat and free). Null = every column is usable.</summary>
        public Func<int, int, int, int, bool> FootprintFree { get; set; }

        /// <summary>Saved removals/destroyed entities. Null = nothing was removed.</summary>
        public Func<string, bool> Removed { get; set; }

        /// <summary>Return value of <see cref="Surface"/> meaning "this column has no world data".</summary>
        public const int UnknownSurface = int.MinValue;

        public ZoneClimate ClimateOf(WorldCellKey cell)
        {
            if (Climate == null) throw new InvalidOperationException("CONTENT_CLIMATE_MISSING " + cell);
            return Climate(cell);
        }

        public bool TrySurfaceVoxel(int voxelX, int voxelZ, out int topSolidY)
        {
            if (Surface == null) { topSolidY = 0; return false; }
            topSolidY = Surface(voxelX, voxelZ);
            return topSolidY != UnknownSurface;
        }

        public bool IsFlatAndFree(int voxelX, int voxelZ, int width, int depth) =>
            FootprintFree == null || FootprintFree(voxelX, voxelZ, width, depth);

        public bool IsRemoved(string stableId) => Removed != null && Removed(stableId);
    }
}
