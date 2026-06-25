#include "face.h"
#include "point.h"
#include <stdexcept>

int Face::_globalIdCounter = 0;

Face::Face(Point *point1, Point *point2, Point *point3, bool trackFaceInPoints)
    : _id(++_globalIdCounter)
{
    Vector3 p1 = point1->get_position();
    Vector3 p2 = point2->get_position();
    Vector3 p3 = point3->get_position();

    Vector3 center(
        (p1.x + p2.x + p3.x) / 3.0f,
        (p1.y + p2.y + p3.y) / 3.0f,
        (p1.z + p2.z + p3.z) / 3.0f);

    Vector3 cross = (p2 - p1).cross(p3 - p1);
    float crossLen = cross.length();
    bool outward = center.length_squared() < (center + (cross / crossLen)).length_squared();

    if (outward)
    {
        _points[0] = point1;
        _points[1] = point2;
        _points[2] = point3;
    }
    else
    {
        _points[0] = point1;
        _points[1] = point3;
        _points[2] = point2;
    }

    if (trackFaceInPoints)
    {
        _points[0]->assign_face(this);
        _points[1]->assign_face(this);
        _points[2]->assign_face(this);
    }
}

Vector3 Face::get_center_position() const
{
    return Vector3(
        (_points[0]->get_position().x + _points[1]->get_position().x + _points[2]->get_position().x) / 3.0f,
        (_points[0]->get_position().y + _points[1]->get_position().y + _points[2]->get_position().y) / 3.0f,
        (_points[0]->get_position().z + _points[1]->get_position().z + _points[2]->get_position().z) / 3.0f);
}

void Face::get_other_points(Point *point, Point *&out_a, Point *&out_b) const
{
    int id = point->get_id();
    if (_points[0]->get_id() == id)
    {
        out_a = _points[1];
        out_b = _points[2];
        return;
    }
    if (_points[1]->get_id() == id)
    {
        out_a = _points[0];
        out_b = _points[2];
        return;
    }
    if (_points[2]->get_id() == id)
    {
        out_a = _points[0];
        out_b = _points[1];
        return;
    }
    throw std::invalid_argument("Given point must be one of the points on the face!");
}

bool Face::is_adjacent_to_face(const Face *face) const
{
    int a0 = _points[0]->get_id(), a1 = _points[1]->get_id(), a2 = _points[2]->get_id();
    int b0 = face->_points[0]->get_id(), b1 = face->_points[1]->get_id(), b2 = face->_points[2]->get_id();
    int shared = 0;
    if (a0 == b0 || a0 == b1 || a0 == b2) shared++;
    if (a1 == b0 || a1 == b1 || a1 == b2) shared++;
    if (shared == 2) return true;
    if (a2 == b0 || a2 == b1 || a2 == b2) shared++;
    return shared == 2;
}
