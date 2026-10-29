// ms_gatehit_a -- the PAINT BUBBLE a FLUDD drop throws off the M gate (SMS pass 127g). The game's TEV (doldecomp/sms JPADrawSetupTev.cpp, ETX1
// indirect mode 1 with a second texture): TEXMAP0 is the dummy P_ms_fb_8x8i4 swapped for TScreenTexture (the frame at half size, 320 x 224) read at
// the quad's SCREEN position, so the bubble shows what is BEHIND it -- the rainbow paint; the lookup is displaced by the indirect texture
// P_msm_mizutama1 (a water-drop bump; GX takes an indirect texel's S from its ALPHA and T from its BLUE = the I of an IA8), biased (I - 128) and
// scaled by the 2 x 3 matrix [0.5 0 0 / 0 0.5 0] x 2^-2: (I - 128) / 8 texels of the copy. Stage 0: colour = TEXC x prm + env, alpha = prmA (the
// ESP1 fade); the second texture P_msm_kemuball3 (a shaded ball): colour x= its I, alpha x= its A. Alpha compare GREATER 101 / 255 with blend
// ONE / ZERO: a HARD-EDGED glass bubble that replaces the screen where it passes. prm / env come from a 3-key RANDOM table (calc 4): each bubble
// is one of peach (159, 207, 255)+(111, 48, 0), mint (255, 159, 207)+(0, 96, 48), lavender (207, 255, 159)+(48, 0, 96) -- the particle's COLOR
// carries the prm (a 3-step color_initial_ramp) and the pair's env is picked here by the nearest prm. Display-space maths like the other GX shaders.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear, repeat_disable;
uniform sampler2D warp_tex : filter_linear, repeat_enable;       // P_msm_mizutama1 IA8 as (I, I, I, A): S from .a, T from .r
uniform sampler2D mask_tex : filter_linear, repeat_disable;      // P_msm_kemuball3 IA8 as (I, I, I, A): I x colour, A -> the alpha test
uniform vec2 screen_texels = vec2(320.0, 224.0);                 // TScreenTexture: SMSGetGameRenderWidth / 2 x Height / 2
uniform float ind_scale = 0.125;                                 // mtx 0.5 x 2^-2: screen texels per 8-bit warp step
uniform float alpha_ref = 0.3961;                                // GX_GREATER 101 / 255
uniform float tint = 1.0;                                        // 1 = the game's prm / env; 0 = the bare refraction (a knob)
uniform vec3 prm0 = vec3(0.6235, 0.8118, 1.0);                   // (159, 207, 255)
uniform vec3 env0 = vec3(0.4353, 0.1882, 0.0);                   // (111, 48, 0)
uniform vec3 prm1 = vec3(1.0, 0.6235, 0.8118);                   // (255, 159, 207)
uniform vec3 env1 = vec3(0.0, 0.3765, 0.1882);                   // (0, 96, 48)
uniform vec3 prm2 = vec3(0.8118, 1.0, 0.6235);                   // (207, 255, 159)
uniform vec3 env2 = vec3(0.1882, 0.0, 0.3765);                   // (48, 0, 96)

vec3 to_srgb(vec3 c) {
	c = clamp(c, 0.0, 1.0);
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	// a camera-facing quad (JPA Billboard) keeping the particle's own scale
	mat4 mv = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = mv * mat4(
		vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
		vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
		vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
		vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	vec4 w = texture(warp_tex, UV);
	vec2 d = (vec2(w.a, w.r) * 255.0 - 128.0) * ind_scale / screen_texels;
	vec3 scr = to_srgb(texture(screen_tex, clamp(SCREEN_UV + d, vec2(0.0), vec2(1.0))).rgb);
	vec4 mk = texture(mask_tex, UV);
	vec3 prm = COLOR.rgb;
	float d0 = distance(prm, prm0), d1 = distance(prm, prm1), d2 = distance(prm, prm2);
	vec3 env = d0 <= d1 && d0 <= d2 ? env0 : (d1 <= d2 ? env1 : env2);
	vec3 col = mix(scr, scr * prm + env, tint) * mk.r;
	float a = mk.a * COLOR.a;
	if (a <= alpha_ref) {
		discard;
	}
	ALBEDO = to_linear(clamp(col, 0.0, 1.0));
	ALPHA = 1.0;
}
