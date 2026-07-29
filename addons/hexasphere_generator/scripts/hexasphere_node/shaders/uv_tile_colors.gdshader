shader_type canvas_item;

uniform sampler2D tile_colors : source_color, filter_nearest;

void fragment() {
    COLOR = texture(tile_colors, UV);
    COLOR.a = 1.0;
}
