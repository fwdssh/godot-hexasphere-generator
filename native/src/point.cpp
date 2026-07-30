#include "point.h"
#include "face.h"
#include <cmath>
#include <sstream>
#include <stdexcept>

Point::Point(const Vector3& position, int localId)
    : _id(localId), _position(position)
{
}

std::vector<int32_t> Point::subdivide(int32_t targetIdx, int count, const std::function<int32_t(const Vector3&)>& cache_func, const std::vector<Point>& points) const
{
    std::vector<int32_t> segments;
    segments.reserve(static_cast<size_t>(count) + 2);
    segments.push_back(_id);

    float invCount = 1.0f / count;
    const Point& target = points[targetIdx];

    for (int i = 1; i < count; i++)
    {
        float t = i * invCount;
        float oneMinusT = 1.0f - t;
        Vector3 pos(
            _position.x * oneMinusT + target._position.x * t,
            _position.y * oneMinusT + target._position.y * t,
            _position.z * oneMinusT + target._position.z * t);
        segments.push_back(cache_func(pos));
    }

    segments.push_back(targetIdx);
    return segments;
}

std::vector<int32_t> Point::get_ordered_faces(const std::vector<Face>& faces) const
{
    int count = _faceCount;
    if (count == 0) return {};

    std::vector<int32_t> ordered;
    ordered.reserve(static_cast<size_t>(count));
    ordered.push_back(_faceIndices[0]);

    std::vector<bool> visited(static_cast<size_t>(count), false);
    visited[0] = true;
    int currentIdx = 0;

    while (static_cast<int>(ordered.size()) < count)
    {
        const Face& cur = faces[_faceIndices[currentIdx]];
        const int32_t* cur_ids = cur.get_point_indices();

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (visited[i]) continue;
            int shared = 0;
            const int32_t* cand_ids = faces[_faceIndices[i]].get_point_indices();
            for (int a = 0; a < 3 && shared < 2; a++)
                for (int b = 0; b < 3 && shared < 2; b++)
                    if (cur_ids[a] == cand_ids[b]) shared++;
            if (shared == 2)
            {
                visited[i] = true;
                currentIdx = i;
                ordered.push_back(_faceIndices[i]);
                found = true;
                break;
            }
        }
        if (!found) break;
    }

    return ordered;
}

bool Point::is_overlapping(const Point& a, const Point& b, float epsilon)
{
    return a._position.distance_to(b._position) <= epsilon;
}

bool Point::is_overlapping(const Point& a, const Vector3& b_pos, float epsilon)
{
    return a._position.distance_to(b_pos) <= epsilon;
}

std::string Point::to_string() const
{
    std::stringstream ss;
    ss << "Point(" << _id << ")";
    return ss.str();
}
