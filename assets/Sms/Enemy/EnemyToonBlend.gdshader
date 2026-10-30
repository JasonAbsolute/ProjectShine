// SMS small-enemy toon part, alpha-BLENDED (pass 128: the Cataquack's eye, poihana/default.bmd '_eye' -- mode 4 translucent, blend SRCALPHA /
// INVSRCALPHA, z test on, z write OFF, alphaCmp ALWAYS; alpha = TEXA of M_poihana_eye (the eye's shape) x K0_A (1)). See EnemyToon.gdshaderinc,
// form 3. Drawn after the opaque body it decals (RenderPriority on the material).
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	enemy_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
}

void fragment() {
	ALBEDO = enemy_color(UV, UV2);
	ALPHA = texture(base_tex, UV).a;
}
