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
// 153e: the four parts share this include; the two .gdshader files over it differ in ONE word -- the body / fangs / mouth cull_back, the crown
// cull_disabled (btelesa.bmd's MAT3: _mat_crown cull NONE, the other three BACK -- cull153.py): the inside of the crown shows when you look down into it.
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/BossTelesa/BossTelesaToon.gdshaderinc"
