using Godot;
using Godot.Hexasphere;

public partial class HexasphereProjectorController
{
    // At most 64 MiB per RGBA8/ID buffer, independent of topology size.
    private const long MaxRasterPixels = 16_777_216;
    private bool _rasterMode;
    private int[] _rasterTileIds = System.Array.Empty<int>();
    private ImageTexture _rasterIdTexture;
    private Vector2I _rasterResolution;

    private Vector2I ResolveRasterResolution()
    {
        var resolution = RasterQuality switch
        {
            UvRasterQuality.Custom => RasterResolution,
            UvRasterQuality.Low => new Vector2I(1024, 512),
            UvRasterQuality.Medium => new Vector2I(2048, 1024),
            UvRasterQuality.High => new Vector2I(4096, 2048),
            _ => throw new System.ArgumentOutOfRangeException(nameof(RasterQuality))
        };
        if (resolution.X <= 0 || resolution.Y <= 0 || resolution.X > 8192 || resolution.Y > 8192
            || (long)resolution.X * resolution.Y > MaxRasterPixels)
            throw new System.ArgumentOutOfRangeException(nameof(RasterResolution),
                "Raster dimensions must be 1..8192 with at most 16,777,216 pixels in total.");
        return resolution;
    }

    private void BuildRasterMap(NativeHexasphere hexasphere, Color[] colors, Vector2 mapSize, Vector2I resolution)
    {
        _rasterMode = true;
        if (_rasterIdTexture == null || _rasterResolution != resolution)
        {
            ClearRasterCache();
            _rasterResolution = resolution;
            _rasterTileIds = new int[resolution.X * resolution.Y];
            System.Array.Fill(_rasterTileIds, -1);
            BuildRasterTileIds(hexasphere);

            var bytes = new byte[_rasterTileIds.Length * 4];
            for (int i = 0; i < _rasterTileIds.Length; i++)
            {
                uint id = (uint)(_rasterTileIds[i] + 1);
                int offset = i * 4;
                bytes[offset] = (byte)id;
                bytes[offset + 1] = (byte)(id >> 8);
                bytes[offset + 2] = (byte)(id >> 16);
                bytes[offset + 3] = (byte)(id >> 24);
            }
            using var image = Image.CreateFromData(resolution.X, resolution.Y, false, Image.Format.Rgba8, bytes);
            _rasterIdTexture = ImageTexture.CreateFromImage(image);
        }

        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        AddRasterVertex(surface, Vector2.Zero, Vector2.Zero);
        AddRasterVertex(surface, new Vector2(mapSize.X, 0), new Vector2(1, 0));
        AddRasterVertex(surface, mapSize, Vector2.One);
        AddRasterVertex(surface, Vector2.Zero, Vector2.Zero);
        AddRasterVertex(surface, mapSize, Vector2.One);
        AddRasterVertex(surface, new Vector2(0, mapSize.Y), new Vector2(0, 1));
        ReplaceMapMesh(surface.Commit());

        // Drop the detailed-mode backing arrays as well as their contents.
        _hitTris = new();
        _spatialGrid = new();
        _cachedVertices = null;
        _cachedTriToTile = null;
        _cachedHitTris = null;
        _hasCachedGeometry = false;
        SetupTextureMaterial(colors);
    }

    private static void AddRasterVertex(SurfaceTool surface, Vector2 position, Vector2 uv)
    {
        surface.SetUV(uv);
        surface.SetColor(Colors.White);
        surface.AddVertex(new Vector3(position.X, position.Y, 0));
    }

