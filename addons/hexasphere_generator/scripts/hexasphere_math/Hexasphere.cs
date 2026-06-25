using System.Collections.Generic;
using Godot;

namespace Godot.Hexasphere
{
    public class Hexasphere 
    {
        private readonly float _radius;
        private readonly int _divisions;
        private readonly float _hexSize;
        private readonly MeshDetails _meshDetails;

        private readonly List<Tile> _tiles;
        private readonly List<Point> _points;
        private readonly List<Face> _icosahedronFaces;
        private readonly Dictionary<Vector3I, Point> _pointGrid;
        private const float GridScale = 5000f;

        public Hexasphere(float radius, int divisions, float hexSize)
        {
            _radius    = radius;
            _divisions = divisions;
            _hexSize   = hexSize;

            int estimatedPoints = 10 * divisions * divisions + 2;
            _points   = new List<Point>(estimatedPoints);
            _pointGrid = new Dictionary<Vector3I, Point>(estimatedPoints);
            _tiles    = new List<Tile>(estimatedPoints);

            _icosahedronFaces = ConstructIcosahedron();
            SubdivideIcosahedron();
            ConstructTiles();
            _meshDetails = StoreMeshDetails();
        }

        public List<Tile> Tiles => _tiles;
        public MeshDetails MeshDetails => _meshDetails;

        private List<Face> ConstructIcosahedron()
        {
            const float tao = Mathf.Pi / 2;
            const float s = 100f;
            float ts = tao * s;

            var corners = new Point[12]
            {
                new Point(new Vector3( s,  ts, 0)),
                new Point(new Vector3(-s,  ts, 0)),
                new Point(new Vector3( s, -ts, 0)),
                new Point(new Vector3(-s, -ts, 0)),
                new Point(new Vector3(0,   s,  ts)),
                new Point(new Vector3(0,  -s,  ts)),
                new Point(new Vector3(0,   s, -ts)),
                new Point(new Vector3(0,  -s, -ts)),
                new Point(new Vector3( ts, 0,   s)),
                new Point(new Vector3(-ts, 0,   s)),
                new Point(new Vector3( ts, 0,  -s)),
                new Point(new Vector3(-ts, 0,  -s))
            };
            foreach (var p in corners) CachePoint(p);

            var c = corners;
            return new List<Face>(20)
            {
                new Face(c[0],  c[1],  c[4],  false),
                new Face(c[1],  c[9],  c[4],  false),
                new Face(c[4],  c[9],  c[5],  false),
                new Face(c[5],  c[9],  c[3],  false),
                new Face(c[2],  c[3],  c[7],  false),
                new Face(c[3],  c[2],  c[5],  false),
                new Face(c[7],  c[10], c[2],  false),
                new Face(c[0],  c[8],  c[10], false),
                new Face(c[0],  c[4],  c[8],  false),
                new Face(c[8],  c[2],  c[10], false),
                new Face(c[8],  c[4],  c[5],  false),
                new Face(c[8],  c[5],  c[2],  false),
                new Face(c[1],  c[0],  c[6],  false),
                new Face(c[3],  c[9],  c[11], false),
                new Face(c[6],  c[10], c[7],  false),
                new Face(c[3],  c[11], c[7],  false),
                new Face(c[11], c[6],  c[7],  false),
                new Face(c[6],  c[0],  c[10], false),
                new Face(c[11], c[1],  c[6],  false),
                new Face(c[9],  c[1],  c[11], false)
            };
        }

        private Point CachePoint(Point point)
        {
            Vector3I gridPos = new Vector3I(
                Mathf.RoundToInt(point.Position.X * GridScale),
                Mathf.RoundToInt(point.Position.Y * GridScale),
                Mathf.RoundToInt(point.Position.Z * GridScale)
            );

            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (_pointGrid.TryGetValue(gridPos + new Vector3I(x, y, z), out Point existing))
                    if (Point.IsOverlapping(existing, point))
                        return existing;
            }

            _points.Add(point);
            _pointGrid[gridPos] = point;
            return point;
        }

        private void SubdivideIcosahedron()
        {
            foreach (var icoFace in _icosahedronFaces)
            {
                var fp = icoFace.Points;
                List<Point> previousRow;
                List<Point> bottomRow = new List<Point> { fp[0] };
                List<Point> leftSide  = fp[0].Subdivide(fp[1], _divisions, CachePoint);
                List<Point> rightSide = fp[0].Subdivide(fp[2], _divisions, CachePoint);

                for (int i = 1; i <= _divisions; i++)
                {
                    previousRow = bottomRow;
                    bottomRow = leftSide[i].Subdivide(rightSide[i], i, CachePoint);

                    new Face(previousRow[0], bottomRow[0], bottomRow[1]);
                    for (int j = 1; j < i; j++)
                    {
                        new Face(previousRow[j],     bottomRow[j],     bottomRow[j + 1]);
                        new Face(previousRow[j - 1], previousRow[j],   bottomRow[j]);
                    }
                }
            }
        }

        private void ConstructTiles()
        {
            foreach (var point in _points)
                _tiles.Add(new Tile(point, _radius, _hexSize));

            var tileMap = new Dictionary<int, Tile>(_tiles.Count);
            foreach (var tile in _tiles)
                tileMap[tile.Center.ID] = tile;

            foreach (var tile in _tiles)
                tile.ResolveNeighbourTilesFast(tileMap);
        }

        private MeshDetails StoreMeshDetails()
        {
            int tileCount = _tiles.Count;
            var vertices  = new List<Vector3>(tileCount * 6);
            var triangles = new List<int>(tileCount * 12);
            var vertexIndexMap = new Dictionary<int, int>(tileCount * 6);
            int idx = 0;

            foreach (var tile in _tiles)
            {
                foreach (var pt in tile.Points)
                {
                    vertices.Add(pt.Position);
                    vertexIndexMap[pt.ID] = idx++;
                }
                foreach (var face in tile.Faces)
                {
                    var pts = face.Points;
                    triangles.Add(vertexIndexMap[pts[0].ID]);
                    triangles.Add(vertexIndexMap[pts[1].ID]);
                    triangles.Add(vertexIndexMap[pts[2].ID]);
                }
            }

            return new MeshDetails(vertices, triangles);
        }
    }
}
