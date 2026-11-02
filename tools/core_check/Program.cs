// CubeGame — standalone Core check.
//
// Compiles the Unity-independent Core sources (Assets/CubeGame/Core) with Roslyn and verifies the
// determinism acceptance cases TC-GEN-01..TC-GEN-05 from
// docs/research/alpha2013/GENERATION_MOVEMENT_SPEC.md without starting the Unity Editor.
//
// It is NOT a replacement for the Unity EditMode/PlayMode run: it checks Core math and geometry
// only (no UnityEngine, no rendering, no physics). Its purpose is to prove the deterministic
// core works even when the Unity Editor cannot be started (see S01_PILOT_REPORT.md section 3).
//
// Build+run: powershell -ExecutionPolicy Bypass -File tools\core_check\run_core_check.ps1

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CubeGame.Core.Generation;
using CubeGame.Core.Rendering;

internal static class CoreCheckProgram
{
    private const uint SeedA = 666666u;
    private const uint SeedB = 666667u;
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Console.WriteLine("CubeGame Core check (no Unity Editor required)");
        Console.WriteLine($"seed A = {SeedA}, seed B = {SeedB}");
        Console.WriteLine(new string('-', 78));

        SameSeedProducesIdenticalHeights();
        DifferentSeedChangesHeights();
        AdjacentChunksShareEdgeHeights();
        HeightsStayInsideConfiguredRange();
        NegativeCoordinatesAreSupported();
        SameSeedProducesIdenticalMesh();
        DifferentSeedProducesDifferentMesh();
        IndicesStayInsideVertexBuffer();
        MeshAreaMatchesChunkSize();
        MeasureEightByEightChunks();

