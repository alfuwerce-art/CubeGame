using System;

namespace CubeGame.Core.Generation
{
    /// <summary>
    /// Integer-only deterministic hash. Same inputs always give the same output on every
    /// platform and every run: no float math, no System.Random, no time, no memory addresses.
    /// </summary>
    public static class DeterministicHash
    {
        private const uint Prime1 = 0x9E3779B1u;
        private const uint Prime2 = 0x85EBCA77u;
        private const uint Prime3 = 0xC2B2AE3Du;

        public static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= Prime1;
            value ^= value >> 13;
            value *= Prime2;
            value ^= value >> 16;
            return value;
        }

        /// <summary>Hash of a lattice point (x, z) for a given world seed.</summary>
        public static uint Lattice(uint seed, int x, int z)
        {
            unchecked
            {
                uint h = seed * Prime3;
                h = Mix(h ^ (uint)x * Prime1);
                h = Mix(h ^ (uint)z * Prime2);
                return h;
            }
        }

        /// <summary>Hash of an arbitrary set of integer components.</summary>
        public static uint Combine(params int[] values)
        {
            unchecked
            {
                uint h = Prime3;
                for (int i = 0; i < values.Length; i++)
                {
                    h = Mix(h ^ ((uint)values[i] * Prime1));
                }

                return h;
            }
        }
    }
}
