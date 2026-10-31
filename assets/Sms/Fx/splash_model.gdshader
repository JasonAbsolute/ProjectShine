// Pass 134: the dive splash MODEL's five materials (mario/04_tobikomi.bmd in chr0.szs), one shader, the material picked by `variant`. All five
// are GX blend SRCALPHA / ONE (additive), z test on, z write off, no fog, lighting off but the ring's. The TEV per material, from the bmd's
// MAT3 (bmd_mat_dump.py), with the I8 textures' intensity standing for TEXC and TEXA alike:
//   0 _hamon (the ring quad, P_ms_hamonring3, cull NONE):   rgb = 3/8 + 3/8 x RASC (lit white: the stage light, a constant here);
//                                                            a = TEXA x K0.a (K0.a keyed by the brk: 200 -> 200 at f25 -> 0 at f80).
//   1 _mizubashira (the column, cull NONE):                  tex0 P_wave1 (S 1.5,1; t clamped), tex1 P_tamatama (S 4,1; btk T.y 0 -> 1.5);
//                                                            rgb = 1/8 + RASC(white) x tex1; a = clamp(4 x (tex0 x RASA - tex1)) -- the
//                                                            foam shows where the wave band beats the scrolling droplet noise; RASA = the
//                                                            material alpha (bpk: 200 -> 200 at f27 -> 0 at f50).
//   2 _mizubashira1r (the column again, reversed winding, cull NONE): tex0 P_wave1 (S 1.5,1), tex1 P_wave1 (S 1.5,1; btk T.y 0.2 -> 0.8);
//                                                            a0 = clamp(2 x tex0 x RASA); rgb = 1/8 + RASC x tex1; a = clamp(4 x a0 x tex1);
//                                                            alpha test GEQUAL 64; RASA 150 -> 0 over f0..50.
//   3 _mizubashira2 / 2r (the crown, outside and inside: cull BACK on opposite windings): matColor (207,207,207); tex0 P_wave1 (S 3,1),
//                                                            tex1 P_tamatama (S 7, 1 -> 0.4 and T.y 0 -> 1.5 by the btk); rgb = 1/8 + RASC x
//                                                            tex1; a = clamp(tex0 x RASA + tex1 - 1/2) (stage 1: (TEXA, TEXA, APREV, APREV)
//                                                            ADD bias SUBHALF); alpha test GEQUAL 253 XNOR LEQUAL 100 = drawn only while
//                                                            100 < a < 253 -- a cut-out band; RASA 255 -> 0 over f27..50.
// The J3D texture matrix (mode 0, centre 0.5,0.5): uv' = S x (uv - 0.5) + 0.5 + T. GX colours are display-referred: the TEV runs raw and
// the result is put to linear. `intensity` is a knob (1 = the game).
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, fog_disabled, specular_disabled;

uniform int variant = 1;                       // 0 ring, 1 column, 2 column (reversed copy), 3 crown
uniform sampler2D tex0 : filter_linear, repeat_enable;
uniform sampler2D tex1 : filter_linear, repeat_enable;
uniform vec2 tex0_scale = vec2(1.0, 1.0);
uniform vec2 tex0_trans = vec2(0.0, 0.0);
uniform bool tex0_clamp_t = true;              // P_wave1 wraps s and clamps t
uniform vec2 tex1_scale = vec2(1.0, 1.0);
uniform vec2 tex1_trans = vec2(0.0, 0.0);
uniform bool tex1_clamp_t = false;
uniform vec3 mat_rgb = vec3(1.0, 1.0, 1.0);    // matColor rgb (RASC with lighting off)
uniform float mat_alpha = 0.784;               // matColor a (RASA), keyed by the bpk
uniform float k0_alpha = 0.784;                // the ring's K0 alpha, keyed by the brk
uniform float ras_lit = 1.0;                   // the ring's lit vertex colour (chan0: ambient 128 + the stage light, clamped), a constant
uniform float konst_c = 0.125;                 // the stage-1 konst 1/8 added to the colour
uniform bool cull_back = false;                // GX_CULL_BACK (the crown pair)
uniform float intensity = 1.0;                 // knob: brightness, 1 = the game

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

vec2 texmtx(vec2 uv, vec2 s, vec2 t, bool clamp_t) {
	vec2 r = s * (uv - 0.5) + 0.5 + t;
	if (clamp_t) r.y = clamp(r.y, 0.0, 1.0);
	return r;
}

void fragment() {
	if (cull_back && !FRONT_FACING) discard;
	float t0 = texture(tex0, texmtx(UV, tex0_scale, tex0_trans, tex0_clamp_t)).r;
	float t1 = texture(tex1, texmtx(UV, tex1_scale, tex1_trans, tex1_clamp_t)).r;
	vec3 rgb;
	float a;
	if (variant == 0) {
		rgb = vec3(0.375 + 0.375 * ras_lit);
		a = t0 * k0_alpha;
	} else if (variant == 1) {
		rgb = konst_c + mat_rgb * t1;
		a = clamp(4.0 * (clamp(t0 * mat_alpha, 0.0, 1.0) - t1), 0.0, 1.0);
	} else if (variant == 2) {
		float a0 = clamp(2.0 * t0 * mat_alpha, 0.0, 1.0);
		rgb = konst_c + mat_rgb * t1;
		a = clamp(4.0 * a0 * t1, 0.0, 1.0);
		if (a < 64.0 / 255.0) discard;
	} else {
		float a0 = clamp(t0 * mat_alpha, 0.0, 1.0);
		rgb = konst_c + mat_rgb * t1;
		a = clamp(a0 + t1 - 0.5, 0.0, 1.0);
		if (a >= 253.0 / 255.0 || a <= 100.0 / 255.0) discard;
	}
	ALBEDO = to_linear(clamp(rgb, 0.0, 1.0)) * intensity;
	ALPHA = a;
}
