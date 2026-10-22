shader_type canvas_item;

uniform sampler2D tile_colors : source_color, filter_nearest, repeat_disable;
uniform int tile_count;
uniform int tex_width;
uniform bool raster_mode = false;
uniform sampler2D raster_ids : filter_nearest, repeat_disable;

uniform int hover_idx = -1;
uniform vec4 hover_color = vec4(1.0, 0.0, 0.0, 1.0);
uniform int selected_idx = -1;
uniform vec4 selected_color = vec4(1.0, 0.8, 0.0, 1.0);

void fragment() {
    int idx;
    if (raster_mode) {
        ivec4 encoded = ivec4(round(texture(raster_ids, UV) * 255.0));
        idx = (encoded.r | (encoded.g << 8) | (encoded.b << 16) | (encoded.a << 24)) - 1;
    } else {
        int tex_height = (tile_count + tex_width - 1) / tex_width;
        idx = int(floor(UV.y * float(tex_height))) * tex_width + int(floor(UV.x * float(tex_width)));
    }

    COLOR = texture(tile_colors, UV);
    COLOR.a = 1.0;

    if (idx >= 0 && idx == hover_idx) {
        COLOR = hover_color;
    } else if (idx >= 0 && idx == selected_idx) {
        COLOR = selected_color;
    }
}
