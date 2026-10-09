// ms_moe_fire_b (the burning NPC's glow, BSP1): Billboard, blend SRCALPHA + ONE (additive), z test on / no write; colour = prm FLAT
// (255,172,101) -- the texture only shapes the alpha: TEXA (= I of the I8 radial gradient) x prmA x the ESP1 fade (COLOR.a). The texture is
// the raw I8 (I in .r and .a). Pass 99: a BILLBOARD (pass 98 left the quad flat in the emitter's XY).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear_mipmap, repeat_disable;
uniform vec3 prm_color = vec3(1.0, 0.675, 0.396);        // (255, 172, 101) / 255

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	// the billboard: the view's axes at the particle's position, the particle's scale kept, rolled by its angle (StandardMaterial3D's
	// BILLBOARD_PARTICLES does the same with CUSTOM.x = the ESP1 rotate)
	float a = INSTANCE_CUSTOM.x;
	mat4 world = mat4(normalize(INV_VIEW_MATRIX[0]) * length(MODEL_MATRIX[0].xyz), normalize(INV_VIEW_MATRIX[1]) * length(MODEL_MATRIX[1].xyz),
	                  normalize(INV_VIEW_MATRIX[2]) * length(MODEL_MATRIX[2].xyz), MODEL_MATRIX[3]);
	world = world * mat4(vec4(cos(a), -sin(a), 0.0, 0.0), vec4(sin(a), cos(a), 0.0, 0.0), vec4(0.0, 0.0, 1.0, 0.0), vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_MATRIX = VIEW_MATRIX * world;
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	float a = texture(tex, UV).a;
	ALBEDO = to_linear(prm_color);
	ALPHA = a * COLOR.a;
}
