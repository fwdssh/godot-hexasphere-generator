using System;
using System.Collections.Generic;
using Godot;

namespace Godot.Hexasphere
{
    public class Face 
    {
        private static int _globalIdCounter = 0;

        private readonly int _id;
        private readonly Point[] _points;

        public Face(Point point1, Point point2, Point point3, bool trackFaceInPoints = true)
        {
            _id = System.Threading.Interlocked.Increment(ref _globalIdCounter);

            Vector3 p1 = point1.Position;
            Vector3 p2 = point2.Position;
            Vector3 p3 = point3.Position;

            Vector3 center = new Vector3(
                (p1.X + p2.X + p3.X) / 3f,
                (p1.Y + p2.Y + p3.Y) / 3f,
                (p1.Z + p2.Z + p3.Z) / 3f
            );

            Vector3 cross = (p2 - p1).Cross(p3 - p1);
            bool outward = center.LengthSquared() < (center + cross / cross.Length()).LengthSquared();

            _points = outward
                ? new Point[] { point1, point2, point3 }
                : new Point[] { point1, point3, point2 };

            if (trackFaceInPoints)
            {
                _points[0].AssignFace(this);
                _points[1].AssignFace(this);
                _points[2].AssignFace(this);
            }
        }

        public int ID => _id;

        public Point[] Points => _points;

        // возвращаем Vector3 а не Point — не нужен new Point + счётчик ID
        public Vector3 GetCenterPosition()
        {
            return new Vector3(
                (_points[0].Position.X + _points[1].Position.X + _points[2].Position.X) / 3f,
                (_points[0].Position.Y + _points[1].Position.Y + _points[2].Position.Y) / 3f,
                (_points[0].Position.Z + _points[1].Position.Z + _points[2].Position.Z) / 3f
            );
        }

        public (Point, Point) GetOtherPoints(Point point)
        {
            int id = point.ID;
            if (_points[0].ID == id) return (_points[1], _points[2]);
            if (_points[1].ID == id) return (_points[0], _points[2]);
            if (_points[2].ID == id) return (_points[0], _points[1]);
            throw new ArgumentException("Given point must be one of the points on the face!");
        }

        public bool IsAdjacentToFace(Face face)
        {
            // Прямое сравнение int ID: 9 операций максимум
            int a0 = _points[0].ID, a1 = _points[1].ID, a2 = _points[2].ID;
            int b0 = face._points[0].ID, b1 = face._points[1].ID, b2 = face._points[2].ID;
            int shared = 0;
            if (a0 == b0 || a0 == b1 || a0 == b2) shared++;
            if (a1 == b0 || a1 == b1 || a1 == b2) shared++;
            if (shared == 2) return true;
            if (a2 == b0 || a2 == b1 || a2 == b2) shared++;
            return shared == 2;
        }
    }
}
