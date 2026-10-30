// SMS small-enemy toon part, cut out and two-sided (pass 129: the Pokey family's petals and the leaf -- '_flower' / '_mat_flower' / '_mat_leaf1':
// cull NONE, alpha stage (ZERO, TEXA, APREV, ZERO) x4 with APREV = ALPHA0 (the lit alpha), alphaCmp GEQUAL 128: the clear texels are never
// drawn, the petal is, from both sides). See EnemyToon.gdshaderinc.
shader_type spatial;
render_mode cull_disabled, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	enemy_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
}

void fragment() {
	ALBEDO = enemy_color(UV, UV2);
	ALPHA = clamp(texture(base_tex, UV).a * v_lita * 4.0, 0.0, 1.0);
	ALPHA_SCISSOR_THRESHOLD = 0.5;
}
