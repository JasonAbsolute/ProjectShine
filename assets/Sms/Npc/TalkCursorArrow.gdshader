// TTalkCursor -- /common/cursor_b/default.bmd material _mat7, the chevron under the icon (shape 1: a down-pointing arrow 54 units across,
// 25 tall, a diamond section; spun and bobbed by icon_rot.bck's joint 1). One TEV stage (GX, display-referred) over TEXMAP0 =
// H_soap_ref_ia8 sampled by the view-space NORMAL (texgen src NRM through TEXMTX0: centre (0.5, 0.5), scale 0.91936, translation
// (0.620117, 0.371094) -> u = 0.919 nx + 0.660, v = 0.919 ny + 0.411, clamped): a lit-sphere map -- the I channel shades the arrow by its
// facing, the A channel is the white gloss blob (normals toward the upper left of the view).
//   colour = 4 x (TEXA + C0 x TEXC), clamped   with C0 = (210, 40, 40): the red arrow, dark where the sphere map is, white where it glints
//   alpha  = A0 x K0.a = 1
// GXSetZMode(TRUE, GX_ALWAYS, TRUE): drawn over the world. The light channel is unused by the TEV (no RAS input): unshaded.
shader_type spatial;
render_mode unshaded, blend_mix, cull_back, depth_test_disabled, depth_draw_never, shadows_disabled;

uniform sampler2D soap : filter_linear, repeat_disable;      // talk_cursor_soap.png: IA8 as RGB = I, A = A
uniform vec4 c0 = vec4(0.823529, 0.156863, 0.156863, 1.0);   // GX_TEVREG0 (210, 40, 40)
uniform vec2 env_scale = vec2(0.91936, 0.91936);
uniform vec2 env_offset = vec2(0.660149, 0.411410);           // centre x (1 - scale) + translation

vec3 srgb_to_linear(vec3 c) {
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
    vec3 n = normalize(NORMAL);                     // view space (Godot's fragment NORMAL): x right, y up
    vec2 uv = n.xy * env_scale + env_offset;
    vec4 t = texture(soap, clamp(uv, 0.0, 1.0));
    vec3 col = clamp(4.0 * (vec3(t.a) + c0.rgb * t.r), 0.0, 1.0);
    ALBEDO = srgb_to_linear(col);
    ALPHA = 1.0;
}
