// ms_watcoin_hit (BSP1): Billboard, blend SRCALPHA / INVSRCALPHA, z test on / no write; colour = lerp(env -> prm, TEXC) with prm WHITE and
// env (150, 199, 231) -- a light water blue -- alpha = TEXA x prmA x the ESP1 alpha-out. The texture is P_msm_mizutama1_ia (I in .r, A in .a).
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;
uniform vec4 env : source_color = vec4(0.588, 0.780, 0.906, 1.0);
uniform vec4 prm : source_color = vec4(1.0, 1.0, 1.0, 1.0);

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
	ALBEDO = mix(env.rgb, prm.rgb, t.r);
	ALPHA = t.a * prm.a * COLOR.a;
}
