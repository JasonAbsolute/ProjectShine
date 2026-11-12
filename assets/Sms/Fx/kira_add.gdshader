// ms_mnt_kira / ms_mare_kira (BSP1): Billboard, blend SRCALPHA + ONE, z test on / no write; colour = lerp(env -> prm, TEXC) with prm WHITE
// and env a per-star RANDOM phase of the 2-key table (255,206,24) -> (255,63,15) (colour calc flag bit 0), alpha = TEXA x prmA x the ESP1
// AddSin twinkle. The ramp feeds env as COLOR.rgb (display-space values) and the twinkle as COLOR.a; the texture is the raw IA8 (I in .r).
// 216: alpha_floor / edge_fade (both 0 = the game's, as before): the IA8 star has a little alpha everywhere (4-20 of 255 over most of the quad) and
// its arms run into the quad's edge, so a BIG star against a black sky adds a faint square -- King Boo's (the rematch's) cut the haze under
// alpha_floor and fade the quad's edge out radially from edge_from (the distance from the centre; 1 = the middle of an edge).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;   // star4s_raw: the 64 x 64 star mirrored 2 x 2 (the quad's tiling 2.0 under mirror wrap)
uniform float alpha_floor : hint_range(0.0, 0.5) = 0.0;
uniform float edge_fade : hint_range(0.0, 1.0) = 0.0;
uniform float edge_from : hint_range(0.0, 1.0) = 0.62;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	mat4 mv = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = mv * mat4(
		vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
		vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
		vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	vec4 t = texture(tex, UV);
	ALBEDO = to_linear(mix(COLOR.rgb, vec3(1.0), t.r));
	float a = alpha_floor > 0.0 ? clamp((t.a - alpha_floor) / (1.0 - alpha_floor), 0.0, 1.0) : t.a;
	if (edge_fade > 0.0) {
		a *= 1.0 - edge_fade * smoothstep(edge_from, 1.0, length(UV - vec2(0.5)) * 2.0);
	}
	ALPHA = a * COLOR.a;
}
