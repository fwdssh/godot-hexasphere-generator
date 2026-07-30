shader_type canvas_item;

uniform sampler2D tile_colors : source_color, filter_nearest;
uniform int tile_count;
uniform int tex_width;

uniform int hover_idx = -1;
uniform vec4 hover_color = vec4(1.0, 0.0, 0.0, 1.0);
uniform int selected_idx = -1;
uniform vec4 selected_color = vec4(1.0, 0.8, 0.0, 1.0);

void fragment() {
    int tex_height = (tile_count + tex_width - 1) / tex_width;
    int idx = int(floor(UV.y * float(tex_height))) * tex_width + int(floor(UV.x * float(tex_width)));

    COLOR = texture(tile_colors, UV);
    COLOR.a = 1.0;

    if (idx == hover_idx) {
        COLOR = hover_color;
    } else if (idx == selected_idx) {
        COLOR = selected_color;
    }
}
