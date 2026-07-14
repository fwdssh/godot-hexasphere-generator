vec2 tile_uv(int idx, int tex_width, int tile_count) {
    float u = (float(idx % tex_width) + 0.5) / float(tex_width);
    float v = (float(idx / tex_width) + 0.5) / float((tile_count + tex_width - 1) / tex_width);
    return vec2(u, v);
}
