using Godot;
using Godot.Collections;
using System;

public class NativeHexasphere : IDisposable
{
    private GodotObject _native;
    private bool _disposed;

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

    public void Generate(float radius, int divisions, float hexSize)
    {
        _native.Call(MethodGenerate, radius, divisions, hexSize);
    }

    public int GetTileCount()
    {
        return (int)_native.Call(MethodGetTileCount);
    }

    public Vector3 GetTileCenter(int tileIdx)
    {
        return (Vector3)_native.Call(MethodGetTileCenter, tileIdx);
    }

    public Vector3[] GetTilePoints(int tileIdx)
    {
        return (Vector3[])_native.Call(MethodGetTilePoints, tileIdx);
    }

    public int[] GetTileFaces(int tileIdx)
    {
        return (int[])_native.Call(MethodGetTileFaces, tileIdx);
    }

    public Dictionary GetBuildData()
    {
        return (Dictionary)_native.Call(MethodGetBuildData);
    }

    public Dictionary GetBorderData()
    {
        return (Dictionary)_native.Call(MethodGetBorderData);
    }

    public Dictionary BuildMesh()
    {
        return (Dictionary)_native.Call(MethodBuildMesh);
    }

    public Vector3[] GetAllTileCenters()
    {
        return (Vector3[])_native.Call(MethodGetAllTileCenters);
    }
}
