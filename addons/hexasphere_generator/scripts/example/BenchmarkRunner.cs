using Godot;
using System.Diagnostics;

public partial class BenchmarkRunner : Node
{
    [Export] public int Iterations = 5;
    [Export] public int WarmupIterations = 2;

    private readonly int[] _subDivisions = { 5, 10, 20, 30, 50, 75, 100 };

    public override void _Ready()
    {
        GD.Print("--- Hexasphere Benchmark (C++ native) ---");
        GD.Print($"Iterations per config: {Iterations} (+ {WarmupIterations} warmup)\n");

        foreach (int div in _subDivisions)
        {
            RunBenchmark(div);
        }

        GD.Print("\n--- Benchmark complete ---");
    }

    private void RunBenchmark(int divisions)
    {
        double genTotal = 0;
        double buildTotal = 0;
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
            var hexasphere = new NativeHexasphere();
            hexasphere.Generate(10f, divisions, 1f);
            sw.Stop();
            genTotal += sw.Elapsed.TotalMilliseconds;
            tiles = hexasphere.GetTileCount();

            sw.Restart();
            hexasphere.BuildMesh();
            sw.Stop();
            buildTotal += sw.Elapsed.TotalMilliseconds;
        }

        double genAvg = genTotal / Iterations;
        double buildAvg = buildTotal / Iterations;

        GD.Print($"Divisions={divisions,3} | Tiles={tiles,6} | Generate={genAvg,6:F1}ms | BuildMesh={buildAvg,6:F1}ms");
    }
}
