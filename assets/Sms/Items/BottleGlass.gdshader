// The water bottle's glass: bottle_short.bmd's _suimen1 (tools/projectshine/bmd_mat_dump.py on scene/bianco2/mapobj/bottle_short.bmd;
// bottle_large.bmd carries the same material), written out stage by stage. An ADDITIVE translucent shell -- blend SRCALPHA + ONE, z
// LEQUAL with the depth written (so depth_draw_always), cull back, draw mode 4 (translucent) -- whose alpha is the model's VERTEX
// alpha (0.24 at the neck .. 1 at the base: the water); the label band is _mat_label4 = BottleLabel.tres, a plain StandardMaterial3D.
//
// TEV (out = d + (1 - c) a + c b, then bias and scale; the maths is on the display-referred 8-bit values, so the texture sample is
// taken to sRGB first and the result put back to linear for Godot):
//   0: PREV = TEX x COLOR0 + 1/2          bias ADDHALF   COLOR0 = chan0, lighting OFF, matSrc VTX = the vertex colour, (1, 1, 1) on the body
//      A    = TEXA x COLOR0.A                            TEXA = 1 (J_bottle_s is CMPR, every texel opaque) -> the vertex alpha
//   1: PREV = PREV + COLOR1 x 1           konst ONE      COLOR1 = chan2: matColor1 (255, 255, 255), amb (0, 0, 0), lit by GX_LIGHT2 alone
//                                                        (lightMask 0x04) as GX_AF_SPEC: the object sun as a specular light (TLightCommon
//                                                        ::setLight: GXInitSpecularDir(-L), GXInitLightShininess(50)); J3D forces GX_DF_NONE on
//                                                        a spec channel, so COLOR1 = sun x spec, spec = c^2 / (s/2 + (1 - s/2) c^2), c = N.H
//                                                        gated by N.L >= 0, H = normalize(L + (0, 0, 1)) in view space -- per VERTEX (the XF unit)
//      A    = PREV.A + 1/4 x 1 - 1/2      bias SUBHALF   RASA1 = chan3: lighting OFF, matSrc REG = matColor1.a = 1; konst 1/4 -> alpha = vcA - 1/4
// SmsWaterBottle.cs feeds sun_dir / sun_color from the scene's DirectionalLight3D (the game's "太陽（オブジェクト）" is the default here).
shader_type spatial;
render_mode blend_add, cull_back, depth_draw_always, unshaded, fog_disabled;

uniform sampler2D glass_tex : source_color, filter_linear_mipmap, repeat_disable;   // J_bottle_s (the body samples its plain blue patch)
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);   // toward the sun, world space: scene.bin (200000, 500000, 200000)
uniform vec3 sun_color = vec3(1.0);                 // (255, 255, 255)
uniform float shininess = 50.0;                     // TLightCommon::mShininess
uniform float konst_a = 0.25;                       // stage 1's alpha konst: GX_TEV_KASEL_1_4

varying vec3 v_spec;

vec3 to_srgb(vec3 c) {
	c = clamp(c, 0.0, 1.0);
	return mix(12.92 * c, 1.055 * pow(c, vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	vec3 l = normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz);
	float ndl = dot(n, l);
	vec3 h = normalize(l + vec3(0.0, 0.0, 1.0));
	float c = ndl >= 0.0 ? max(dot(n, h), 0.0) : 0.0;
	float c2 = c * c;
	float spec = c2 / (0.5 * shininess + (1.0 - 0.5 * shininess) * c2);   // cosatt (0, 0, 1) / distatt (s/2, 0, 1 - s/2)
	v_spec = clamp(sun_color * spec, 0.0, 1.0);
}

void fragment() {
	vec4 tex = texture(glass_tex, UV);
	vec3 prev = clamp(to_srgb(tex.rgb) * COLOR.rgb + 0.5, 0.0, 1.0);   // stage 0 (bias ADDHALF)
	float a = clamp(tex.a * COLOR.a, 0.0, 1.0);
	prev = clamp(prev + v_spec, 0.0, 1.0);                               // stage 1
	a = clamp(a + konst_a - 0.5, 0.0, 1.0);                              // stage 1 alpha (bias SUBHALF)
	ALBEDO = to_linear(prev);
	ALPHA = a;
}