        Console.WriteLine(new string('-', 78));
        Console.WriteLine($"PASS {_passed}  FAIL {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    private static void SameSeedProducesIdenticalHeights()
    {
        var first = new PilotHeightField(SeedA);
        var second = new PilotHeightField(SeedA);
        bool ok = true;
        for (int z = -64; z <= 96 && ok; z++)
        {
            for (int x = -64; x <= 96 && ok; x++)
            {
                if (first.HeightAt(x, z) != second.HeightAt(x, z))
                {
                    ok = false;
                }
            }
        }

        Report("TC-GEN-01 same seed produces identical heights", ok);
    }

    private static void DifferentSeedChangesHeights()
    {
        var first = new PilotHeightField(SeedA);
        var second = new PilotHeightField(SeedB);
        int total = 0;
        int different = 0;
        for (int z = 0; z < 256; z += 4)
        {
            for (int x = 0; x < 256; x += 4)
            {
                total++;
                if (first.HeightAt(x, z) != second.HeightAt(x, z))
                {
                    different++;
                }
            }
        }

        double ratio = different / (double)total;
        Report($"TC-GEN-02 different seed changes heights ({ratio:P1} of columns differ, need >= 90.0%)", ratio >= 0.90);
    }

    private static void AdjacentChunksShareEdgeHeights()
    {
        var field = new PilotHeightField(SeedA);
        int size = PilotWorldLayout.ChunkSize;
        bool heightsEqual = true;
        for (int z = 0; z <= size; z++)
        {
            if (field.HeightAt(size, z) != new PilotHeightField(SeedA).HeightAt(size, z))
            {
                heightsEqual = false;
            }
        }

        MeshData left = ChunkMeshBuilder.Build(field, 0, 0);
        MeshData right = ChunkMeshBuilder.Build(field, 1, 0);
        float leftMaxX = MaxCoordinate(left, 0);
        float rightMinX = MinCoordinate(right, 0);
        bool planesMeet = Math.Abs(leftMaxX - rightMinX) < 0.0001f;

        Report("TC-GEN-03 adjacent chunks share the edge plane", heightsEqual && planesMeet);
    }

    private static void HeightsStayInsideConfiguredRange()
    {
        var field = new PilotHeightField(SeedA);
        bool ok = true;
        int min = int.MaxValue;
        int max = int.MinValue;
        for (int z = -512; z <= 512; z += 16)
        {
            for (int x = -512; x <= 512; x += 16)
            {
                int height = field.HeightAt(x, z);
                min = Math.Min(min, height);
                max = Math.Max(max, height);
                if (height < PilotWorldLayout.MinHeight || height > PilotWorldLayout.MaxHeight)
                {
                    ok = false;
                }
            }
        }

        Console.WriteLine($"    observed height range: {min}..{max} (configured {PilotWorldLayout.MinHeight}..{PilotWorldLayout.MaxHeight})");
        Report("TC-GEN-05a heights stay inside the configured range", ok);
    }

    private static void NegativeCoordinatesAreSupported()
    {
        var field = new PilotHeightField(SeedA);
        bool ok = true;
        for (int i = -8; i <= 8; i++)
        {
            int height = field.HeightAt(i * 37 - 149, i * -53 + 211);
            if (height < PilotWorldLayout.MinHeight || height > PilotWorldLayout.MaxHeight)
            {
                ok = false;
            }
        }

        Report("negative block coordinates are supported", ok);
    }

    private static void SameSeedProducesIdenticalMesh()
    {
        var field = new PilotHeightField(SeedA);
        MeshData first = ChunkMeshBuilder.Build(field, 0, 0);
        MeshData second = ChunkMeshBuilder.Build(field, 0, 0);
        bool ok = first.VertexCount > 0
                  && first.VertexCount == second.VertexCount
                  && first.IndexCount == second.IndexCount
                  && first.ComputeFingerprint() == second.ComputeFingerprint();
        Console.WriteLine($"    chunk(0,0): vertices={first.VertexCount} indices={first.IndexCount} fingerprint={first.ComputeFingerprint():X16}");
        Report("same seed produces an identical chunk mesh", ok);
    }

    private static void DifferentSeedProducesDifferentMesh()
    {
        MeshData first = ChunkMeshBuilder.Build(new PilotHeightField(SeedA), 0, 0);
        MeshData second = ChunkMeshBuilder.Build(new PilotHeightField(SeedB), 0, 0);
        Report("different seed produces a different chunk mesh", first.ComputeFingerprint() != second.ComputeFingerprint());
    }

    private static void IndicesStayInsideVertexBuffer()
    {
        MeshData mesh = ChunkMeshBuilder.Build(new PilotHeightField(SeedA), 1, -1);
        bool ok = mesh.IndexCount > 0 && mesh.IndexCount % 3 == 0;
        if (ok)
        {
            for (int i = 0; i < mesh.IndexCount; i++)
            {
                int index = mesh.Indices[i];
                if (index < 0 || index >= mesh.VertexCount)
                {
                    ok = false;
                    break;
                }
            }
        }

        Report("all indices stay inside the vertex buffer", ok);
    }

    private static void MeshAreaMatchesChunkSize()
    {
        MeshData mesh = ChunkMeshBuilder.Build(new PilotHeightField(SeedA), -2, 3);
        int size = PilotWorldLayout.ChunkSize;
        float minX = MinCoordinate(mesh, 0), maxX = MaxCoordinate(mesh, 0);
        float minZ = MinCoordinate(mesh, 2), maxZ = MaxCoordinate(mesh, 2);
        bool ok = Math.Abs(minX - (-2 * size)) < 0.0001f
                  && Math.Abs(maxX - (-1 * size)) < 0.0001f
                  && Math.Abs(minZ - (3 * size)) < 0.0001f
                  && Math.Abs(maxZ - (4 * size)) < 0.0001f;
        Report($"chunk (-2,3) covers exactly {size}x{size} blocks", ok);
    }

    private static void MeasureEightByEightChunks()
    {
        var field = new PilotHeightField(SeedA);
        var stopwatch = Stopwatch.StartNew();
        long vertices = 0;
        for (int chunkZ = 0; chunkZ < PilotWorldLayout.ViewChunks; chunkZ++)
        {
            for (int chunkX = 0; chunkX < PilotWorldLayout.ViewChunks; chunkX++)
            {
                vertices += ChunkMeshBuilder.Build(field, chunkX, chunkZ).VertexCount;
            }
        }

        stopwatch.Stop();
        double perChunk = stopwatch.Elapsed.TotalMilliseconds / (PilotWorldLayout.ViewChunks * PilotWorldLayout.ViewChunks);
        Console.WriteLine($"    TC-GEN-05b {PilotWorldLayout.ViewChunks}x{PilotWorldLayout.ViewChunks} chunks: " +
                          $"{stopwatch.Elapsed.TotalMilliseconds:F1} ms total, {perChunk:F2} ms/chunk, {vertices} vertices");
        Report("8x8 chunk area is generated within 2 s", stopwatch.Elapsed.TotalSeconds < 2.0);
    }

    private static float MinCoordinate(MeshData mesh, int axis)
    {
        float value = float.MaxValue;
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            value = Math.Min(value, mesh.Vertices[i * 3 + axis]);
        }

        return value;
    }

    private static float MaxCoordinate(MeshData mesh, int axis)
    {
        float value = float.MinValue;
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            value = Math.Max(value, mesh.Vertices[i * 3 + axis]);
        }

        return value;
    }

    private static void Report(string name, bool ok)
    {
        if (ok)
        {
            _passed++;
            Console.WriteLine($"[PASS] {name}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"[FAIL] {name}");
        }
    }
}
