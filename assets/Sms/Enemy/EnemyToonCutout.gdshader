// SMS small-enemy toon part, cut out (the Stu's eye: _mat_eye1 -- the pupil decal over the body; alpha stage (ZERO, TEXA, APREV, ZERO) x4 with
// APREV = ALPHA0 (the lit alpha), alphaCmp GEQUAL 128: the transparent texels are never drawn, the pupil and its glint are). See EnemyToon.gdshaderinc.
shader_type spatial;
render_mode cull_back, unshaded, fog_disabled;
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
