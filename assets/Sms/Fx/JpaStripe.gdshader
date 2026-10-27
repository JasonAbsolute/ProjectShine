// A JPA STRIPE's ribbon (pass 126e: ms_gkpa_yodare_l / _s, the gatekeeper's drool -- SmsGoopStrand builds the mesh): the texture runs along the
// strip (V: 0 at the mouth, 1 at the tip, as JPADrawExecStripeCross), U across it; the texture is CLAMPED (the BTI), so it maps once. Colour as JPA's colour in-select 0: lerp(env, prm, I) of the
// raw IA texture; alpha = A x prm.a; the GX alpha compare GEQUAL 192 is the alpha scissor; z written (the stripe is a solid thing). GX colours
// are display-referred: the mix is done raw and the result put to linear.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, unshaded, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_disable;          // raw IA: r = intensity, a = alpha (P_msm_yodare_ia: BTI wrap CLAMP -- the teardrop maps once along the strand)
uniform vec4 prm = vec4(0.7098, 0.5176, 0.4, 1.0);              // colorPrm: SMSSetEmitterPolColor's light goop colour (Bianco's (181, 132, 102))
uniform vec4 env = vec4(0.3843, 0.0549, 0.0, 1.0);              // colorEnv: its dark one ((98, 14, 0))
uniform float alpha_ref = 0.7529;                               // GEQUAL 192

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	vec4 t = texture(tex, UV);
	ALBEDO = to_linear(mix(env.rgb, prm.rgb, t.r));
	ALPHA = t.a * prm.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_ref;
}
