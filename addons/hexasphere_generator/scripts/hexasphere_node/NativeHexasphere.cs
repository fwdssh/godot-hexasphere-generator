using Godot;
using Godot.Collections;
using System;
using System.Threading;

public class NativeHexasphere : IDisposable
{
    private GodotObject _native;
    private bool _disposed;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private static readonly StringName MethodGenerate = "generate";
    private static readonly StringName MethodGetTileCount = "get_tile_count";
    private static readonly StringName MethodGetTileCenter = "get_tile_center";
    private static readonly StringName MethodGetTilePoints = "get_tile_points";
    private static readonly StringName MethodGetTileFaces = "get_tile_faces";
    private static readonly StringName MethodGetBuildData = "get_build_data";
    private static readonly StringName MethodGetBorderData = "get_border_data";
    private static readonly StringName MethodBuildMesh = "build_mesh";
    private static readonly StringName MethodGetAllTileCenters = "get_all_tile_centers";

    public NativeHexasphere()
    {
        if (!ClassDB.ClassExists("NativeHexasphere"))
            throw new InvalidOperationException(
                "NativeHexasphere GDExtension not found. Check that .gdextension is loaded and built for the current platform.");

        _native = ClassDB.Instantiate("NativeHexasphere").AsGodotObject();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _native?.Dispose();
        _native = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Generate the hexasphere mesh data. Thread-safe via internal semaphore.
    /// Each instance is independently safe to call from background threads.
    /// Note: reentrancy across different NativeHexasphere instances depends on the
    /// GDExtension implementation — ensure the C++ side does not use shared static state.
    /// </summary>
    public void Generate(float radius, int divisions, float hexSize)
    {
        _semaphore.Wait();
        try
        {
            _native.Call(MethodGenerate, radius, divisions, hexSize);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public int GetTileCount()
    {
        _semaphore.Wait();
        try
        {
            return (int)_native.Call(MethodGetTileCount);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Vector3 GetTileCenter(int tileIdx)
    {
        _semaphore.Wait();
        try
        {
            return (Vector3)_native.Call(MethodGetTileCenter, tileIdx);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Vector3[] GetTilePoints(int tileIdx)
    {
        _semaphore.Wait();
        try
        {
            return (Vector3[])_native.Call(MethodGetTilePoints, tileIdx);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public int[] GetTileFaces(int tileIdx)
    {
        _semaphore.Wait();
        try
        {
            return (int[])_native.Call(MethodGetTileFaces, tileIdx);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Dictionary GetBuildData()
    {
        _semaphore.Wait();
        try
        {
            return (Dictionary)_native.Call(MethodGetBuildData);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Dictionary GetBorderData()
    {
        _semaphore.Wait();
        try
        {
            return (Dictionary)_native.Call(MethodGetBorderData);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Dictionary BuildMesh()
    {
        _semaphore.Wait();
        try
        {
            return (Dictionary)_native.Call(MethodBuildMesh);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Vector3[] GetAllTileCenters()
    {
        _semaphore.Wait();
        try
        {
            return (Vector3[])_native.Call(MethodGetAllTileCenters);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
