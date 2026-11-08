using Godot;
using Array = Godot.Collections.Array;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

// One producer fills at most two CPU surfaces. The scene thread owns each
// surface after reading it and closes the stream when its request goes away.
internal sealed class SurfaceStream : IDisposable
{
    private readonly Channel<Array> _channel = Channel.CreateBounded<Array>(2);
    private readonly Func<int, Array> _prepare;
    private Exception _error;
    private int _started;

    internal int Count { get; }

    internal SurfaceStream(int count, Func<int, Array> prepare)
    {
        Count = count;
        _prepare = prepare;
    }

    internal void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        _ = Task.Run(Produce);
    }

    private async Task Produce()
    {
        try
        {
            for (int index = 0; index < Count && await _channel.Writer.WaitToWriteAsync(); index++)
            {
                var surface = _prepare(index);
                if (!_channel.Writer.TryWrite(surface))
                {
                    surface.Dispose();
                    break;
                }
            }
        }
        catch (Exception error) { Volatile.Write(ref _error, error); }
        finally { _channel.Writer.TryComplete(); }
    }

    internal bool TryUpload(ArrayMesh mesh, Mesh.PrimitiveType primitive)
    {
        if (!_channel.Reader.TryRead(out var surface))
        {
            if (_channel.Reader.Completion.IsCompleted)
                throw new InvalidOperationException("Surface preparation stopped before the mesh was complete.", Volatile.Read(ref _error));
            return false;
        }
        using (surface)
        {
            int before = mesh.GetSurfaceCount();
            mesh.AddSurfaceFromArrays(primitive, surface);
            if (mesh.GetSurfaceCount() != before + 1)
                throw new InvalidOperationException("Godot rejected a prepared mesh surface.");
        }
        return true;
    }

    public void Dispose()
    {
        _channel.Writer.TryComplete();
        while (_channel.Reader.TryRead(out var surface)) surface.Dispose();
    }
}
