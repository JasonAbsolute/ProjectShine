// SMS pass 150 -- the Chain Chomp (bwanwan_body.bmd / bwanwan_chain.bmd / bwanwan_picket.bmd): the boss's three-stage TEV on the enemy toon
// lighting (EnemyToon.gdshaderinc: COLOR0 = the sun's toon light, COLOR1 = its specular). The GX, in display space, every stage clamped:
//   stage 0 (TEXC toon, RASC, KONST k, C0) ADD, bias ZERO, scale / 2:        s0 = 1/2 (c0 + mix(toon, COLOR0, k))        (bw_form 0 / 2; k 5/8)
//           ... bias SUBHALF (the teeth, k 3/4):                               s0 = 1/2 (c0 + mix(toon, COLOR0, k) - 1/2)  (bw_form 1)
//   stage 1 (C2, ONE, TEXC, CPREV) ADD SUBHALF / 2:                           s1 = 1/2 (s0 + mix(c2, 1, TEX) - 1/2)        (bw_form 0 / 2)
//           TEX = N_bwanwan_body, a 16 x 16 palette: black for the body (-> c2, the HEAT: (250, 0, 0) hot, (0, 0, 60) cold), its red / gold /
//           white corners for the lips, the scar and the teeth's bases; the picket's N_bwanwan_kui (grey 140 / dark)
//           the teeth (C2, C1, TEXC, CPREV) SUBHALF / 2:                       s1 = 1/2 (s0 + mix(c2, c1, TEX) - 1/2)       (bw_form 1)
//   stage 2 (ZERO, C1, TEXC env, CPREV) ADD:                                  s2 = s1 + c1 x ENV                           (bw_form 0)
//           ENV = N_mat_spe1 (a soft white spot) through texgen NRM / TEXMTX1 ENVMAP_OLD -- the view-space normal mapped (0.5 n.x + 0.5,
//           -0.5 n.y + 0.5), then the matrix's SRT about (0.5, 0.5): the glint in the heat colour c1 ((250, 120, 40) hot, (0, 30, 80) cold)
//           the teeth (ZERO, HALF, TEXC env, CPREV):                           s2 = s1 + 1/2 ENV (N_bwanwan_env2, the stripes)  (bw_form 1)
//   the mouth (_mat_kuti) has no stage 2 (bw_form 2): c2 (150, 0, 0) at rest, the brk's 50 cold / 250 hot.
// c0 (200, 200, 200) is constant; c1 / c2 are written by SmsBossWanwan from the brk keys (bwanwan_body.brk by the hit points, the gold brk
// after the bath; the chain's by each link's temperature). Opaque; the bmds cull NONE for the body (cull_disabled here), BACK for the teeth.
shader_type spatial;
render_mode cull_disabled, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

uniform int bw_form = 0;                        // 0 the body / root / scar / chain / picket, 1 the teeth, 2 the mouth
uniform sampler2D env_tex : filter_linear, repeat_disable;   // texmap1: N_mat_spe1 (the glint) / N_bwanwan_env2 (the teeth)
uniform vec2 env_scale = vec2(1.0, 1.0);        // TEXMTX1 scale
uniform vec2 env_trans = vec2(0.0, 0.0);        // TEXMTX1 translation
uniform float env_rot = 0.0;                    // TEXMTX1 rotation, degrees

varying vec3 v_nrm;

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	enemy_light(n, normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec; v_nrm = n;
}

vec3 env_color() {
	vec3 n = normalize(v_nrm);
	vec2 p = vec2(0.5 * n.x, -0.5 * n.y);      // the ENVMAP projection about the centre
	float r = radians(env_rot);
	vec2 q = vec2(p.x * cos(r) - p.y * sin(r), p.x * sin(r) + p.y * cos(r)) * env_scale + vec2(0.5) + env_trans;
	return texture(env_tex, q).rgb;
}

void fragment() {
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	vec3 tex = texture(base_tex, UV).rgb;
	vec3 s0, prev;
	if (bw_form == 1) {
		s0 = clamp(0.5 * (c0 + mix(toon, v_lit0, s0_k) - 0.5), 0.0, 1.0);
		prev = clamp(0.5 * (s0 + mix(c2, c1, tex) - 0.5), 0.0, 1.0);
		prev = clamp(prev + 0.5 * env_color(), 0.0, 1.0);
	} else {
		s0 = clamp(0.5 * (c0 + mix(toon, v_lit0, s0_k)), 0.0, 1.0);
		prev = clamp(0.5 * (s0 + mix(c2, vec3(1.0), tex) - 0.5), 0.0, 1.0);
		if (bw_form == 0) prev = clamp(prev + c1 * env_color(), 0.0, 1.0);
	}
	ALBEDO = to_linear(prev);
}
