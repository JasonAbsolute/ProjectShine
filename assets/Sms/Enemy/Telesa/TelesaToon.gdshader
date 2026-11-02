// SMS pass 151: the Boo's materials (telesa.bmd _mat_body / _mat_eye / _mat_kiba / _mat_mouth; telesablock.bmd _mat_telesaBlock / _eye share it).
// The bmd's TEV (4 stages, from the rip's _mat_*.fragment.glsl, the MAT3 colours all white): X = clamp((C0 + TOON x (1 - 3/4) + LIT x 3/4 - 1/2) / 2)
// and the colour is mix(MAT x (LIGHT1 + AMB1), TEX, X) -- MAT the material colour (white), LIGHT1 the second channel (the sun's glint, dark but for the
// highlight), C0 = GX_TEVREG0 = mTelesaBaseColor (S10, OVER 255: (350, 350, 350) the white Boo, (600, 270, 220) the pink box Boo). The white Boo's X
// sits at ~0.9 so it is its white texture, pulled toward the dark glint channel in the toon's shade; the pink C0 saturates X in red and leaves green
// and blue at 0.73 / 0.63 -- the salmon pink is the dark channel showing through. Every material is multiplied by K0_A = mTelesaFadeColor.a: the
// ghost's FADE (0 invisible .. 1 solid) -- `fade` here, set by SmsTelesa each tick. The eye / fangs / block eye alpha-test their texture (GEQUAL 128).
// The block: its C0 is the juice block's unk138, never set for the Boo's block (0), so X ~ 0.2 and the slab is 0.8 of its flat term -- given the pink
// here through mat_color + amb1 (the player's pink block). DEVIATION: a fading Boo is LIGHT_TYPE_INDIRECT in the game -- its body drawn with the
// screen texture (H_ma_rak_dummy swapped for it), a see-through refraction; here the fade is plain alpha.
// 153c: depth_draw_always -- a shader that writes ALPHA renders in the transparent pass, where depth_draw_opaque writes NO depth: the Boo's
// mouth, eyes and fangs were drawn over its body from behind. With depth written, the surfaces occlude each other in order.
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

uniform float fade : hint_range(0.0, 1.0) = 1.0;   // mTelesaFadeColor.a / 255
uniform bool cutout = false;                        // the eye / the fangs: the texture's alpha, GEQUAL 128
uniform float tex_k = 0.75;                         // the stage konst 3/4
uniform vec3 mat_color = vec3(1.0);                 // GxMaterialColor0 (white in both bmds)
uniform vec3 amb1 = vec3(0.0);                      // the second channel's ambient (dark for the Boo; 1 for the block's flat pink)

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	enemy_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
}

void fragment() {
	vec4 t = texture(base_tex, UV);
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	vec3 x = clamp((c0 + toon * (1.0 - tex_k) + v_lit0 * tex_k - 0.5) * 0.5, 0.0, 1.0);
	vec3 flat_c = mat_color * clamp(v_spec + amb1, 0.0, 1.0);
	vec3 col = clamp(mix(flat_c, t.rgb, x), 0.0, 1.0);
	if (cutout && t.a < 0.5) discard;
	ALBEDO = to_linear(col);
	ALPHA = fade;
}
