#include "face.h"
#include "point.h"
#include <stdexcept>

Face::Face(int32_t pointIdx1, int32_t pointIdx2, int32_t pointIdx3, int localId,
           const Vector3& p1, const Vector3& p2, const Vector3& p3)
    : _id(localId)
{
    Vector3 center((p1.x + p2.x + p3.x) / 3.0f,
                   (p1.y + p2.y + p3.y) / 3.0f,
                   (p1.z + p2.z + p3.z) / 3.0f);

    Vector3 cross = (p2 - p1).cross(p3 - p1);
    float crossLen = cross.length();
    bool outward = center.length_squared() < (center + (cross / crossLen)).length_squared();

    if (outward)
    {
        _pointIndices[0] = pointIdx1;
        _pointIndices[1] = pointIdx2;
        _pointIndices[2] = pointIdx3;
    }
    else
    {
        _pointIndices[0] = pointIdx1;
        _pointIndices[1] = pointIdx3;
        _pointIndices[2] = pointIdx2;
    }
}

Vector3 Face::get_center_position(const std::vector<Point>& points) const
{
    const Point& p1 = points[_pointIndices[0]];
    const Point& p2 = points[_pointIndices[1]];
    const Point& p3 = points[_pointIndices[2]];
    return Vector3(
        (p1.get_position().x + p2.get_position().x + p3.get_position().x) / 3.0f,
        (p1.get_position().y + p2.get_position().y + p3.get_position().y) / 3.0f,
        (p1.get_position().z + p2.get_position().z + p3.get_position().z) / 3.0f);
}

void Face::get_other_point_indices(int32_t pointIdx, int32_t& out_a, int32_t& out_b) const
{
    if (_pointIndices[0] == pointIdx)
    {
        out_a = _pointIndices[1];
        out_b = _pointIndices[2];
        return;
    }
    if (_pointIndices[1] == pointIdx)
    {
        out_a = _pointIndices[0];
        out_b = _pointIndices[2];
        return;
    }
    if (_pointIndices[2] == pointIdx)
    {
        out_a = _pointIndices[0];
        out_b = _pointIndices[1];
        return;
    }
    throw std::invalid_argument("Given point must be one of the points on the face!");
}

bool Face::is_adjacent_to_face(const Face& face) const
{
    int shared = 0;
    if (_pointIndices[0] == face._pointIndices[0] || _pointIndices[0] == face._pointIndices[1] || _pointIndices[0] == face._pointIndices[2]) shared++;
    if (_pointIndices[1] == face._pointIndices[0] || _pointIndices[1] == face._pointIndices[1] || _pointIndices[1] == face._pointIndices[2]) shared++;
    if (shared == 2) return true;
    if (_pointIndices[2] == face._pointIndices[0] || _pointIndices[2] == face._pointIndices[1] || _pointIndices[2] == face._pointIndices[2]) shared++;
    return shared == 2;
}
