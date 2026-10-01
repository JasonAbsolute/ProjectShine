// ms_m_awa / ms_m_awa_s (BSP1): Billboard, blend SRCALPHA + ONE, z test on / no write; colour = lerp(env -> prm, TEXC), alpha = TEXA x prmA
// x the ESP1 alpha (COLOR.a from the MultiMesh). The I8 texture is both TEXC and TEXA. The quad faces the camera and keeps the instance's
// X / Y scale (Godot's billboard_keep_scale), so the wobble shows.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled;

uniform sampler2D tex : source_color, filter_linear, repeat_disable;
uniform vec3 env_color : source_color = vec3(0.529, 0.718, 0.906); // (135, 183, 231)
uniform vec3 prm_color : source_color = vec3(1.0, 1.0, 1.0);

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
	float i = texture(tex, UV).r;
	ALBEDO = mix(env_color, prm_color, i);
	ALPHA = i * COLOR.a;
}
