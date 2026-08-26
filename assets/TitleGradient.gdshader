shader_type canvas_item;

// Vertical gradient fill for Label text.
//
// A Label's TEXTURE is the font atlas and its UV is per-glyph, so a UV-based
// gradient would restart on every character. Instead we take the vertex's
// position in the Label's own local space and normalise it by the control
// height, which the companion script keeps in sync.
uniform vec4 top_color : source_color = vec4(1.0, 0.65, 0.13, 1.0);
uniform vec4 bottom_color : source_color = vec4(0.87, 0.13, 0.04, 1.0);
uniform float control_height = 100.0;

varying float v_local_y;

void vertex() {
    v_local_y = VERTEX.y;
}

void fragment() {
    float t = clamp(v_local_y / max(control_height, 1.0), 0.0, 1.0);
    vec4 glyph = texture(TEXTURE, UV);
    COLOR.rgb = mix(top_color.rgb, bottom_color.rgb, t);
    // Keep the glyph's own coverage so antialiased edges stay soft.
    COLOR.a = glyph.a * COLOR.a;
}
