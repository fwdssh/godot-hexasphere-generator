using Godot;
using Godot.Hexasphere;
using System.Diagnostics;

public partial class BenchmarkRunner : Node
{
    [Export] public int Iterations = 1;
    [Export] public int WarmupIterations = 0;

    private readonly int[] _subDivisions = {  500};

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
        double genTotal = 0;
        double buildTotal = 0;
        int tiles = 0;

        // warmup
        for (int i = 0; i < WarmupIterations; i++)
        {
            var h = new Hexasphere(10f, divisions, 1f);
            var b = new HexasphereMeshBuilder();
            b.Build(h);
        }

        for (int i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var hexasphere = new Hexasphere(10f, divisions, 1f);
            sw.Stop();
            genTotal += sw.Elapsed.TotalMilliseconds;
            tiles = hexasphere.Tiles.Count;

            sw.Restart();
            var builder = new HexasphereMeshBuilder();
            var (_, _) = builder.Build(hexasphere);
            sw.Stop();
            buildTotal += sw.Elapsed.TotalMilliseconds;
        }

        double genAvg = genTotal / Iterations;
        double buildAvg = buildTotal / Iterations;

        GD.Print($"Divisions={divisions,3} | Tiles={tiles,6} | Generate={genAvg,6:F1}ms | BuildMesh={buildAvg,6:F1}ms");
    }
}
