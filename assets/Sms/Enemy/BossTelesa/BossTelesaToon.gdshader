// SMS pass 153 / 153b: King Boo's four materials (btelesa.bmd _mat_body / _mat_crown / _mat_ha / _mat_mouth), rebuilt from the bmd's own MAT3 TEV
// stages (j3d_dump153.py) -- the rip's GLSL had collapsed them. Every one ends with  PREV = lerp(SCREEN, him, K0_A)  (stage N: (C2, CPREV, KONST K0_A,
// ZERO) with C2 = the screen texture written in stage 0): King Boo is not alpha-blended, he is MIXED with the picture behind him by K0 alpha = unk34C.a
// (150 / 255 normal, + 30 a lost hit point, 255 while he is hit) -- `opacity`. The texture named H_ma_rak_dummy is the screen (TBossTelesa::init swaps
// TScreenTexture in; LIGHT_TYPE 3): `screen_tex`. ALPHA = RASA (the lit alpha, ~1): the blend is opaque, the see-through is the mix.
//   BODY (8 stages): REG2 = screen; REG0 = polmask.I (UV1, repeat) > K2_A ? 1 : 0; REG1 = K1 x fluits_dummy (white) = the fruit TINT (unk348, white
//     normally); PREV = EYE (texmap0: N_nbtelesa_eye_open / _half / _close / _roll by the btp) x REG0 + REG1 x (1 - REG0): the body's UV0 sits on the
//     eye sheet's WHITE, so the body is white and the eyes are the eye; raising K2_A (btelesa_damage.brk: 0 -> 150 in 20 frames; _lick.brk: 150 held 39
//     frames then 0 at 99) speckles the body into the fruit's colour through the noise mask -- `mask_k`. REG0 = clamp(5/8 + mix(toon, COLOR0, 5/8) - 1/2)
//     (the lit shade), PREV = PREV + mix(REG0, COLOR1 glint, 1/2) - 1/2 (no clamp: S10), PREV = lerp(screen, PREV, K0_A).
//   CROWN (5): REG2 = screen; PREV = clamp((C0 (255,255,0) + mix(toon, COLOR0, 3/8) - 1/2) / 2); PREV = PREV x SHEET (N_btelesa_body's gold / red strip,
//     UV0); PREV = PREV + env2 / 2 (N_btelesa_env2 through the normal: the sphere-map sheen); lerp(screen, PREV, K0_A).
//   FANGS / MOUTH (4): REG2 = screen; PREV = clamp(5/8 + mix(toon, COLOR0, 5/8) - 1/2); PREV = clamp(SHEET + mix(PREV, COLOR1 glint, 5/8) - 1/2);
//     lerp(screen, PREV, K0_A).
// The toon ramp H_toon_2_4i (136 in shadow, 255 lit) through SRTG = COLOR0, the lighting EnemyToon's (COLOR0 the sun + ambient, COLOR1 the glint).
// The indirect warp (P_ms_indwp3_ia) is bound but every stage's indirect matrix is OFF in the bmd: no wobble. DEVIATION: Godot's screen texture is the
// opaque pass -- another see-through thing behind him (a Boo attacker) is not in his mix.
// 153c: depth_draw_always -- a material that reads the screen renders in the transparent pass, where depth_draw_opaque writes NO depth: his mouth and
// tongue were drawn over his head from behind. With depth written, the four surfaces occlude each other as the solid things they are.
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

uniform int part = 0;                                       // 0 the body, 1 the crown, 2 the fangs / the mouth
uniform float opacity : hint_range(0.0, 1.0) = 1.0;        // K0_A = unk34C.a / 255: the screen mix (0 = the picture behind him, 1 = solid); the game 0.588
uniform vec3 tint = vec3(1.0);                              // K1 = unk348: the fruit tint (the body; white normally)
uniform float mask_k : hint_range(0.0, 1.0) = 0.0;         // K2_A: the polmask threshold the brk animates (0 = all eye sheet / white)
uniform sampler2D env_tex : filter_linear, repeat_disable;  // N_btelesa_env2: the crown's sphere map
uniform vec3 crown_c0 = vec3(1.0, 1.0, 0.0);                // _mat_crown's TEVREG0 (255, 255, 0)
uniform sampler2D screen_tex : hint_screen_texture, filter_linear, repeat_disable;   // H_ma_rak_dummy = TScreenTexture
uniform float screen_gain = 1.0;                            // DEVIATION knob: x the picture behind him in his mix (1 = the game's copy as is)

varying vec3 v_nrm_view;

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	enemy_light(n, normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
	v_nrm_view = n;
}

void fragment() {
	vec3 t = texture(base_tex, UV).rgb;                                                       // the eye sheet (body) / N_btelesa_body (crown, fangs, mouth)
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;                                             // SRTG: the ramp at COLOR0
	vec3 scr = pow(max(texture(screen_tex, SCREEN_UV).rgb, vec3(0.0)), vec3(1.0 / 2.2)) * screen_gain;   // REG2: the screen, in the TEV's display space
	vec3 him;
	if (part == 0) {
		float mask_i = texture(pol_mask, UV2 * pol_scale + pol_offset).r;                     // I4: TEXA = the intensity
		float eye_on = mask_i > mask_k ? 1.0 : 0.0;                                           // stage 1: COMP_GR16_GT (TEXA, K2_A) -> REG0
		vec3 base = mix(tint, t, eye_on);                                                     // stages 3 + 4: EYE x REG0 + REG1 (K1 x white) x (1 - REG0)
		vec3 shade = clamp(0.625 + mix(toon, v_lit0, 0.625) - 0.5, 0.0, 1.0);                 // stage 5 -> REG0
		him = base + mix(shade, v_spec, 0.5) - 0.5;                                           // stage 6, unclamped
	} else if (part == 1) {
		vec3 s1 = clamp((crown_c0 + mix(toon, v_lit0, 0.375) - 0.5) * 0.5, 0.0, 1.0);        // stage 1
		vec3 s2 = clamp(s1 * t, 0.0, 1.0);                                                    // stage 2: x the sheet
		vec2 env_uv = v_nrm_view.xy * 0.5 + 0.5;                                              // texgen NRM: the sphere map
		him = clamp(s2 + 0.5 * texture(env_tex, env_uv).rgb, 0.0, 1.0);                      // stage 3: + HALF x env
	} else {
		vec3 s1 = clamp(0.625 + mix(toon, v_lit0, 0.625) - 0.5, 0.0, 1.0);                   // stage 1
		him = clamp(t + mix(s1, v_spec, 0.625) - 0.5, 0.0, 1.0);                              // stage 2: the sheet + mix(shade, glint, 5/8) - 1/2
	}
	vec3 col = clamp(mix(scr, him, opacity), 0.0, 1.0);                                       // the last stage: lerp(screen, him, K0_A)
	ALBEDO = to_linear(col);
	ALPHA = 1.0;
}
