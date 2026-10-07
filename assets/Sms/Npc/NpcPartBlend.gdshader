// SMS NPC part, blended by the texture's alpha with no depth write (the glasses' _megane_mat, the Toad's _mat_sunglass: blend SRCALPHA /
// INVSRCALPHA, z write off). See NpcPartCommon.gdshaderinc.
shader_type spatial;
render_mode cull_disabled, specular_disabled, diffuse_lambert, blend_mix, depth_draw_never;
#include "res://assets/Sms/Npc/NpcPartCommon.gdshaderinc"

void vertex() {
	vec3 lit0; vec3 spec;
	part_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), COLOR.rgb, lit0, spec);
	v_lit0 = lit0;
	v_spec = spec;
}

void fragment() {
	vec4 c = part_color(UV);
	ALBEDO = c.rgb;
	ALPHA = c.a;
}
