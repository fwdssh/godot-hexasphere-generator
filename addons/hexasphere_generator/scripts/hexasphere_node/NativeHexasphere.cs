using Godot;
using Godot.Collections;
using System;

public class NativeHexasphere : IDisposable
{
    private GodotObject _native;
    private bool _disposed;

    public NativeHexasphere()
    {
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
        _native.Call("generate", radius, divisions, hexSize);
    }

    public int GetTileCount()
    {
        return (int)_native.Call("get_tile_count");
    }

    public Vector3 GetTileCenter(int tileIdx)
    {
        return (Vector3)_native.Call("get_tile_center", tileIdx);
    }

    public Vector3[] GetTilePoints(int tileIdx)
    {
        return (Vector3[])_native.Call("get_tile_points", tileIdx);
    }

    public int[] GetTileFaces(int tileIdx)
    {
        return (int[])_native.Call("get_tile_faces", tileIdx);
    }

    public Dictionary GetBuildData()
    {
        return (Dictionary)_native.Call("get_build_data");
    }

    public Dictionary GetBorderData()
    {
        return (Dictionary)_native.Call("get_border_data");
    }

    public Dictionary BuildMesh()
    {
        return (Dictionary)_native.Call("build_mesh");
    }
}
