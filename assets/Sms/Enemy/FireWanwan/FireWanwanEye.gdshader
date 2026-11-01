// SMS pass 149 -- the Chain Chomplet's eye (wanwan.bmd '_mat_eye'): the pupil decal (K_wanwan_eye, IA: alpha-tested at 128 after
// (ZERO, TEXA, APREV, ZERO) x4) over the eyeball, its texture matrix driven by wanwan_eye.btk -- a five-frame squash and stretch of the pupil
// about the texture's centre (0.5, 0.5): X 1 / 1.3 / 1 / 0.9 / 1, Y 1 / 0.9 / 1 / 1.3 / 1 -- while the Chomplet is cooled (uv_scale, set by
// SmsFireWanwan each tick). The colour is the enemy toon (EnemyToon.gdshaderinc, form 0) with C0 (400, 404, 424).
shader_type spatial;
render_mode cull_back, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

uniform vec2 uv_scale = vec2(1.0, 1.0);     // the btk's texture scale about the centre (1 = the still eye)

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	enemy_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
}

void fragment() {
	vec2 uv = (UV - vec2(0.5)) * uv_scale + vec2(0.5);
	ALBEDO = enemy_color(uv, UV2);
	ALPHA = clamp(texture(base_tex, uv).a * v_lita * 4.0, 0.0, 1.0);
	ALPHA_SCISSOR_THRESHOLD = 0.5;
}
