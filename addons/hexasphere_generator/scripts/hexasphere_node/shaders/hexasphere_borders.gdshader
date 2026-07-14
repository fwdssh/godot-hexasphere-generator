shader_type spatial;
render_mode unshaded, cull_disabled;
#include "res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/tile_uv.gdshaderinc"

uniform sampler2D tile_colors;
uniform int tile_count;
uniform int tex_width;
uniform int selected_idx;
uniform vec4 border_color = vec4(0.0, 0.0, 0.0, 1.0);

void vertex() {
    int idxA = int(round(UV2.x));
    int idxB = int(round(UV2.y));

    // Use idxA for tile color lookup (same as before)
    vec2 uv = tile_uv(idxA, tex_width, tile_count);
    vec4 tileColor = texture(tile_colors, uv);

    if (idxA == selected_idx || idxB == selected_idx) {
        COLOR = vec4(1.0, 1.0, 1.0, 1.0);
    } else {
        COLOR = border_color;
    }
}

void fragment() {
    ALBEDO = COLOR.rgb;
}
