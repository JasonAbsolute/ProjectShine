// SMS pass 123d -- the graffiti M (koopa_jr_sign.bmd '_model1', bmd_mat_dump): ONE TEV stage, colour = C0 (GX_TEVREG0 = the stage's picture
// colour, set by TWaterHitPictureHideObj::load via initPacketMatColor; the texture's colour is NEVER read -- Q_jr_picture_IA8 has I = 0) x RASC
// (lit, chan0 lightMask 0x03), alpha = TEXA x A0 (the register's alpha = mProgress) with scale x4, clamp; blend NONE (opaque); alphaCmp
// GEQUAL 128. So a texel is DRAWN iff texA x progress x 4 >= 0.5 -- the M does not fade: it ERODES from its soft edges inward as the progress
// drops (255: 253 of the 1024 texels; 128: 183; 64: 110; 33: 5; 32 and under: NONE), which is why the game's min progress is 20 -- the picture
// is already gone. `tex` = the alpha shape with white RGB (jr_picture_a.png is the game's; your own PNG works the same: its alpha is the shape,
// its colour multiplies `color`).
// 123e -- THE DISSOLVE (Jaysonn: "the erosion kinda doesnt work untill right before its done ... more random erosion"): by the game's rule the
// first ~110 drops only nibble the outline and the body goes in the last ~18. `erosion_noise` adds a random dissolve ON TOP of the game's test:
// a smooth value noise over the quad (`erosion_grain` cells across; each hole grows from its centre as the spraying advances) removes a pixel
// once its noise falls under erosion_noise x the erosion fraction (0 fresh -> 1 at the game's finish). 0 = the game's exact look.
shader_type spatial;
render_mode cull_back, depth_draw_opaque;

uniform sampler2D tex : source_color, filter_linear, repeat_disable;
uniform vec4 color : source_color = vec4(1.0, 0.0, 0.0, 1.0);   // the Bianco M signs: (255, 0, 0)
uniform float progress = 1.0;                                  // mProgress / 255
uniform float alpha_scale = 4.0;                               // the TEV stage's scale x4
uniform float alpha_ref = 0.50196;                             // alphaCmp GEQUAL 128 / 255
uniform float finish = 0.0784;                                 // the game's mMinProgress 20 / 255: the erosion fraction reaches 1 here
uniform float erosion_noise : hint_range(0.0, 1.0) = 0.6;      // 0 = the game; 1 = every pixel dissolved by the finish
uniform float erosion_grain = 6.0;                             // noise cells across the quad: small = big ragged holes, large = fine speckle

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

// value noise with smooth interpolation: holes grow from their centres instead of flickering in pixel by pixel
float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash21(i);
	float b = hash21(i + vec2(1.0, 0.0));
	float c = hash21(i + vec2(0.0, 1.0));
	float d = hash21(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

void fragment() {
	vec4 t = texture(tex, UV);
	float a = clamp(t.a * progress * alpha_scale, 0.0, 1.0);
	if (a < alpha_ref) {
		discard;
	}
	if (erosion_noise > 0.0) {
		float eroded = clamp((1.0 - progress) / max(1.0 - finish, 1e-4), 0.0, 1.0);
		float n = 0.65 * vnoise(UV * erosion_grain) + 0.35 * vnoise(UV * erosion_grain * 2.7 + 17.0);   // two octaves: ragged hole edges
		// the two-octave noise piles up around 0.5 (quantiles 0.43 / 0.50 / 0.57 / 0.65 / 0.71 over the M): stretched over 0.35..0.78 it is close
		// to uniform, so erosion_noise reads as the share of the body the dissolve claims, spread evenly over the spraying (erosion_curve.py)
		float u = clamp((n - 0.35) / 0.43, 0.0, 1.0);
		if (u < eroded * erosion_noise) {
			discard;
		}
	}
	ALBEDO = color.rgb * t.rgb;
	ROUGHNESS = 0.9;
	SPECULAR = 0.0;
}
