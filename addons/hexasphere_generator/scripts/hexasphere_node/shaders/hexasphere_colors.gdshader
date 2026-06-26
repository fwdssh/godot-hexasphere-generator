shader_type spatial;
render_mode unshaded;
uniform sampler2D tile_colors;
uniform int tile_count;
uniform int tex_width;
uniform float roughness : hint_range(0.0, 1.0) = 0.6;
uniform int selected_idx = -1;
uniform vec4 selected_color = vec4(1.0, 0.8, 0.0, 1.0);
uniform int hover_idx = -1;
uniform vec4 hover_color = vec4(1.0, 0.0, 0.0, 1.0);

void vertex() {
    int idx = int(round(UV2.x));

    float u = (float(idx % tex_width) + 0.5) / float(tex_width);
    float v = (float(idx / tex_width) + 0.5) / float((tile_count + tex_width - 1) / tex_width);

    COLOR = texture(tile_colors, vec2(u, v));

    if (idx == hover_idx) {
        COLOR = hover_color;
    } else if (idx == selected_idx) {
        COLOR = selected_color;
    }
}

void fragment() {
    ALBEDO    = COLOR.rgb;
    ROUGHNESS = roughness;
}
