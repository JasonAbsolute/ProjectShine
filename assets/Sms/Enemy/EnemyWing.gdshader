// SMS small-enemy wing (hanekuri.bmd _mat_Lwing1 / _mat_Rwing1, pass 125f): one TEV stage -- color = TEXC x RASC, alpha = TEXA x RASA -- with chan0
// lit from the VERTEX colour (matSrc VTX, lights 0 + 1, ambient (128, 128, 128)) and the alpha channel unlit (matSrc REG 255), blended
// SRCALPHA / INVSRCALPHA, cull NONE, z write off. K_hane_wing is IA4: every texel white, the shape in its alpha. The texture goes in base_tex.
shader_type spatial;
render_mode blend_mix, cull_disabled, unshaded, fog_disabled, depth_draw_opaque;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	enemy_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
}

void fragment() {
	vec4 t = texture(base_tex, UV);
	vec3 ras = clamp(COLOR.rgb * v_lit0, 0.0, 1.0);          // RASC: the vertex colour under the lights (display space)
	ALBEDO = to_linear(clamp(t.rgb * ras, 0.0, 1.0));
	ALPHA = clamp(t.a * COLOR.a, 0.0, 1.0);                   // RASA = the register alpha 255: the texture's shape
}
