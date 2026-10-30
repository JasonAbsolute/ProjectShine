// SMS pass 130 -- the Bob-omb's materials (nejibomb_model1.bmd / downnejibomb_model1.bmd, bmd_mat_dump.py), evaluated in display space the
// way the GX does and converted back to linear. No toon here: plain TLightCommon lighting (COLOR0 = clamp(amb (128) + sun x N.L), COLOR1 = the
// sun as a GX_AF_SPEC light, shininess 50 -- enemy_light of EnemyToon.gdshaderinc).
//   mode 0 '_body1':    stage 0 TEX x COLOR0, stage 1 (RASC1, CPREV, ONE, RASC1) = + COLOR1 (the glint)       -- the black body with the face
//   mode 1 '_led1':     chan0 unlit (COLOR0 = matColor 1): TEX, stage 1 + TEX1 (Q_nejibomb_kankyo through a NRM texgen: a sphere map by the
//                       view-space normal) -- the LED display (the texture is swapped by the btp: the 3-2-1 count, the doused face)
//   mode 2 '_rleg1(2)': TEX x COLOR0 + the sphere map
//   mode 3 '_rleg1', '_rleg1(3)': TEX x COLOR0
//   mode 4 '_mat3' .. '_mat7' (the fragments): matColor BLACK (0, 0, 0) x the light = black, the vertex-colour ones (matSrc VTX) their colour
//                       x COLOR0 -- the exploded pieces.
shader_type spatial;
render_mode cull_back, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

uniform int mode = 0;
uniform sampler2D env_tex : filter_linear, repeat_disable;   // Q_nejibomb_kankyo_s3tc (I8 32x32: a vertical gradient)
uniform float env_gain = 1.0;                                // x the sphere-map term (1 = the game)
varying vec2 v_env;
varying vec4 v_col;

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	enemy_light(n, normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
	v_env = n.xy * 0.5 + 0.5;          // GX_TG_NRM through TEXMTX1: the view-space normal mapped onto the sphere map
	v_col = COLOR;
}

void fragment() {
	vec3 tex = texture(base_tex, UV).rgb;
	vec3 prev;
	if (mode == 0) prev = clamp(tex * v_lit0 + v_spec, 0.0, 1.0);
	else if (mode == 1) prev = clamp(tex + env_gain * texture(env_tex, v_env).rgb, 0.0, 1.0);
	else if (mode == 2) prev = clamp(tex * v_lit0 + env_gain * texture(env_tex, v_env).rgb, 0.0, 1.0);
	else if (mode == 3) prev = clamp(tex * v_lit0, 0.0, 1.0);
	else if (mode == 4) prev = clamp(v_col.rgb * v_lit0, 0.0, 1.0);
	else prev = vec3(0.0);             // matSrc REG, matColor (0, 0, 0): black whatever the light
	ALBEDO = to_linear(prev);
}
