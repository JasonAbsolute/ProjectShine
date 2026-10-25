// SMS small-enemy toon materials (pass 125b) -- the TEV of the enemy bmds (hamukuri/default.bmd: bmd_mat_dump.py), evaluated in display
// space the way the GX does it and converted back to linear for Godot. The lighting is TLightCommon's, per VERTEX as the XF unit does it:
//   COLOR0 = clamp(ambColor0 (128,128,128) + sun x max(N.L, 0))      the object sun, GX_LIGHT0, (255,255,255), no attenuation
//   ALPHA0 = clamp(ambColor0.a (50) + sun.a (255) x max(N.L, 0))      the lit alpha (the eye's alpha test reads it)
//   COLOR1 = clamp(sun x spec)                                        GX_LIGHT2 = the sun as a GX_AF_SPEC light, shininess 50 (the 3rd channel in
//                                                                    the bmd's list: [COLOR0, ALPHA0, COLOR1, ALPHA1])
//   toon   = H_toon_2_4i at (COLOR0.r, COLOR0.g)                      GX_TG_SRTG
// TEV (out = d + mix(a, b, c), then the bias and the scale; all on the 8-bit display values, S10 registers may exceed 1):
//   stage 0:  s0 = 1/2 (c0 + mix(toon, COLOR0, k) - 1/2)              (TEXC toon, RASC, KONST k, C0) SUBHALF /2; k = 3/4 (nose, feet, eye) or 5/8 (body)
//   form 0:   s1 = mix(COLOR1, TEX, s0)                               (RASC1, TEXC, CPREV, ZERO): the texture where lit, the specular where dark
//             nose / feet (TEX = K_hamu_dot's white corner: a plain colour), the body top (the dot texture's white: plain orange, a darker band at
//             the bottom), the eye (TEX = K_hamu_eye1: the pupil over the lit white; alpha = TEXA x ALPHA0 x 4, alpha test GEQUAL 128)
//   form 1:   s1 = s0 + mix(c1, c2, TEX) - 1/2;  s2 = s1 + COLOR1     the trousers: TEX = K_hamu_dot (1 = c2 the red, 0 = c1 the white dots),
//                                                                    then (RASC1, RASC1, KONST, CPREV) adds the specular
//   pollution (the body top, 7 stages): stage 0 REG2 = polmask.a > konst.a ? 1 : 0 through TEXMTX1 (the dirt level: 0 = clean -- the game's
//             plain Stu; 128/255 = the bmd's own value, the goop spatter of the generator Stus), then REG1 = s1 x REG2, PREV = goop x COLOR0
//             (the stage's h_ma_rak swapped for the dummy), PREV = PREV x (1 - REG2) + REG1. dirt 0 skips it all.

uniform float s0_k = 0.75;                  // the stage-0 konst: 3/4 or 5/8
uniform int s1_form = 0;                    // 0 mix(spec, tex, s0); 1 the trousers' two-register mask + the specular
uniform vec3 c0 = vec3(1.0);                // GX_TEVREG0 / 255 (S10: may exceed 1 or go below 0)
uniform vec3 c1 = vec3(0.902);              // GX_TEVREG1 / 255 (form 1)
uniform vec3 c2 = vec3(0.949, 0.063, 0.365);// GX_TEVREG2 / 255 (form 1)
uniform sampler2D base_tex : filter_linear, repeat_disable;   // texmap0, read RAW (display-referred, as the TEV sees it): K_hamu_dot / K_hamu_eye1
uniform sampler2D toon_tex : filter_linear, repeat_disable;   // H_toon_2_4i: 136 in shadow, 255 lit
uniform float dirt = 0.0;                   // the pollution konst alpha: 0 = clean (no mask read), 0.5 = the bmd's 128 (the generator Stu's spatter)
uniform sampler2D pol_mask : filter_linear, repeat_enable;    // H_ma_polmask1_i4_hamu through UV2 x pol_scale + pol_offset (TEXMTX1; S repeats, T clamps)
uniform vec2 pol_scale = vec2(4.000002, 2.48926);
uniform vec2 pol_offset = vec2(0.16601562, 0.0390625);
uniform sampler2D goop_tex : filter_linear, repeat_enable;    // the stage's h_ma_rak (the dummy's swap)
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);             // toward the sun, world space: scene.bin (200000, 500000, 200000)
uniform vec3 sun_color = vec3(1.0);
uniform vec3 ambient = vec3(0.502);                           // ambColor0 (128, 128, 128)
uniform float ambient_a = 0.196;                              // ambColor0.a (50)
uniform float shininess = 50.0;

varying vec3 v_lit0;
varying float v_lita;
varying vec3 v_spec;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

// from vertex(): the view-space normal and the view-space direction toward the sun (the built-ins are only visible there, and a varying may
// only be written there -- so the three come back as out parameters)
void enemy_light(vec3 n, vec3 l, out vec3 lit0, out float lita, out vec3 spec_out) {
	float ndl = dot(n, l);
	lit0 = clamp(ambient + sun_color * max(ndl, 0.0), 0.0, 1.0);
	lita = clamp(ambient_a + max(ndl, 0.0), 0.0, 1.0);
	vec3 h = normalize(l + vec3(0.0, 0.0, 1.0));
	float c = ndl >= 0.0 ? max(dot(n, h), 0.0) : 0.0;
	float c2s = c * c;
	float spec = c2s / (0.5 * shininess + (1.0 - 0.5 * shininess) * c2s);
	spec_out = clamp(sun_color * spec, 0.0, 1.0);
}

// stage 0: the toon-lit base colour (display space)
vec3 enemy_s0() {
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	return clamp(0.5 * (c0 + mix(toon, v_lit0, s0_k) - 0.5), 0.0, 1.0);
}

// the display-space colour of the opaque forms, with the pollution stages when dirt > 0
vec3 enemy_color(vec2 uv, vec2 uv2) {
	vec3 s0 = enemy_s0();
	vec3 tex = texture(base_tex, uv).rgb;
	vec3 prev;
	if (s1_form == 1) {
		prev = clamp(s0 + mix(c1, c2, tex) - 0.5, 0.0, 1.0);
		prev = clamp(prev + v_spec, 0.0, 1.0);
	} else {
		prev = clamp(mix(v_spec, tex, s0), 0.0, 1.0);
	}
	if (dirt > 0.0) {
		float mask_a = texture(pol_mask, uv2 * pol_scale + pol_offset).r;        // I4: TEXA = the intensity
		float clean = mask_a > dirt ? 1.0 : 0.0;                                   // GX_TEV_COMP_GR16_GT on (TEXA, KONST)
		vec3 goop = clamp(texture(goop_tex, uv2).rgb * v_lit0, 0.0, 1.0);         // stage 4: the rak texture (texgen2: TEX1 through the identity) x COLOR0
		prev = mix(goop, prev, clean);                                             // stages 3, 5, 6
	}
	return to_linear(prev);
}
