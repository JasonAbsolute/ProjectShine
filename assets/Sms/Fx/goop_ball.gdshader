// ms_mnt_yogore_b / _f, ms_kino_yogore, ms_mnt_wash, ms_mare_wash (BSP1): Billboard, blend SRCALPHA + INVSRCALPHA, alphaCmp GEQUAL 190,
// z test + WRITE (an opaque goop ball with a cut edge); colour = lerp(env -> prm, TEXC), alpha = TEXA x prmA. env / prm are the STAGE's
// pollution colours (SMSSetEmitterPolColor 6: Delfino's (98,14,0) -> (181,132,102); the other maps' rows are in EmitterViewObj.cpp 644).
// The texture is the raw IA4 ball (I in .r, A in .a); the mix runs in display space like the GX and is converted back for Godot.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;
uniform vec3 env_color = vec3(0.3843, 0.0549, 0.0);     // (98, 14, 0) / 255, display space
uniform vec3 prm_color = vec3(0.7098, 0.5176, 0.4);     // (181, 132, 102) / 255
uniform float alpha_ref = 0.745;                         // 190 / 255

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
	ALBEDO = to_linear(mix(env_color, prm_color, t.r));
	ALPHA = t.a * COLOR.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_ref;
}
