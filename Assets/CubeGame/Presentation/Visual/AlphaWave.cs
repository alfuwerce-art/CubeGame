using System;
using System.Buffers.Binary;
using System.IO;
using UnityEngine;

namespace CubeGame.Presentation.Visual
{
    /// <summary>Decodes the two sample encodings shipped in the Alpha RIFF catalog.</summary>
    public static class AlphaWave
    {
        public static AudioClip Decode(byte[] bytes, string name)
        {
            ReadOnlySpan<byte> wave = bytes;
            if (wave.Length < 12 || Id(wave, 0) != 0x46464952 || Id(wave, 8) != 0x45564157
                || Id(wave, 4) != wave.Length - 8)
            {
                throw new InvalidDataException("Invalid RIFF/WAVE header or size.");
            }

            ReadOnlySpan<byte> format = default;
            ReadOnlySpan<byte> data = default;
            bool foundData = false;
            for (int offset = 12; offset < wave.Length;)
            {
                if (wave.Length - offset < 8)
                {
                    throw new InvalidDataException("Truncated RIFF chunk header.");
                }

                uint kind = Id(wave, offset);
                uint length = Id(wave, offset + 4);
                int start = offset + 8;
                long end = start + (long)length + (length & 1);
                if (end > wave.Length)
                {
                    throw new InvalidDataException("Truncated RIFF chunk.");
                }

                if (kind == 0x20746D66) // fmt
                {
                    if (!format.IsEmpty || length < 16)
                    {
                        throw new InvalidDataException("Invalid WAVE format chunk.");
                    }
                    format = wave.Slice(start, (int)length);
                }
                else if (kind == 0x61746164) // data
                {
                    if (foundData)
                    {
                        throw new InvalidDataException("Multiple WAVE data chunks.");
                    }
                    data = wave.Slice(start, (int)length);
                    foundData = true;
                }
                offset = (int)end;
            }

            if (format.IsEmpty || data.IsEmpty)
            {
                throw new InvalidDataException("WAVE format or samples are missing.");
            }

            int encoding = BinaryPrimitives.ReadUInt16LittleEndian(format);
            int channels = BinaryPrimitives.ReadUInt16LittleEndian(format.Slice(2));
            uint rate = Id(format, 4);
            int alignment = BinaryPrimitives.ReadUInt16LittleEndian(format.Slice(12));
            int bits = BinaryPrimitives.ReadUInt16LittleEndian(format.Slice(14));
            bool pcm16 = encoding == 1 && bits == 16;
            bool float32 = encoding == 3 && bits == 32;
            int sampleBytes = bits / 8;
            if ((!pcm16 && !float32) || channels < 1 || channels > 2 || rate == 0 || rate > int.MaxValue
                || alignment != channels * sampleBytes || Id(format, 8) != (long)rate * alignment
                || data.Length % alignment != 0)
            {
                throw new InvalidDataException("Unsupported WAVE encoding or invalid frame layout.");
            }

            var samples = new float[data.Length / sampleBytes];
            for (int i = 0; i < samples.Length; i++)
            {
                ReadOnlySpan<byte> sample = data.Slice(i * sampleBytes);
                float value = pcm16 ? BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768f
                    : BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(sample));
                samples[i] = float.IsNaN(value) ? 0f : Mathf.Clamp(value, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, samples.Length / channels, channels, (int)rate, false);
            if (!clip.SetData(samples, 0))
            {
                UnityEngine.Object.Destroy(clip);
                throw new InvalidDataException("Unity rejected the decoded samples.");
            }
            return clip;
        }

        private static uint Id(ReadOnlySpan<byte> data, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset));
    }
}
