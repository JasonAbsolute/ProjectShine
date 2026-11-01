// SMS pass 147 -- the Bullet Bill's materials (killer_model1.bmd / downkiller_model1.bmd, bmd_mat_dump.py), evaluated in display space the
// way the GX does and converted back to linear. No toon: plain TLightCommon lighting (COLOR0 = clamp(amb (128) + sun x N.L) -- enemy_light of
// EnemyToon.gdshaderinc). The game drives one GX_TEVREG0 colour per material every frame (SMS_InitPacket_OneTevColor: mBodyColor, mEyesColor,
// mNoseColor, mDownBodyColor -- black for the plain Bill, (200, 185, 0) / (255, 225, 70) gold, (70, 20, 70) the homing one, the nose and eyes
// flashing in search1 and waving in chase1); here it is the tev_color uniform the species writes.
//   part 0 / 1 / 2 ('_body1' / '_eyesmat1' / '_nosemat1', one recipe, three registers):
//       stage 0 (ZERO, TEXC, RASC, C0)  = Q_killer2 x COLOR0 + C0
//       stage 1 (ZERO, C2, TEXC, CPREV) = + C2 (225, 255, 225) x Q_killer_kankyo3_I4 (a 32 x 32 intensity map: 34-51 in its middle, 150-220 at its
//                                         rim) through a NRM texgen in J3D's Envmap mode with TEXMTX1 (scale 0.5, trans -0.039): the sphere map by
//                                         the view-space normal, the normals landing in the dark middle half -- the glint at a glancing angle
//   part 3 ('_bodymat1', the pieces of downkiller_model1, cull NONE, no texture):
//       stage 0 (RASC, ZERO, ZERO, C0)  = the vertex colour x COLOR0 + C0 (matSrc VTX, ambient 128)
shader_type spatial;
render_mode cull_disabled, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

uniform int part = 0;                                        // 0 the body, 1 the eyes, 2 the nose, 3 the pieces
uniform vec3 tev_color = vec3(0.0);                          // C0 (GX_TEVREG0) / 255: black -- the species writes the gold / homing colours
uniform vec3 env_color = vec3(0.882, 1.0, 0.882);            // C2 (GX_TEVREG2): (225, 255, 225)
uniform sampler2D env_tex : filter_linear, repeat_disable;   // Q_killer_kankyo3_I4 (32 x 32: the environment gradient)
uniform float env_gain = 1.0;                                // x the sphere-map glint (1 = the game)
varying vec2 v_env;
varying vec4 v_col;

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	enemy_light(n, normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
	// GX_TG_NRM in J3D's Envmap mode (texmtx info 0x06): the view-space normal onto the texture as n.xy x 0.5 + 0.5, then the material's own SRT about its
	// centre (scale 0.5, trans -0.0390625) -> n.xy x 0.25 + 0.46: the normals land in the gradient's dark middle half (the body stays black; the glint is
	// its lighter ring at a glancing angle). With the plain n.xy x 0.5 the whole body came out pale green.
	v_env = n.xy * 0.25 + 0.5 - 0.0390625;
	v_col = COLOR;
}

void fragment() {
	vec3 prev;
	if (part == 3) prev = clamp(v_col.rgb * v_lit0 + tev_color, 0.0, 1.0);
	else {
		vec3 tex = texture(base_tex, UV).rgb;
		prev = clamp(tex * v_lit0 + tev_color, 0.0, 1.0);
		prev = clamp(prev + env_gain * env_color * texture(env_tex, v_env).rgb, 0.0, 1.0);
	}
	ALBEDO = to_linear(prev);
}
