// OsenBubble.gdshader -- SMS 232: the HOT-SPRING BUBBLE's one material, _mat_osenbubbles_ind (btelesa_osenbubbles_ind.bmd, delfinoBoss.szs), read off the
// bmd's MAT3 (tools/projectshine/rematch157/j3d_mat232.py). Three TEV stages over four textures:
//   stage 0: C = TEXC of texmap 1 (N_btelesa_dummy -- a flat (0, 172, 232) blue; TBubble::init asks SMS_ChangeTextureAll for "H_ma_rak_dummy", the SCREEN
//            texture, a name this model has not, so in the game the swap misses and the flat blue stays) at the position-projected texcoord (texgen 1: type 0
//            from POS, texmtx 1) WARPED by the indirect stage -- the ind map N_btelesa_bubble_ind (IA4) through ind mtx 1 (0.5 x 2^1): the refraction wobble.
//            A = REG0's alpha (255).
//   stage 1: C = REG1 x (1 - prev) + REG0 x prev -- the (warped screen) remapped between tevreg1 (100, 0, -100: a dark red, the blue clamps to 0) for its darks
//            and tevreg0 (230, 200, 255: a pale lavender) for its lights. A = KONST(1/2) x TEXA (N_btelesa_bubble, IA8: the rim alpha -- 255 at the dome's edge,
//            60-90 at its middle) x 2 = the bubble texture's alpha.
//   stage 2: C = prev + REG2 (white) x TEXC of N_btelesa_bubble_spe (I4: a soft highlight blob); A = prev. Blend SRC_ALPHA / INV_SRC_ALPHA, no lighting (no
//            rasterizer channel in any stage), cull back.
// Here: `refraction` on (the intent: the screen behind the bubble, wobbled by the ind map and remapped) -- off = the game's own picture, the dummy's flat blue in
// the remap (100, 135, 223). `refraction_uv` is the wobble's reach as a fraction of the screen (the GX offsets came to some 8 texels of a 640-wide screen).
// `opacity` x the alpha. The screen is read in display space (pow 1 / 2.2) as the TEV saw it; the result goes to ALBEDO in linear.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, unshaded, shadows_disabled;

uniform sampler2D bubble_tex : source_color, filter_linear, repeat_disable;   // N_btelesa_bubble (the alpha)
uniform sampler2D spe_tex : source_color, filter_linear, repeat_disable;      // N_btelesa_bubble_spe (the highlight)
uniform sampler2D ind_tex : filter_linear, repeat_enable;                     // N_btelesa_bubble_ind (the warp: (a, i) -> (s, t))
uniform sampler2D screen_tex : hint_screen_texture, filter_linear, repeat_disable;
uniform bool refraction = true;
uniform float refraction_uv : hint_range(0.0, 0.1) = 0.015;
uniform vec3 c0 = vec3(0.902, 0.784, 1.0);              // tevreg0 (230, 200, 255): the lights
uniform vec3 c1 = vec3(0.392, 0.0, 0.0);                // tevreg1 (100, 0, -100): the darks (the blue clamps to 0)
uniform vec3 c2 = vec3(1.0);                              // tevreg2: the highlight's colour
uniform vec3 dummy_blue = vec3(0.0, 0.675, 0.910);      // N_btelesa_dummy (0, 172, 232): what the game's stage 0 sampled
uniform float opacity : hint_range(0.0, 1.0) = 1.0;
uniform float ind_scroll = 0.08;                           // the warp map drifting over the dome (a slow boil: the TEV's ind texmtx was static -- a touch of life)

void fragment() {
	vec4 bub = texture(bubble_tex, UV);
	vec3 spe = texture(spe_tex, UV).rgb;
	vec3 s;
	if (refraction) {
		vec4 ind = texture(ind_tex, UV + vec2(TIME * ind_scroll, TIME * ind_scroll * 0.7));
		vec2 warp = (vec2(ind.a, ind.r) - 0.5) * 2.0 * refraction_uv;
		vec2 suv = clamp(SCREEN_UV + warp, vec2(0.001), vec2(0.999));
		s = pow(max(texture(screen_tex, suv).rgb, vec3(0.0)), vec3(1.0 / 2.2));
	} else {
		s = dummy_blue;
	}
	vec3 col = clamp(c1 * (1.0 - s) + c0 * s, 0.0, 1.0);                                   // stage 1
	col = clamp(col + c2 * spe, 0.0, 1.0);                                                   // stage 2
	ALBEDO = pow(col, vec3(2.2));
	ALPHA = clamp(bub.a * opacity, 0.0, 1.0);
}
