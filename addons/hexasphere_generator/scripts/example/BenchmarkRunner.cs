using Godot;
using Godot.Hexasphere;
using System.Diagnostics;

public partial class BenchmarkRunner : Node
{
    [Export] public int Iterations = 5;
    [Export] public int WarmupIterations = 2;

    private readonly int[] _subDivisions = { 200 };

    public override void _Ready()
    {
        GD.Print("--- Hexasphere Benchmark ---");
        GD.Print($"Iterations per config: {Iterations} (+ {WarmupIterations} warmup)\n");

        foreach (int div in _subDivisions)
        {
            RunBenchmark(div);
        }

        GD.Print("\n--- Benchmark complete ---");
    }

    private void RunBenchmark(int divisions)
    {
        // ---------- C++ ----------
        double cppGenTotal = 0;
        double cppMeshTotal = 0;
        int tiles = 0;

        for (int i = 0; i < WarmupIterations; i++)
        {
            var h = new NativeHexasphere();
            h.Generate(10f, divisions, 1f);
            h.BuildMesh();
        }

        for (int i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var h = new NativeHexasphere();
            h.Generate(10f, divisions, 1f);
            sw.Stop();
            cppGenTotal += sw.Elapsed.TotalMilliseconds;
            tiles = h.GetTileCount();

            sw.Restart();
            h.BuildMesh();
            sw.Stop();
            cppMeshTotal += sw.Elapsed.TotalMilliseconds;
        }

        double cppGenAvg = cppGenTotal / Iterations;
        double cppMeshAvg = cppMeshTotal / Iterations;
        long memCpp = (long)(OS.GetStaticMemoryUsage() / 1048576);

        // ---------- C# ----------
        double csGenTotal = 0;
        double csMeshTotal = 0;

        for (int i = 0; i < WarmupIterations; i++)
        {
            var cs = new Hexasphere(10f, divisions, 1f);
            new HexasphereMeshBuilder().Build(cs);
        }

        for (int i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var cs = new Hexasphere(10f, divisions, 1f);
            sw.Stop();
            csGenTotal += sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            new HexasphereMeshBuilder().Build(cs);
            sw.Stop();
            csMeshTotal += sw.Elapsed.TotalMilliseconds;
        }

        double csGenAvg = csGenTotal / Iterations;
        double csMeshAvg = csMeshTotal / Iterations;

        long memAll = (long)(OS.GetStaticMemoryUsage() / 1048576);

        GD.Print($"Div={divisions,3} | Tiles={tiles,6} | C++ Gen={cppGenAvg,6:F1}ms C# Gen={csGenAvg,7:F1}ms | C++ Mesh={cppMeshAvg,8:F1}ms C# Mesh={csMeshAvg,8:F1}ms | C++ All={cppGenAvg+cppMeshAvg,7:F1}ms C# All={csGenAvg+csMeshAvg,7:F1}ms | Mem C++={memCpp,4}MB Mem All={memAll,4}MB");
    }
}
