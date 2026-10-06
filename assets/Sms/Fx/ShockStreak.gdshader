// ms_m_biri_a, the shock streak: a Direction quad (Y = its direction) twisting about its own axis by the particle's angle
// (INSTANCE_CUSTOM.x: the process material's angle + angular velocity x age), as JPADrawExecRotDirectional turns the quad with rot type Y
// before placing it. Colour = the ramp in COLOR (TEXC x prm + env baked), alpha = the texture's x COLOR.a; blend SRCALPHA / ONE.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D streak : source_color, filter_linear_mipmap, repeat_disable;   // P_ms_biri_l1: the streak as alpha

void vertex() {
	float a = INSTANCE_CUSTOM.x;                       // the twist: 540..1620 deg/s about the quad's Y from a random start
	float c = cos(a), s = sin(a);
	VERTEX = vec3(c * VERTEX.x + s * VERTEX.z, VERTEX.y, c * VERTEX.z - s * VERTEX.x);
}

void fragment() {
	vec4 t = texture(streak, UV);
	ALBEDO = COLOR.rgb * t.rgb;
	ALPHA = COLOR.a * t.a;
}
