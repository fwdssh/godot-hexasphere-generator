shader_type spatial;
//render_mode unshaded;
#include "res://addons/hexasphere_generator/scripts/hexasphere_node/shaders/tile_uv.gdshaderinc"
uniform sampler2D tile_colors;
uniform int tile_count;
uniform int tex_width;

uniform int selected_idx = -1;
uniform vec4 selected_color = vec4(1.0, 0.8, 0.0, 1.0);
uniform int hover_idx = -1;
uniform vec4 hover_color = vec4(1.0, 0.0, 0.0, 1.0);

uniform bool is_emissive = false;

void vertex() {
    int idx = int(round(UV2.x));

    vec2 uv = tile_uv(idx, tex_width, tile_count);
    COLOR = texture(tile_colors, uv);

    if (idx == hover_idx) {
        COLOR = hover_color;
    } else if (idx == selected_idx) {
        COLOR = selected_color;
    }
}

void fragment() {
    ALBEDO    = COLOR.rgb;
    
    if (is_emissive) {
        EMISSION = COLOR.rgb;
    }
}
