#include "point.h"
#include "face.h"
#include <algorithm>
#include <cmath>
#include <sstream>

int Point::_globalIdCounter = 0;

Point::Point(const Vector3 &position)
    : _id(++_globalIdCounter), _position(position)
{
    _faces.reserve(6);
}

Point::Point(Point &&other) noexcept
    : _id(other._id), _position(other._position), _faces(std::move(other._faces))
{
    other._id = -1;
}

Point &Point::operator=(Point &&other) noexcept
{
    if (this != &other)
    {
        _id = other._id;
        _position = other._position;
        _faces = std::move(other._faces);
        other._id = -1;
    }
    return *this;
}

std::vector<Point *> Point::subdivide(Point *target, int count, const std::function<Point *(const Vector3 &)> &cache_func)
{
    std::vector<Point *> segments;
    segments.reserve(count + 2);
    segments.push_back(this);

    float invCount = 1.0f / count;

    for (int i = 1; i <= count; i++)
    {
        float t = i * invCount;
        float oneMinusT = 1.0f - t;
        Vector3 pos(
            _position.x * oneMinusT + target->_position.x * t,
            _position.y * oneMinusT + target->_position.y * t,
            _position.z * oneMinusT + target->_position.z * t);
        segments.push_back(cache_func(pos));
    }

    segments.push_back(target);
    return segments;
}

std::vector<Face *> Point::get_ordered_faces()
{
    int count = (int)_faces.size();
    if (count == 0) return _faces;

    std::vector<Face *> ordered;
    ordered.reserve(count);
    ordered.push_back(_faces[0]);

    std::vector<bool> visited(count, false);
    visited[0] = true;
    int currentIdx = 0;

    while ((int)ordered.size() < count)
    {
        Face *cur = _faces[currentIdx];
        int cur_ids[3] = {
            cur->get_points()[0]->get_id(),
            cur->get_points()[1]->get_id(),
            cur->get_points()[2]->get_id()
        };

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            if (visited[i]) continue;
            Face *cand = _faces[i];
            int cand_ids[3] = {
                cand->get_points()[0]->get_id(),
                cand->get_points()[1]->get_id(),
                cand->get_points()[2]->get_id()
            };

            int shared = 0;
            for (int a = 0; a < 3 && shared < 2; a++)
                for (int b = 0; b < 3 && shared < 2; b++)
                    if (cur_ids[a] == cand_ids[b]) shared++;

            if (shared == 2)
            {
                visited[i] = true;
                currentIdx = i;
                ordered.push_back(_faces[i]);
                found = true;
                break;
            }
        }
        if (!found) break;
    }

    return ordered;
}

bool Point::is_overlapping(const Point &a, const Point &b)
{
    return is_overlapping(a, b._position);
}

bool Point::is_overlapping(const Point &a, const Vector3 &b_pos)
{
    return
        std::abs(a._position.x - b_pos.x) <= PointComparisonAccuracy &&
        std::abs(a._position.y - b_pos.y) <= PointComparisonAccuracy &&
        std::abs(a._position.z - b_pos.z) <= PointComparisonAccuracy;
}

std::string Point::to_string() const
{
    std::stringstream ss;
    ss << _position.x << "," << _position.y << "," << _position.z;
    return ss.str();
}
