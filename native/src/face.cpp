#include "face.h"
#include "point.h"
#include <cmath>
#include <stdexcept>

Face::Face(int32_t pointIdx1, int32_t pointIdx2, int32_t pointIdx3, int localId,
           const Vector3& p1, const Vector3& p2, const Vector3& p3)
    : _id(localId)
{
    // Double intermediates keep the sign independent of the sphere's scale,
    // including radii whose squared length underflows or overflows real_t.
    const double ax = double(p2.x) - p1.x, ay = double(p2.y) - p1.y, az = double(p2.z) - p1.z;
    const double bx = double(p3.x) - p1.x, by = double(p3.y) - p1.y, bz = double(p3.z) - p1.z;
    const double nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
    const double cx = (double(p1.x) + p2.x + p3.x) / 3.0;
    const double cy = (double(p1.y) + p2.y + p3.y) / 3.0;
    const double cz = (double(p1.z) + p2.z + p3.z) / 3.0;
    const double orientation = cx * nx + cy * ny + cz * nz;
    if (!std::isfinite(orientation) || orientation == 0.0)
        throw std::invalid_argument("Cannot orient a degenerate sphere face");
    const bool outward = orientation > 0.0;

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
        (double(p1.get_position().x) + p2.get_position().x + p3.get_position().x) / 3.0,
        (double(p1.get_position().y) + p2.get_position().y + p3.get_position().y) / 3.0,
        (double(p1.get_position().z) + p2.get_position().z + p3.get_position().z) / 3.0);
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
