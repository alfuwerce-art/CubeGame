// CubeGame.Content — bridges the game's climate grids to the W-18 climate selection (D86 M0-02C).
//
// The world module owns the humidity/temperature/region-cell grids; this adapter samples them at a cell
// and answers with AlphaScatterSelector's translated Alpha selection, or with concrete vegetation
// placements. Nothing here guesses climate: a missing grid is an error, not a default value.
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
        readonly double _spacing;

        /// <param name="spacing">World_placeObjectWithSpacing (fVar6); not ported, caller supplies it.</param>
        public AlphaScatterField(IAlphaClimateGrid grid, double spacing)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _spacing = spacing;
        }

        public AlphaScatterSelection SelectAt(double x, double z, int level, int mode = 0)
        {
            if (_grid == null) throw new InvalidOperationException("CLIMATE_GRID_MISSING");
            return AlphaScatterSelector.Select(_grid.Humidity(x, z), _grid.Temperature(x, z),
                _grid.RegionCell(x, z), _spacing, level, mode);
        }

        /// <summary>Place the selected objects of one cell (deterministic; only staged kinds are placed).</summary>
        public List<AlphaScatterPlacement> PlaceCell(uint seed, int regionX, int regionZ, int cellX, int cellZ,
            int level, IReadOnlyList<int> stagedKinds, List<string> problems = null, int mode = 0)
        {
            int cellSize = AlphaContentTables.RegionBlocks / AlphaContentTables.RegionCells;
            double x = regionX * AlphaContentTables.RegionBlocks + cellX * cellSize + cellSize * 0.5;
            double z = regionZ * AlphaContentTables.RegionBlocks + cellZ * cellSize + cellSize * 0.5;
            if (_grid == null)
            {
                problems?.Add("CLIMATE_GRID_MISSING");
                return new List<AlphaScatterPlacement>();
            }
            return AlphaVegetationPlanner.Plan(seed, regionX, regionZ, cellX, cellZ,
                _grid.Humidity(x, z), _grid.Temperature(x, z), _grid.RegionCell(x, z), _spacing,
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
                for (int i = 0; i < stagedKinds.Count; i++) if (stagedKinds[i] == placement.Kind) staged = true;
                if (!staged) p.Add($"CLIMATE_PLACEMENT_NOT_STAGED {placement.Kind}");
            }
            return p;
        }
    }
}
