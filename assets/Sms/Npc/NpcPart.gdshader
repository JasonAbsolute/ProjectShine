// SMS NPC part, opaque (the hats' bodies, belts, moustache, lapel, tie, luggage, arrows, the flower's centre). See NpcPartCommon.gdshaderinc.
shader_type spatial;
render_mode cull_disabled, specular_disabled, diffuse_lambert;
#include "res://assets/Sms/Npc/NpcPartCommon.gdshaderinc"

void vertex() {
	vec3 lit0; vec3 spec;
	part_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), COLOR.rgb, lit0, spec);
	v_lit0 = lit0;
	v_spec = spec;
}

void fragment() {
	ALBEDO = part_color(UV).rgb;
}
