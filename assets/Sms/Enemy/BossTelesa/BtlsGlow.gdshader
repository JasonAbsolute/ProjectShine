// ms_btls_glow (SMS pass 153b): King Boo's soft light, emitAndBindToPosPtr at joint 1 every tick he shows -- a camera-facing additive quad (the port's
// glow_add.gdshader's billboard, the radial gradient's alpha), coloured pale blue-white, x `strength` (the boss sets it from his opacity: a faint ghost
// glows faintly).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear_mipmap, repeat_disable;
uniform vec3 prm_color = vec3(0.75, 0.85, 1.0);
uniform float strength : hint_range(0.0, 2.0) = 0.6;
uniform float pulse = 0.12;                                  // a slow breathing of the light (0 = steady)

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	mat4 world = mat4(normalize(INV_VIEW_MATRIX[0]) * length(MODEL_MATRIX[0].xyz), normalize(INV_VIEW_MATRIX[1]) * length(MODEL_MATRIX[1].xyz),
	                  normalize(INV_VIEW_MATRIX[2]) * length(MODEL_MATRIX[2].xyz), MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = VIEW_MATRIX * world;
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	float a = texture(tex, UV).a;
	float breath = 1.0 + pulse * sin(TIME * 2.4);
	ALBEDO = to_linear(prm_color);
	ALPHA = a * strength * breath;
}
