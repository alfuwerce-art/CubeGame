namespace CubeGame.Core.Generation
{
    /// <summary>
    /// Layout constants of the pilot world.
    ///
    /// PROVENANCE: the chunk edge of 32 blocks follows the Alpha 2013 ZoneTile size
    /// (reconstructed from Zone::constructTileGrid: 4096 tiles per 2048-block zone).
    /// Everything else here is a pilot decision and is marked PLACEHOLDER_NOT_ALPHA,
    /// see docs/research/alpha2013/GENERATION_MOVEMENT_SPEC.md.
    /// </summary>
    public static class PilotWorldLayout
    {
        /// <summary>Blocks per chunk edge. Same as Alpha ZoneTile (RECONSTRUCTED_SOURCE).</summary>
        public const int ChunkSize = 32;

        /// <summary>Inclusive minimum terrain height in blocks. PLACEHOLDER_NOT_ALPHA.</summary>
        public const int MinHeight = 8;

        /// <summary>Inclusive maximum terrain height in blocks. PLACEHOLDER_NOT_ALPHA.</summary>
        public const int MaxHeight = 96;

        /// <summary>Blocks per chunk of chunks shown by the pilot (area = ViewChunks x ViewChunks).</summary>
        public const int ViewChunks = 8;

        /// <summary>Number of independent noise octaves in the pilot height field.</summary>
        public const int Octaves = 4;

        /// <summary>Lattice step of the first (largest) octave, in blocks.</summary>
        public const int BaseScale = 64;

        /// <summary>Smooth interpolation of noise values; keeps the terrain blocky but not spiky.</summary>
        public const bool SmoothInterpolation = true;
    }
}
