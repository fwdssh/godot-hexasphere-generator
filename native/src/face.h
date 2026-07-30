#ifndef HEXASPHERE_FACE_H
#define HEXASPHERE_FACE_H

#include <cstdint>
#include <vector>
#include <godot_cpp/variant/vector3.hpp>

using namespace godot;

class Point;

class Face
{
private:
    int _id;
    int32_t _pointIndices[3];

public:
    Face() : _id(-1), _pointIndices{} {}
    Face(int32_t pointIdx1, int32_t pointIdx2, int32_t pointIdx3, int localId,
         const Vector3& p1, const Vector3& p2, const Vector3& p3);

    int get_id() const { return _id; }
    const int32_t* get_point_indices() const { return _pointIndices; }

    Vector3 get_center_position(const std::vector<Point>& points) const;
    void get_other_point_indices(int32_t pointIdx, int32_t& out_a, int32_t& out_b) const;
    bool is_adjacent_to_face(const Face& face) const;
};

#endif
