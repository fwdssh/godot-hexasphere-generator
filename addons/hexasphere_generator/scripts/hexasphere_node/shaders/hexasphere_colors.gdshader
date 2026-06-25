shader_type spatial;
//render_mode diffuse_lambert;
render_mode unshaded;
uniform sampler2D tile_colors;
uniform int tile_count;
uniform int tex_width;
uniform float roughness : hint_range(0.0, 1.0) = 0.6;

void vertex() {
    int idx = int(UV2.x);
    
    // Разворачиваем линейный индекс в UV координаты текстуры
    float u = (float(idx % tex_width) + 0.5) / float(tex_width);
    float v = (float(idx / tex_width) + 0.5) / float((tile_count + tex_width - 1) / tex_width);
    
    COLOR = texture(tile_colors, vec2(u, v));
}

void fragment() {
    ALBEDO    = COLOR.rgb;
    ROUGHNESS = roughness;
}