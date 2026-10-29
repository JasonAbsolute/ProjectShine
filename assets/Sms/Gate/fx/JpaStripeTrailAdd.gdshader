// A JPA SSP1 Stripe drawn as a Godot GPU particle TRAIL (SMS pass 127c: ms_gatewind_a / a2 / a3's children behind each wind head). The
// RibbonTrailMesh runs V along the ribbon (0 at the head, 1 at the tail) and U across it, as JPADrawExecStripe lays the children's strip;
// colour = lerp(env, prm, I) of the raw I texture, alpha = I x prm.a x the particle's alpha (COLOR.a: the ESP1 fade, inherited from the head),
// blended SRCALPHA / ONE like the parent, no z write. GX colours are display-referred: the mix is done raw and the result put to linear.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D tex : filter_linear, repeat_enable;           // raw I: r = intensity, a = intensity (P_msm_smash2_i)
uniform vec4 prm = vec4(0.0, 1.0, 0.7843, 1.0);                 // colorPrm (0, 255, 200): the ribbon's bright cyan
uniform vec4 env = vec4(0.0, 0.0, 1.0, 1.0);                    // colorEnv (0, 0, 255): its blue
uniform float tail_fade = 1.0;                                  // 1 = the strip fades to nothing at its tail (a Godot nicety: the game's children die all at once)
uniform float head_gate = 4.0;                                  // 127g: the strip shows at FULL strength while the head is alive (SSP1 inherit alp = False:
                                                                // the children keep their own alpha, not the head's 0.5 fade); COLOR.a x this, clamped, only
                                                                // hides the ribbon in the head's dead frames and eases it out over the head's last fade

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	vec4 t = texture(tex, UV);
	ALBEDO = to_linear(mix(env.rgb, prm.rgb, t.r));
	ALPHA = t.a * prm.a * clamp(COLOR.a * head_gate, 0.0, 1.0) * mix(1.0, 1.0 - UV.y, tail_fade);
}
