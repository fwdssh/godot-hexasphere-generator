#ifndef HEXASPHERE_NATIVE_HEXASPHERE_H
#define HEXASPHERE_NATIVE_HEXASPHERE_H

#include <godot_cpp/classes/ref_counted.hpp>
#include <godot_cpp/variant/dictionary.hpp>
#include <godot_cpp/variant/packed_vector3_array.hpp>
#include <godot_cpp/variant/packed_int32_array.hpp>
#include <memory>

using namespace godot;

class Hexasphere;

class NativeHexasphere : public RefCounted
{
    GDCLASS(NativeHexasphere, RefCounted)

private:
    std::unique_ptr<Hexasphere> _hexasphere;

protected:
    static void _bind_methods();

public:
    NativeHexasphere();
    ~NativeHexasphere();

    void generate(float radius, int divisions, float hexSize);

    int get_tile_count() const;
    Vector3 get_tile_center(int tile_idx) const;
    PackedVector3Array get_tile_points(int tile_idx) const;
    PackedInt32Array get_tile_faces(int tile_idx) const;
    Dictionary get_build_data() const;
    Dictionary get_border_data() const;
    Dictionary build_mesh() const;
};

#endif // HEXASPHERE_NATIVE_HEXASPHERE_H