    private void BuildRasterTileIds(NativeHexasphere hexasphere)
    {
        using var data = hexasphere.GetBuildData();
        var points = (Vector3[])data["points"];
        var counts = (int[])data["point_counts"];
        var centers = hexasphere.GetAllTileCenters();
        int pointOffset = 0;

        for (int tile = 0; tile < counts.Length; tile++)
        {
            int count = counts[tile];
            if (count >= 3)
            {
                Vector2 center = HexasphereUvProjector.CalculateUv(centers[tile]);
                var ring = new Vector2[count];
                float winding = 0;
                for (int i = 0; i < count; i++)
                {
                    ring[i] = HexasphereUvProjector.CalculateUv(points[pointOffset + i]);
                    float next = HexasphereUvProjector.CalculateUv(points[pointOffset + (i + 1) % count]).X;
                    float delta = next - ring[i].X;
                    winding += delta - Mathf.Round(delta);
                }

                if (Mathf.Abs(Mathf.Abs(winding) - 1f) < 0.1f)
                {
                    int[] order = new int[count];
                    for (int i = 0; i < count; i++) order[i] = i;
                    System.Array.Sort(order, (a, b) => ring[a].X.CompareTo(ring[b].X));
                    float poleV = centers[tile].Y > 0 ? 0 : 1;
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 a = ring[order[i]], b = ring[order[(i + 1) % count]];
                        if (i == count - 1) b.X += 1;
                        RasterizeTriangleWrapped(a, b, new Vector2(b.X, poleV), tile);
                        RasterizeTriangleWrapped(a, new Vector2(b.X, poleV), new Vector2(a.X, poleV), tile);
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 a = ring[i], b = ring[(i + 1) % count];
                        a.X += HexasphereUvProjector.GetSeamOffset(a.X, center.X);
                        b.X += HexasphereUvProjector.GetSeamOffset(b.X, center.X);
                        RasterizeTriangleWrapped(center, a, b, tile);
                    }
                }
            }
            pointOffset += count;
        }
    }

    private void RasterizeTriangleWrapped(Vector2 a, Vector2 b, Vector2 c, int tile)
    {
        for (int offset = -1; offset <= 1; offset++)
        {
            var shift = new Vector2(offset, 0);
            RasterizeTriangle(a + shift, b + shift, c + shift, tile);
        }
    }

    private void RasterizeTriangle(Vector2 a, Vector2 b, Vector2 c, int tile)
    {
        a *= (Vector2)_rasterResolution;
        b *= (Vector2)_rasterResolution;
        c *= (Vector2)_rasterResolution;
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.X, Mathf.Min(b.X, c.X))));
        int maxX = Mathf.Min(_rasterResolution.X - 1, Mathf.CeilToInt(Mathf.Max(a.X, Mathf.Max(b.X, c.X))));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.Y, Mathf.Min(b.Y, c.Y))));
        int maxY = Mathf.Min(_rasterResolution.Y - 1, Mathf.CeilToInt(Mathf.Max(a.Y, Mathf.Max(b.Y, c.Y))));
        float denominator = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        if (Mathf.Abs(denominator) < 0.0001f) return;

        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            float px = x + 0.5f, py = y + 0.5f;
            float u = ((b.Y - c.Y) * (px - c.X) + (c.X - b.X) * (py - c.Y)) / denominator;
            float v = ((c.Y - a.Y) * (px - c.X) + (a.X - c.X) * (py - c.Y)) / denominator;
            if (u >= 0 && v >= 0 && u + v <= 1)
                _rasterTileIds[y * _rasterResolution.X + x] = tile;
        }
    }

    private void WriteRasterColorsToImage(Color[] colors)
    {
        int size = _rasterTileIds.Length * 4;
        if (_colorBuffer == null || _colorBuffer.Length != size) _colorBuffer = new byte[size];
        for (int i = 0; i < _rasterTileIds.Length; i++)
        {
            int tile = _rasterTileIds[i];
            Color color = colors != null && tile >= 0 && tile < colors.Length ? colors[tile] : Colors.Black;
            int offset = i * 4;
            _colorBuffer[offset] = (byte)(Mathf.Clamp(color.R, 0, 1) * 255);
            _colorBuffer[offset + 1] = (byte)(Mathf.Clamp(color.G, 0, 1) * 255);
            _colorBuffer[offset + 2] = (byte)(Mathf.Clamp(color.B, 0, 1) * 255);
            _colorBuffer[offset + 3] = 255;
        }
        _tileColorImage.SetData(_rasterResolution.X, _rasterResolution.Y, false, Image.Format.Rgba8, _colorBuffer);
        _tileColorTexture.Update(_tileColorImage);
    }

    private void ClearRasterCache()
    {
        _rasterIdTexture?.Dispose();
        _rasterIdTexture = null;
        _rasterTileIds = System.Array.Empty<int>();
        _rasterResolution = Vector2I.Zero;
    }
}
