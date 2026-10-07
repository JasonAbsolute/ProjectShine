// TTalk2D2::openWindow (Talk2D2.cpp 1399) -- one message line's blue band rolling out, then the finished 'bac' pane.
// GX: TEXMAP0 = message_back_1.bti (an I8 ramp, 332 x 1: 255 for its first 290 texels, falling to 0 over the last 40) sampled at
// (s + roll, t) with GX_CLAMP in s; stage 0: colour = lerp(C0, C1, texC) with both registers 0000ff, alpha = lerp(0x00, 0xA0, texA);
// stage 1: alpha x TEXMAP1's alpha = the 'bac' pane's own picture (message_back_0.bti, the curved band shape). roll = unk234[line]:
// 1.0 (the ramp's tail clamped over the whole band: nothing) down to -0.109 at 0.04 a tick -- the band wipes in left to right with a
// 40-texel soft edge. Once every band has landed the game hides these quads and shows the 'bac' pictures instead: alpha 160 x the shape,
// black 0000ff00 / white 0000ffff -- the same colour, so roll <= -1 here IS that pane (the ramp fully under the band).
shader_type canvas_item;
render_mode blend_mix;

uniform sampler2D ramp : repeat_disable, filter_linear;   // talk_band_ramp.png: the ramp as alpha
uniform float roll = 1.0;                                  // unk234[line]
uniform vec4 band_color : source_color = vec4(0.0, 0.0, 1.0, 1.0);   // C0 / C1 = 0000ff; the 'bac' tint 0000ff
uniform float band_alpha = 0.6274510;                      // 0xA0 / 255 (the pane: 160 / 255, the same)
uniform float sharpness : hint_range(0.0, 1.0) = 1.0;      // pass 87: the x4 band sheet's edge sharpened to a pixel (TalkGlyph.gdshader's trick); 0 = as sampled
uniform float edge = 0.75;

varying vec4 modulate;   // the pane's colour alpha (the root's fade reaches it through the tree), before the texture

void vertex() {
    modulate = COLOR;
}

void fragment() {
    float c = texture(TEXTURE, UV).a;                                    // message_back_0's intensity (TEXMAP1 alpha), the x4 coverage
    float w = max(fwidth(c) * edge, 0.005);
    float shape = mix(c, smoothstep(0.5 - w, 0.5 + w, c), sharpness);
    float r = texture(ramp, vec2(clamp(UV.x + roll, 0.0, 1.0), 0.5)).a; // GX_CLAMP: past the end the last texel (0) holds
    COLOR = vec4(band_color.rgb, r * shape * band_alpha * modulate.a);
}
