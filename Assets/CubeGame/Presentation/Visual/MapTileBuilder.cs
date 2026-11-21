// CubeGame.Presentation.Visual — map tiles from Alpha surface colours (D6, UI data).
//
// The colours are Alpha's own: they come from the ported world colour rules (AlphaColors.SurfaceColor,
// already used by terrain generation) and are only *sampled* here — nothing is redrawn by hand. What is a
// CubeGame contract is the tile size and the sampling grid, because Alpha's map widget built its own tiles
// inside the Plasma UI and that code is not ported.
using System;
using CubeGame.Content;
using CubeGame.Features.World;

namespace CubeGame.Presentation.Visual
{
    /// <summary>Renders one top-down map tile (RGBA32, row 0 = bottom, Unity texture order).</summary>
    public static class MapTileBuilder
    {
        public const string Provenance =
            "COLORS=ALPHA_DATA (ported surface colour rules); tile size/sampling=CUBEGAME_CONTRACT (Plasma map widget not ported)";

        /// <summary>Default tile edge in pixels (CubeGame choice).</summary>
        public const int DefaultTileSize = 64;

        /// <summary>Alpha reads an uncovered air column as water below y = 1; such a column is drawn as water.</summary>
        public const byte WaterAlpha = 255;

        public const int BytesPerPixel = 4;

        /// <summary>Colour of one map sample; <paramref name="solid"/> = false means "no ground here".</summary>
        public readonly struct Sample
        {
            public readonly byte R, G, B;
            public readonly bool Solid;

            public Sample(byte r, byte g, byte b, bool solid)
            {
                R = r;
                G = g;
                B = b;
                Solid = solid;
            }
        }

        /// <summary>
        /// Fills an RGBA32 buffer of <paramref name="size"/>² by sampling <paramref name="sample"/> on a
        /// <paramref name="size"/>×<paramref name="size"/> grid. Deterministic: the same sampler gives the same bytes.
        /// </summary>
        public static byte[] Render(int size, Func<int, int, Sample> sample)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));
            if (size <= 0 || size > 1024) throw new ArgumentOutOfRangeException(nameof(size));
            var buffer = new byte[size * size * BytesPerPixel];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Sample s = sample(x, y);
                int i = (y * size + x) * BytesPerPixel;
                buffer[i] = s.R;
                buffer[i + 1] = s.G;
                buffer[i + 2] = s.B;
                buffer[i + 3] = s.Solid ? (byte)255 : (byte)0;
            }

            return buffer;
        }

        /// <summary>Convenience sampler from a world column: surface voxel colour, transparent when nothing is there.</summary>
        public static Sample FromSurface(uint voxel, bool hasGround)
        {
            if (!hasGround) return new Sample(0, 0, 0, false);
            return new Sample((byte)Voxel.R(voxel), (byte)Voxel.G(voxel), (byte)Voxel.B(voxel), true);
        }

        /// <summary>Map colour of a column of the ported terrain (Alpha colour rules, no invented palette).</summary>
        public static Sample FromColumn(VoxelColumn column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));
            int top = column.TopSolid();
            if (top <= 0) return new Sample(0, 0, 0, false);
            return FromSurface(column.Read(top), true);
        }
    }
}
