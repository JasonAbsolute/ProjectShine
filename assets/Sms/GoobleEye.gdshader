// The Gooble's eyes: name.bmd's _mat_eye TEV (bmd_mat_dump.py), lit like the body (GoobleBody.gdshader: the sun of the Light Group, the
// spec light, per vertex). The glb that FinModelUtility wrote gives this surface the toon ramp as its texture; the eye texture is
// K_new_name_eye (64 x 64 IA4: I = the white and the dark pupil, A = the eye's shape), shipped here as gooble_eye.png.
//   COLOR0 = clamp(ambColor0 (128) + sun x N.L)          GX_DF_SIGN: the back of the eye darkens below the ambient
//   ALPHA0 = clamp(ambColor0.a (50) + sun.a x max(N.L, 0)) lit alpha, GX_DF_CLAMP
//   COLOR1 = the sun's specular (GX_AF_SPEC, shininess 50)
//   0: PREV = (REG0 + mix(TOON, COLOR0, 3/4) - 1/2) / 2   REG0 = (400, 404, 424) S10 -> 1.0 lit, ~0.8 in shadow; alpha = ALPHA0
//   1: PREV = mix(COLOR1, EYE, PREV); alpha = 4 x EYE.A x ALPHA0
//   alpha compare GEQUAL 128 AND LEQUAL 255 -> the eye's shape cut out; blend NONE, z LEQUAL + write (zCompLoc 0), no fog
// Drawn after the body (material 1 after material 0 in the game; render_priority 1 here) so it wins the depth tie on the dome.
shader_type spatial;
render_mode cull_back, depth_draw_always, unshaded, fog_disabled;

uniform sampler2D eye_tex : filter_linear, repeat_disable;      // K_new_name_eye (CLAMP, GX_LINEAR)
uniform sampler2D toon_tex : filter_linear, repeat_disable;     // H_toon_2_4i
uniform vec3 reg0 = vec3(1.5686, 1.5843, 1.6627);               // GX_TEVREG0 (400, 404, 424) / 255
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);
uniform vec3 sun_color = vec3(1.0);
uniform vec3 ambient = vec3(0.502);                             // ambColor0 (128, 128, 128)
uniform float ambient_a = 0.196;                                // ambColor0.a = 50 / 255
uniform float shininess = 50.0;

varying vec3 v_lit0;
varying float v_lit0a;
varying vec3 v_spec;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	vec3 l = normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz);
	float ndl = dot(n, l);
	v_lit0 = clamp(ambient + sun_color * ndl, 0.0, 1.0);                     // GX_DF_SIGN
	v_lit0a = clamp(ambient_a + max(ndl, 0.0), 0.0, 1.0);                    // the sun's alpha 255
	vec3 h = normalize(l + vec3(0.0, 0.0, 1.0));
	float c = ndl >= 0.0 ? max(dot(n, h), 0.0) : 0.0;
	float c2 = c * c;
	float spec = c2 / (0.5 * shininess + (1.0 - 0.5 * shininess) * c2);
	v_spec = clamp(sun_color * spec, 0.0, 1.0);
}

void fragment() {
	vec3 toon = texture(toon_tex, v_lit0.rg).rgb;
	vec4 eye = texture(eye_tex, UV);
	vec3 prev = clamp((reg0 + mix(toon, v_lit0, 0.75) - 0.5) * 0.5, 0.0, 1.0);     // stage 0 (bias SUBHALF, scale /2)
	vec3 col = mix(v_spec, eye.rgb, prev);                                          // stage 1
	float a = clamp(4.0 * eye.a * v_lit0a, 0.0, 1.0);                               // stage 1 alpha (scale x4)
	if (a * 255.0 < 128.0) {                                                         // GX_GEQUAL 128
		discard;
	}
	ALBEDO = to_linear(col);
	ALPHA = 1.0;
}
