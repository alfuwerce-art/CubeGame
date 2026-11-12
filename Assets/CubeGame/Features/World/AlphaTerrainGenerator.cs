using System;
using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>Generated data of one terrain column (voxels).</summary>
    public readonly struct TerrainColumn
    {
        public TerrainColumn(int surface, bool rock, float mountain, float baseHeight)
        {
            Surface = surface;
            Rock = rock;
            Mountain = mountain;
            BaseHeight = baseHeight;
        }

        /// <summary>Top solid voxel Y (Alpha <c>surfaceY = int(height)</c>, Field +0x10).</summary>
        public int Surface { get; }

        /// <summary>Alpha material 6 (stone) when smoothstep(1.25·m·t2) &gt; 0.5.</summary>
        public bool Rock { get; }

        /// <summary>Mountain mask m (0x5EFFA0).</summary>
        public float Mountain { get; }

        /// <summary>Base height (0x5C5E20).</summary>
        public float BaseHeight { get; }
    }

    /// <summary>Region record (Alpha <c>generateRegion</c> 0x5DA280).</summary>
    public readonly struct RegionInfo
    {
        public RegionInfo(RegionKey key, int level, int variant, int orientation, AlphaClimateCell climate)
        {
            Key = key;
            Level = level;
            Variant = variant;
            Orientation = orientation;
            Climate = climate;
        }

        public RegionKey Key { get; }

        /// <summary>Region +0xC = <c>FUN_005A4710</c>: 1 in the centre, else 2 + trunc(0.75·distance) (DIRECT).</summary>
        public int Level { get; }

        /// <summary>Region +0x10: rand()%5 when level &gt; 4, else 0 (DIRECT).</summary>
        public int Variant { get; }

        /// <summary>Region +0x14: rand()&amp;3, or 4 + (rand()&amp;1) when climate Field4 &gt; 0.81 (DIRECT).</summary>
        public int Orientation { get; }

        public AlphaClimateCell Climate { get; }
    }

    public interface IWorldGenerator
    {
        string GeneratorId { get; }

        uint MasterSeed { get; }

        /// <summary>Water surface Y; columns whose surface is below it are under water.</summary>
        int WaterLevel { get; }

        TerrainColumn Column(int x, int z);

        RegionInfo Region(RegionKey region);

        /// <summary>Generated voxel column (Alpha zone Field) containing voxel (x, z); null outside the world.</summary>
        VoxelColumn Voxels(int x, int z);
    }

    /// <summary>
    /// Alpha terrain in voxel coordinates, including region landmarks.
    ///
    /// <b>Verified against executed Cube.exe code</b> (EMULATED_ALPHA_CODE fixtures, FullM0AlphaOracleTests):
    /// climate cells 0x5D7A70; region landmark columns of generateRegion 0x5DA280 (AlphaRegionGenerator);
    /// base height 0x5C5E20 and mountain mask 0x5EFFA0 with their helpers (0x5F9340, 0x5EEA60, 0x5CADE0,
    /// 0x5F9F70, 0x59FC90, 0x5EF5A0, 0x5989D0, 0x5EF040, 0x5F8E10) — with and without landmarks.
    /// <b>RECONSTRUCTED</b> (inline in 0x5E4850, constants from the decompilation, inputs verified): ridges t1/t2,
    /// the Canyon/Peak dome, height = (n+1.5)·60·m + m'·t2·8 + base, rock flag.
    /// <b>Not ported</b> (PARTIAL): zone grid / village roads / houses (M0-07), Peak (0xD) distance warp (Peak is
    /// not produced by generateRegion), caves, rivers/roads in terrain, water level (0 = hypothesis), colours.
    /// </summary>
    public sealed class AlphaTerrainGenerator : IWorldGenerator
    {
        public const string Id = "cubegame.alpha_terrain/4";
        public const string ContentId = "cubegame.alpha_terrain/5";
        public const string WorldRuntimeId = "cubegame.alpha_terrain/6";

        public const int DefaultWaterLevel = 0;

        private readonly AlphaWorldSeeds _seeds;
        private readonly AlphaClimateGrid _climate;
        private readonly Dictionary<long, AlphaRegion> _regions = new Dictionary<long, AlphaRegion>();
        private readonly object _regionLock = new object();
        private static long _nextCacheOwner;
        private readonly long _cacheOwner = System.Threading.Interlocked.Increment(ref _nextCacheOwner);

        /// <summary>Generated zones kept in memory (bounded; a zone is regenerated identically when evicted).</summary>
        public const int ZoneCacheSize = 24;

        private readonly Dictionary<long, Lazy<AlphaZone>> _zones = new Dictionary<long, Lazy<AlphaZone>>();
        private readonly LinkedList<long> _zoneOrder = new LinkedList<long>();
        private readonly object _zoneLock = new object();
        private bool _streamed;

        public AlphaTerrainGenerator(uint masterSeed, IReadOnlyList<IZoneContentProvider> contentProviders = null)
            : this(masterSeed, contentProviders, Id)
        {
        }

        internal AlphaTerrainGenerator(uint masterSeed, IReadOnlyList<IZoneContentProvider> contentProviders, string generatorId)
        {
            MasterSeed = masterSeed;
            GeneratorId = generatorId;
            _seeds = new AlphaWorldSeeds(masterSeed);
            _climate = new AlphaClimateGrid(_seeds, WorldLayout.StartVoxel, WorldLayout.StartVoxel);
            ContentProviders = contentProviders ?? Array.Empty<IZoneContentProvider>();
        }

        /// <summary>Content stage of every zone (<see cref="IZoneContentProvider"/>), in order; empty = terrain only.</summary>
        public IReadOnlyList<IZoneContentProvider> ContentProviders { get; }

        public string GeneratorId { get; }

        public uint MasterSeed { get; }

        public int WaterLevel => DefaultWaterLevel;

        public AlphaWorldSeeds Seeds => _seeds;

        public AlphaClimateGrid Climate => _climate;

        public RegionInfo Region(RegionKey region)
        {
            if (!WorldLayout.IsInside(region))
            {
                return new RegionInfo(region, 0, 0, 0, null);
            }

            AlphaRegion record = RegionRecord(region.X, region.Z);
            return new RegionInfo(region, record.Level, record.Variant, record.Orientation, _climate.Get(region.X, region.Z));
        }

        /// <summary>Lazily generated (thread-safe) region record with its 64 landmark columns.</summary>
        public AlphaRegion RegionRecord(int rx, int rz)
        {
            if (rx < 0 || rz < 0 || rx >= WorldLayout.RegionGrid || rz >= WorldLayout.RegionGrid)
            {
                return null;
            }

            long key = ((long)rx << 20) | (uint)rz;
            lock (_regionLock)
            {
                if (!_regions.TryGetValue(key, out AlphaRegion region))
                {
                    region = AlphaRegionGenerator.Generate(this, rx, rz);
                    _regions.Add(key, region);
                }

                return region;
            }
        }

        /// <summary>GameController_getVoxelColumn8 (0x487DA0): landmark column containing voxel (x, z).</summary>
        public LandmarkColumn ColumnAt(int x, int z)
        {
            if (x < 0 || z < 0 || x >= WorldLayout.WorldVoxels || z >= WorldLayout.WorldVoxels)
            {
                return null;
            }

            int rx = x >> 14;
            int rz = z >> 14;
            AlphaRegion region = _lastRegion;
            if (region == null || region.Rx != rx || region.Rz != rz || _lastOwner != _cacheOwner)
            {
                region = RegionRecord(rx, rz);
                _lastRegion = region;
                _lastOwner = _cacheOwner;
            }

            return region?.Column((x >> 11) & 7, (z >> 11) & 7);
        }

        // Per-thread cache of the last region looked up (worker threads sample neighbouring voxels in bursts).
        [ThreadStatic]
        private static AlphaRegion _lastRegion;

        [ThreadStatic]
        private static long _lastOwner;

        /// <summary>Zone (256×256 voxels) generated by <see cref="AlphaZoneGenerator"/>, cached and thread-safe.</summary>
        public AlphaZone Zone(int zx, int zz)
        {
            long key = ((long)zx << 32) | (uint)zz;
            Lazy<AlphaZone> entry;
            lock (_zoneLock)
            {
                if (!_zones.TryGetValue(key, out entry))
                {
                    entry = new Lazy<AlphaZone>(() => AlphaZoneGenerator.Generate(this, zx, zz), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
                    _zones.Add(key, entry);
                    _zoneOrder.AddLast(key);
                    while (!_streamed && _zoneOrder.Count > ZoneCacheSize)
                    {
                        long old = _zoneOrder.First.Value;
                        _zoneOrder.RemoveFirst();
                        _zones.Remove(old);
                    }
                }
            }

            return entry.Value;
        }

        public bool IsZoneGenerated(ZoneKey zone)
        {
            long key = ((long)zone.X << 32) | (uint)zone.Z;
            lock (_zoneLock)
            {
                return _zones.TryGetValue(key, out Lazy<AlphaZone> entry) && entry.IsValueCreated;
            }
        }

        public void UseStreamingRetention()
        {
            lock (_zoneLock)
            {
                _streamed = true;
            }
        }

        // 0x46A8A0: the generated zone, region, and climate records have separate lifetimes.
        public void EvictOutsideStreamingRange(ZoneKey center, WorldDeltaStore deltas)
        {
            lock (_zoneLock)
            {
                for (LinkedListNode<long> node = _zoneOrder.First; node != null;)
                {
                    LinkedListNode<long> next = node.Next;
                    long key = node.Value;
                    var zone = new ZoneKey((int)(key >> 32), (int)key);
                    if (_zones[key].IsValueCreated && !WorldStreamingPolicy.KeepGeneratedZone(zone, center))
                    {
                        deltas.MarkDiscovered(zone);
                        _zones.Remove(key);
                        _zoneOrder.Remove(node);
                    }
                    node = next;
                }
            }

            var region = new RegionKey(center.X >> 6, center.Z >> 6);
            lock (_regionLock)
            {
                foreach (long key in new List<long>(_regions.Keys))
                {
                    if (Math.Abs((int)(key >> 20) - region.X) >= 3 || Math.Abs((int)(key & 0xFFFFF) - region.Z) >= 3)
                    {
                        _regions.Remove(key);
                    }
                }
            }
            _climate.EvictOutside(region);
        }

        public VoxelColumn Voxels(int x, int z)
        {
            if (!WorldLayout.IsInsideVoxels(x, z))
            {
                return null;
            }

            return Zone(x >> 8, z >> 8).Column(x, z);
        }

        /// <summary>0x5C4800: temperature (weighted climate field 3, raised inside cold Forest landmarks).</summary>
        public float Temperature(int x, int z)
        {
            float t = ClimateAverage(x, z, useField4: false);
            LandmarkColumn column = ColumnAt(x, z);
            if (t < 0.2f && column != null && column.Type == LandmarkType.Forest)
            {
                t = Math.Min(Falloff(Distance2(column, x, z)) * 0.3f + t, 1.0f);
            }

            return t;
        }

        /// <summary>0x5C4DD0: humidity (weighted climate field 4).</summary>
        public float Humidity(int x, int z) => ClimateAverage(x, z, useField4: true);

        /// <summary>vec3_distanceSquared (0x5F8E10) for callers outside the generator.</summary>
        public float LandmarkDistance2(LandmarkColumn column, int x, int z) => Distance2(column, x, z);

        /// <summary>Alpha <c>FUN_005A4710</c>.</summary>
        public static int Level(int rx, int rz)
        {
            if (rx == 0x200 && rz == 0x200)
            {
                return 1;
            }

            float dz = 0x200 - rz;
            float dx = 0x200 - rx;
            double d = Math.Sqrt(dz * dz + dx * dx);
            return 2 - (int)((float)d * -0.75f);
        }

        public TerrainColumn Column(int x, int z)
        {
            double dx = x;
            double dz = z;
            float baseHeight = Base(x, z);
            float m = Mountain(x, z);

            float a = AlphaNoise.Sample(dx * 0.04 + 432.0, dz * 0.04 + 432.0);
            float b = AlphaNoise.Sample(dx * 0.08 + 432.0, dz * 0.08 + 432.0);
            float r1 = 1.0f - Math.Abs(a + b * 0.05f);
            r1 = 1.0f - r1 * r1 * r1;
            float t1 = r1 * r1;

            double c = AlphaNoise.Sample(dx * 0.08 + 4234.0, dz * 0.08 + 234.0) * 0.05;
            float d = AlphaNoise.Sample(dx * 0.05 + 423432.0, dz * 0.05 + 54352.0);
            float r2 = (float)(1.0 - Math.Abs(c + d));
            r2 = 1.0f - r2 * r2 * r2;
            float t2 = r2 * r2 + t1;

            // Dome of Canyon (6) / Peak (13) columns on the mountain weight of the ridge term (0x5E4850).
            float domed = m;
            LandmarkColumn column = ColumnAt(x, z);
            if (column != null && (column.Type == LandmarkType.Canyon || column.Type == LandmarkType.Peak))
            {
                float f = Falloff(Distance2(column, x, z)) * 2.0f;
                if (f > 1.0f)
                {
                    f = 1.0f;
                }

                f = 1.0f - f * f;
                float k = 1.0f - f * f;
                float s = AlphaNoise.Sample(dx * 0.01 + 985.0, dz * 0.01 + 98584.0) * 1.3f + 1.0f;
                s = s < 0f ? 0f : (s > 1f ? 1f : s);
                domed = Smooth(s) * k * 0.4f + m;
            }

            float mt = domed * t2;
            float rockWeight = Smooth(Clamp01(mt * 1.25f));
            float n = AlphaNoise.Sample(dx * 0.01 + 34432.0, dz * 0.01 + 8992.0);
            float height = (n + 1.5f) * 60.0f * m + mt * 8.0f + baseHeight;
            return new TerrainColumn((int)height, rockWeight > 0.5f, m, baseHeight);
        }

        public float Base(int x, int z) => BaseCore(x, z, true);

        /// <summary>Base without landmark columns (used while generating regions, and by the landmark-free oracle).</summary>
        public float BaseIsolated(int x, int z) => BaseCore(x, z, false);

        public float Mountain(int x, int z) => MountainCore(x, z, true);

        public float MountainIsolated(int x, int z) => MountainCore(x, z, false);

        // ------------------------------------------------------------ 0x5C5E20 base height
        private float BaseCore(int x, int z, bool landmarks)
        {
            double fx = x;
            double fz = z;
            float l14c = SquaredHalf(0x80018C, 0x800190, fx, fz, 0.0001);
            float l148 = SquaredHalf(0x800194, 0x800198, fx, fz, 0.0001);
            float l160 = SquaredHalf(0x80019C, 0x8001A0, fx, fz, 0.001);
            float l158 = SquaredHalf(0x8001A4, 0x8001A8, fx, fz, 0.001);
            float l140 = SquaredHalf(0x8001AC, 0x8001B0, fx, fz, 0.002);

            float s = Math.Min(SampleTerrainHeight(x, z, landmarks) * 4.0f, 1.0f);
            float l150 = Smooth(s);
            l150 = l150 * l150;
            float le4 = l150 * l160;
            float l174 = l150 * l158;
            l150 = l150 * l140;

            float n1 = AlphaNoise.Sample(fz * 0.0005, 3423.0);
            float n2 = AlphaNoise.Sample(fx * 0.0005, 23421.0);
            double px = x + (int)((n1 * 3.0f * 256.0f) * 65536.0f) / 65536.0;
            double pz = z + (int)((n2 * 3.0f * 256.0f) * 65536.0f) / 65536.0;
            int rx0 = (x - 0x4000) >> 14;
            int rx1 = (x + 0x4000) >> 14;
            int rz0 = (z - 0x4000) >> 14;
            int rz1 = (z + 0x4000) >> 14;

            bool found = false;
            float dmin = 0f;
            for (int rx = rx0; rx <= rx1; rx++)
            {
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    AlphaClimateCell cell = _climate.Get(rx, rz);
                    if (cell == null)
                    {
                        continue;
                    }

                    float d2 = AnchorDistance2(cell, px, pz);
                    if (!found || d2 < dmin)
                    {
                        found = true;
                        dmin = d2;
                    }
                }
            }

            float weighted = 0f;
            float land = 1f;
            if (found)
            {
                float wsum = 0f;
                float wbase = 0f;
                float wland = 0f;
                for (int rx = rx0; rx <= rx1; rx++)
                {
                    for (int rz = rz0; rz <= rz1; rz++)
                    {
                        AlphaClimateCell cell = _climate.Get(rx, rz);
                        if (cell == null)
                        {
                            continue;
                        }

                        float t = Math.Min((AnchorDistance2(cell, px, pz) - dmin) * 5e-08f, 1.0f);
                        float w = (1.0f - t) * (1.0f - t);
                        wsum += w;
                        wbase += cell.BaseHeight * w;
                        if (cell.BaseHeight > 0)
                        {
                            wland += w;
                        }
                    }
                }

                weighted = wbase / wsum;
                land = wland / wsum;
            }

            float a = AlphaNoise.Sample(_seeds.At(0x8001BC) + fx * 0.0002, _seeds.At(0x8001C0) + fz * 0.0002);
            float l154 = (a + 1.0f) * 100.0f * l148;
            float b = AlphaNoise.Sample(_seeds.At(0x8001B4) + fx * 0.0002, _seeds.At(0x8001B8) + fz * 0.0002);
            l154 = (l154 + (b + 1.0f) * 100.0f * l14c) * land + weighted;

            float r = HeightAtXY(x, z, landmarks);
            if (r > 0.5f)
            {
                float f = Math.Min((r - 0.5f) * 2.0f, 1.0f);
                l150 = (1.0f - Smooth(f)) * l150;
            }

            float q = Math.Min(Math.Max(RotateAndPlace(x, z, landmarks), 0.02f) * 2.0f, 1.0f);
            float g = 1.0f - q;
            float f4 = 1.0f - g * g * g * g;
            float k = f4 * 0.1f + 0.9f;
            l150 = (f4 * 0.5f + 0.5f) * l150;
            l148 = k * le4;
            l14c = k * l174;

            LandmarkColumn column = landmarks ? ColumnAt(x, z) : null;
            if (column != null && column.Type == LandmarkType.Village)
            {
                float f = 1.0f - Falloff(Distance2(column, x, z)) * 0.5f;
                l148 *= f;
                l14c *= f;
                l150 *= f;
            }

            float c1 = AlphaNoise.Sample(_seeds.At(0x8001CC) + fx * 0.002, _seeds.At(0x8001D0) + fz * 0.002);
            float result = (c1 + 1.0f) * 50.0f * l14c;
            float c2 = AlphaNoise.Sample(_seeds.At(0x8001C4) + fx * 0.002, _seeds.At(0x8001C8) + fz * 0.002);
            result += (c2 + 1.0f) * 50.0f * l148;
            float c3 = AlphaNoise.Sample(_seeds.At(0x8001D4) + fx * 0.01, _seeds.At(0x8001D8) + fz * 0.01);
            result = result + (c3 + 1.0f) * 20.0f * l150 + l154;

            if (column != null && column.Type != LandmarkType.None)
            {
                AlphaClimateCell near = NearestCell((int)column.X, (int)column.Z);
                if (near != null && near.BaseHeight < 0 && column.Type != LandmarkType.Rock)
                {
                    // Landmark over a water cell: raise an island (0x5C5E20, nearest-cell branch).
                    float na = AlphaNoise.Sample(x * 0.0025 + 8432984.0, z * 0.0025 + 90493.0);
                    float ddx = (float)((double)(na * 100.0f) + x - column.X);
                    float nb = AlphaNoise.Sample(x * 0.0025 + 3423.0, z * 0.0025 + 112.0);
                    // Alpha adds z in float here ((float)n·100 + (float)z, 1-voxel steps at 8.4M), x in exact 16.16.
                    // Explicit (float) forces the rounding even if the runtime keeps float temporaries in double.
                    float ddz = (float)((double)(float)(nb * 100.0f + (float)z) - column.Z);
                    float rr = column.Radius + 256.0f;
                    float f = Math.Min((ddz * ddz + ddx * ddx) / (rr * rr), 1.0f);
                    if (1.0f - f > 0f)
                    {
                        float h = Math.Min((1.0f - f) * 1.1f, 1.0f);
                        result -= Smooth(h) * near.BaseHeight;
                    }
                }

                if (column.Type == LandmarkType.Lake)
                {
                    float d2 = Distance2(column, x, z);
                    if (d2 > 0.25f)
                    {
                        if (d2 < 1.0f)
                        {
                            float f = ((float)Math.Sqrt(d2) - 0.5f) * 2.0f;
                            f = 1.0f - f * f;
                            result = ((column.Base - 25.0f) - result) * f * f + result;
                        }
                    }
                    else
                    {
                        float f = 1.0f - d2 * 4.0f;
                        f *= f;
                        result = (1.0f - f) * (column.Base - 25.0f) + (column.Base - 50.0f) * f;
                    }
                }

                if (column.Type == LandmarkType.Canyon || column.Type == LandmarkType.Valley)
                {
                    float d2 = Distance2(column, x, z);
                    if (d2 > 0.25f)
                    {
                        if (d2 < 1.0f)
                        {
                            float f = ((float)Math.Sqrt(d2) - 0.5f) * 2.0f;
                            f = 1.0f - f * f;
                            result = f * f * 10.0f + result;
                        }
                    }
                    else
                    {
                        float f = 1.0f - d2 * 4.0f;
                        f *= f;
                        result = (1.0f - f) * (result + 10.0f) + (result - 30.0f) * f;
                    }

                    if (result < weighted)
                    {
                        result = weighted;
                    }
                }
            }

            return result;
        }

        // ------------------------------------------------------------ 0x5EFFA0 mountain mask
        private float MountainCore(int x, int z, bool landmarks)
        {
            double nx = x * 0.01;
            double nz = z * 0.01;
            double ox = _seeds.At(0x8001DC);
            double oz = _seeds.At(0x8001E0);

            float n0 = AlphaNoise.Sample(nx, nz);
            float k = 1.0f - n0 * n0;
            float n1 = AlphaNoise.Sample(ox + nx + 843.0, oz + nz + 984.0) * 0.1f;
            float n2 = AlphaNoise.Sample(x * 0.0025 + ox, z * 0.0025 + oz);
            float v = 1.0f - Math.Abs(n1 + n2) * ((1.0f - k * k) * 1.3f + 2.0f);
            float n3 = AlphaNoise.Sample(x * 0.005 + 94.0, z * 0.005 + 874.0);
            v *= n3 * 0.4f + 0.6f;

            LandmarkColumn column = landmarks ? ColumnAt(x, z) : null;
            if (column != null && (column.Type == LandmarkType.Canyon || column.Type == LandmarkType.Valley))
            {
                float d2 = Distance2(column, x, z);
                float rim = v < 1.0f ? (v + 0.5f > 1.0f ? 1.0f - v : 0.5f) : 0.0f;
                if (d2 > 0.36f)
                {
                    if (d2 < 1.0f)
                    {
                        float f = ((float)Math.Sqrt(d2) - 0.6f) / 0.39999998f;
                        f = 1.0f - f * f;
                        v = f * f * rim + v;
                    }
                }
                else
                {
                    float f = Math.Min((1.0f - d2 / 0.36f) * 1.5f, 1.0f);
                    v = (rim + v) * (1.0f - f * f);
                }
            }

            if (v >= 0f)
            {
                float s = Math.Min(SampleTerrainHeight(x, z, landmarks) * 2.0f, 1.0f);
                v = Smooth(s) * v;
                float q = Math.Min(RotateAndPlace(x, z, landmarks), 1.0f);
                v = Smooth(q) * v;
                float r = 1.0f - Smooth(Math.Min(HeightAtXY(x, z, landmarks) * 2.0f, 1.0f));
                v = r * r * r * v;
            }
            else
            {
                v = 0f;
            }

            if (column != null)
            {
                if (column.Type == LandmarkType.Mountain)
                {
                    return Smooth(v) + Falloff(Distance2(column, x, z));
                }

                if (column.Type == LandmarkType.Unnamed10 && column.Radius > 0f)
                {
                    float d2 = Distance2(column, x, z, warped: false);
                    if (d2 < 1.0f)
                    {
                        float f = 1.0f - d2;
                        v = (1.0f - f * f) * v;
                    }
                }

                if (column.Type == LandmarkType.Lake || column.Type == LandmarkType.Ruins)
                {
                    float d2 = Distance2(column, x, z);
                    if (d2 > 0.25f)
                    {
                        if (d2 < 1.0f)
                        {
                            float f = ((float)Math.Sqrt(d2) - 0.5f) * 2.0f;
                            f = 1.0f - f * f;
                            v = (1.0f - f * f) * v;
                        }
                    }
                    else
                    {
                        v = 0f;
                    }
                }
            }

            return Smooth(v);
        }

        // ------------------------------------------------------------ helpers
        /// <summary>0x5F9340: terrain sampler used for placement weights.</summary>
        public float SampleTerrainHeight(int x, int z, bool landmarks = true)
        {
            float a = AlphaNoise.Sample(_seeds.At(0x800170) + x * 0.01, _seeds.At(0x800174) + z * 0.01);
            float b = AlphaNoise.Sample(_seeds.At(0x800168) + x * 0.001, _seeds.At(0x80016C) + z * 0.001);
            float v = a * 0.1f + b;
            float c = AlphaNoise.Sample(x * 0.001, z * 0.001);
            v = Math.Abs(v) * ((c + 1.0f) * 0.1f + 0.8f);
            float near = NearestEdge(x, z);
            float k = 1.0f - near * 0.75f;
            if (k > 0f)
            {
                v = k * k * 0.05f + v;
            }

            LandmarkColumn column = landmarks ? ColumnAt(x, z) : null;
            if (column != null)
            {
                LandmarkType t = column.Type;
                if (t == LandmarkType.Village || t == LandmarkType.Mountain || t == LandmarkType.Lake || t == LandmarkType.Peak)
                {
                    v = Falloff(Distance2(column, x, z)) + v;
                }

                if (t == LandmarkType.Canyon || t == LandmarkType.Valley)
                {
                    v = Falloff(Distance2(column, x, z)) * 0.5f + v;
                }
            }

            return RareCellTerm(x, z) + v;
        }

        /// <summary>0x5989D0: village weight (1 − d²)² inside a Village disk, else 0.</summary>
        public float HeightAtXY(int x, int z, bool landmarks = true)
        {
            LandmarkColumn column = landmarks ? ColumnAt(x, z) : null;
            return column != null && column.Type == LandmarkType.Village ? Falloff(Distance2(column, x, z)) : 0f;
        }

        /// <summary>0x5F9F70: nearest-edge measure with village street grid and mountain/lake boost.</summary>
        public float RotateAndPlace(int x, int z, bool landmarks = true)
        {
            float r = HeightAtXY(x, z, landmarks);
            WarpedPoint(x, z, out double hx, out double hz);
            float near = NearestEdge(x, z);
            if (r > 0f)
            {
                float f = Math.Min(r * 3.0f, 1.0f);
                f = 1.0f - f * f;
                float w = 1.0f - f * f;
                float c = (float)(Math.Cos(hx / 16384.0 * 360.0) * w + 1.0);
                if (c < near)
                {
                    near = c;
                }

                c = (float)(Math.Cos(hz / 16384.0 * 360.0) * w + 1.0);
                if (c < near)
                {
                    near = c;
                }
            }

            if (r > 0.65f)
            {
                float f = (0.7f - r) / 0.05f;
                near = (f > 0f ? f : 0f) * near;
            }

            LandmarkColumn column = landmarks ? ColumnAt(x, z) : null;
            if (column != null && (column.Type == LandmarkType.Mountain || column.Type == LandmarkType.Lake))
            {
                near = Falloff(Distance2(column, x, z)) * 2.0f + near;
            }

            return near;
        }

        /// <summary>0x5EF040: weight share of "rare" (flagged) climate cells around the warped point.</summary>
        public float RareCellTerm(int x, int z)
        {
            int rx0 = (x - 0x4000) >> 14;
            int rx1 = (x + 0x4000) >> 14;
            int rz0 = (z - 0x4000) >> 14;
            int rz1 = (z + 0x4000) >> 14;
            bool anyFlag = false;
            for (int rx = rx0; rx <= rx1; rx++)
            {
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    AlphaClimateCell cell = _climate.Get(rx, rz);
                    if (cell == null)
                    {
                        return 0f;
                    }

                    anyFlag |= cell.Flag;
                }
            }

            if (!anyFlag)
            {
                return 0f;
            }

            WarpedCellPoint(x, z, out int px, out int pz);
            int best = int.MaxValue;
            for (int rx = rx0; rx <= rx1; rx++)
            {
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    best = Math.Min(best, IntDistance(_climate.Get(rx, rz), px, pz));
                }
            }

            float total = 0f;
            float flagged = 0f;
            for (int rx = rx0; rx <= rx1; rx++)
            {
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    AlphaClimateCell cell = _climate.Get(rx, rz);
                    float w = 1.0f - Math.Min(1.0f, (IntDistance(cell, px, pz) - best) * 5e-07f);
                    total += w;
                    if (cell.Flag)
                    {
                        flagged += w;
                    }
                }
            }

            return total > 0f ? flagged / total : 0f;
        }

        /// <summary>0x5C4800 (field 3) / 0x5C4DD0 (field 4) without the Forest bonus: weighted climate field.</summary>
        public float ClimateAverage(int x, int z, bool useField4)
        {
            WarpedCellPoint(x, z, out int px, out int pz);
            int rx0 = (x - 0x4000) >> 14;
            int rx1 = (x + 0x4000) >> 14;
            int rz0 = (z - 0x4000) >> 14;
            int rz1 = (z + 0x4000) >> 14;
            int best = int.MaxValue;
            for (int rx = rx0; rx <= rx1; rx++)
            {
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    AlphaClimateCell cell = _climate.Get(rx, rz);
                    if (cell == null)
                    {
                        return 0f; // Alpha bails out at the world edge
                    }

                    best = Math.Min(best, IntDistance(cell, px, pz));
                }
            }

            float tw = 0f;
            float tv = 0f;
            for (int rx = rx0; rx <= rx1; rx++)
            {
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    AlphaClimateCell cell = _climate.Get(rx, rz);
                    float w = 1.0f - Math.Min(1.0f, (IntDistance(cell, px, pz) - best) * 5e-07f);
                    tw += w;
                    tv += (useField4 ? cell.Field4 : cell.Field3) * w;
                }
            }

            return tv / tw;
        }

        /// <summary>
        /// 0x5EEA60: squared distance (·0.0002) from the warped point (0x59FC90) to the edges between the nearest
        /// climate anchor (0x5CADE0) and the anchors of its four neighbour regions (0x5EF5A0).
        /// </summary>
        public float NearestEdge(int x, int z)
        {
            if (!NearestCellRegion(x, z, out int rx, out int rz))
            {
                return 0f;
            }

            AlphaClimateCell center = _climate.Get(rx, rz);
            double px = center.AnchorX;
            double pz = center.AnchorZ;
            WarpedPoint(x, z, out double bx, out double bz);
            float best = -1f;
            Consider(_climate.Get(rx - 1, rz), px, pz, bx, bz, ref best);
            Consider(_climate.Get(rx + 1, rz), px, pz, bx, bz, ref best);
            Consider(_climate.Get(rx, rz - 1), px, pz, bx, bz, ref best);
            Consider(_climate.Get(rx, rz + 1), px, pz, bx, bz, ref best);
            return best < 0f ? 0f : best * 0.0002f;
        }

        /// <summary>0x5CADE0 / 0x477E10: region whose climate anchor is nearest to the warped point (first wins).</summary>
        public bool NearestCellRegion(int x, int z, out int bestRx, out int bestRz)
        {
            WarpedCellPoint(x, z, out int wx, out int wz);
            bestRx = bestRz = -1;
            bool found = false;
            int bestDistance = 0;
            for (int rx = (x - 0x4000) >> 14; rx <= (x + 0x4000) >> 14; rx++)
            {
                for (int rz = (z - 0x4000) >> 14; rz <= (z + 0x4000) >> 14; rz++)
                {
                    AlphaClimateCell cell = _climate.Get(rx, rz);
                    if (cell == null)
                    {
                        continue;
                    }

                    int distance = IntDistance(cell, wx, wz);
                    if (!found || distance < bestDistance)
                    {
                        found = true;
                        bestDistance = distance;
                        bestRx = rx;
                        bestRz = rz;
                    }
                }
            }

            return found;
        }

        private AlphaClimateCell NearestCell(int x, int z)
        {
            return NearestCellRegion(x, z, out int rx, out int rz) ? _climate.Get(rx, rz) : null;
        }

        private static void WarpedCellPoint(int x, int z, out int wx, out int wz)
        {
            float n1 = AlphaNoise.Sample(z * 0.0005, 3423.0);
            float n2 = AlphaNoise.Sample(x * 0.0005, 23421.0);
            wx = (int)(float)((float)x + (float)(n1 * 3.0f * 256.0f));
            wz = (int)(float)((float)z + (float)(n2 * 3.0f * 256.0f));
        }

        private static int IntDistance(AlphaClimateCell cell, int wx, int wz)
        {
            float dx = cell.AnchorX - wx;
            float dz = cell.AnchorZ - wz;
            return (int)(dz * dz + dx * dx);
        }

        /// <summary>
        /// vec3_distanceSquared (0x5F8E10): squared distance to the landmark centre / radius². Except Rock, Tree and
        /// Zone14 the point is displaced by noise(x·0.0025+8432984, z·0.0025+90493)·256 and
        /// noise(x·0.0025+3423, z·0.0025+112)·256 (scale fitted to executed values), giving irregular outlines.
        /// </summary>
        private static float Distance2(LandmarkColumn column, int x, int z, bool warped = true)
        {
            float r = column.Radius;
            if (r < 0.001f)
            {
                return 0f;
            }

            float dx;
            float dz;
            LandmarkType t = column.Type;
            if (!warped || t == LandmarkType.Rock || t == LandmarkType.Tree || t == LandmarkType.Zone14)
            {
                dx = (float)(x - column.X);
                dz = (float)(z - column.Z);
            }
            else
            {
                float n1 = AlphaNoise.Sample(x * 0.0025 + 8432984.0, z * 0.0025 + 90493.0);
                float n2 = AlphaNoise.Sample(x * 0.0025 + 3423.0, z * 0.0025 + 112.0);
                // Alpha works in 16.16 fixed point here: keep double precision at ~8.4M voxels.
                dx = (float)((double)(n1 * 256.0f) + x - column.X);
                dz = (float)((double)(n2 * 256.0f) + z - column.Z);
            }

            return (dz * dz + dx * dx) / (r * r);
        }

        /// <summary>GameController_entityDistanceSq: (1 − d²)² inside, 0 outside.</summary>
        public static float Falloff(float d2)
        {
            float f = 1.0f - d2;
            return f > 0f ? f * f : 0f;
        }

        private static void Consider(AlphaClimateCell neighbour, double px, double pz, double bx, double bz, ref float best)
        {
            if (neighbour == null)
            {
                return;
            }

            float d = SegmentDistance2(px, pz, neighbour.AnchorX, neighbour.AnchorZ, bx, bz);
            if (best < 0f || d < best)
            {
                best = d;
            }
        }

        /// <summary>0x5EF5A0: squared distance from b to the segment p→a.</summary>
        private static float SegmentDistance2(double px, double pz, double ax, double az, double bx, double bz)
        {
            double dx3 = bx - px;
            double dx4 = ax - px;
            double dy5 = az - pz;
            double dy2 = bz - pz;
            double len = dx4 * dx4 + dy5 * dy5;
            if (len < 9.999999682655225e-21)
            {
                return (float)(dy2 * dy2 + dx3 * dx3);
            }

            double t = (dy2 * dy5 + dx3 * dx4) / len;
            if (t <= 0.0)
            {
                return (float)(dy2 * dy2 + dx3 * dx3);
            }

            if (t >= 1.0)
            {
                double ex = bx - ax;
                double ey = bz - az;
                return (float)(ey * ey + ex * ex);
            }

            dx3 -= dx4 * t;
            dy2 -= dy5 * t;
            return (float)(dy2 * dy2 + dx3 * dx3);
        }

        /// <summary>0x59FC90 in voxels: point displaced by up to ±500 voxels of noise.</summary>
        private void WarpedPoint(int x, int z, out double wx, out double wz)
        {
            double nx = x * 0.01;
            double nz = z * 0.01;
            double nx2 = x * 0.0005;
            double nz2 = z * 0.0005;
            float a = AlphaNoise.Sample(_seeds.At(0x800204) + nx, _seeds.At(0x800208) + nz);
            float b = AlphaNoise.Sample(_seeds.At(0x8001FC) + nx2, _seeds.At(0x800200) + nz2);
            double hx = ((double)a * 0.1 + b) * 500.0 * 6.103515625e-05 + x * 6.103515625e-05;
            a = AlphaNoise.Sample(_seeds.At(0x800214) + nx, _seeds.At(0x800218) + nz);
            b = AlphaNoise.Sample(_seeds.At(0x80020C) + nx2, _seeds.At(0x800210) + nz2);
            double hz = ((double)a * 0.1 + b) * 500.0 * 6.103515625e-05 + z * 6.103515625e-05;
            wx = hx * 16384.0;
            wz = hz * 16384.0;
        }

        private float SquaredHalf(int offsetA, int offsetB, double fx, double fz, double scale)
        {
            float v = AlphaNoise.Sample(_seeds.At(offsetA) + fx * scale, _seeds.At(offsetB) + fz * scale);
            float h = (v + 1.0f) * 0.5f;
            return h * h;
        }

        private static float AnchorDistance2(AlphaClimateCell cell, double px, double pz)
        {
            float dx = (float)(cell.AnchorX - px);
            float dz = (float)(cell.AnchorZ - pz);
            return dz * dz + dx * dx;
        }

        private static float Smooth(float f)
        {
            return f * 3.0f * f - f * 2.0f * f * f;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
