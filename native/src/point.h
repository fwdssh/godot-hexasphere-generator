#ifndef HEXASPHERE_POINT_H
#define HEXASPHERE_POINT_H

#include <array>
#include <cstdint>
#include <functional>
#include <string>
#include <vector>
#include <godot_cpp/variant/vector3.hpp>

using namespace godot;

class Face;

class Point
{
private:
    int _id;
    Vector3 _position;
    std::array<int32_t, 6> _faceIndices{};
    int _faceCount = 0;

public:
    Point() : _id(-1), _position(Vector3()) {}
    Point(const Vector3& position, int localId);

    Vector3 get_position() const { return _position; }
    int get_id() const { return _id; }

    const int32_t* get_face_indices() const { return _faceIndices.data(); }
    int get_face_count() const { return _faceCount; }
    void assign_face_index(int32_t faceIdx) { _faceIndices[_faceCount++] = faceIdx; }

    std::vector<int32_t> subdivide(int32_t targetIdx, int count, const std::function<int32_t(const Vector3&)>& cache_func, const std::vector<Point>& points) const;
    std::vector<int32_t> get_ordered_faces(const std::vector<Face>& faces) const;

    static bool is_overlapping(const Point& a, const Point& b, float epsilon);
    static bool is_overlapping(const Point& a, const Vector3& b_pos, float epsilon);

    std::string to_string() const;
};

#endif
