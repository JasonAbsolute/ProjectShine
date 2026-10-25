// SMS small-enemy toon part, opaque, back-face culled (the Stu's nose and feet: cull BACK, blend NONE, no fog). See EnemyToon.gdshaderinc.
// unshaded: the GX lighting is in the TEV result, as NpcSkin / GoobleBody do it.
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
}
