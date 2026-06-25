#ifndef HEXASPHERE_POINT_H
#define HEXASPHERE_POINT_H

#include <functional>
#include <string>
#include <vector>
#include <godot_cpp/variant/vector3.hpp>

using namespace godot;

class Face;

class Point
{
private:
    static int _globalIdCounter;

    int _id;
    Vector3 _position;
    std::vector<Face *> _faces;

    static constexpr float PointComparisonAccuracy = 0.0001f;

public:
    Point(const Vector3 &position);
    Point(const Point &other) = delete;
    Point &operator=(const Point &other) = delete;
    Point(Point &&other) noexcept;
    Point &operator=(Point &&other) noexcept;

    Vector3 get_position() const { return _position; }
    int get_id() const { return _id; }
    std::vector<Face *> &get_faces() { return _faces; }
    const std::vector<Face *> &get_faces() const { return _faces; }

    void assign_face(Face *face) { _faces.push_back(face); }

    std::vector<Point *> subdivide(Point *target, int count, const std::function<Point *(const Vector3 &)> &cache_func);

    std::vector<Face *> get_ordered_faces();

    static bool is_overlapping(const Point &a, const Point &b);
    static bool is_overlapping(const Point &a, const Vector3 &b_pos);

    std::string to_string() const;
};

#endif // HEXASPHERE_POINT_H
