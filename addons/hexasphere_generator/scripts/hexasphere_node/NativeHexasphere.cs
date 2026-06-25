using Godot;
using Godot.Collections;

public class NativeHexasphere
{
    private GodotObject _native;

    public NativeHexasphere()
    {
        _native = ClassDB.Instantiate("NativeHexasphere").AsGodotObject();
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
