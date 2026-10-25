// SMS pass 123d -- the graffiti M (koopa_jr_sign.bmd '_model1', bmd_mat_dump): ONE TEV stage, colour = C0 (GX_TEVREG0 = the stage's picture
// colour, set by TWaterHitPictureHideObj::load via initPacketMatColor; the texture's colour is NEVER read -- Q_jr_picture_IA8 has I = 0) x RASC
// (lit, chan0 lightMask 0x03), alpha = TEXA x A0 (the register's alpha = mProgress) with scale x4, clamp; blend NONE (opaque); alphaCmp
// GEQUAL 128. So a texel is DRAWN iff texA x progress x 4 >= 0.5 -- the M does not fade: it ERODES from its soft edges inward as the progress
// drops (255: 253 of the 1024 texels; 128: 183; 64: 110; 33: 5; 32 and under: NONE), which is why the game's min progress is 20 -- the picture
// is already gone. `tex` = the alpha shape with white RGB (jr_picture_a.png is the game's; your own PNG works the same: its alpha is the shape,
// its colour multiplies `color`).
shader_type spatial;
render_mode cull_back, depth_draw_opaque;

uniform sampler2D tex : source_color, filter_linear, repeat_disable;
uniform vec4 color : source_color = vec4(1.0, 0.0, 0.0, 1.0);   // the Bianco M signs: (255, 0, 0)
uniform float progress = 1.0;                                  // mProgress / 255
uniform float alpha_scale = 4.0;                               // the TEV stage's scale x4
uniform float alpha_ref = 0.50196;                             // alphaCmp GEQUAL 128 / 255

void fragment() {
	vec4 t = texture(tex, UV);
	float a = clamp(t.a * progress * alpha_scale, 0.0, 1.0);
	if (a < alpha_ref) {
		discard;
	}
	ALBEDO = color.rgb * t.rgb;
	ROUGHNESS = 0.9;
	SPECULAR = 0.0;
}
