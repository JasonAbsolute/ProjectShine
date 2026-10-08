// ms_mnt_kira / ms_mare_kira (BSP1): Billboard, blend SRCALPHA + ONE, z test on / no write; colour = lerp(env -> prm, TEXC) with prm WHITE
// and env a per-star RANDOM phase of the 2-key table (255,206,24) -> (255,63,15) (colour calc flag bit 0), alpha = TEXA x prmA x the ESP1
// AddSin twinkle. The ramp feeds env as COLOR.rgb (display-space values) and the twinkle as COLOR.a; the texture is the raw IA8 (I in .r).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;   // star4s_raw: the 64 x 64 star mirrored 2 x 2 (the quad's tiling 2.0 under mirror wrap)

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
	ALPHA = t.a * COLOR.a;
}
