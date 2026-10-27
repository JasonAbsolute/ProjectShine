// The launcher's goop pile (pass 126): /scene/clauncher/generator_model1.bmd, material _body1 (bmd_mat_dump.py) -- opaque, cull BACK, blend NONE,
// 2 TEV stages on 2 colour channels:
//   COLOR0 = the vertex colour (chan0: lighting off, matSrc VTX)                         the baked shading of the blob (0.12 .. 1.0 grey)
//   COLOR1 = matColor1 (white) x GX_LIGHT2 as a GX_AF_SPEC light (chan2: lighting on, lightMask 0x04, diffuse SIGN, attn SPEC), amb1 black
//   stage 0: PREV = TEXC x RASC0                                                           the stage's goop mark (h_ma_rak.bti swapped into Q_gene_dummy_I4)
//   stage 1: PREV = PREV + RASC1                                                           the specular glint on top (RASC, CPREV, ONE, RASC)
//   alpha = the material alpha (255): opaque.
// Evaluated in display space like the GX and converted to linear; the light is TLightCommon's object sun as EnemyToon.gdshaderinc has it
// (sun_dir, shininess 50, per vertex). unshaded: the GX lighting is in the TEV result.
shader_type spatial;
render_mode cull_back, unshaded, fog_disabled;

uniform sampler2D goop_tex : filter_linear, repeat_enable;     // the stage's pollution mark (goop_mark_bianco.png = Bianco's h_ma_rak.bti), read RAW
uniform vec3 mat_color1 = vec3(1.0);                           // matColor1 (255, 255, 255) / 255: the specular channel's material colour
uniform vec3 sun_dir = vec3(0.348, 0.870, 0.348);              // toward the sun, world space: scene.bin (200000, 500000, 200000)
uniform vec3 sun_color = vec3(1.0);
uniform float shininess = 50.0;

varying vec3 v_spec;

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	vec3 n = normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
	vec3 l = normalize((VIEW_MATRIX * vec4(normalize(sun_dir), 0.0)).xyz);
	float ndl = dot(n, l);
	vec3 h = normalize(l + vec3(0.0, 0.0, 1.0));
	float c = ndl >= 0.0 ? max(dot(n, h), 0.0) : 0.0;
	float c2s = c * c;
	float spec = c2s / (0.5 * shininess + (1.0 - 0.5 * shininess) * c2s);     // GX_AF_SPEC's attenuation, as EnemyToon.gdshaderinc
	v_spec = clamp(mat_color1 * sun_color * spec, 0.0, 1.0);
}

void fragment() {
	vec3 goop = texture(goop_tex, UV).rgb;
	vec3 prev = clamp(goop * COLOR.rgb, 0.0, 1.0);                           // stage 0: TEXC x RASC0 (the vertex colour)
	prev = clamp(prev + v_spec, 0.0, 1.0);                                   // stage 1: + RASC1 (the specular light)
	ALBEDO = to_linear(prev);
}
