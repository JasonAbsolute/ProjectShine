// SMS pass 150 -- the Chain Chomp's eyes (bwanwan_body.bmd '_mat_me'): a translucent decal over the eye sockets, z-tested, no z write,
// SRCALPHA / INVSRCALPHA. stage 0 (TEXC toon, RASC, KONST 5/8, C0) SUBHALF x1: s0 = c0 + mix(toon, COLOR0, 5/8) - 1/2 (c0 (200, 200, 200));
// stage 1 (CPREV, RASC1 spec, KONST 1/2, TEXC) SUBHALF x1: TEX + mix(s0, spec, 1/2) - 1/2, the alpha (ZERO, A0, TEXA, ZERO) = the picture's.
// The picture is swapped by the btps: N_bwanwan_me_open at rest, bwanwan_tdown.btp in the bath (the blinks, then the X eyes) -- SmsBossWanwan
// writes base_tex (open / close / cross).
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	enemy_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
}

void fragment() {
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	vec4 tex = texture(base_tex, UV);
	vec3 s0 = clamp(c0 + mix(toon, v_lit0, s0_k) - 0.5, 0.0, 1.0);
	vec3 prev = clamp(tex.rgb + mix(s0, v_spec, 0.5) - 0.5, 0.0, 1.0);
	ALBEDO = to_linear(prev);
	ALPHA = tex.a;
}
