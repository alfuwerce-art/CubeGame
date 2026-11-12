using System;
using System.Collections.Generic;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Alpha voxel (uint32): R, G, B in the low three bytes, flags byte: &amp; 0x1F material (0 air, 1 default solid,
    /// 2 water, 3 lake bed, 4 grass, 6 stone, 9 snow/sand, 10 shore, 12 rare ground), 0x20 surface, 0x40 covered
    /// cave air, 0x80 explicitly placed/changed.
    /// </summary>
    public static class Voxel
    {
        public const uint Air = 0u;

        /// <summary>DAT_0076DDF8: the air constant Alpha writes when clearing roads/river banks (255, 255, 255, 0).</summary>
        public static readonly uint ClearedAir = Pack(255, 255, 255, 0);

        /// <summary>DAT_0076DDF4 / DAT_0076B010: water (255, 255, 255, 2 | 0x80).</summary>
        public static readonly uint Water = Pack(255, 255, 255, 0x82);

        /// <summary>DAT_0076DDFC: solid ground below the stored voxels (200, 200, 200, material 1).</summary>
        public static readonly uint DefaultSolid = Pack(200, 200, 200, 1);

        public static uint Pack(int r, int g, int b, int flags)
        {
            return (uint)(r & 0xFF) | ((uint)(g & 0xFF) << 8) | ((uint)(b & 0xFF) << 16) | ((uint)(flags & 0xFF) << 24);
        }

        public static int R(uint v) => (int)(v & 0xFF);

        public static int G(uint v) => (int)((v >> 8) & 0xFF);

        public static int B(uint v) => (int)((v >> 16) & 0xFF);

        public static int Flags(uint v) => (int)(v >> 24);

        public static int Material(uint v) => (int)((v >> 24) & 0x1F);

        public static bool IsSolid(uint v)
        {
            int m = Material(v);
            return m != 0 && m != 2;
        }

        public static bool IsWater(uint v) => Material(v) == 2;
    }

    /// <summary>
    /// One voxel column of a zone (Alpha zone Field cell): voxels[0] is at Y = <see cref="Bottom"/>.
    /// Read rule: below Bottom → default solid; inside → stored (uncovered air below y = 1 reads as water);
    /// above → water up to y = 0, air above.
    /// </summary>
    public sealed class VoxelColumn
    {
        private uint[] _voxels = Array.Empty<uint>();

        public float Temperature;
        public float Humidity;
        public float Rare;

        public int Bottom { get; internal set; }

        public int Count { get; private set; }

        public IReadOnlyList<uint> Voxels => _voxels;

        /// <summary>Highest Y that is stored (Bottom + Count − 1).</summary>
        public int TopStored => Bottom + Count - 1;

        public uint Read(int y)
        {
            if (y < Bottom)
            {
                return Voxel.DefaultSolid;
            }

            int i = y - Bottom;
            if (i < Count)
            {
                uint v = _voxels[i];
                int flags = Voxel.Flags(v);
                if ((flags & 0x1F) == 0 && y < 1 && (flags & 0x40) == 0)
                {
                    return Voxel.Water;
                }

                return v;
            }

            return y <= 0 ? Voxel.Water : Voxel.Air;
        }

        /// <summary>Highest solid voxel Y (ground for placement); Bottom − 1 when nothing stored is solid.</summary>
        public int TopSolid()
        {
            for (int y = TopStored; y >= Bottom; y--)
            {
                if (Voxel.IsSolid(Read(y)))
                {
                    return y;
                }
            }

            return Bottom - 1;
        }

        internal void Reset(int bottom, uint v0, uint v1)
        {
            Bottom = bottom;
            _voxels = new uint[4];
            _voxels[0] = v0;
            _voxels[1] = v1;
            Count = 2;
        }

        /// <summary>sparseArray_setAt (0x450C50 family): grows down/up; explicit voxels keep their 0x80 mark.</summary>
        internal void SetAt(int index, uint v)
        {
            if (index < 0)
            {
                int grow = -index;
                var bigger = new uint[Count + grow + 4];
                Array.Copy(_voxels, 0, bigger, grow, Count);
                _voxels = bigger;
                Count += grow;
                Bottom -= grow;
                _voxels[0] = v;
                return;
            }

            if (index >= Count)
            {
                if (index >= _voxels.Length)
                {
                    Array.Resize(ref _voxels, Math.Max(index + 4, _voxels.Length * 2));
                }

                Count = index + 1;
            }

            uint cell = _voxels[index];
            if ((Voxel.Flags(cell) & 0x80) != 0)
            {
                if ((Voxel.Flags(v) & 0x1F) == 0)
                {
                    return;
                }

                _voxels[index] = v | 0x80000000u;
                return;
            }

            _voxels[index] = v;
        }

        /// <summary>Independent copy (edits go onto copies; generated columns are shared and never mutated).</summary>
        public VoxelColumn Clone()
        {
            var copy = (VoxelColumn)MemberwiseClone();
            copy._voxels = (uint[])_voxels.Clone();
            return copy;
        }

        /// <summary>Player/game edit of one voxel: replaces the cell unconditionally (grows the column as needed).</summary>
        public void Overwrite(int y, uint v)
        {
            int index = y - Bottom;
            if (index < 0)
            {
                int grow = -index;
                var bigger = new uint[Count + grow + 4];
                Array.Copy(_voxels, 0, bigger, grow, Count);
                for (int i = 1; i < grow; i++)
                {
                    bigger[i] = Voxel.DefaultSolid; // the cells between were implicit default solid
                }

                _voxels = bigger;
                Count += grow;
                Bottom -= grow;
                index = 0;
            }
            else if (index >= Count)
            {
                if (index >= _voxels.Length)
                {
                    Array.Resize(ref _voxels, Math.Max(index + 4, _voxels.Length * 2));
                }

                for (int i = Count; i < index; i++)
                {
                    _voxels[i] = Voxel.Air;
                }

                Count = index + 1;
            }

            _voxels[index] = v;
        }

        /// <summary>
        /// Height edit (<see cref="Contracts.WorldDeltaKind.ColumnSurfaceSet"/>): everything above <paramref name="surface"/>
        /// becomes air; non-solid cells at or below it are filled with the column's top material.
        /// </summary>
        public void SetSurface(int surface)
        {
            int top = TopSolid();
            uint fill = top >= Bottom ? Read(top) & 0x7FFFFFFFu : Voxel.DefaultSolid;
            for (int y = surface + 1; y <= TopStored; y++)
            {
                Overwrite(y, Voxel.Air);
            }

            for (int y = Math.Min(top + 1, surface); y <= surface; y++)
            {
                if (!Voxel.IsSolid(Read(y)))
                {
                    Overwrite(y, fill);
                }
            }

            while (Count > 0 && _voxels[Count - 1] == Voxel.Air && TopStored > surface)
            {
                Count--;
            }
        }
    }

    /// <summary>Content candidate recorded while generating a zone (consumed by content providers, DeepSeek #13).</summary>
    public readonly struct ZoneSpawnCandidate
    {
        public ZoneSpawnCandidate(string kind, int x, int y, int z, int variant)
        {
            Kind = kind;
            X = x;
            Y = y;
            Z = z;
            Variant = variant;
        }

        /// <summary>
        /// cave_object_0x130b / cave_creature / cave_creature_timed / cave_object (cave branch switch), river_object
        /// (rand() % 200 on a river bed), landmark_giant_tree (Tree zone centre, Y = top of the column).
        /// </summary>
        public string Kind { get; }

        public int X { get; }

        public int Y { get; }

        public int Z { get; }

        public int Variant { get; }
    }

    /// <summary>One generated Alpha zone: 256×256 voxel columns.</summary>
    public sealed class AlphaZone
    {
        public const int Size = WorldLayout.ZoneSize;
        private readonly Dictionary<Contracts.ChunkKey, List<AlphaPlantPlacement>> _plants =
            new Dictionary<Contracts.ChunkKey, List<AlphaPlantPlacement>>();

        public AlphaZone(int zx, int zz)
        {
            Zx = zx;
            Zz = zz;
            Columns = new VoxelColumn[Size * Size];
        }

        public int Zx { get; }

        public int Zz { get; }

        /// <summary>Index = lx + lz · 256 (Alpha Field layout).</summary>
        public VoxelColumn[] Columns { get; }

        /// <summary>Candidates recorded by the terrain passes (cave/river content, giant tree position).</summary>
        public List<ZoneSpawnCandidate> Spawns { get; } = new List<ZoneSpawnCandidate>();

        /// <summary>Spawns created by the content providers (<see cref="IZoneContentProvider"/>).</summary>
        public List<Contracts.SpawnDefinition> ContentSpawns { get; } = new List<Contracts.SpawnDefinition>();

        /// <summary>State of the zone CRT after the P2 pre-population passes.</summary>
        public int PopulationRandomState { get; internal set; }

        internal void AddPlant(AlphaPlantPlacement plant)
        {
            var key = WorldLayout.ChunkOf(plant.X, plant.Z);
            if (!_plants.TryGetValue(key, out var plants))
            {
                plants = new List<AlphaPlantPlacement>();
                _plants.Add(key, plants);
            }
            plants.Add(plant);
        }

        public IReadOnlyList<AlphaPlantPlacement> PlantsIn(Contracts.ChunkKey chunk) =>
            _plants.TryGetValue(chunk, out var plants) ? plants : (IReadOnlyList<AlphaPlantPlacement>)Array.Empty<AlphaPlantPlacement>();

        public VoxelColumn Column(int x, int z) => Columns[(x - Zx * Size) + (z - Zz * Size) * Size];
    }
}
