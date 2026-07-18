#ifndef HEXASPHERE_POINT_H
#define HEXASPHERE_POINT_H

#include <functional>
#include <string>
#include <vector>
#include <godot_cpp/variant/vector3.hpp>

using namespace godot;

class Face;

/// <summary>
/// A vertex point on the sphere surface, used as a corner of faces and tiles.
/// Points are cached and reused during subdivision to avoid duplicates.
/// Internal — not exposed directly to Godot.
/// </summary>
class Point
{
private:
    int _id;
    Vector3 _position;
    std::vector<Face *> _faces;

public:
    Point();
    Point(const Vector3 &position, int localId); // local id, no global counter (thread-safe)
    Point(const Point &other) = delete;
    Point &operator=(const Point &other) = delete;
    Point(Point &&other) noexcept;
    Point &operator=(Point &&other) noexcept;

    /// <summary>
    /// Returns the world-space position of this point on the sphere surface.
    /// </summary>
    Vector3 get_position() const { return _position; }

    /// <summary>
    /// Returns the unique identifier of this point.
    /// </summary>
    int get_id() const { return _id; }

    /// <summary>
    /// Returns a modifiable reference to the list of faces that include this point.
    /// </summary>
    std::vector<Face *> &get_faces() { return _faces; }

    /// <summary>
    /// Returns a const reference to the list of faces that include this point.
    /// </summary>
    const std::vector<Face *> &get_faces() const { return _faces; }

    /// <summary>
    /// Registers a face that references this point.
    /// </summary>
    void assign_face(Face *face) { _faces.push_back(face); }

    /// <summary>
    /// Generates subdivision points along the edge between this point and a target point.
    /// Uses the provided cache function to reuse existing points.
    /// </summary>
    std::vector<Point *> subdivide(Point *target, int count, const std::function<Point *(const Vector3 &)> &cache_func);

    /// <summary>
    /// Returns the list of faces surrounding this point, ordered clockwise.
    /// </summary>
    std::vector<Face *> get_ordered_faces();

    /// <summary>
    /// Checks whether two points are at approximately the same position within an epsilon tolerance.
    /// </summary>
    static bool is_overlapping(const Point &a, const Point &b, float epsilon);

    /// <summary>
    /// Checks whether a point is at approximately the same position as a given Vector3 position within an epsilon tolerance.
    /// </summary>
    static bool is_overlapping(const Point &a, const Vector3 &b_pos, float epsilon);

    /// <summary>
    /// Returns a human-readable string representation of this point.
    /// </summary>
    std::string to_string() const;
};

#endif // HEXASPHERE_POINT_H
