// Pass 136: the GOROGORO's body (bosspaku_head.bmd material _body1 -- Petey Piranha's head under a coat of the stage's goop; igaiga.cpp
// TGorogoro::init swaps the dummy sheet M_dummy for /scene/map/pollution/H_ma_rak.bti, the stage's goop mark). The bmd's TEV (bmd_mat_dump.py):
//   stage 0: REG2 = (yogore2.a > K0.a) ? 1 : 0           the DIRT MASK: the dirt sheet's alpha against the konst alpha K0.a (TEXMTX1: S 1.86, T 1.07 on UV2)
//   stage 1: PREV = pakkun_head x REG2                    Petey's own skin where the mask passes (UV)
//   stage 2: PREV = PREV + goop x (1 - REG2)              the goop sheet elsewhere (TEXMTX2: S 2, T 2 on UV2)
//   stage 3: REG1 = C0 + mix(toon(lit), RASC, 5/8) - 1/2  the toon term (unused by stage 4 as the bmd stands)
//   stage 4: PREV = PREV + mix(C1, RASC1, 1/2) - 1/2      a flat lift by C1 / 2
// K0.a is the WASH: TGorogoro::behaveToWater sets it to hitPoints x 255 / maxHitPoints (255 at birth -> nothing passes the mask -> all goop; the
// hose takes hit points off and the goop washes away along the dirt sheet's pattern, down to 1 / 255 -- the head never fully cleans and never dies
// of water). Here: `wash` = that fraction; the body = goop where the dirt sheet's alpha <= wash, Petey's skin where it is above; lit by the project's
// enemy toon (EnemyToon.gdshaderinc: ambient 128 + the sun, the H_toon_2_4i ramp) instead of the bmd's stage 3 / 4 oddity (logged). The goop sheet
// takes the gatekeeper's knobs (goop_colour / goop_colour_mode: Texture (the game) / Tint / Recolour) so the head matches the gate it came out of.
shader_type spatial;
render_mode cull_back, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

group_uniforms Goop_colour;
uniform sampler2D goop_texture : filter_linear, repeat_enable;        // the stage's goop mark (goop_mark_bianco.png = Bianco's h_ma_rak.bti)
uniform vec4 goop_colour : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform int goop_colour_mode : hint_enum("Texture (the game)", "Tint", "Recolour") = 0;
uniform vec2 goop_uv_scale = vec2(2.0, 2.0);                            // TEXMTX2
group_uniforms Wash;
uniform float wash : hint_range(0.0, 1.0) = 1.0;                        // K0.a: the hit points' fraction -- 1 = all goop, 0 = Petey's skin
uniform sampler2D dirt_mask : filter_linear, repeat_enable;             // yogore2 (I8 128x128): the wash pattern
uniform vec2 dirt_uv_scale = vec2(1.86035, 1.07422);                    // TEXMTX1
uniform sampler2D head_texture : filter_linear, repeat_disable;         // pakkun_head (RGB5A3 128x128): Petey's skin
group_uniforms;

varying vec3 v_lit;

vec3 goop_rgb(vec2 uv) {
	vec3 sheet = texture(goop_texture, uv).rgb;
	vec3 tint = goop_colour.rgb;
	if (goop_colour_mode == 1) return sheet * tint;
	if (goop_colour_mode == 2) {
		float l = dot(sheet, vec3(0.299, 0.587, 0.114));
		return tint * clamp(l / 0.55, 0.3, 1.3);
	}
	return sheet;
}

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	enemy_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit = lit0;
}

void fragment() {
	vec2 uv2m = (UV2 - 0.5) * dirt_uv_scale + 0.5;
	vec2 uv2g = (UV2 - 0.5) * goop_uv_scale + 0.5;
	float dirt_a = texture(dirt_mask, uv2m).r;                           // the I8 sheet: intensity = alpha
	float skin = dirt_a > wash ? 1.0 : 0.0;                              // stage 0: COMP_GR16_GT
	vec3 base = mix(goop_rgb(uv2g), texture(head_texture, UV).rgb, skin);   // stages 1 + 2
	float ramp = texture(toon_tex, vec2(clamp(v_lit.r, 0.0, 1.0), 0.5)).r; // the toon ramp read by the lit value (stage 3's SRTG lookup)
	ALBEDO = to_linear(clamp(base * ramp, 0.0, 1.0));
}
