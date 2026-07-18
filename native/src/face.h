#ifndef HEXASPHERE_FACE_H
#define HEXASPHERE_FACE_H

#include <godot_cpp/variant/vector3.hpp>

using namespace godot;

class Point;

/// <summary>
/// Represents a triangular face on the subdivided icosahedron.
/// Faces are the building blocks of the hexasphere's mesh topology.
/// Internal — not exposed directly to Godot.
/// </summary>
class Face
{
private:
    int _id;
    Point *_points[3];

public:
    Face(Point *point1, Point *point2, Point *point3, int localId, bool trackFaceInPoints = true); // local id, no global counter (thread-safe)

    Face(const Face &) = delete;
    Face &operator=(const Face &) = delete;
    Face(Face &&other) noexcept;
    Face &operator=(Face &&other) noexcept;

    /// <summary>
    /// Returns the unique identifier of this face.
    /// </summary>
    int get_id() const { return _id; }

    /// <summary>
    /// Returns a pointer to the array of three corner points.
    /// </summary>
    Point *const *get_points() const { return _points; }

    /// <summary>
    /// Returns the geometric center position of this triangular face.
    /// </summary>
    Vector3 get_center_position() const;

    /// <summary>
    /// Given one point of this face, retrieves the two other corner points via output parameters.
    /// </summary>
    void get_other_points(Point *point, Point *&out_a, Point *&out_b) const;

    /// <summary>
    /// Checks whether this face shares an edge with the given face.
    /// </summary>
    bool is_adjacent_to_face(const Face *face) const;
};

#endif // HEXASPHERE_FACE_H
