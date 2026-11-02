// SMS pass 153: King Boo's materials (btelesa.bmd _mat_body / _mat_crown / _mat_ha / _mat_mouth). The body's TEV from the rip's _mat_body.fragment.glsl:
// colour = TEX0 + (clamp(C0 + TOON x (1 - k) + LIT x k - 1/2) - 1/2) / 2 + MAT x (LIGHT2 + AMB1) / 2 - 1/2 with k = K0 (TBossTelesa::init writes K0 = unk34C =
// mNormalAlpha 0x96 on every channel: the mix konst 150 / 255 AND the alpha); the alpha = MAT_A x (light0.a + amb_a) x K0_A -- the ghost at 150 / 255, more
// solid per hit point lost (+30), solid while he is hit. TEX0 of the BODY is the SCREEN: init swaps the screen texture into H_ma_rak_dummy and sets
// LIGHT_TYPE 3 -- King Boo is a dark see-through refraction of the casino behind him with the toon's highlights (Sunshine's King Boo is the dark
// translucent one). Here `screen_tex` (hint_screen_texture) is that TEX0 at `screen_k`, the body sheet (N_btelesa_body: his face) laid over it at
// `sheet_k`; the crown / fangs / mouth use their own sheet (screen_k 0). K1 = unk348 is the body's tint: white, or the colour of the fruit that hit him
// (coconut pink, papaya / pineapple orange, durian green) -- `tint`. The fangs / mouth use a (76, 108, 97) konst instead of K0 and a second texture
// (the sheen); the crown adds an env map (N_btelesa_env2 through the normal) -- plain toon here. The brk flashes drive `c0` (SmsBossTelesa).
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

uniform float fade : hint_range(0.0, 1.0) = 0.588;   // unk34C.a / 255 (0x96 = 150 normal)
uniform vec3 tint = vec3(1.0);                        // K1 = unk348: the fruit tint
uniform float mix_k = 0.588;                          // K0.rgb / 255: the lit mix konst (the body); 0.298 / 0.424 / 0.380 for the fangs / mouth
uniform vec3 mat_color = vec3(1.0);                   // GxMaterialColor0 (white)
uniform vec3 amb1 = vec3(0.0);                        // the second channel's ambient (dark: the glint channel)
uniform bool cutout = false;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear, repeat_disable;   // the body's TEX0: the screen (LIGHT_TYPE 3)
uniform float screen_k : hint_range(0.0, 1.0) = 0.0; // 1 = TEX0 is the screen behind him (the body), 0 = the sheet (the crown, fangs, mouth)
uniform float sheet_k : hint_range(0.0, 1.0) = 0.45; // how much of the body sheet (his face) shows over the screen
uniform float screen_lift = 0.3;                      // DEVIATION: the game's refraction is a camera copy at full brightness; the screen texture here is darker -- lifted a little

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	enemy_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
}

void fragment() {
	vec4 t = texture(base_tex, UV);
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	vec3 shade = clamp(c0 + toon * (1.0 - mix_k) + v_lit0 * mix_k - 0.5, 0.0, 1.0);           // the lit shade term (C0 = 0 but for the brk flashes)
	vec3 glint = mat_color * clamp(v_spec + amb1, 0.0, 1.0);
	vec3 scr = pow(max(texture(screen_tex, SCREEN_UV).rgb, vec3(0.0)), vec3(1.0 / 2.2)) + vec3(screen_lift);   // the screen, in the sheet's gamma
	vec3 base = mix(t.rgb, mix(scr, t.rgb, sheet_k), screen_k);
	vec3 col = clamp(base + (shade - 0.5) * 0.5 + glint * 0.5 - 0.5, 0.0, 1.0);
	col = clamp(col * tint, 0.0, 1.0);
	if (cutout && t.a < 0.5) discard;
	ALBEDO = to_linear(col);
	ALPHA = fade;
}
