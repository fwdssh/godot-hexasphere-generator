using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Godot.Hexasphere
{
    public class Tile 
    {
        private readonly Point _center;
        private readonly float _radius;
        private readonly float _size;

        private readonly List<Face> _faces;
        private readonly List<Point> _points;
        private readonly List<Point> _neighbourCenters;
        private List<Tile> _neighbours;

        public Tile(Point center, float radius, float size)
        {
            _center = center;
            _radius = radius;
            _size = Mathf.Clamp(size, 0.01f, 1f);

            List<Face> icosahedronFaces = center.GetOrderedFaces();
            int faceCount = icosahedronFaces.Count; // 5 или 6

            _neighbourCenters = new List<Point>(faceCount * 2);
            _neighbours       = new List<Tile>(faceCount * 2);
            _points           = new List<Point>(faceCount);
            _faces            = new List<Face>(faceCount - 2);

            StoreNeighbourCenters(icosahedronFaces);
            BuildFaces(icosahedronFaces);
        }

        public Point Center => _center;
        public List<Point> Points => _points;
        public List<Face> Faces => _faces;
        public List<Tile> Neighbours => _neighbours;

        public void ResolveNeighbourTiles(List<Tile> allTiles)
        {
            var neighbourIds = new HashSet<int>();
            foreach (var c in _neighbourCenters) neighbourIds.Add(c.ID);
            _neighbours = allTiles.Where(tile => neighbourIds.Contains(tile._center.ID)).ToList();
        }

        public void ResolveNeighbourTilesFast(Dictionary<int, Tile> tileMap)
        {
            _neighbours.Clear();
            foreach (var c in _neighbourCenters)
            {
                if (tileMap.TryGetValue(c.ID, out Tile t))
                    _neighbours.Add(t);
            }
        }

        public override string ToString() =>
            $"{_center.Position.X},{_center.Position.Y},{_center.Position.Z}";

        public string ToJson() =>
            $"{{\"centerPoint\":{_center.ToJson()},\"boundary\":[{string.Join(",", _points.Select(p => p.ToJson()))}]}}";

        private void StoreNeighbourCenters(List<Face> icosahedronFaces)
        {
            var seen = new HashSet<int>();
            foreach (var face in icosahedronFaces)
            {
                var (a, b) = face.GetOtherPoints(_center);
                if (seen.Add(a.ID)) _neighbourCenters.Add(a);
                if (seen.Add(b.ID)) _neighbourCenters.Add(b);
            }
        }

        private void BuildFaces(List<Face> icosahedronFaces)
        {
            Vector3 centerPos = _center.Position;
            float projRadiusHalf = _radius * 0.5f;

            foreach (var face in icosahedronFaces)
            {
                Vector3 lerped = centerPos.Lerp(face.GetCenterPosition(), _size);
                float scale = projRadiusHalf / lerped.Length();
                _points.Add(new Point(lerped * scale));
            }

            int n = _points.Count;
            for (int i = 1; i < n - 1; i++)
                _faces.Add(new Face(_points[0], _points[i], _points[i + 1]));
        }
    }
}
