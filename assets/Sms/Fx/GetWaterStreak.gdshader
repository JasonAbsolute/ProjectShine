// ms_itemget1_b (the water-get sparkle), the parent streaks: GX TEV lerp(env -> prm, TEXC), alpha = TEXA x prmA, blend SRCALPHA / ONE
// (additive), z test without z write. prm and env are each a 10-frame key pair the colour calc samples at a RANDOM phase per particle
// (JEFFjpa1 colour calc Random): COLOR.r carries that phase (the process material's color_initial_ramp, 0..1) and COLOR.a the ESP1 alpha
// curve (color_ramp). GX colours are display-referred: the lerps run raw and the result is put to linear. The quad is a DirectionCross
// bar (particle_flag_align_y: Y along the velocity), drawn as the mesh is -- no billboarding.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D glow : filter_linear, repeat_disable;          // P_msm_glow3_i (I8): the intensity in r
uniform vec4 prm0 = vec4(0.5294, 1.0, 1.0, 0.6078);              // prm key 0 (135, 255, 255, 155) raw 8-bit / 255
uniform vec4 prm1 = vec4(0.7176, 1.0, 1.0, 0.7020);              // prm key 1 (183, 255, 255, 179)
uniform vec3 env0 = vec3(0.0, 0.7176, 1.0);                      // env key 0 (0, 183, 255)
uniform vec3 env1 = vec3(0.0, 0.4980, 0.5294);                   // env key 1 (0, 127, 135)

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	float i = texture(glow, UV).r;
	float t = COLOR.r;
	vec4 prm = mix(prm0, prm1, t);
	vec3 env = mix(env0, env1, t);
	ALBEDO = to_linear(mix(env, prm.rgb, i));
	ALPHA = i * prm.a * COLOR.a;
}
