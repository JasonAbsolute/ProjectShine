// ms_mari_rakuhamon (the goop ripple ring) / ms_mari_rakuball (the goop balls): BSP1 projection = TEXCOORD0 is the vertex's SCREEN position
// and TEXMAP0 is the game's dummy P_ms_fb_8x8i4 swapped for TScreenTexture (the frame at half size, 320 x 224: EmitterViewObj.cpp 472,
// ScreenUtil.cpp 218), so the quad shows what is BEHIND it. ETX1 indirect mode 1 (JPADrawSetupTev.cpp 25): the warp texture's texel, read by
// the quad's own UV, biased (GX_ITB_STU: I - 128) and scaled by the 2 x 3 matrix [0.5 0 0 / 0 0.5 0] x 2^-2 (GXSetIndTexMtx) displaces the
// screen lookup by (I - 128) / 8 TEXELS of the copy on S and T; the second texture (the mask) multiplies colour (its I) and alpha (its A).
// colour = TEXC x prm (white), alpha = TEXA x prmA x the ESP1 alpha (COLOR.a). blend SRCALPHA + INVSRCALPHA, z test on / no write.
// The ring is a Direction quad lying flat on the goop (vel axis 0.01: radial -> the quad's normal is the emitter's Y = the ground normal);
// the balls are billboards (`billboard` on). Display-space maths like the other GX shaders.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear, repeat_disable;
uniform sampler2D warp_tex : filter_linear, repeat_enable;       // the indirect texture (I = S = T): indhamon2 / indwp4
uniform sampler2D mask_tex : filter_linear, repeat_disable;      // the second texture: indmsk2 / indmsk4 (I -> colour, A -> alpha)
uniform vec2 screen_texels = vec2(320.0, 224.0);                 // TScreenTexture: SMSGetGameRenderWidth / 2 x Height / 2
uniform float ind_scale = 0.125;                                 // mtx 0.5 x 2^-2: screen texels per 8-bit warp step
uniform vec3 prm_color = vec3(1.0);
uniform bool billboard = false;

vec3 to_srgb(vec3 c) {
	c = clamp(c, 0.0, 1.0);
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	if (billboard) {
		mat4 mv = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		MODELVIEW_MATRIX = mv * mat4(
			vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
			vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
			vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
			vec4(0.0, 0.0, 0.0, 1.0));
		MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
	}
}

void fragment() {
	float w = texture(warp_tex, UV).r;
	vec2 d = vec2((w * 255.0 - 128.0) * ind_scale) / screen_texels;
	vec3 scr = to_srgb(texture(screen_tex, clamp(SCREEN_UV + d, vec2(0.0), vec2(1.0))).rgb);
	vec4 mk = texture(mask_tex, UV);
	ALBEDO = to_linear(clamp(scr * prm_color * mk.r, 0.0, 1.0));
	ALPHA = mk.a * COLOR.a;
}
