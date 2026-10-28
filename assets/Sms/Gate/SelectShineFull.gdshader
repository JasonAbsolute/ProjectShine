// The episode select's FULL shine body -- shine_menu.bmd '_mat_shine_body' as read (bmd_mat_dump.py; SMS pass 127d). GX: matColor (225, 120, 30)
// lit by the channel (ambient (128, 128, 128) + one light's N.L), then two radgrad lookups by the VIEW-SPACE NORMAL (texgen src NRM): stage 0
// adds konst (255, 196, 155) x radgrad(texmtx0: scale 1.528, trans -0.15) -- a broad cream highlight where the normal faces the view --,
// stage 1 adds radgrad(texmtx1: scale 4.09, trans 0.908) -- a small white spot; alpha = RASA = the material alpha (the bpk's depth fade).
// Blend SRCALPHA / INVSRCALPHA, z tested AND written (a translucent star that occludes itself). EVERY sum is done in DISPLAY space, as the
// GX adds 8-bit values: the lit orange (225, 120, 30) + the cream highlight x 0.65 clamps to (255, 247, 131) -- the bright yellow of the game's
// menu shine; summed in linear space (Godot's lighting + EMISSION) the same values gave a dull copper. So: unshaded, the light done here.
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D radgrad : filter_linear, repeat_disable;             // P_ms_radgrad_i (raw: r = I)
uniform vec4 mat_color = vec4(0.8824, 0.4706, 0.1176, 1.0);          // (225, 120, 30)
uniform float ambient = 0.5;                                          // ambColor0 (128, 128, 128)
uniform vec3 light_dir = vec3(0.15, 0.35, 0.92);                      // the select's light, VIEW space (toward the light): nearly frontal, a little up-right -- the menu shines are lit evenly in the game's screen
uniform float light_strength = 1.0;                                   // the light's colour (white x this)
uniform vec4 konst0 = vec4(1.0, 0.7686, 0.6078, 1.0);                 // (255, 196, 155): the highlight's colour
uniform vec2 scale0 = vec2(1.52832, 1.52832);
uniform vec2 trans0 = vec2(-0.15, -0.15);
uniform vec2 scale1 = vec2(4.0918, 4.0918);
uniform vec2 trans1 = vec2(0.908203, 0.908203);
uniform float highlight = 1.0;                                        // x the two highlights (1 = the game)
uniform float fade = 1.0;                                             // the bpk's alpha: 1 - frame / 99 (0.7 for the selected shine)

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	vec3 n = normalize(NORMAL);                                        // view space
	float ndl = max(dot(n, normalize(light_dir)), 0.0);
	vec3 lit = clamp(mat_color.rgb * (ambient + ndl * light_strength), 0.0, 1.0);   // GX_DF_CLAMP diffuse + ambient, display space
	vec2 nuv = n.xy * 0.5 + 0.5;                                       // the normal's x / y as a texcoord (GX_TG_NRM)
	vec2 uv0 = (nuv - 0.5) * scale0 + 0.5 + trans0;
	vec2 uv1 = (nuv - 0.5) * scale1 + 0.5 + trans1;
	float i0 = texture(radgrad, uv0).r;
	float i1 = texture(radgrad, uv1).r;
	vec3 col = clamp(lit + (konst0.rgb * i0 + vec3(i1)) * highlight, 0.0, 1.0);
	ALBEDO = to_linear(col);
	ALPHA = fade;
}
