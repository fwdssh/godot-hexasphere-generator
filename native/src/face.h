#ifndef HEXASPHERE_FACE_H
#define HEXASPHERE_FACE_H

#include <godot_cpp/variant/vector3.hpp>

using namespace godot;

class Point;

class Face
{
private:
    static int _globalIdCounter;

    int _id;
    Point *_points[3];

public:
    Face(Point *point1, Point *point2, Point *point3, bool trackFaceInPoints = true);

    Face(const Face &) = delete;
    Face &operator=(const Face &) = delete;
    Face(Face &&) = delete;
    Face &operator=(Face &&) = delete;

    int get_id() const { return _id; }
    Point *const *get_points() const { return _points; }

    Vector3 get_center_position() const;

    void get_other_points(Point *point, Point *&out_a, Point *&out_b) const;
    bool is_adjacent_to_face(const Face *face) const;
};

#endif // HEXASPHERE_FACE_H
