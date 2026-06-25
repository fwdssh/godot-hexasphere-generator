using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace Godot.Hexasphere
{
    public class Point 
    {
        private static int _globalIdCounter = 0;

        private readonly int _id;
        private readonly Vector3 _position;
        private readonly List<Face> _faces;

        private const float PointComparisonAccuracy = 0.0001f;

        public Point(Vector3 position)
        {
            _id = Interlocked.Increment(ref _globalIdCounter);
            _position = position;
            _faces = new List<Face>(6); 
        }

         public Vector3 Position => _position;
         public int ID => _id;
        public List<Face> Faces => _faces;

        public void AssignFace(Face face)
        {
            _faces.Add(face);
        }

        public List<Point> Subdivide(Point target, int count, Func<Point, Point> findDuplicatePointIfExists)
        {
            List<Point> segments = new List<Point>(count + 2);
            segments.Add(this);

            float invCount = 1f / count;
            for (int i = 1; i <= count; i++)
            {
                float t = i * invCount;
                float oneMinusT = 1f - t;
                segments.Add(findDuplicatePointIfExists(new Point(new Vector3(
                    _position.X * oneMinusT + target.Position.X * t,
                    _position.Y * oneMinusT + target.Position.Y * t,
                    _position.Z * oneMinusT + target.Position.Z * t
                ))));
            }

            segments.Add(target);
            return segments;
        }

        /// Упорядочивает фейсы в кольцо. n <= 6, все операции O(1) на практике.
        public List<Face> GetOrderedFaces()
        {
            int count = _faces.Count;
            if (count == 0) return _faces;

            int[][] facePointIds = new int[count][];
            for (int i = 0; i < count; i++)
            {
                var pts = _faces[i].Points;
                facePointIds[i] = new int[3] { pts[0].ID, pts[1].ID, pts[2].ID };
            }

            List<Face> ordered = new List<Face>(count) { _faces[0] };
            bool[] visited = new bool[count];
            visited[0] = true;
            int currentIdx = 0;

            while (ordered.Count < count)
            {
                int[] cur = facePointIds[currentIdx];
                bool found = false;
                for (int i = 0; i < count; i++)
                {
                    if (visited[i]) continue;
                    int[] cand = facePointIds[i];
                    int shared = 0;
                    for (int a = 0; a < 3 && shared < 2; a++)
                        for (int b = 0; b < 3 && shared < 2; b++)
                            if (cur[a] == cand[b]) shared++;

                    if (shared == 2)
                    {
                        visited[i] = true;
                        currentIdx = i;
                        ordered.Add(_faces[i]);
                        found = true;
                        break;
                    }
                }
                if (!found) break;
            }

            return ordered;
        }

        public static bool IsOverlapping(Point a, Point b)
        {
            return
                Mathf.Abs(a._position.X - b._position.X) <= PointComparisonAccuracy &&
                Mathf.Abs(a._position.Y - b._position.Y) <= PointComparisonAccuracy &&
                Mathf.Abs(a._position.Z - b._position.Z) <= PointComparisonAccuracy;
        }

        public override string ToString() => $"{_position.X},{_position.Y},{_position.Z}";

        public string ToJson() =>
            $"{{\"x\":{_position.X},\"y\":{_position.Y},\"z\":{_position.Z},\"id\":{_id}}}";
    }
}
