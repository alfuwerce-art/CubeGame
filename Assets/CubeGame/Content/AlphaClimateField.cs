// CubeGame.Content — bridges the game's climate grids to the W-18 climate selection (D86 M0-02C).
//
// The world module owns the humidity/temperature/region-cell grids; this adapter samples them at a
// landmark cell (8×8 zones = 2 048 voxels with the executed Alpha scale) and answers with
// AlphaScatterSelector's translated Alpha selection, or with concrete vegetation placements.
// Nothing here guesses climate: a missing grid is an error, not a default value.
//
// Spacing (fVar6 of 0x5F56C0): Alpha computes it with the terrain mask function 0x5EFFA0, which the
// M0-02 oracle executed as the mountain/terrain mask (300 points, 2.1·10⁻⁷). It is NOT an object
// placement algorithm — see docs/reports/D86_W18_ADDRESS_VERIFICATION.md. The field accepts either a
// fixed value or a per-position provider so the executed formula can be plugged in without changing
// callers; until then the value is a documented CubeGame input, not a claimed Alpha value.
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    public interface IAlphaClimateGrid
    {
        /// <summary>GameController_sampleHumidityGrid at a world position (0..1).</summary>
        double Humidity(double x, double z);
        /// <summary>GameController_sampleTemperatureGrid at a world position (0..1).</summary>
        double Temperature(double x, double z);
        /// <summary>World_getRegionCellPtr value at a world position.</summary>
        double RegionCell(double x, double z);
    }

    public sealed class AlphaScatterField
    {
        readonly IAlphaClimateGrid _grid;
        readonly Func<double, double, double> _spacingAt;

        /// <param name="spacing">fVar6 of 0x5F56C0 (Alpha: terrain mask 0x5EFFA0); caller supplies it.</param>
        public AlphaScatterField(IAlphaClimateGrid grid, double spacing)
            : this(grid, (x, z) => spacing)
        {
        }

        /// <param name="spacingAt">Per-position provider (x, z) → fVar6; plug in the executed 0x5EFFA0 mask.</param>
        public AlphaScatterField(IAlphaClimateGrid grid, Func<double, double, double> spacingAt)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _spacingAt = spacingAt ?? throw new ArgumentNullException(nameof(spacingAt));
        }

        public double SpacingAt(double x, double z) => _spacingAt(x, z);

        public AlphaScatterSelection SelectAt(double x, double z, int level, int mode = 0)
        {
            if (_grid == null) throw new InvalidOperationException("CLIMATE_GRID_MISSING");
            return AlphaScatterSelector.Select(_grid.Humidity(x, z), _grid.Temperature(x, z),
                _grid.RegionCell(x, z), _spacingAt(x, z), level, mode);
        }

        /// <summary>Place the selected objects of one landmark cell (deterministic; only staged kinds are placed).</summary>
        public List<AlphaScatterPlacement> PlaceCell(uint seed, int regionX, int regionZ, int cellX, int cellZ,
            int level, IReadOnlyList<int> stagedKinds, List<string> problems = null, int mode = 0)
        {
            double x = AlphaWorldScale.RegionOriginVoxel(regionX) + AlphaWorldScale.CellOriginVoxel(cellX) +
                       AlphaWorldScale.CellVoxels * 0.5;
            double z = AlphaWorldScale.RegionOriginVoxel(regionZ) + AlphaWorldScale.CellOriginVoxel(cellZ) +
                       AlphaWorldScale.CellVoxels * 0.5;
            if (_grid == null)
            {
                problems?.Add("CLIMATE_GRID_MISSING");
                return new List<AlphaScatterPlacement>();
            }
            return AlphaVegetationPlanner.Plan(seed, regionX, regionZ, cellX, cellZ,
                _grid.Humidity(x, z), _grid.Temperature(x, z), _grid.RegionCell(x, z), _spacingAt(x, z),
                level, mode, stagedKinds, problems);
        }

        public static List<string> Validate(IAlphaClimateGrid grid, double spacing, IReadOnlyList<int> stagedKinds)
        {
            var p = new List<string>();
            if (grid == null) { p.Add("CLIMATE_GRID_MISSING"); return p; }
            var field = new AlphaScatterField(grid, spacing);
            var dry = field.SelectAt(0, 0, 6);
            if (Array.IndexOf(dry.ObjectKinds, 0x15) < 0 && Array.IndexOf(dry.ObjectKinds, 0x2e) < 0)
                p.Add("CLIMATE_DRY_BRANCH_NOT_SELECTED");
            var placements = field.PlaceCell(1u, 0, 0, 0, 0, 6, stagedKinds);
            foreach (var placement in placements)
            {
                bool staged = false;
                for (int i = 0; stagedKinds != null && i < stagedKinds.Count; i++)
                    if (stagedKinds[i] == placement.Kind) staged = true;
                if (!staged) p.Add($"CLIMATE_PLACEMENT_NOT_STAGED {placement.Kind}");
                if (placement.X < 0 || placement.Z < 0 || placement.X >= AlphaWorldScale.CellVoxels ||
                    placement.Z >= AlphaWorldScale.CellVoxels)
                    p.Add($"CLIMATE_PLACEMENT_OUTSIDE_CELL {placement.X},{placement.Z}");
            }
            return p;
        }
    }
}
