// A JPA quad drawn in the EMITTER's frame (no billboarding): the particle's transform places it, its scale curve stretches it, its angle
// turns it about its own normal (ParticleProcessMaterial's angle lands in the transform). Colour as JPA's colour in-select 0: lerp(env,
// prm, I) of the raw IA texture; alpha = A x prm.a x COLOR.a; the GX alpha compare is the alpha scissor (alpha_ref). GX colours are
// display-referred: the mix is done raw and the result put to linear. Pass 125c: ms_enm_wallhit_o, the goop splat on the wall.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, unshaded, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;          // raw IA: r = intensity, a = alpha
uniform vec4 prm = vec4(1.0, 0.89, 0.659, 1.0);                 // colorPrm: SMSSetEmitterPolColor's prmarray for the stage (Dolpic's by default)
uniform vec4 env = vec4(0.847, 0.188, 0.376, 1.0);              // colorEnv: its envarray
uniform float alpha_ref = 0.133;                                // the GX alpha compare reference (GREATER 33)

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	vec4 t = texture(tex, UV);
	ALBEDO = to_linear(mix(env.rgb, prm.rgb, t.r));
	ALPHA = t.a * prm.a * COLOR.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_ref;
}
