// SMS pass 206 (Jaysonn: "let's experiment with the SMS Boos to replace the Bomb Boos -- swap the model and textures to look like the normal Boos from SMS"):
// the BOMB BOO in the SUNSHINE BOO's body -- the hotel Boo's own toon (TelesaToon.gdshader, 151: the bmd's TEV, the white body pulled toward the dark glint
// channel by C0, the mouth's reds on the same 8 x 8 texture; the eye and the fangs cut out on their alpha) with what the Bomb Boo needs on top:
// `opacity` (SmsBombBoo drives it: 0 under the floor, up to its Opacity; x the material's own `fade`), `tint` (white = the game's Boo; a dark grey = a shadow
// Boo), the fuse's red `flash` (200b: the body mixed toward `flash_color`, pushed past white by `flash_emit` so the blink reads in the dark arena), and
// `flat_tex` / `flat_rgb` for the port's lasso tongue (bomb_boo_tongue.glb, whose UVs mean nothing on this texture: the tongue's orange-red flat).
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, unshaded, fog_disabled;
#include "res://assets/Sms/Enemy/EnemyToon.gdshaderinc"

uniform float fade : hint_range(0.0, 1.0) = 1.0;   // mTelesaFadeColor.a / 255 (the material's own; 1 here)
uniform bool cutout = false;                        // the eye / the fangs: the texture's alpha, GEQUAL 128
uniform float tex_k = 0.75;                         // the stage konst 3/4
uniform vec3 mat_color = vec3(1.0);                 // GxMaterialColor0 (white)
uniform vec3 amb1 = vec3(0.0);                      // the second channel's ambient (dark for the Boo)
uniform float opacity : hint_range(0.0, 1.0) = 1.0;                  // 206: the Bomb Boo's
uniform vec3 tint : source_color = vec3(1.0);                        // 206: white = the game's
uniform float flash : hint_range(0.0, 1.0) = 0.0;                    // 200b / 206: the fuse's blink
uniform vec3 flash_color : source_color = vec3(1.0, 0.05, 0.02);
uniform float flash_emit : hint_range(0.0, 4.0) = 0.8;
uniform bool flat_tex = false;                                       // 206: the lasso tongue -- no texture, this colour
uniform vec3 flat_rgb : source_color = vec3(0.91, 0.235, 0.125);     // the texture's tongue red (232, 60, 32)

void vertex() {
	vec3 lit0; float lita; vec3 spec;
	enemy_light(normalize(MODELVIEW_NORMAL_MATRIX * NORMAL), normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz), lit0, lita, spec);
	v_lit0 = lit0; v_lita = lita; v_spec = spec;
}

void fragment() {
	vec4 t = texture(base_tex, UV);
	if (flat_tex) t = vec4(flat_rgb, 1.0);
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	vec3 x = clamp((c0 + toon * (1.0 - tex_k) + v_lit0 * tex_k - 0.5) * 0.5, 0.0, 1.0);
	vec3 flat_c = mat_color * clamp(v_spec + amb1, 0.0, 1.0);
	vec3 col = clamp(mix(flat_c, t.rgb, x), 0.0, 1.0) * tint;
	col = clamp(mix(col, flash_color * (1.0 + flash_emit), flash), 0.0, 1.0);
	if (cutout && t.a < 0.5) discard;
	ALBEDO = to_linear(col);
	ALPHA = fade * opacity;
}
