using Godot;
using Godot.Collections;
using System;
using System.Threading;

/// <summary>
/// Wrapper around the native GDExtension hexasphere implementation.
/// Provides thread-safe access to mesh generation and tile geometry data.
/// </summary>
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
    private static readonly StringName MethodGetAllTileNeighbors = "get_all_tile_neighbors";

    /// <summary>
    /// Instantiates the native GDExtension hexasphere object.
    /// Throws if the NativeHexasphere GDExtension is not available.
    /// </summary>
    public NativeHexasphere()
    {
        if (!ClassDB.ClassExists("NativeHexasphere"))
            throw new InvalidOperationException(
                "NativeHexasphere GDExtension not found. Check that .gdextension is loaded and built for the current platform.");

        _native = ClassDB.Instantiate("NativeHexasphere").AsGodotObject();
    }

    /// <summary>
    /// Releases the native GDExtension resources. Safe to call multiple times.
    /// </summary>
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
    public virtual void Generate(float radius, int divisions, float hexSize)
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

    /// <summary>Returns the total number of tiles on the generated sphere.</summary>
    public virtual int GetTileCount()
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

    /// <summary>Gets the center position of a specific tile in local space.</summary>
    /// <param name="tileIdx">The tile index.</param>
    public virtual Vector3 GetTileCenter(int tileIdx)
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

    /// <summary>Gets the vertex positions of a specific tile in local space.</summary>
    /// <param name="tileIdx">The tile index.</param>
    public virtual Vector3[] GetTilePoints(int tileIdx)
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

    /// <summary>Gets the face vertex indices of a specific tile.</summary>
    /// <param name="tileIdx">The tile index.</param>
    public virtual int[] GetTileFaces(int tileIdx)
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

    /// <summary>Returns the mesh build data dictionary from the native implementation.</summary>
    public virtual Dictionary GetBuildData()
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

    /// <summary>Returns the border data dictionary (positions and tile line counts) from the native implementation.</summary>
    public virtual Dictionary GetBorderData()
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

    /// <summary>Builds and returns the final ArrayMesh from the generated data.</summary>
    public virtual Dictionary BuildMesh()
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

    /// <summary>Returns the center positions of all tiles in a single array.</summary>
    public virtual Vector3[] GetAllTileCenters()
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

    /// <summary>
    /// Returns all tile neighbor relationships in CSR format.
    /// Dictionary keys: "neighbor_indices" (int[]), "offsets" (int[]).
    /// </summary>
    public virtual Dictionary GetAllTileNeighbors()
    {
        _semaphore.Wait();
        try
        {
            return (Dictionary)_native.Call(MethodGetAllTileNeighbors);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Unpacks CSR-format neighbor data into a jagged array int[][] where
    /// result[t] contains the neighbor indices for tile t.
    /// </summary>
    public static int[][] BuildNeighborLists(Dictionary data)
    {
        int[] neighborIndices = (int[])data["neighbor_indices"];
        int[] offsets = (int[])data["offsets"];
        int tileCount = offsets.Length - 1;
        int[][] result = new int[tileCount][];
        for (int t = 0; t < tileCount; t++)
        {
            int start = offsets[t];
            int end = offsets[t + 1];
            int count = end - start;
            int[] neighbors = new int[count];
            for (int i = 0; i < count; i++)
                neighbors[i] = neighborIndices[start + i];
            result[t] = neighbors;
        }
        return result;
    }
}
