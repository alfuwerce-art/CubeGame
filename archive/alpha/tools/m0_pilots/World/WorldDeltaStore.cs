using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CubeGame.M0Pilots.World
{
    public sealed class BlockEdit
    {
        public int X;
        public int Y;
        public int Z;
        public int BlockType;
    }

    /// <summary>
    /// CubeGame world-delta contract for M0-02D (not an Alpha file format).
    ///
    /// Alpha persists only entity blobs and a discovered mask; the concrete save layout of
    /// edited voxels is PARTIAL (DATA_AND_SAVE_SCHEMAS §L4). This contract stores only
    /// player edits over a deterministic base generator, so unchanged chunks can be
    /// regenerated from the seed and the delta file can be validated independently.
    /// </summary>
    public sealed class WorldDeltaStore
    {
        public const string Header = "M0DELTA";
        public const int Version = 1;
        private const int MaxEdits = 1000000;

        private readonly Dictionary<string, BlockEdit> _edits = new Dictionary<string, BlockEdit>(StringComparer.Ordinal);
        private readonly int _seed;

        public WorldDeltaStore(int seed)
        {
            _seed = seed;
        }

        public int Seed => _seed;

        public int Count => _edits.Count;

        public void SetBlock(int x, int y, int z, int blockType)
        {
            ValidateBlock(x, y, z, blockType);
            string key = Key(x, y, z);
            _edits[key] = new BlockEdit { X = x, Y = y, Z = z, BlockType = blockType };
        }

        public bool TryGetBlock(int x, int y, int z, out int blockType)
        {
            BlockEdit edit;
            if (_edits.TryGetValue(Key(x, y, z), out edit))
            {
                blockType = edit.BlockType;
                return true;
            }

            blockType = 0;
            return false;
        }

        /// <summary>Resolved block: an explicit edit wins, otherwise the deterministic base generator.</summary>
        public int ResolveBlock(int x, int y, int z)
        {
            int blockType;
            return TryGetBlock(x, y, z, out blockType) ? blockType : BaseBlock(_seed, x, y, z);
        }

        /// <summary>
        /// Deterministic base terrain stand-in. STATUS: CUBEGAME_CONTRACT, not Alpha:
        /// the real Alpha height field is not ported yet (W-03/W-04 are PARTIAL/PLACEHOLDER).
        /// </summary>
        public static int BaseBlock(int seed, int x, int y, int z)
        {
            if (y < 0 || y > 255 || x < 0 || x >= AlphaRegion.WorldBlocks || z < 0 || z >= AlphaRegion.WorldBlocks)
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Block coordinate out of the pilot world bounds.");
            }

            uint h = unchecked((uint)seed);
            h ^= unchecked((uint)x * 0x9E3779B1u);
            h ^= unchecked((uint)z * 0x85EBCA77u);
            h ^= unchecked(h >> 15);
            int surface = 8 + (int)(h % 24u);
            if (y == 0)
            {
                return 1;
            }

            if (y > surface)
            {
                return 0;
            }

            h ^= unchecked((uint)y * 0xC2B2AE3Du);
            h ^= unchecked(h >> 13);
            return 1 + (int)(h % 4u);
        }

        public ulong ComputeChunkHash(int regionX, int regionZ, int chunkX, int chunkZ)
        {
            if (regionX < 0 || regionX >= AlphaRegion.Grid || regionZ < 0 || regionZ >= AlphaRegion.Grid)
            {
                throw new ArgumentOutOfRangeException(nameof(regionX), "Region coordinate out of bounds.");
            }

            int chunksPerRegion = AlphaRegion.RegionBlocks / AlphaRegion.ChunkBlocks;
            if (chunkX < 0 || chunkX >= chunksPerRegion || chunkZ < 0 || chunkZ >= chunksPerRegion)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkX), "Chunk coordinate out of the region.");
            }

            int baseX = regionX * AlphaRegion.RegionBlocks + chunkX * AlphaRegion.ChunkBlocks;
            int baseZ = regionZ * AlphaRegion.RegionBlocks + chunkZ * AlphaRegion.ChunkBlocks;
            var builder = new StringBuilder(AlphaRegion.ChunkBlocks * AlphaRegion.ChunkBlocks * 4);
            for (int y = 0; y < 256; y++)
            {
                for (int z = 0; z < AlphaRegion.ChunkBlocks; z++)
                {
                    for (int x = 0; x < AlphaRegion.ChunkBlocks; x++)
                    {
                        builder.Append(ResolveBlock(baseX + x, y, baseZ + z).ToString("X2", CultureInfo.InvariantCulture));
                    }
                }
            }

            return CubeGame.Core.Persistence.SaveService.Fnv1a64(builder.ToString());
        }

        /// <summary>Deterministic text serialization with length, count and checksum.</summary>
        public string Serialize()
        {
            var ordered = new List<BlockEdit>(_edits.Values);
            ordered.Sort(CompareEdits);
            var payload = new StringBuilder();
            for (int i = 0; i < ordered.Count; i++)
            {
                BlockEdit edit = ordered[i];
                if (i > 0)
                {
                    payload.Append('\n');
                }

                payload.Append(edit.X.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(edit.Y.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(edit.Z.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(edit.BlockType.ToString(CultureInfo.InvariantCulture));
            }

            string body = payload.ToString();
            ulong hash = CubeGame.Core.Persistence.SaveService.Fnv1a64(body);
            return Header + "|" + Version.ToString(CultureInfo.InvariantCulture) + "|"
                   + _seed.ToString(CultureInfo.InvariantCulture) + "|"
                   + ordered.Count.ToString(CultureInfo.InvariantCulture) + "|"
                   + hash.ToString("x16", CultureInfo.InvariantCulture) + "\n" + body;
        }

        public static bool TryDeserialize(string text, out WorldDeltaStore store, out string error)
        {
            store = null;
            error = string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                error = "empty delta file";
                return false;
            }

            int newline = text.IndexOf('\n');
            string header = newline < 0 ? text : text.Substring(0, newline);
            string body = newline < 0 ? string.Empty : text.Substring(newline + 1);
            string[] parts = header.TrimEnd(new[] { '\r' }).Split(new[] { '|' });
            if (parts.Length != 5 || parts[0] != Header)
            {
                error = "delta header is not recognized";
                return false;
            }

            int version;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out version) || version != Version)
            {
                error = "unsupported delta version: " + parts[1];
                return false;
            }

            int seed;
            if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
            {
                error = "delta seed is not an integer";
                return false;
            }

            int count;
            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 0 || count > MaxEdits)
            {
                error = "delta edit count is invalid";
                return false;
            }

            ulong expectedHash;
            if (!ulong.TryParse(parts[4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out expectedHash))
            {
                error = "delta checksum is not hexadecimal";
                return false;
            }

            if (CubeGame.Core.Persistence.SaveService.Fnv1a64(body) != expectedHash)
            {
                error = "delta checksum mismatch: file is corrupted";
                return false;
            }

            string[] lines = body.Length == 0 ? new string[0] : body.Split(new[] { '\n' });
            if (lines.Length != count)
            {
                error = "delta edit count mismatch: header=" + count + " actual=" + lines.Length;
                return false;
            }

            var parsed = new WorldDeltaStore(seed);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd(new[] { '\r' });
                string[] fields = line.Split(new[] { ',' });
                int x;
                int y;
                int z;
                int blockType;
                if (fields.Length != 4
                    || !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                    || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y)
                    || !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out z)
                    || !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out blockType))
                {
                    error = "delta line " + (i + 1) + " is malformed";
                    return false;
                }

                try
                {
                    parsed.SetBlock(x, y, z, blockType);
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    error = "delta line " + (i + 1) + ": " + exception.Message;
                    return false;
                }
            }

            store = parsed;
            return true;
        }

        private static int CompareEdits(BlockEdit left, BlockEdit right)
        {
            int result = left.X.CompareTo(right.X);
            if (result != 0)
            {
                return result;
            }

            result = left.Z.CompareTo(right.Z);
            return result != 0 ? result : left.Y.CompareTo(right.Y);
        }

        private static string Key(int x, int y, int z)
        {
            return x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture) + ","
                   + z.ToString(CultureInfo.InvariantCulture);
        }

        private static void ValidateBlock(int x, int y, int z, int blockType)
        {
            if (x < 0 || x >= AlphaRegion.WorldBlocks || z < 0 || z >= AlphaRegion.WorldBlocks)
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Block x/z must be in 0..65535.");
            }

            if (y < 0 || y > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(y), "Block y must be in 0..255.");
            }

            if (blockType < 0 || blockType > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(blockType), "Block type must be in 0..255.");
            }
        }
    }
}
